using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WinZenShell.Native;
using WinZenShell.Services;
using Button = System.Windows.Controls.Button;
using Image = System.Windows.Controls.Image;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Forms = System.Windows.Forms;

namespace WinZenShell.Windows;

public partial class TrayOverflowFlyoutWindow : Window
{
    private readonly TrayIconService _trayService;
    private readonly Action? _onWinZenSelfClick;
    private readonly Action? _onChanged;
    private readonly Dictionary<string, BitmapImage> _bitmapCache = new(StringComparer.Ordinal);
    private string _lastRenderSignature = "";
    private SolidColorBrush _accentBrush = new(Color.FromRgb(0x36, 0x84, 0xE3));
    private bool _isLightTheme;
    private bool _isContextMenuOpen;
    private System.Windows.Threading.DispatcherTimer? _nativeMenuWatchTimer;
    public DateTime LastDeactivatedTime { get; private set; } = DateTime.MinValue;

    public TrayOverflowFlyoutWindow(TrayIconService trayService, Action? onWinZenSelfClick = null, Action? onChanged = null)
    {
        InitializeComponent();
        _trayService = trayService;
        _onWinZenSelfClick = onWinZenSelfClick;
        _onChanged = onChanged;

        Deactivated += (_, _) =>
        {
            if (_isContextMenuOpen) return;
            FlyoutAnimationHelper.HandleDeactivated(this, () => LastDeactivatedTime = DateTime.UtcNow);
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int ncrp = 1; // DWMNCRP_DISABLED
        Win32Api.DwmSetWindowAttribute(hwnd, 2, ref ncrp, sizeof(int));
        int borderColor = unchecked((int)0xFFFFFFFE); // DWMWA_COLOR_NONE
        Win32Api.DwmSetWindowAttribute(hwnd, 34, ref borderColor, sizeof(int));
        int cornerPref = 1; // DWMWCP_DONOTROUND
        Win32Api.DwmSetWindowAttribute(hwnd, 33, ref cornerPref, sizeof(int));
    }

    public void RefreshIcons(bool forceScan = true)
    {
        var all = _trayService.GetActiveTrayIcons(forceScan);
        ApplyIconsList(all);
    }

    private void ApplyIconsList(List<TrayIconItem> all)
    {
        var overflow = all.Where(i => !i.IsPromoted).ToList();

        // If user has no overflow items yet, show all active items so the container is never blank when clicked
        var display = overflow.Count > 0 ? overflow : all;
        string countText = overflow.Count > 0 ? $"{overflow.Count} hidden" : $"{all.Count} active";
        TxtCount.Text = countText;

        string sig = $"{_isLightTheme}|{_accentBrush.Color}|{countText}|" +
                     string.Join(";", display.Select(i => $"{i.Id}:{(i.IsPromoted ? 1 : 0)}:{i.Tooltip}"));
        if (sig == _lastRenderSignature && (IconsWrapPanel.Children.Count > 0 || display.Count == 0))
            return;
        _lastRenderSignature = sig;

        IconsWrapPanel.Children.Clear();
        if (display.Count == 0)
        {
            TxtEmpty.Visibility = Visibility.Visible;
            TxtHint.Visibility = Visibility.Collapsed;
            return;
        }

        TxtEmpty.Visibility = Visibility.Collapsed;
        TxtHint.Visibility = Visibility.Visible;

        foreach (var item in display)
        {
            IconsWrapPanel.Children.Add(BuildIconTile(item));
        }
    }

    private Button BuildIconTile(TrayIconItem item)
    {
        var img = CreateIconElement(item);
        var innerGrid = new Grid();
        innerGrid.Children.Add(img);

        if (item.IsPromoted)
        {
            var pinDot = new Border
            {
                Width = 5,
                Height = 5,
                CornerRadius = new CornerRadius(2.5),
                Background = _accentBrush,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, -2, -2, 0)
            };
            innerGrid.Children.Add(pinDot);
        }

        var template = new ControlTemplate(typeof(Button));
        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.Name = "TileBorder";
        borderFactory.SetValue(Border.BackgroundProperty, Resources["TileBg"]);
        borderFactory.SetValue(Border.BorderBrushProperty, Resources["TileBorder"]);
        borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        borderFactory.SetValue(Border.PaddingProperty, new Thickness(8));

        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
        cp.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        borderFactory.AppendChild(cp);
        template.VisualTree = borderFactory;

        var hoverTrigger = new Trigger { Property = IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, Resources["TileHoverBg"], "TileBorder"));
        template.Triggers.Add(hoverTrigger);

