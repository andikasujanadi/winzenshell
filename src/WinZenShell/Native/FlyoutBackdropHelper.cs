using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using MediaColor = System.Windows.Media.Color;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;

namespace WinZenShell.Native;

/// <summary>
/// Applies BackgroundOpacity, BackgroundBlurStrength, and BlurEngine (wallpaper or realtime)
/// to any flyout window's OuterBorder using a DrawingBrush clipped to OuterBorder.CornerRadius
/// so there is zero corner bleed and zero DWM drop shadow.
/// </summary>
public static class FlyoutBackdropHelper
{
    private sealed class BackdropState
    {
        public int Token;
        public WriteableBitmap? LastRealtimeBitmap;
        public int LastLeft;
        public int LastTop;
        public int LastW;
        public int LastH;
        public int LastBlur;
    }

    private static readonly int CurrentPid = Environment.ProcessId;
    private static readonly ConditionalWeakTable<Window, BackdropState> _windowStates = new();
    private static readonly object _wpCacheLock = new();
    private static string _wpPathCache = "";
    private static long _wpTicksCache;
    private static Bitmap? _wpScreenBmpCache;
    private static int _wpScreenW;
    private static int _wpScreenH;

    public static void ApplyTo(Window window, double finalTopDip)
    {
        if (window.FindName("OuterBorder") is not Border outerBorder)
            return;

        var bState = _windowStates.GetOrCreateValue(window);
        int token = ++bState.Token;

        int opacity = Math.Clamp(FlyoutAnimationHelper.FlyoutBackgroundOpacity, 0, 100);
        int blurStrength = Math.Clamp(FlyoutAnimationHelper.FlyoutBackgroundBlurStrength, 0, 100);
        bool isLight = FlyoutAnimationHelper.IsLightTheme;
        bool useRealtime = string.Equals(FlyoutAnimationHelper.BlurEngine, "realtime", StringComparison.OrdinalIgnoreCase);

        byte alpha = (byte)Math.Round(opacity / 100.0 * 255.0);
        byte r = isLight ? (byte)0xF3 : (byte)0x1E;
        byte g = isLight ? (byte)0xF3 : (byte)0x1E;
        byte b = isLight ? (byte)0xF3 : (byte)0x1E;
        var tintColor = MediaColor.FromArgb(alpha, r, g, b);

        if (opacity >= 100)
        {
            outerBorder.Background = new SolidColorBrush(MediaColor.FromRgb(r, g, b));
            return;
        }

        if (blurStrength <= 0)
        {
            outerBorder.Background = new SolidColorBrush(tintColor);
            return;
        }

        double dipW = !double.IsNaN(window.Width) && window.Width > 0 ? window.Width : window.ActualWidth;
        double dipH = !double.IsNaN(window.Height) && window.Height > 0 ? window.Height : window.ActualHeight;
        if (dipW <= 4 || dipH <= 4)
        {
            try
            {
                window.Measure(new System.Windows.Size(
                    !double.IsNaN(window.Width) && window.Width > 0 ? window.Width : double.PositiveInfinity,
                    !double.IsNaN(window.Height) && window.Height > 0 ? window.Height : double.PositiveInfinity));
                if (dipW <= 4 && window.DesiredSize.Width > 4) dipW = window.DesiredSize.Width;
                if (dipH <= 4 && window.DesiredSize.Height > 4) dipH = window.DesiredSize.Height;
            }
            catch { }
        }
        if (dipW <= 4 || dipH <= 4)
        {
            outerBorder.Background = new SolidColorBrush(tintColor);
            return;
        }

        double dpiX = 1.0, dpiY = 1.0;
        try
        {
            var dpi = VisualTreeHelper.GetDpi(window);
            if (dpi.DpiScaleX > 0) dpiX = dpi.DpiScaleX;
            if (dpi.DpiScaleY > 0) dpiY = dpi.DpiScaleY;
        }
        catch { }

        int pxLeft = (int)Math.Round(window.Left * dpiX);
        int pxTop = (int)Math.Round(finalTopDip * dpiY);
        int pxW = Math.Max(8, (int)Math.Round(dipW * dpiX));
        int pxH = Math.Max(8, (int)Math.Round(dipH * dpiY));

        int smallW = Math.Clamp(pxW / 4, 8, 480);
        int smallH = Math.Clamp(pxH / 4, 8, 360);
        int radius = Math.Clamp((int)Math.Round(1 + (blurStrength / 100.0) * 12.0), 1, 16);

        WriteableBitmap baseWb;
        bool hasCachedRealtime = useRealtime &&
            bState.LastRealtimeBitmap != null &&
            bState.LastLeft == pxLeft &&
            bState.LastTop == pxTop &&
            bState.LastW == smallW &&
            bState.LastH == smallH &&
            bState.LastBlur == blurStrength;

        if (hasCachedRealtime)
        {
            baseWb = bState.LastRealtimeBitmap!;
        }
        else
        {
            byte[]? wpBgra = CaptureRegionBgra(pxLeft, pxTop, pxW, pxH, smallW, smallH, includeWindows: false);
            if (wpBgra == null)
            {
                outerBorder.Background = new SolidColorBrush(tintColor);
                return;
            }

            SeparableBoxBlurBgra(wpBgra, smallW, smallH, radius);
            SeparableBoxBlurBgra(wpBgra, smallW, smallH, Math.Max(1, radius - 1));

            baseWb = new WriteableBitmap(smallW, smallH, 96, 96, PixelFormats.Pbgra32, null);
            baseWb.WritePixels(new Int32Rect(0, 0, smallW, smallH), wpBgra, smallW * 4, 0);
            baseWb.Freeze();
        }

        var unitRect = new Rect(0, 0, 1, 1);
        var tintDrawing = new GeometryDrawing(new SolidColorBrush(tintColor), null, new RectangleGeometry(unitRect));
        tintDrawing.Freeze();

        if (!useRealtime)
        {
            bState.LastRealtimeBitmap = null;
            var group = new DrawingGroup();
            group.Children.Add(new ImageDrawing(baseWb, unitRect));
            group.Children.Add(tintDrawing);
            group.Freeze();

            var brush = new DrawingBrush(group)
            {
                Stretch = Stretch.Fill,
                Viewbox = unitRect,
                ViewboxUnits = BrushMappingMode.RelativeToBoundingBox
            };
            brush.Freeze();
            outerBorder.Background = brush;
            return;
        }

        // Realtime mode: show cached realtime (or wallpaper) blur immediately, then capture live windows in background & cross-fade
        var rtImageDrawing = new ImageDrawing(baseWb, unitRect);
        var rtOverlayGroup = new DrawingGroup { Opacity = 0.0 };
        rtOverlayGroup.Children.Add(rtImageDrawing);

        var rootGroup = new DrawingGroup();
        rootGroup.Children.Add(new ImageDrawing(baseWb, unitRect));
        rootGroup.Children.Add(rtOverlayGroup);
        rootGroup.Children.Add(tintDrawing);

        var rtBrush = new DrawingBrush(rootGroup)
        {
            Stretch = Stretch.Fill,
            Viewbox = unitRect,
            ViewboxUnits = BrushMappingMode.RelativeToBoundingBox
        };
        outerBorder.Background = rtBrush;

        IntPtr targetHwnd = IntPtr.Zero;
        try { targetHwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle; } catch { }

        _ = Task.Run(() =>
        {
            byte[]? rtBgra = CaptureRegionBgra(pxLeft, pxTop, pxW, pxH, smallW, smallH, includeWindows: true, excludeHwnd: targetHwnd);
            if (rtBgra == null) return;
            SeparableBoxBlurBgra(rtBgra, smallW, smallH, radius);
            SeparableBoxBlurBgra(rtBgra, smallW, smallH, Math.Max(1, radius - 1));

            window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                if (bState.Token != token || !window.IsVisible) return;
                var rtWb = new WriteableBitmap(smallW, smallH, 96, 96, PixelFormats.Pbgra32, null);
                rtWb.WritePixels(new Int32Rect(0, 0, smallW, smallH), rtBgra, smallW * 4, 0);
                rtWb.Freeze();

                bState.LastRealtimeBitmap = rtWb;
                bState.LastLeft = pxLeft;
                bState.LastTop = pxTop;
                bState.LastW = smallW;
                bState.LastH = smallH;
                bState.LastBlur = blurStrength;

                rtOverlayGroup.Opacity = 0.0;
                rtImageDrawing.ImageSource = rtWb;

                var fade = new DoubleAnimation(0.0, 1.0, new Duration(TimeSpan.FromMilliseconds(380)))
                {
                    BeginTime = TimeSpan.FromMilliseconds(16),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                    FillBehavior = FillBehavior.HoldEnd
                };
                rtOverlayGroup.BeginAnimation(DrawingGroup.OpacityProperty, fade, HandoffBehavior.SnapshotAndReplace);
            }));
        });
    }

    private static byte[]? CaptureRegionBgra(
        int leftPx, int topPx, int widthPx, int heightPx,
        int outW, int outH, bool includeWindows, IntPtr excludeHwnd = default)
    {
        try
        {
            using var bmp = new Bitmap(outW, outH, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = InterpolationMode.Bilinear;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.Clear(System.Drawing.Color.FromArgb(255, 24, 24, 24));

                DrawWallpaperRegion(g, leftPx, topPx, widthPx, heightPx, outW, outH);

                if (includeWindows)
                {
                    CompositeIntersectingWindows(g, leftPx, topPx, widthPx, heightPx, outW, outH, excludeHwnd);
                }
            }

            byte[] bytes = new byte[outW * outH * 4];
            var data = bmp.LockBits(
                new Rectangle(0, 0, outW, outH),
                ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            try
            {
                Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
            }
            finally
            {
                bmp.UnlockBits(data);
            }
            return bytes;
        }
        catch
        {
            return null;
        }
    }

    private static void DrawWallpaperRegion(
        Graphics g, int leftPx, int topPx, int widthPx, int heightPx, int outW, int outH)
    {
        string wpPath = GetCurrentWallpaperPath();
        if (string.IsNullOrWhiteSpace(wpPath) || !File.Exists(wpPath))
            return;

        var mon = WorkAreaAppBarService.GetPrimaryMonitorInfo().rcMonitor;
        int screenW = mon.Width > 0 ? mon.Width : 1920;
        int screenH = mon.Height > 0 ? mon.Height : 1080;

        long ticks = 0;
        try { ticks = File.GetLastWriteTimeUtc(wpPath).Ticks; } catch { }

        lock (_wpCacheLock)
        {
            if (wpPath != _wpPathCache || ticks != _wpTicksCache || _wpScreenBmpCache == null || _wpScreenW != screenW || _wpScreenH != screenH)
            {
                byte[] fileBytes = File.ReadAllBytes(wpPath);
                using var msIn = new MemoryStream(fileBytes);
                using var srcImage = System.Drawing.Image.FromStream(msIn, false, false);
                double scale = Math.Max((double)screenW / srcImage.Width, (double)screenH / srcImage.Height);
                int dispW = (int)Math.Ceiling(srcImage.Width * scale);
                int dispH = (int)Math.Ceiling(srcImage.Height * scale);
                int offsetX = (dispW - screenW) / 2;
                int offsetY = (dispH - screenH) / 2;

                var cachedBmp = new Bitmap(screenW, screenH, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                using (var cg = Graphics.FromImage(cachedBmp))
                {
                    cg.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    cg.DrawImage(srcImage, new Rectangle(-offsetX, -offsetY, dispW, dispH));
                }

                _wpScreenBmpCache?.Dispose();
                _wpScreenBmpCache = cachedBmp;
                _wpPathCache = wpPath;
                _wpTicksCache = ticks;
                _wpScreenW = screenW;
                _wpScreenH = screenH;
            }

            using var attrs = new ImageAttributes();
            attrs.SetWrapMode(WrapMode.TileFlipXY);
            g.DrawImage(_wpScreenBmpCache, new Rectangle(0, 0, outW, outH), leftPx, topPx, widthPx, heightPx, GraphicsUnit.Pixel, attrs);
        }
    }

    private static void CompositeIntersectingWindows(
        Graphics g, int leftPx, int topPx, int widthPx, int heightPx, int outW, int outH, IntPtr excludeHwnd)
    {
        int rightPx = leftPx + widthPx;
        int bottomPx = topPx + heightPx;
        var candidates = new List<(IntPtr Hwnd, Win32Api.RECT RawRect, Win32Api.RECT FrameRect)>();

        Win32Api.EnumWindows((wnd, _) =>
        {
            if (wnd == IntPtr.Zero || wnd == excludeHwnd || !Win32Api.IsWindowVisible(wnd) || Win32Api.IsIconic(wnd))
                return true;

            if (Win32Api.DwmGetWindowAttribute(wnd, Win32Api.DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0)
                return true;

            if (!Win32Api.GetWindowRect(wnd, out var rawRect) || rawRect.Width <= 10 || rawRect.Height <= 10)
                return true;

            var frameRect = rawRect;
            if (Win32Api.DwmGetWindowAttribute(wnd, Win32Api.DWMWA_EXTENDED_FRAME_BOUNDS, out Win32Api.RECT extRect, Marshal.SizeOf<Win32Api.RECT>()) == 0 &&
                extRect.Width > 0 && extRect.Height > 0)
            {
                frameRect = extRect;
            }

            if (frameRect.Right <= leftPx || frameRect.Left >= rightPx ||
                frameRect.Bottom <= topPx || frameRect.Top >= bottomPx)
            {
                return true;
            }

            var classSb = new StringBuilder(128);
            Win32Api.GetClassName(wnd, classSb, classSb.Capacity);
            string cls = classSb.ToString();
            if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd")
                return true;

            Win32Api.GetWindowThreadProcessId(wnd, out uint pid);
            if (pid == CurrentPid)
                return true;

            candidates.Add((wnd, rawRect, frameRect));
            return candidates.Count < 3;
        }, IntPtr.Zero);

        if (candidates.Count == 0) return;

        double sx = (double)outW / widthPx;
        double sy = (double)outH / heightPx;

        for (int i = candidates.Count - 1; i >= 0; i--)
        {
            var (wnd, rawRect, frameRect) = candidates[i];
            int w = rawRect.Width;
            int h = rawRect.Height;
            if (w <= 0 || h <= 0 || w > 4096 || h > 2560) continue;

            using var wndBmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            bool ok;
            using (var wg = Graphics.FromImage(wndBmp))
            {
                IntPtr hdc = wg.GetHdc();
                ok = Win32Api.PrintWindow(wnd, hdc, Win32Api.PW_RENDERFULLCONTENT);
                wg.ReleaseHdc(hdc);
            }
            if (!ok) continue;

            int interLeft = Math.Max(leftPx, frameRect.Left);
            int interTop = Math.Max(topPx, frameRect.Top);
            int interRight = Math.Min(rightPx, frameRect.Right);
            int interBottom = Math.Min(bottomPx, frameRect.Bottom);
            int interW = interRight - interLeft;
            int interH = interBottom - interTop;
            if (interW <= 0 || interH <= 0) continue;

            var dstRect = new RectangleF(
                (float)((interLeft - leftPx) * sx),
                (float)((interTop - topPx) * sy),
                (float)(interW * sx),
                (float)(interH * sy));

            g.DrawImage(
                wndBmp,
                dstRect,
                new RectangleF(interLeft - rawRect.Left, interTop - rawRect.Top, interW, interH),
                GraphicsUnit.Pixel);
        }
    }

    private static void SeparableBoxBlurBgra(byte[] data, int w, int h, int radius)
    {
        if (radius <= 0 || w <= 1 || h <= 1) return;
        byte[] tmp = new byte[data.Length];
        int diam = radius * 2 + 1;

        // Horizontal pass
        for (int y = 0; y < h; y++)
        {
            int row = y * w * 4;
            int sumB = 0, sumG = 0, sumR = 0;
            for (int k = -radius; k <= radius; k++)
            {
                int cx = Math.Clamp(k, 0, w - 1) * 4;
                sumB += data[row + cx];
                sumG += data[row + cx + 1];
                sumR += data[row + cx + 2];
            }
            for (int x = 0; x < w; x++)
            {
                int dst = row + x * 4;
                tmp[dst] = (byte)(sumB / diam);
                tmp[dst + 1] = (byte)(sumG / diam);
                tmp[dst + 2] = (byte)(sumR / diam);
                tmp[dst + 3] = 255;

                int addX = Math.Clamp(x + radius + 1, 0, w - 1) * 4;
                int remX = Math.Clamp(x - radius, 0, w - 1) * 4;
                sumB += data[row + addX] - data[row + remX];
                sumG += data[row + addX + 1] - data[row + remX + 1];
                sumR += data[row + addX + 2] - data[row + remX + 2];
            }
        }

        // Vertical pass
        int stride = w * 4;
        for (int x = 0; x < w; x++)
        {
            int col = x * 4;
            int sumB = 0, sumG = 0, sumR = 0;
            for (int k = -radius; k <= radius; k++)
            {
                int cy = Math.Clamp(k, 0, h - 1) * stride;
                sumB += tmp[cy + col];
                sumG += tmp[cy + col + 1];
                sumR += tmp[cy + col + 2];
            }
            for (int y = 0; y < h; y++)
            {
                int dst = y * stride + col;
                data[dst] = (byte)(sumB / diam);
                data[dst + 1] = (byte)(sumG / diam);
                data[dst + 2] = (byte)(sumR / diam);
                data[dst + 3] = 255;

                int addY = Math.Clamp(y + radius + 1, 0, h - 1) * stride;
                int remY = Math.Clamp(y - radius, 0, h - 1) * stride;
                sumB += tmp[addY + col] - tmp[remY + col];
                sumG += tmp[addY + col + 1] - tmp[remY + col + 1];
                sumR += tmp[addY + col + 2] - tmp[remY + col + 2];
            }
        }
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
}
