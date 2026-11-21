using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using WinZenShell.Native;

namespace WinZenShell.Services;

/// <summary>
/// Captures the live screen strip behind the taskbar (wallpaper base layer + live intersecting
/// windows via PrintWindow PW_RENDERFULLCONTENT, without using WDA_EXCLUDEFROMCAPTURE so the
/// taskbar never disappears on screen or in screenshots), downsamples 2x into a zero-copy
/// CoreWebView2SharedBuffer, and drives the WebGL2 Dual Kawase GPU blur shader in taskbar.js.
/// </summary>
public sealed class RealtimeBlurService : IDisposable
{
    private const int MaxBufferBytes = 512 * 1024; // 512 KB (supports up to 1920x64 half-res RGBA)
    private static readonly int CurrentPid = Environment.ProcessId;

    private readonly Dispatcher _dispatcher;
    private IntPtr _hwnd;
    private CoreWebView2? _webView;
    private CoreWebView2SharedBuffer? _sharedBuffer;
    private IntPtr _sharedBufferPtr;
    private volatile bool _sharedBufferPosted;

    private volatile bool _isActive;
    private volatile int _blurStrength = 60;

    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    // Reusable GDI capture resources for taskbar strip (accessed only on capture loop thread)
    private IntPtr _memDc;
    private IntPtr _dibBitmap;
    private IntPtr _oldBitmap;
    private IntPtr _dibBits;
    private int _dibW;
    private int _dibH;

    // Reusable GDI capture resources for overlapping windows
    private IntPtr _wndDc;
    private IntPtr _wndBitmap;
    private IntPtr _wndOldBitmap;
    private int _wndBufW;
    private int _wndBufH;

    // Cached wallpaper strip matching taskbar bounds
    private string _wpCacheKey = "";
    private byte[] _wpBgraBytes = Array.Empty<byte>();
    private IntPtr _liveWallpaperHwnd;
    private long _liveWallpaperProbeTick;
    private long _liveWallpaperCaptureTick;
    private byte[] _liveWpStripBytes = Array.Empty<byte>();
    private string _liveWpStripKey = "";

    private byte[] _srcScratch = Array.Empty<byte>();
    private byte[] _rgbaScratch = new byte[MaxBufferBytes];

    private ulong _lastFrameHash;
    private int _lastOutW;
    private int _lastOutH;
    private int _lastBlurStrength = -1;
    private volatile bool _forceNextFrame;

    public RealtimeBlurService(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public void ResetSharedBufferPosting()
    {
        _sharedBufferPosted = false;
        _forceNextFrame = true;
    }

    public void UpdateConfig(IntPtr hwnd, CoreWebView2? webView, bool active, int blurStrength)
    {
        _hwnd = hwnd;
        _blurStrength = Math.Clamp(blurStrength, 0, 100);

        if (webView != null && !ReferenceEquals(_webView, webView))
        {
            _webView = webView;
            _sharedBufferPosted = false;
        }

        // Ensure WDA_NONE so the taskbar is always visible on monitor and in screenshots
        if (hwnd != IntPtr.Zero)
        {
            Win32Api.SetWindowDisplayAffinity(hwnd, Win32Api.WDA_NONE);
        }

        if (active)
        {
            EnsureSharedBuffer();
            _forceNextFrame = true;
            if (!_isActive)
            {
                _isActive = true;
                StartCaptureLoop();
            }
        }
        else
        {
            if (_isActive)
            {
                _isActive = false;
                StopCaptureLoop();
            }
        }
    }

    public void Invalidate()
    {
        _forceNextFrame = true;
    }

    private void EnsureSharedBuffer()
    {
        if (_webView == null) return;
        try
        {
            if (_sharedBuffer == null)
            {
                _sharedBuffer = _webView.Environment.CreateSharedBuffer(MaxBufferBytes);
                _sharedBufferPtr = _sharedBuffer.Buffer;
                _sharedBufferPosted = false;
            }
            if (!_sharedBufferPosted && _sharedBuffer != null)
            {
                _sharedBufferPtr = _sharedBuffer.Buffer;
                _webView.PostSharedBufferToScript(
                    _sharedBuffer,
                    CoreWebView2SharedBufferAccess.ReadOnly,
                    "{\"type\":\"blur_shared_buffer\"}");
                _sharedBufferPosted = true;
            }
        }
        catch
        {
            // SharedBuffer unavailable or WebView not ready yet
        }
    }

    private void StartCaptureLoop()
    {
        StopCaptureLoop();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _loopTask = Task.Run(() => CaptureLoopAsync(token), token);
    }

    private void StopCaptureLoop()
    {
        try { _cts?.Cancel(); } catch { }
        _cts = null;
        _lastFrameHash = 0;
        _lastBlurStrength = -1;
    }

    private async Task CaptureLoopAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(66)); // ~15 FPS for low GPU/CPU load
        try
        {
            while (!token.IsCancellationRequested && _isActive)
            {
                CaptureAndDispatchFrame();
                if (!await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
                    break;
            }
        }
        catch (OperationCanceledException) { }
        catch { }
        finally
        {
            FreeGdiResources();
        }
    }

