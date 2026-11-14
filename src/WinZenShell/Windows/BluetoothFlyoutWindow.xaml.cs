using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using WinZenShell.Native;
using WinZenShell.Services;
using Color = System.Windows.Media.Color;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using ColorConverter = System.Windows.Media.ColorConverter;
using WpfButton = System.Windows.Controls.Button;
using WpfCursors = System.Windows.Input.Cursors;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;

namespace WinZenShell.Windows;

public partial class BluetoothFlyoutWindow : Window
{
    private readonly BluetoothService _bluetoothService;
    private SolidColorBrush _accentBrush = new(Color.FromRgb(0x36, 0x84, 0xE3));
    private bool _isLightTheme;
    public DateTime LastDeactivatedTime { get; private set; } = DateTime.MinValue;

    private const string PATH_AUDIO = "M12 3a9 9 0 0 0-9 9v7c0 1.1.9 2 2 2h2a2 2 0 0 0 2-2v-4a2 2 0 0 0-2-2H5a7 7 0 1 1 14 0h-2a2 2 0 0 0-2 2v4a2 2 0 0 0 2 2h2c1.1 0 2-.9 2-2v-7a9 9 0 0 0-9-9z";
    private const string PATH_PHONE = "M17 1.01L7 1c-1.1 0-2 .9-2 2v18c0 1.1.9 2 2 2h10c1.1 0 2-.9 2-2V3c0-1.1-.9-1.99-2-1.99zM17 19H7V5h10v14z";
    private const string PATH_COMPUTER = "M20 18c1.1 0 1.99-.9 1.99-2L22 6c0-1.1-.9-2-2-2H4c-1.1 0-2 .9-2 2v10c0 1.1.9 2 2 2H0v2h24v-2h-4zM4 6h16v10H4V6z";
    private const string PATH_MOUSE = "M13 2v8h6V8a6 6 0 0 0-6-6zm-2 0a6 6 0 0 0-6 6v2h6V2zm8 10h-6v10a6 6 0 0 0 6-6v-4zm-8 10V12H5v4a6 6 0 0 0 6 6z";
    private const string PATH_BLUETOOTH = "M7,7 L17,17 L12,22 V2 L17,7 L7,17";

