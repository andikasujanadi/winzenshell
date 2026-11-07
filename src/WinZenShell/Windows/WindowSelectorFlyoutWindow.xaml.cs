using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WinZenShell.Native;
using WinZenShell.Services;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Image = System.Windows.Controls.Image;
using Orientation = System.Windows.Controls.Orientation;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace WinZenShell.Windows;

public class SelectorWindowEntry
{
    public long Hwnd { get; set; }
    public string Title { get; set; } = "";
    public bool IsActive { get; set; }
    public bool IsMinimized { get; set; }
}

public partial class WindowSelectorFlyoutWindow : Window
{
    private readonly SystemMetricsService _metricsService;
    private readonly Action _onActionCompleted;
    private static readonly Dictionary<long, BitmapSource> _thumbnailCache = new();
    private bool _isLightTheme;

    public DateTime LastDeactivatedTime { get; private set; } = DateTime.MinValue;
    public string CurrentAppId { get; private set; } = "";

    public WindowSelectorFlyoutWindow(SystemMetricsService metricsService, Action onActionCompleted)
    {
        InitializeComponent();
        _metricsService = metricsService;
        _onActionCompleted = onActionCompleted;

        Deactivated += (_, _) =>
        {
            LastDeactivatedTime = DateTime.UtcNow;
            if (IsVisible)
            {
                Hide();
            }
        };

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Hide();
            }
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        int exStyle = Win32Api.GetWindowLong(hwnd, Win32Api.GWL_EXSTYLE);
        exStyle |= Win32Api.WS_EX_TOOLWINDOW | Win32Api.WS_EX_TOPMOST;
        exStyle &= ~Win32Api.WS_EX_APPWINDOW;
        Win32Api.SetWindowLong(hwnd, Win32Api.GWL_EXSTYLE, exStyle);
    }

    public void ShowAt(
        string appId,
        string processName,
        string iconDataUrl,
        List<SelectorWindowEntry> windows,
        double anchorScreenX,
        double anchorWidth,
        double taskbarTop,
        double taskbarHeight,
        string dockEdge,
        string? theme = null)
    {
        CurrentAppId = appId ?? "";
        _isLightTheme = string.Equals(theme, "light", StringComparison.OrdinalIgnoreCase);

        if (_isLightTheme)
        {
            OuterBorder.Background = new SolidColorBrush(Color.FromRgb(0xF3, 0xF3, 0xF3));
            OuterBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(0x28, 0x00, 0x00, 0x00));
        }
        else
        {
            OuterBorder.Background = new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2C));
            OuterBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x42, 0x42, 0x42));
        }

        BitmapSource? appIcon = DecodeDataUrl(iconDataUrl);
        BuildWindowCards(processName, appIcon, windows);

        UpdateLayout();
        Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Arrange(new Rect(0, 0, DesiredSize.Width, DesiredSize.Height));

        double flyoutWidth = DesiredSize.Width > 40 ? DesiredSize.Width : (windows.Count * 226 + 40);
        double flyoutHeight = DesiredSize.Height > 40 ? DesiredSize.Height : 202;

        double left = anchorScreenX + (anchorWidth / 2.0) - (flyoutWidth / 2.0);
        const double floatingGap = 4.0;
        double top;

        if (string.Equals(dockEdge, "Top", StringComparison.OrdinalIgnoreCase))
        {
            top = taskbarTop + taskbarHeight + floatingGap;
        }
        else
        {
            top = taskbarTop - flyoutHeight - floatingGap;
        }

        var mon = WorkAreaAppBarService.GetPrimaryMonitorInfo().rcMonitor;
        double monitorLeft = mon.Left;
        double monitorRight = mon.Right;
        const double sideMargin = 10.0;

        if (left + flyoutWidth > monitorRight - sideMargin)
            left = monitorRight - flyoutWidth - sideMargin;
        if (left < monitorLeft + sideMargin)
            left = monitorLeft + sideMargin;

        Left = left;
        Top = top;

        Show();
        Activate();
        Focus();
    }

    private void BuildWindowCards(string processName, BitmapSource? appIcon, List<SelectorWindowEntry> windows)
    {
        CardsPanel.Children.Clear();

        Brush textBrush = _isLightTheme
            ? new SolidColorBrush(Color.FromRgb(0x1B, 0x1B, 0x1B))
            : new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0));

        Brush cardHoverBrush = _isLightTheme
            ? new SolidColorBrush(Color.FromRgb(0xE4, 0xE4, 0xE4))
            : new SolidColorBrush(Color.FromRgb(0x3B, 0x3B, 0x3B));

        Brush cardActiveBrush = _isLightTheme
            ? new SolidColorBrush(Color.FromRgb(0xEA, 0xEA, 0xEA))
            : new SolidColorBrush(Color.FromRgb(0x34, 0x34, 0x34));

        foreach (var win in windows)
        {
            Brush defaultBg = win.IsActive ? cardActiveBrush : Brushes.Transparent;

            var cardBorder = new Border
            {
                Width = 216,
                Height = 162,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 8, 10, 10),
                Margin = new Thickness(4, 2, 4, 2),
                Background = defaultBg,
                Cursor = Cursors.Hand,
                SnapsToDevicePixels = true
            };

            var cardStack = new StackPanel
            {
                Orientation = Orientation.Vertical
            };

            // Header Row: App Icon + Window Title + Close Button
            var headerGrid = new Grid
            {
                Height = 22
            };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            if (appIcon != null)
            {
                var iconImg = new Image
                {
                    Source = appIcon,
                    Width = 16,
                    Height = 16,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0)
                };
                RenderOptions.SetBitmapScalingMode(iconImg, BitmapScalingMode.HighQuality);
                Grid.SetColumn(iconImg, 0);
                headerGrid.Children.Add(iconImg);
            }

            var titleBlock = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(win.Title) ? processName : win.Title,
                Foreground = textBrush,
                FontSize = 12.5,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Text, Segoe UI"),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 6, 0)
            };
            Grid.SetColumn(titleBlock, 1);
            headerGrid.Children.Add(titleBlock);

            var closeBtn = new Border
            {
                Width = 22,
                Height = 22,
                CornerRadius = new CornerRadius(4),
                Background = Brushes.Transparent,
                Opacity = 0.0,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Close window"
            };
            var closeGlyph = new TextBlock
            {
                Text = "✕",
                FontSize = 10.5,
                Foreground = textBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            closeBtn.Child = closeGlyph;

            closeBtn.MouseEnter += (_, _) =>
            {
                closeBtn.Background = new SolidColorBrush(Color.FromRgb(0xE8, 0x11, 0x23));
                closeGlyph.Foreground = Brushes.White;
            };
            closeBtn.MouseLeave += (_, _) =>
            {
                closeBtn.Background = Brushes.Transparent;
                closeGlyph.Foreground = textBrush;
            };
            closeBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                _metricsService.CloseWindow(win.Hwnd);
                CardsPanel.Children.Remove(cardBorder);
                _onActionCompleted();
                if (CardsPanel.Children.Count == 0)
                {
                    Hide();
                }
            };

            Grid.SetColumn(closeBtn, 2);
            headerGrid.Children.Add(closeBtn);
            cardStack.Children.Add(headerGrid);

            // Preview Thumbnail Box
            var thumbBorder = new Border
            {
                Width = 196,
                Height = 114,
                Margin = new Thickness(0, 8, 0, 0),
                CornerRadius = new CornerRadius(4),
                ClipToBounds = true,
                Background = new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x18)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x38, 0x38, 0x38)),
                BorderThickness = new Thickness(1)
            };

            BitmapSource? thumb = CaptureOrGetThumbnail(win.Hwnd);
            if (thumb != null)
            {
                var thumbImg = new Image
                {
                    Source = thumb,
                    Stretch = Stretch.UniformToFill,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Top
                };
                RenderOptions.SetBitmapScalingMode(thumbImg, BitmapScalingMode.HighQuality);
                thumbBorder.Child = thumbImg;
            }
            else
            {
                // Clean realistic window mockup fallback when window is minimized without prior cache
                var fallbackGrid = new Grid
                {
                    Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A))
                };
                var topChrome = new Border
                {
                    Height = 20,
                    VerticalAlignment = VerticalAlignment.Top,
                    Background = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x25)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
                    BorderThickness = new Thickness(0, 0, 0, 1)
                };
                fallbackGrid.Children.Add(topChrome);

                if (appIcon != null)
                {
                    var centerIcon = new Image
                    {
                        Source = appIcon,
                        Width = 36,
                        Height = 36,
                        Margin = new Thickness(0, 14, 0, 0),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    RenderOptions.SetBitmapScalingMode(centerIcon, BitmapScalingMode.HighQuality);
                    fallbackGrid.Children.Add(centerIcon);
                }
                thumbBorder.Child = fallbackGrid;
            }

            cardStack.Children.Add(thumbBorder);
            cardBorder.Child = cardStack;

            cardBorder.MouseEnter += (_, _) =>
            {
                cardBorder.Background = cardHoverBrush;
                closeBtn.Opacity = 1.0;
            };
            cardBorder.MouseLeave += (_, _) =>
            {
                cardBorder.Background = defaultBg;
                closeBtn.Opacity = 0.0;
            };
            cardBorder.MouseLeftButtonUp += (_, _) =>
            {
                Hide();
                _metricsService.ActivateWindowOnly(win.Hwnd);
                _onActionCompleted();
            };

            CardsPanel.Children.Add(cardBorder);
        }
    }

    public static void CacheWindowThumbnailIfVisible(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero || Win32Api.IsIconic(hWnd)) return;
        CaptureOrGetThumbnail(hWnd.ToInt64(), forceCapture: false);
    }

    public static BitmapSource? CaptureOrGetThumbnail(long hwndVal, bool forceCapture = true)
    {
        IntPtr hWnd = new IntPtr(hwndVal);
        if (hWnd == IntPtr.Zero) return null;

        if (!forceCapture && _thumbnailCache.ContainsKey(hwndVal))
            return _thumbnailCache[hwndVal];

        if (!Win32Api.IsIconic(hWnd) && Win32Api.GetWindowRect(hWnd, out var rc))
        {
            int w = rc.Width;
            int h = rc.Height;
            if (w > 40 && h > 40)
            {
                try
                {
                    using var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                    using (var g = Graphics.FromImage(bmp))
                    {
                        IntPtr hdc = g.GetHdc();
                        bool ok = Win32Api.PrintWindow(hWnd, hdc, Win32Api.PW_RENDERFULLCONTENT);
                        g.ReleaseHdc(hdc);
                        if (!ok)
                        {
                            if (_thumbnailCache.TryGetValue(hwndVal, out var prev))
                                return prev;
                            return null;
                        }
                    }

                    const int targetW = 392;
                    int targetH = Math.Max(120, (int)Math.Round((double)h / w * targetW));
                    using var resized = new Bitmap(bmp, new System.Drawing.Size(targetW, targetH));
                    using var ms = new MemoryStream();
                    resized.Save(ms, ImageFormat.Png);
                    ms.Position = 0;

                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.StreamSource = ms;
                    bi.EndInit();
                    bi.Freeze();

                    _thumbnailCache[hwndVal] = bi;
                    return bi;
                }
                catch
                {
                    // Fall through to cache
                }
            }
        }

        return _thumbnailCache.TryGetValue(hwndVal, out var cached) ? cached : null;
    }

    private static BitmapSource? DecodeDataUrl(string? dataUrl)
    {
        if (string.IsNullOrWhiteSpace(dataUrl)) return null;
        try
        {
            int commaIdx = dataUrl.IndexOf(',');
            string b64 = commaIdx >= 0 ? dataUrl[(commaIdx + 1)..] : dataUrl;
            byte[] bytes = Convert.FromBase64String(b64);
            using var ms = new MemoryStream(bytes);
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.StreamSource = ms;
            bi.EndInit();
            bi.Freeze();
            return bi;
        }
        catch
        {
            return null;
        }
    }

    public void CaptureToFile(string outPath)
    {
        try
        {
            UpdateLayout();
            int w = Math.Max(1, (int)Math.Ceiling(ActualWidth));
            int h = Math.Max(1, (int)Math.Ceiling(ActualHeight));
            var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(this);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using var fs = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.None);
            encoder.Save(fs);
        }
        catch { }
    }
}