    private void CaptureAndDispatchFrame()
    {
        IntPtr hwnd = _hwnd;
        IntPtr bufPtr = _sharedBufferPtr;
        if (hwnd == IntPtr.Zero || bufPtr == IntPtr.Zero || !_sharedBufferPosted) return;
        if (!Win32Api.GetWindowRect(hwnd, out var rc)) return;

        int capW = rc.Width;
        int capH = rc.Height;
        if (capW <= 4 || capH <= 4) return;

        int outW = Math.Clamp(capW / 4, 2, 960);
        int outH = Math.Clamp(capH / 4, 2, 32);
        int outBytes = outW * outH * 4;
        if (outBytes > MaxBufferBytes) return;

        IntPtr screenDc = Win32Api.GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero) return;

        try
        {
            if (!EnsureDibSection(screenDc, capW, capH)) return;

            // 1. Base Layer: Desktop Wallpaper cropped to taskbar screen bounds
            EnsureWallpaperStrip(rc.Left, rc.Top, capW, capH);
            int srcBytes = capW * capH * 4;
            if (_wpBgraBytes.Length == srcBytes)
            {
                Marshal.Copy(_wpBgraBytes, 0, _dibBits, srcBytes);
            }

            // 1b. Live Wallpaper Layer (e.g. Lively Wallpaper, Wallpaper Engine hosted under WorkerW/Progman)
            CompositeLiveWallpaperLayer(screenDc, rc);

            // 2. Live Windows Layer: Composite visible windows intersecting the taskbar rect
            CompositeIntersectingWindows(screenDc, hwnd, rc);

            if (_srcScratch.Length < srcBytes)
                _srcScratch = new byte[srcBytes];

            Marshal.Copy(_dibBits, _srcScratch, 0, srcBytes);
        }
        finally
        {
            Win32Api.ReleaseDC(IntPtr.Zero, screenDc);
        }

        // 3. 2x2 box downsample + BGRA->RGBA swizzle + 64-bit FNV-1a dirty checksum
        ulong hash = 14695981039346656037UL;
        int srcStride = capW * 4;
        int dstIdx = 0;
        byte[] src = _srcScratch;
        byte[] dst = _rgbaScratch;

        for (int y = 0; y < outH; y++)
        {
            int sy0 = Math.Min(capH - 1, (y * capH) / outH);
            int sy1 = Math.Min(capH - 1, sy0 + 1);
            int row0 = sy0 * srcStride;
            int row1 = sy1 * srcStride;

            for (int x = 0; x < outW; x++)
            {
                int sx0 = Math.Min(capW - 1, (x * capW) / outW) * 4;
                int sx1 = Math.Min(capW - 1, (x * capW) / outW + 1) * 4;

                int i00 = row0 + sx0;
                int i10 = row0 + sx1;
                int i01 = row1 + sx0;
                int i11 = row1 + sx1;

                byte b = (byte)((src[i00] + src[i10] + src[i01] + src[i11] + 2) >> 2);
                byte g = (byte)((src[i00 + 1] + src[i10 + 1] + src[i01 + 1] + src[i11 + 1] + 2) >> 2);
                byte r = (byte)((src[i00 + 2] + src[i10 + 2] + src[i01 + 2] + src[i11 + 2] + 2) >> 2);

                dst[dstIdx] = r;
                dst[dstIdx + 1] = g;
                dst[dstIdx + 2] = b;
                dst[dstIdx + 3] = 255;

                if ((x & 3) == 0)
                {
                    uint packed = (uint)(r | (g << 8) | (b << 16));
                    hash ^= packed;
                    hash *= 1099511628211UL;
                }

                dstIdx += 4;
            }
        }