        var btn = new Button
        {
            Width = 38,
            Height = 38,
            Margin = new Thickness(3),
            Content = innerGrid,
            Template = template,
            ToolTip = item.Tooltip + (item.IsPromoted ? "\n(Pinned to taskbar — right-click for options)" : "\n(Right-click for options)"),
            Cursor = System.Windows.Input.Cursors.Hand
        };

        btn.Click += (_, _) =>
        {
            Hide();
            _trayService.ActivateTrayIcon(item.Id, rightClick: false, onWinZenSelfClick: _onWinZenSelfClick);
        };

        btn.MouseRightButtonUp += (_, e) =>
        {
            e.Handled = true;
            bool shiftHeld = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) != 0;
            if (item.HasNativeCallback && !shiftHeld)
            {
                _isContextMenuOpen = true;
                _trayService.ActivateTrayIcon(item.Id, rightClick: true, _onWinZenSelfClick);
                StartNativeMenuWatch();
                return;
            }
            ShowTrayItemContextMenu(item);
        };

        return btn;
    }

    private void StartNativeMenuWatch()
    {
        _nativeMenuWatchTimer?.Stop();
        DateTime startedUtc = DateTime.UtcNow;
        bool sawMenu = false;
        _nativeMenuWatchTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _nativeMenuWatchTimer.Tick += (_, _) =>
        {
            double elapsedMs = (DateTime.UtcNow - startedUtc).TotalMilliseconds;
            bool menuVisible = Win32Api.FindWindow("#32768", null) != IntPtr.Zero;
            if (menuVisible) sawMenu = true;

            // Keep flyout alive while menu is open (or during initial 600ms launch window)
            if (menuVisible || (!sawMenu && elapsedMs < 600))
            {
                return;
            }

            _nativeMenuWatchTimer?.Stop();
            _nativeMenuWatchTimer = null;
            _isContextMenuOpen = false;
            if (!IsActive)
            {
                FlyoutAnimationHelper.HandleDeactivated(this, () => LastDeactivatedTime = DateTime.UtcNow);
            }
        };
        _nativeMenuWatchTimer.Start();
    }

    private void ShowTrayItemContextMenu(TrayIconItem item)
    {
        var menu = new Forms.ContextMenuStrip
        {
            Renderer = new TaskbarWebWindow.Win11DarkContextMenuRenderer(),
            ForeColor = System.Drawing.Color.FromArgb(245, 245, 245),
            BackColor = System.Drawing.Color.FromArgb(44, 44, 44),
            ShowImageMargin = false,
            ShowCheckMargin = false,
            Font = new System.Drawing.Font("Segoe UI", 9.5f, System.Drawing.FontStyle.Regular),
            Padding = new Forms.Padding(4, 4, 4, 4)
        };

        menu.HandleCreated += (_, _) =>
        {
            int corner = 2; // DWMWCP_ROUND
            Win32Api.DwmSetWindowAttribute(menu.Handle, 33, ref corner, sizeof(int));
            int dark = 1;
            Win32Api.DwmSetWindowAttribute(menu.Handle, 20, ref dark, sizeof(int));
        };

        _isContextMenuOpen = true;
        menu.Closed += (_, _) =>
        {
            _isContextMenuOpen = false;
            if (!IsActive)
            {
                FlyoutAnimationHelper.HandleDeactivated(this, () => LastDeactivatedTime = DateTime.UtcNow);
            }
        };

        string headerText = !string.IsNullOrWhiteSpace(item.Name)
            ? item.Name
            : (!string.IsNullOrWhiteSpace(item.Tooltip) ? item.Tooltip.Split('\n')[0].Trim() : "Tray App");

        var itemPadding = new Forms.Padding(0, 6, 0, 6);

        var headerItem = new Forms.ToolStripMenuItem(headerText)
        {
            Enabled = false,
            ForeColor = System.Drawing.Color.FromArgb(170, 170, 170),
            Font = new System.Drawing.Font("Segoe UI", 9.0f, System.Drawing.FontStyle.Bold),
            Padding = itemPadding
        };
        menu.Items.Add(headerItem);
        menu.Items.Add(new Forms.ToolStripSeparator());

        var openItem = new Forms.ToolStripMenuItem("Open")
        {
            ForeColor = System.Drawing.Color.FromArgb(245, 245, 245),
            Padding = itemPadding
        };
        openItem.Click += (_, _) =>
        {
            Hide();
            _trayService.ActivateTrayIcon(item.Id, false, _onWinZenSelfClick);
        };
        menu.Items.Add(openItem);

        if (!string.IsNullOrWhiteSpace(item.ExecutablePath) && System.IO.File.Exists(item.ExecutablePath))
        {
            var locationItem = new Forms.ToolStripMenuItem("Open file location")
            {
                ForeColor = System.Drawing.Color.FromArgb(245, 245, 245),
                Padding = itemPadding
            };
            locationItem.Click += (_, _) =>
            {
                Hide();
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{item.ExecutablePath}\"",
                        UseShellExecute = true
                    });
                }
                catch { }
            };
            menu.Items.Add(locationItem);

            var restartItem = new Forms.ToolStripMenuItem("Restart app")
            {
                ForeColor = System.Drawing.Color.FromArgb(245, 245, 245),
                Padding = itemPadding
            };
            restartItem.Click += async (_, _) =>
            {
                string exePath = item.ExecutablePath;
                Hide();
                _trayService.EndTask(item);
                _lastRenderSignature = "";
                RefreshIcons(forceScan: false);
                _onChanged?.Invoke();
                await System.Threading.Tasks.Task.Delay(400);
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = exePath,
                        UseShellExecute = true
                    });
                }
                catch { }
            };
            menu.Items.Add(restartItem);
        }

        string pinText = item.IsPromoted ? "Unpin from taskbar" : "Pin to taskbar";
        var pinItem = new Forms.ToolStripMenuItem(pinText)
        {
            ForeColor = item.IsPromoted ? System.Drawing.Color.FromArgb(255, 153, 164) : System.Drawing.Color.FromArgb(245, 245, 245),
            Padding = itemPadding,
            Tag = item.IsPromoted ? "danger" : null
        };
        pinItem.Click += (_, _) =>
        {
            _trayService.TogglePromoted(item);
            _lastRenderSignature = "";
            RefreshIcons(forceScan: false);
            _onChanged?.Invoke();
        };
        menu.Items.Add(pinItem);

        var endTaskItem = new Forms.ToolStripMenuItem("End task")
        {
            ForeColor = System.Drawing.Color.FromArgb(255, 153, 164),
            Padding = itemPadding,
            Tag = "danger"
        };
        endTaskItem.Click += (_, _) =>
        {
            _trayService.EndTask(item);
            _lastRenderSignature = "";
            RefreshIcons(forceScan: false);
            _onChanged?.Invoke();
        };
        menu.Items.Add(endTaskItem);

        var cPos = Forms.Cursor.Position;
        NativeContextMenuHelper.AttachAutoDismiss(menu);
        menu.Show(cPos, Forms.ToolStripDropDownDirection.AboveRight);
    }

    private UIElement CreateIconElement(TrayIconItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.IconDataUrl) && item.IconDataUrl.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            if (!_bitmapCache.TryGetValue(item.IconDataUrl, out var bmp))
            {
                int commaIdx = item.IconDataUrl.IndexOf(',');
                if (commaIdx > 0)
                {
                    try
                    {
                        byte[] bytes = Convert.FromBase64String(item.IconDataUrl[(commaIdx + 1)..]);
                        using var ms = new MemoryStream(bytes);
                        bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.StreamSource = ms;
                        bmp.EndInit();
                        bmp.Freeze();
                        _bitmapCache[item.IconDataUrl] = bmp;
                    }
                    catch
                    {
                        bmp = null;
                    }
                }
            }

            if (bmp != null)
            {
                var image = new Image
                {
                    Source = bmp,
                    Width = 18,
                    Height = 18
                };
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
                return image;
            }
        }

        return new Border
        {
            Width = 18,
            Height = 18,
            CornerRadius = new CornerRadius(4),
            Background = _accentBrush,
            Child = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(item.Name) ? "•" : item.Name[..1].ToUpperInvariant(),
                Foreground = Brushes.White,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
    }

    public void ShowAt(double anchorScreenX, double anchorWidth, double taskbarTop, double taskbarHeight, string dockEdge, string? theme = null, string? accent = null)
    {
        ApplyAccentColor(accent);
        ApplyTheme(theme);
        RefreshIcons(forceScan: false);
        UpdateLayout();

        double flyoutWidth = Width;
        double flyoutHeight = ActualHeight > 10 ? ActualHeight : 140;

        double left = anchorScreenX + (anchorWidth / 2.0) - (flyoutWidth / 2.0);
        const double floatingGap = 10.0;
        double top = string.Equals(dockEdge, "Top", StringComparison.OrdinalIgnoreCase)
            ? taskbarTop + taskbarHeight + floatingGap
            : taskbarTop - flyoutHeight - floatingGap;

        var mon = WorkAreaAppBarService.GetPrimaryMonitorInfo().rcMonitor;
        double monitorLeft = mon.Left;
        double monitorRight = mon.Right;
        const double sideMargin = 12.0;

        if (left + flyoutWidth > monitorRight - sideMargin)
            left = monitorRight - flyoutWidth - sideMargin;
        if (left < monitorLeft + sideMargin)
            left = monitorLeft + sideMargin;

        Left = left;
        FlyoutAnimationHelper.AnimateShow(this, top, dockEdge);

        _ = RefreshInBackgroundAsync();
    }

    private async System.Threading.Tasks.Task RefreshInBackgroundAsync()
    {
        try
        {
            var fresh = await _trayService.RefreshTrayIconsAsync();
            if (IsVisible)
            {
                ApplyIconsList(fresh);
            }
        }
        catch { }
    }

    public void ApplyAccentColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return;
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            _accentBrush = new SolidColorBrush(color);
            Resources["AccentBrush"] = _accentBrush;
        }
        catch { }
    }

    public void ApplyTheme(string? theme)
    {
        _isLightTheme = string.Equals(theme, "light", StringComparison.OrdinalIgnoreCase);
        if (_isLightTheme)
        {
            Resources["TileBg"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            Resources["TileHoverBg"] = new SolidColorBrush(Color.FromRgb(0xEA, 0xEA, 0xEA));
            Resources["TileBorder"] = new SolidColorBrush(Color.FromArgb(0x1E, 0, 0, 0));
            Resources["MutedFg"] = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66));

            OuterBorder.Background = new SolidColorBrush(Color.FromRgb(0xF3, 0xF3, 0xF3));
            OuterBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(0x28, 0, 0, 0));
            TxtHeaderTitle.Foreground = new SolidColorBrush(Color.FromRgb(0x1B, 0x1B, 0x1B));
        }
        else
        {
            Resources["TileBg"] = new SolidColorBrush(Color.FromRgb(0x26, 0x26, 0x26));
            Resources["TileHoverBg"] = new SolidColorBrush(Color.FromRgb(0x35, 0x35, 0x35));
            Resources["TileBorder"] = new SolidColorBrush(Color.FromRgb(0x38, 0x38, 0x38));
            Resources["MutedFg"] = new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E));

            OuterBorder.Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
            OuterBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x3E, 0x3E, 0x3E));
            TxtHeaderTitle.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}