    public BluetoothFlyoutWindow(BluetoothService bluetoothService)
    {
        InitializeComponent();
        _bluetoothService = bluetoothService;
        _bluetoothService.BluetoothChanged += (_, _) => Dispatcher.InvokeAsync(UpdateStatus);

        Deactivated += (_, _) =>
        {
            FlyoutAnimationHelper.HandleDeactivated(this, () => LastDeactivatedTime = DateTime.UtcNow);
        };

        KeyDown += (s, e) =>
        {
            if (e.Key == Key.Escape) FlyoutAnimationHelper.AnimateHide(this);
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

    public void UpdateStatus()
    {
        var status = _bluetoothService.GetBluetoothStatus();
        bool isEnabled = status.IsEnabled;
        Color accentCol = _accentBrush.Color;

        if (isEnabled)
        {
            TxtStatusTitle.Text = "Bluetooth";
            TxtStatusDetail.Text = status.IsConnected && !string.IsNullOrWhiteSpace(status.ConnectedDeviceName)
                ? $"Connected to {status.ConnectedDeviceName}"
                : $"On • Discoverable as \"{status.ComputerName}\"";

            BadgeIcon.Opacity = 1.0;
            BadgeIcon.Stroke = _accentBrush;
            IconBadge.Background = new SolidColorBrush(Color.FromArgb(
                (byte)(_isLightTheme ? 0x22 : 0x2E),
                accentCol.R, accentCol.G, accentCol.B));

            if (BtnToggleRadio.Template.FindName("TglBorder", BtnToggleRadio) is Border tglBorder)
            {
                tglBorder.Background = _accentBrush;
            }
            if (BtnToggleRadio.Template.FindName("TxtToggleState", BtnToggleRadio) is TextBlock tglTxt)
            {
                tglTxt.Text = "On";
            }
        }
        else
        {
            TxtStatusTitle.Text = "Bluetooth";
            TxtStatusDetail.Text = status.IsAvailable ? "Off" : "Not available";

            BadgeIcon.Opacity = 0.5;
            BadgeIcon.Stroke = (Brush)Resources["MutedFg"];
            IconBadge.Background = new SolidColorBrush(_isLightTheme
                ? Color.FromArgb(0x12, 0, 0, 0)
                : Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF));

            if (BtnToggleRadio.Template.FindName("TglBorder", BtnToggleRadio) is Border tglBorder)
            {
                tglBorder.Background = new SolidColorBrush(_isLightTheme
                    ? Color.FromRgb(0xCC, 0xCC, 0xCC)
                    : Color.FromRgb(0x44, 0x44, 0x44));
            }
            if (BtnToggleRadio.Template.FindName("TxtToggleState", BtnToggleRadio) is TextBlock tglTxt)
            {
                tglTxt.Text = "Off";
            }
        }

        UpdateDevicesList(status);
    }

    private void UpdateDevicesList(BluetoothStatusInfo status)
    {
        DevicesContainer.Children.Clear();

        var devices = status.DeviceItems;
        TxtDeviceCount.Text = $"{devices.Count} device{(devices.Count == 1 ? "" : "s")}";

        if (devices.Count == 0)
        {
            var emptyTxt = new TextBlock
            {
                Text = status.IsEnabled ? "No paired devices found" : "Turn on Bluetooth to view devices",
                Foreground = (Brush)Resources["MutedFg"],
                FontSize = 11.5,
                Margin = new Thickness(4, 14, 4, 14),
                HorizontalAlignment = WpfHorizontalAlignment.Center
            };
            DevicesContainer.Children.Add(emptyTxt);
            return;
        }

        var cardBg = (Brush)Resources["CardBg"];
        var cardBorder = (Brush)Resources["CardBorder"];
        var primaryText = new SolidColorBrush(_isLightTheme ? Color.FromRgb(0x1B, 0x1B, 0x1B) : Color.FromRgb(0xFF, 0xFF, 0xFF));
        var secondaryText = (Brush)Resources["MutedFg"];
        var defaultIconColor = _isLightTheme ? new SolidColorBrush(Color.FromRgb(0x40, 0x40, 0x40)) : new SolidColorBrush(Color.FromRgb(0xC8, 0xC8, 0xC8));
        Color accentCol = _accentBrush.Color;

        foreach (var dev in devices)
        {
            var itemBorder = new Border
            {
                Background = cardBg,
                BorderBrush = cardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 9, 12, 9),
                Margin = new Thickness(0, 0, 0, 6)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Device Icon
            var iconPath = new Path
            {
                Width = 16,
                Height = 16,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };

            if (dev.DeviceType == "audio" || dev.DeviceType == "phone" || dev.DeviceType == "computer" || dev.DeviceType == "peripheral")
            {
                string pathData = dev.DeviceType switch
                {
                    "audio" => PATH_AUDIO,
                    "phone" => PATH_PHONE,
                    "computer" => PATH_COMPUTER,
                    "peripheral" => PATH_MOUSE,
                    _ => PATH_BLUETOOTH
                };
                iconPath.Data = Geometry.Parse(pathData);
                iconPath.Fill = dev.IsConnected ? _accentBrush : defaultIconColor;
            }
            else
            {
                iconPath.Data = Geometry.Parse(PATH_BLUETOOTH);
                iconPath.Stroke = dev.IsConnected ? _accentBrush : defaultIconColor;
                iconPath.StrokeThickness = 2;
                iconPath.StrokeStartLineCap = PenLineCap.Round;
                iconPath.StrokeEndLineCap = PenLineCap.Round;
                iconPath.StrokeLineJoin = PenLineJoin.Round;
                iconPath.Fill = Brushes.Transparent;
            }

            Grid.SetColumn(iconPath, 0);
            grid.Children.Add(iconPath);

            // Names & Details
            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var nameTxt = new TextBlock
            {
                Text = dev.Name,
                Foreground = primaryText,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 180
            };
            var statusTxt = new TextBlock
            {
                Text = dev.IsConnected ? "Connected" : "Paired",
                Foreground = dev.IsConnected ? _accentBrush : secondaryText,
                FontSize = 10.5,
                Margin = new Thickness(0, 2, 0, 0)
            };
            textStack.Children.Add(nameTxt);
            textStack.Children.Add(statusTxt);
            Grid.SetColumn(textStack, 1);
            grid.Children.Add(textStack);

            // Action Button
            var actionBtn = new WpfButton
            {
                Content = dev.IsConnected ? "Disconnect" : "Connect",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = dev.IsConnected ? primaryText : _accentBrush,
                Background = dev.IsConnected
                    ? (Brush)Resources["BtnBg"]
                    : new SolidColorBrush(Color.FromArgb((byte)(_isLightTheme ? 0x1A : 0x24), accentCol.R, accentCol.G, accentCol.B)),
                BorderBrush = dev.IsConnected
                    ? (Brush)Resources["BtnBorder"]
                    : new SolidColorBrush(Color.FromArgb((byte)(_isLightTheme ? 0x30 : 0x40), accentCol.R, accentCol.G, accentCol.B)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 5, 12, 5),
                Cursor = WpfCursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };

            var btnTemplate = new ControlTemplate(typeof(WpfButton));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(BackgroundProperty));
            borderFactory.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(BorderBrushProperty));
            borderFactory.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(BorderThicknessProperty));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            borderFactory.SetValue(Border.PaddingProperty, new TemplateBindingExtension(PaddingProperty));