        int blur = _blurStrength;
        bool forced = _forceNextFrame;
        if (!forced &&
            hash == _lastFrameHash &&
            outW == _lastOutW &&
            outH == _lastOutH &&
            blur == _lastBlurStrength)
        {
            return;
        }

        _forceNextFrame = false;
        _lastFrameHash = hash;
        _lastOutW = outW;
        _lastOutH = outH;
        _lastBlurStrength = blur;

        try
        {
            Marshal.Copy(_rgbaScratch, 0, bufPtr, outBytes);
        }
        catch
        {
            return;
        }

        _ = _dispatcher.BeginInvoke(DispatcherPriority.Render, () =>
        {
            if (!_isActive || _webView == null) return;
            try
            {
                _webView.PostWebMessageAsJson(
                    $"{{\"type\":\"blur_frame\",\"w\":{outW},\"h\":{outH},\"blur\":{blur}}}");
            }
            catch { }
        });
    }

    private IntPtr ResolveLiveWallpaperHwnd(Win32Api.RECT tbRect)
    {
        long now = Environment.TickCount64;
        if (_liveWallpaperHwnd != IntPtr.Zero &&
            Win32Api.IsWindowVisible(_liveWallpaperHwnd) &&
            now - _liveWallpaperProbeTick < 2000)
        {
            return _liveWallpaperHwnd;
        }

        if (now - _liveWallpaperProbeTick < 2000 && _liveWallpaperHwnd == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        _liveWallpaperProbeTick = now;
        _liveWallpaperHwnd = Win32Api.FindLiveWallpaperWindow(tbRect);
        return _liveWallpaperHwnd;
    }

    private void CompositeLiveWallpaperLayer(IntPtr screenDc, Win32Api.RECT tbRect)
    {
        IntPtr liveWnd = ResolveLiveWallpaperHwnd(tbRect);
        if (liveWnd == IntPtr.Zero) return;

        int capW = tbRect.Width;
        int capH = tbRect.Height;
        int stripBytes = capW * capH * 4;
        string stripKey = $"{liveWnd.ToInt64()}|{tbRect.Left}|{tbRect.Top}|{capW}|{capH}";
        long now = Environment.TickCount64;

        // Throttle full-screen PrintWindow GPU readback of live wallpaper (~2 FPS) and reuse cached strip
        if (_liveWpStripKey == stripKey &&
            _liveWpStripBytes.Length == stripBytes &&
            now - _liveWallpaperCaptureTick < 500)
        {
            Marshal.Copy(_liveWpStripBytes, 0, _dibBits, stripBytes);
            return;
        }

        if (!Win32Api.GetWindowRect(liveWnd, out var rawRect)) return;
        int w = rawRect.Width;
        int h = rawRect.Height;
        if (w <= 0 || h <= 0 || w > 4096 || h > 2560) return;

        if (!EnsureWindowCaptureBuffer(screenDc, w, h)) return;
        if (!Win32Api.PrintWindow(liveWnd, _wndDc, Win32Api.PW_RENDERFULLCONTENT))
            return;

        int interLeft = Math.Max(tbRect.Left, rawRect.Left);
        int interTop = Math.Max(tbRect.Top, rawRect.Top);
        int interRight = Math.Min(tbRect.Right, rawRect.Right);
        int interBottom = Math.Min(tbRect.Bottom, rawRect.Bottom);
        int interW = interRight - interLeft;
        int interH = interBottom - interTop;
        if (interW <= 0 || interH <= 0) return;

        int dstX = interLeft - tbRect.Left;
        int dstY = interTop - tbRect.Top;
        int srcX = interLeft - rawRect.Left;
        int srcY = interTop - rawRect.Top;

        Win32Api.BitBlt(_memDc, dstX, dstY, interW, interH, _wndDc, srcX, srcY, Win32Api.SRCCOPY);

        if (_liveWpStripBytes.Length != stripBytes)
            _liveWpStripBytes = new byte[stripBytes];
        Marshal.Copy(_dibBits, _liveWpStripBytes, 0, stripBytes);
        _liveWpStripKey = stripKey;
        _liveWallpaperCaptureTick = now;
    }

    private void CompositeIntersectingWindows(IntPtr screenDc, IntPtr taskbarHwnd, Win32Api.RECT tbRect)
    {
        var candidates = new List<(IntPtr Hwnd, Win32Api.RECT RawRect, Win32Api.RECT FrameRect)>();

        Win32Api.EnumWindows((wnd, _) =>
        {
            if (wnd == IntPtr.Zero || wnd == taskbarHwnd) return true;
            if (!Win32Api.IsWindowVisible(wnd) || Win32Api.IsIconic(wnd)) return true;

            if (Win32Api.DwmGetWindowAttribute(wnd, Win32Api.DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0)
                return true;

            if (!Win32Api.GetWindowRect(wnd, out var rawRect)) return true;
            if (rawRect.Width <= 10 || rawRect.Height <= 10) return true;

            var frameRect = rawRect;
            if (Win32Api.DwmGetWindowAttribute(wnd, Win32Api.DWMWA_EXTENDED_FRAME_BOUNDS, out Win32Api.RECT extRect, Marshal.SizeOf<Win32Api.RECT>()) == 0 &&
                extRect.Width > 0 && extRect.Height > 0)
            {
                frameRect = extRect;
            }

            // Check intersection with taskbar rect
            if (frameRect.Right <= tbRect.Left || frameRect.Left >= tbRect.Right ||
                frameRect.Bottom <= tbRect.Top || frameRect.Top >= tbRect.Bottom)
            {
                return true;
            }

            var classSb = new StringBuilder(128);
            Win32Api.GetClassName(wnd, classSb, classSb.Capacity);
            string cls = classSb.ToString();
            if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd")
                return true;

            // Exclude WinZenShell's own taskbar/flyout HWNDs, but allow the large Settings window
            Win32Api.GetWindowThreadProcessId(wnd, out uint pid);
            if (pid == CurrentPid)
            {
                if (frameRect.Width < 500 || frameRect.Height < 320)
                    return true;
            }

            candidates.Add((wnd, rawRect, frameRect));
            return candidates.Count < 3; // Cap at top 3 overlapping windows for low latency
        }, IntPtr.Zero);

        if (candidates.Count == 0) return;

        // Composite back-to-front
        for (int i = candidates.Count - 1; i >= 0; i--)
        {
            var (wnd, rawRect, frameRect) = candidates[i];
            int w = rawRect.Width;
            int h = rawRect.Height;
            if (w <= 0 || h <= 0 || w > 4096 || h > 2560) continue;

            if (!EnsureWindowCaptureBuffer(screenDc, w, h)) continue;

            if (!Win32Api.PrintWindow(wnd, _wndDc, Win32Api.PW_RENDERFULLCONTENT))
                continue;

            int interLeft = Math.Max(tbRect.Left, frameRect.Left);
            int interTop = Math.Max(tbRect.Top, frameRect.Top);
            int interRight = Math.Min(tbRect.Right, frameRect.Right);
            int interBottom = Math.Min(tbRect.Bottom, frameRect.Bottom);
            int interW = interRight - interLeft;
            int interH = interBottom - interTop;
            if (interW <= 0 || interH <= 0) continue;

            int dstX = interLeft - tbRect.Left;
            int dstY = interTop - tbRect.Top;
            int srcX = interLeft - rawRect.Left;
            int srcY = interTop - rawRect.Top;

            Win32Api.BitBlt(_memDc, dstX, dstY, interW, interH, _wndDc, srcX, srcY, Win32Api.SRCCOPY);
        }
    }

    private void EnsureWallpaperStrip(int leftPx, int topPx, int widthPx, int heightPx)
    {
        var mon = WorkAreaAppBarService.GetPrimaryMonitorInfo().rcMonitor;
        int screenW = mon.Width > 0 ? mon.Width : 1920;
        int screenH = mon.Height > 0 ? mon.Height : 1080;

        string wpPath = GetCurrentWallpaperPath();
        long writeTicks = 0;
        if (!string.IsNullOrEmpty(wpPath) && File.Exists(wpPath))
        {
            try { writeTicks = File.GetLastWriteTimeUtc(wpPath).Ticks; } catch { }
        }

        string key = $"{wpPath}|{writeTicks}|{leftPx},{topPx},{widthPx},{heightPx}|{screenW}x{screenH}";
        int totalBytes = widthPx * heightPx * 4;
        if (key == _wpCacheKey && _wpBgraBytes.Length == totalBytes)
            return;

        _wpCacheKey = key;
        _wpBgraBytes = new byte[totalBytes];

        if (string.IsNullOrEmpty(wpPath) || !File.Exists(wpPath))
            return;

        try
        {
            byte[] fileBytes = File.ReadAllBytes(wpPath);
            using var msIn = new MemoryStream(fileBytes);
            using var srcImage = Image.FromStream(msIn, false, false);

            double scale = Math.Max((double)screenW / srcImage.Width, (double)screenH / srcImage.Height);
            double dispW = srcImage.Width * scale;
            double dispH = srcImage.Height * scale;
            double offsetX = (dispW - screenW) / 2.0;
            double offsetY = (dispH - screenH) / 2.0;

            float srcX = (float)Math.Max(0, (leftPx + offsetX) / scale);
            float srcY = (float)Math.Max(0, (topPx + offsetY) / scale);
            float srcW = (float)Math.Max(1, Math.Min(srcImage.Width - srcX, widthPx / scale));
            float srcH = (float)Math.Max(1, Math.Min(srcImage.Height - srcY, heightPx / scale));

            using var cropped = new Bitmap(widthPx, heightPx, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(cropped))
            using (var attrs = new ImageAttributes())
            {
                attrs.SetWrapMode(WrapMode.TileFlipXY);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(
                    srcImage,
                    new Rectangle(0, 0, widthPx, heightPx),
                    srcX, srcY, srcW, srcH,
                    GraphicsUnit.Pixel,
                    attrs);
            }

            var bmpData = cropped.LockBits(
                new Rectangle(0, 0, widthPx, heightPx),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);
            try
            {
                Marshal.Copy(bmpData.Scan0, _wpBgraBytes, 0, totalBytes);
            }
            finally
            {
                cropped.UnlockBits(bmpData);
            }
        }
        catch { }
    }

    private static string GetCurrentWallpaperPath()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
            string? path = key?.GetValue("WallPaper") as string;
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                return path;
        }
        catch { }

        try
        {
            string transcoded = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Microsoft", "Windows", "Themes", "TranscodedWallpaper");
            if (File.Exists(transcoded))
                return transcoded;
        }
        catch { }

        return "";
    }

    private bool EnsureWindowCaptureBuffer(IntPtr screenDc, int minW, int minH)
    {
        if (_wndDc != IntPtr.Zero && _wndBitmap != IntPtr.Zero && _wndBufW >= minW && _wndBufH >= minH)
            return true;

        FreeWindowCaptureBuffer();

        int allocW = Math.Max(minW, 1920);
        int allocH = Math.Max(minH, 1080);

        _wndDc = Win32Api.CreateCompatibleDC(screenDc);
        if (_wndDc == IntPtr.Zero) return false;

        var bmi = new Win32Api.BITMAPINFOHEADER
        {
            biSize = (uint)Marshal.SizeOf<Win32Api.BITMAPINFOHEADER>(),
            biWidth = allocW,
            biHeight = -allocH,
            biPlanes = 1,
            biBitCount = 32,
            biCompression = 0
        };

        _wndBitmap = Win32Api.CreateDIBSection(_wndDc, ref bmi, 0, out _, IntPtr.Zero, 0);
        if (_wndBitmap == IntPtr.Zero)
        {
            FreeWindowCaptureBuffer();
            return false;
        }

        _wndOldBitmap = Win32Api.SelectObject(_wndDc, _wndBitmap);
        _wndBufW = allocW;
        _wndBufH = allocH;
        return true;
    }

    private void FreeWindowCaptureBuffer()
    {
        if (_wndDc != IntPtr.Zero && _wndOldBitmap != IntPtr.Zero)
        {
            Win32Api.SelectObject(_wndDc, _wndOldBitmap);
            _wndOldBitmap = IntPtr.Zero;
        }
        if (_wndBitmap != IntPtr.Zero)
        {
            Win32Api.DeleteObject(_wndBitmap);
            _wndBitmap = IntPtr.Zero;
        }
        if (_wndDc != IntPtr.Zero)
        {
            Win32Api.DeleteDC(_wndDc);
            _wndDc = IntPtr.Zero;
        }
        _wndBufW = 0;
        _wndBufH = 0;
    }

    private bool EnsureDibSection(IntPtr screenDc, int w, int h)
    {
        if (_memDc != IntPtr.Zero && _dibBitmap != IntPtr.Zero && _dibBits != IntPtr.Zero && _dibW == w && _dibH == h)
            return true;

        if (_memDc != IntPtr.Zero && _oldBitmap != IntPtr.Zero)
        {
            Win32Api.SelectObject(_memDc, _oldBitmap);
            _oldBitmap = IntPtr.Zero;
        }
        if (_dibBitmap != IntPtr.Zero)
        {
            Win32Api.DeleteObject(_dibBitmap);
            _dibBitmap = IntPtr.Zero;
        }
        if (_memDc != IntPtr.Zero)
        {
            Win32Api.DeleteDC(_memDc);
            _memDc = IntPtr.Zero;
        }

        _memDc = Win32Api.CreateCompatibleDC(screenDc);
        if (_memDc == IntPtr.Zero) return false;

        var bmi = new Win32Api.BITMAPINFOHEADER
        {
            biSize = (uint)Marshal.SizeOf<Win32Api.BITMAPINFOHEADER>(),
            biWidth = w,
            biHeight = -h, // Top-down DIB (row 0 = top of taskbar)
            biPlanes = 1,
            biBitCount = 32,
            biCompression = 0 // BI_RGB
        };

        _dibBitmap = Win32Api.CreateDIBSection(_memDc, ref bmi, 0, out _dibBits, IntPtr.Zero, 0);
        if (_dibBitmap == IntPtr.Zero || _dibBits == IntPtr.Zero)
        {
            FreeGdiResources();
            return false;
        }

        _oldBitmap = Win32Api.SelectObject(_memDc, _dibBitmap);
        _dibW = w;
        _dibH = h;
        return true;
    }

    private void FreeGdiResources()
    {
        FreeWindowCaptureBuffer();
        if (_memDc != IntPtr.Zero && _oldBitmap != IntPtr.Zero)
        {
            Win32Api.SelectObject(_memDc, _oldBitmap);
            _oldBitmap = IntPtr.Zero;
        }
        if (_dibBitmap != IntPtr.Zero)
        {
            Win32Api.DeleteObject(_dibBitmap);
            _dibBitmap = IntPtr.Zero;
        }
        if (_memDc != IntPtr.Zero)
        {
            Win32Api.DeleteDC(_memDc);
            _memDc = IntPtr.Zero;
        }
        _dibBits = IntPtr.Zero;
        _dibW = 0;
        _dibH = 0;
    }

    public void Dispose()
    {
        _isActive = false;
        StopCaptureLoop();
        if (_hwnd != IntPtr.Zero)
        {
            Win32Api.SetWindowDisplayAffinity(_hwnd, Win32Api.WDA_NONE);
        }
        try { _sharedBuffer?.Dispose(); } catch { }
        _sharedBuffer = null;
    }
}
