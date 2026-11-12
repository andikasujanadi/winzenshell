using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using Microsoft.Win32;
using WinZenShell.Native;

namespace WinZenShell.Services;

/// <summary>
/// Extracts and caches a clean strip of the current desktop wallpaper matching
/// the taskbar's physical screen bounds so WebView2's GPU filter can blur it without artifacts.
/// </summary>
public sealed class WallpaperBackdropService
{
    private string _lastCacheKey = "";
    private string _cachedDataUrl = "";
    private readonly object _lock = new();

    public string GetBlurredTaskbarStripDataUrl(
        int leftPx,
        int topPx,
        int widthPx,
        int heightPx,
        int screenWidthPx,
        int screenHeightPx,
        int blurStrength)
    {
        if (widthPx <= 0 || heightPx <= 0 || screenWidthPx <= 0 || screenHeightPx <= 0)
            return "";

        string wallpaperPath = GetCurrentWallpaperPath();
        if (string.IsNullOrWhiteSpace(wallpaperPath) || !File.Exists(wallpaperPath))
            return "";

        long writeTicks = 0;
        try { writeTicks = File.GetLastWriteTimeUtc(wallpaperPath).Ticks; } catch { }

        string cacheKey = $"{wallpaperPath}|{writeTicks}|{leftPx},{topPx},{widthPx},{heightPx}|{screenWidthPx}x{screenHeightPx}";

        lock (_lock)
        {
            if (cacheKey == _lastCacheKey && !string.IsNullOrEmpty(_cachedDataUrl))
                return _cachedDataUrl;
        }

        try
        {
            byte[] fileBytes = File.ReadAllBytes(wallpaperPath);
            using var msIn = new MemoryStream(fileBytes);
            using var srcImage = Image.FromStream(msIn, false, false);

            // Windows "Fill" scaling maps the wallpaper to cover screenWidthPx x screenHeightPx
            double scale = Math.Max((double)screenWidthPx / srcImage.Width, (double)screenHeightPx / srcImage.Height);
            double dispW = srcImage.Width * scale;
            double dispH = srcImage.Height * scale;
            double offsetX = (dispW - screenWidthPx) / 2.0;
            double offsetY = (dispH - screenHeightPx) / 2.0;

            float srcX = (float)Math.Max(0, (leftPx + offsetX) / scale);
            float srcY = (float)Math.Max(0, (topPx + offsetY) / scale);
            float srcW = (float)Math.Max(1, Math.Min(srcImage.Width - srcX, widthPx / scale));
            float srcH = (float)Math.Max(1, Math.Min(srcImage.Height - srcY, heightPx / scale));

            int outW = Math.Clamp(widthPx / 2, 320, 960);
            int outH = Math.Clamp(heightPx, 24, 96);

            using var cropped = new Bitmap(outW, outH, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(cropped))
            using (var attrs = new ImageAttributes())
            {
                attrs.SetWrapMode(WrapMode.TileFlipXY);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(
                    srcImage,
                    new Rectangle(0, 0, outW, outH),
                    srcX, srcY, srcW, srcH,
                    GraphicsUnit.Pixel,
                    attrs);
            }

            using var msOut = new MemoryStream();
            cropped.Save(msOut, ImageFormat.Png);
            string dataUrl = $"data:image/png;base64,{Convert.ToBase64String(msOut.ToArray())}";

            lock (_lock)
            {
                _lastCacheKey = cacheKey;
                _cachedDataUrl = dataUrl;
            }
            return dataUrl;
        }
        catch
        {
            return "";
        }
    }

    private static string GetCurrentWallpaperPath()
    {
        try
        {
            var sb = new StringBuilder(512);
            if (Win32Api.SystemParametersInfo(Win32Api.SPI_GETDESKWALLPAPER, (uint)sb.Capacity, sb, 0))
            {
                string p = sb.ToString().TrimEnd('\0');
                if (!string.IsNullOrWhiteSpace(p) && File.Exists(p))
                    return p;
            }
        }
        catch { }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
            string? regPath = key?.GetValue("WallPaper") as string;
            if (!string.IsNullOrWhiteSpace(regPath) && File.Exists(regPath))
                return regPath;
        }
        catch { }

        return "";
    }
}