            var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, WpfHorizontalAlignment.Center);
            contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(contentFactory);
            btnTemplate.VisualTree = borderFactory;
            actionBtn.Template = btnTemplate;

            string targetName = dev.Name;
            actionBtn.Click += (s, e) =>
            {
                BluetoothService.ConnectDevice(targetName);
            };

            Grid.SetColumn(actionBtn, 2);
            grid.Children.Add(actionBtn);

            itemBorder.Child = grid;
            DevicesContainer.Children.Add(itemBorder);
        }
    }

    public void ShowAtExact(double exactLeft, double exactTop, string dockEdge, string? theme, string? accentColor)
    {
        ApplyTheme(theme);
        ApplyAccentColor(accentColor);
        UpdateStatus();

        Left = exactLeft;

        double finalTop = exactTop;
        if (!string.Equals(dockEdge, "Top", StringComparison.OrdinalIgnoreCase))
        {
            double flyoutH = ActualHeight > 50 ? ActualHeight : 420;
            var mon = WorkAreaAppBarService.GetPrimaryMonitorInfo().rcMonitor;
            double dpiY = VisualTreeHelper.GetDpi(this).DpiScaleY;
            if (dpiY <= 0) dpiY = 1.0;
            double monitorBottom = mon.Bottom / dpiY;
            finalTop = monitorBottom - flyoutH - 10.0;
        }

        Top = finalTop;
        FlyoutAnimationHelper.AnimateShow(this, finalTop, dockEdge);
    }

    public void ShowAt(double targetX, double targetWidth, double taskbarTop, double taskbarHeight, string dockEdge, string? theme, string? accentColor)
    {
        ApplyTheme(theme);
        ApplyAccentColor(accentColor);
        UpdateStatus();

        double dpiX = VisualTreeHelper.GetDpi(this).DpiScaleX;
        double dpiY = VisualTreeHelper.GetDpi(this).DpiScaleY;
        if (dpiX <= 0) dpiX = 1.0;
        if (dpiY <= 0) dpiY = 1.0;

        var mon = WorkAreaAppBarService.GetPrimaryMonitorInfo().rcMonitor;
        double monitorLeft = mon.Left / dpiX;
        double monitorRight = mon.Right / dpiX;
        double monitorTop = mon.Top / dpiY;
        double monitorBottom = mon.Bottom / dpiY;

        const double floatingGap = 10.0;
        double flyoutW = Width;
        double flyoutH = ActualHeight > 50 ? ActualHeight : 420;

        double top;
        if (string.Equals(dockEdge, "Top", StringComparison.OrdinalIgnoreCase))
        {
            top = taskbarTop + taskbarHeight + floatingGap;
            if (top + flyoutH > monitorBottom - floatingGap)
                top = monitorBottom - flyoutH - floatingGap;
        }
        else
        {
            top = taskbarTop - flyoutH - floatingGap;
            if (top < monitorTop + floatingGap)
                top = monitorTop + floatingGap;
        }

        double left = targetX + (targetWidth / 2.0) - (flyoutW / 2.0);
        const double sideMargin = 12.0;
        if (left + flyoutW > monitorRight - sideMargin)
            left = monitorRight - flyoutW - sideMargin;
        if (left < monitorLeft + sideMargin)
            left = monitorLeft + sideMargin;

        Left = left;
        FlyoutAnimationHelper.AnimateShow(this, top, dockEdge);
    }

    public void ApplyAccentColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return;
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            _accentBrush = new SolidColorBrush(color);
            Resources["AccentBrush"] = _accentBrush;
            if (HeaderIcon != null) HeaderIcon.Stroke = _accentBrush;
            if (BadgeIcon != null) BadgeIcon.Stroke = _accentBrush;
            if (BtnMoreSettings != null) BtnMoreSettings.Foreground = _accentBrush;
            if (IsVisible) UpdateStatus();
        }
        catch { }
    }

    public void ApplyTheme(string? theme)
    {
        _isLightTheme = string.Equals(theme, "light", StringComparison.OrdinalIgnoreCase);
        if (_isLightTheme)
        {
            Resources["ScrollBarThumbBrush"] = new SolidColorBrush(Color.FromArgb(0x35, 0, 0, 0));
            Resources["ScrollBarThumbHoverBrush"] = new SolidColorBrush(Color.FromArgb(0x60, 0, 0, 0));
            Resources["CardBg"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            Resources["CardBorder"] = new SolidColorBrush(Color.FromArgb(0x18, 0, 0, 0));
            Resources["BtnBg"] = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5));
            Resources["BtnBorder"] = new SolidColorBrush(Color.FromArgb(0x28, 0, 0, 0));
            Resources["BtnFg"] = new SolidColorBrush(Color.FromRgb(0x1B, 0x1B, 0x1B));
            Resources["BtnHover"] = new SolidColorBrush(Color.FromRgb(0xEB, 0xEB, 0xEB));
            Resources["MutedFg"] = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66));

            OuterBorder.Background = new SolidColorBrush(Color.FromRgb(0xF3, 0xF3, 0xF3));
            OuterBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(0x28, 0, 0, 0));
            TxtHeaderTitle.Foreground = new SolidColorBrush(Color.FromRgb(0x1B, 0x1B, 0x1B));
            TxtStatusTitle.Foreground = new SolidColorBrush(Color.FromRgb(0x1B, 0x1B, 0x1B));
        }
        else
        {
            Resources["ScrollBarThumbBrush"] = new SolidColorBrush(Color.FromArgb(0x4A, 0xFF, 0xFF, 0xFF));
            Resources["ScrollBarThumbHoverBrush"] = new SolidColorBrush(Color.FromArgb(0x7A, 0xFF, 0xFF, 0xFF));
            Resources["CardBg"] = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x25));
            Resources["CardBorder"] = new SolidColorBrush(Color.FromRgb(0x38, 0x38, 0x38));
            Resources["BtnBg"] = new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2C));
            Resources["BtnBorder"] = new SolidColorBrush(Color.FromRgb(0x3E, 0x3E, 0x3E));
            Resources["BtnFg"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            Resources["BtnHover"] = new SolidColorBrush(Color.FromRgb(0x38, 0x38, 0x38));
            Resources["MutedFg"] = new SolidColorBrush(Color.FromRgb(0xA6, 0xA6, 0xA6));

            OuterBorder.Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
            OuterBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x3E, 0x3E, 0x3E));
            TxtHeaderTitle.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            TxtStatusTitle.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
        }

        if (HeaderIcon != null) HeaderIcon.Stroke = _accentBrush;
        if (BadgeIcon != null) BadgeIcon.Stroke = _accentBrush;
        if (IsVisible) UpdateStatus();
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        FlyoutAnimationHelper.AnimateHide(this);
    }

    private async void OnToggleRadioClicked(object sender, RoutedEventArgs e)
    {
        BtnToggleRadio.IsEnabled = false;
        try
        {
            await _bluetoothService.ToggleBluetoothAsync();
            UpdateStatus();
        }
        finally
        {
            BtnToggleRadio.IsEnabled = true;
        }
    }

    private void OnAddDeviceClicked(object sender, RoutedEventArgs e)
    {
        BluetoothService.OpenWindowsAddBluetoothDevice();
        Hide();
    }

    private void OnMoreSettingsClicked(object sender, RoutedEventArgs e)
    {
        BluetoothService.OpenWindowsBluetoothSettings();
        Hide();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}
