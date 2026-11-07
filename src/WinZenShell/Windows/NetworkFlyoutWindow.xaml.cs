using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using WinZenShell.Native;
using WinZenShell.Services;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using MediaBrushes = System.Windows.Media.Brushes;
using WpfButton = System.Windows.Controls.Button;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfCursors = System.Windows.Input.Cursors;

namespace WinZenShell.Windows;

public partial class NetworkFlyoutWindow : Window
{
    private readonly NetworkService _networkService;
    private SolidColorBrush _accentBrush = new(MediaColor.FromRgb(0x36, 0x84, 0xE3));
    private bool _isScanning = false;
    private string _currentTheme = "dark";

    private const string PATH_WIFI_FULL = "M12 3a9 9 0 0 1 6.36 2.64.75.75 0 0 1-1.06 1.06A7.5 7.5 0 0 0 12 4.5a7.5 7.5 0 0 0-5.3 2.2.75.75 0 0 1-1.06-1.06A9 9 0 0 1 12 3zm0 4a5 5 0 0 1 3.54 1.46.75.75 0 1 1-1.06 1.06 3.5 3.5 0 0 0-4.95 0 .75.75 0 0 1-1.06-1.06A5 5 0 0 1 12 7zm0 4a1.5 1.5 0 1 1 0 3 1.5 1.5 0 0 1 0-3z";
    private const string PATH_WIFI_MED = "M12 7a5 5 0 0 1 3.54 1.46.75.75 0 1 1-1.06 1.06 3.5 3.5 0 0 0-4.95 0 .75.75 0 0 1-1.06-1.06A5 5 0 0 1 12 7zm0 4a1.5 1.5 0 1 1 0 3 1.5 1.5 0 0 1 0-3z";
    private const string PATH_WIFI_LOW = "M12 8.5a3 3 0 0 1 2.12.88.75.75 0 1 1-1.06 1.06 1.5 1.5 0 0 0-2.12 0 .75.75 0 0 1-1.06-1.06A3 3 0 0 1 12 8.5zm0 2.5a1.5 1.5 0 1 1 0 3 1.5 1.5 0 0 1 0-3z";
    private const string PATH_WIFI_MIN = "M12 11a1.5 1.5 0 1 1 0 3 1.5 1.5 0 0 1 0-3z";
    private const string PATH_ETHERNET = "M2 3a1 1 0 0 1 1-1h10a1 1 0 0 1 1 1v7a1 1 0 0 1-1 1H9v1h2a.5.5 0 0 1 0 1H5a.5.5 0 0 1 0-1h2v-1H3a1 1 0 0 1-1-1V3zm1 0v7h10V3H3zM4 14.5a.5.5 0 0 1 .5-.5h7a.5.5 0 0 1 0 1h-7a.5.5 0 0 1-.5-.5z";
    private const string PATH_OFFLINE = "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.42 0-8-3.58-8-8 0-1.85.63-3.55 1.69-4.9L16.9 18.31C15.55 19.37 13.85 20 12 20zm6.31-3.1L7.1 5.69C8.45 4.63 10.15 4 12 4c4.42 0 8 3.58 8 8 0 1.85-.63 3.55-1.69 4.9z";
    private const string PATH_LOCK = "M6 5V4a2 2 0 1 1 4 0v1h1a1 1 0 0 1 1 1v5a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V6a1 1 0 0 1 1-1h1zm1-1a1 1 0 1 1 2 0v1H7V4zm1 4a1 1 0 1 0 0 2 1 1 0 0 0 0-2z";

    public DateTime LastDeactivatedTime { get; private set; } = DateTime.MinValue;

    public NetworkFlyoutWindow(NetworkService networkService)
    {
        InitializeComponent();
        _networkService = networkService;

        Deactivated += (_, _) =>
        {
            LastDeactivatedTime = DateTime.UtcNow;
            Hide();
        };

        KeyDown += (s, e) =>
        {
            if (e.Key == Key.Escape) Hide();
        };
    }

    public void ShowAt(
        double targetX,
        double targetWidth,
        double taskbarTop,
        double taskbarHeight,
        string dockEdge,
        string theme,
        string accentColor)
    {
        ApplyTheme(theme);
        ApplyAccentColor(accentColor);

        double flyoutWidth = Width;
        double flyoutHeight = Height;
        const double floatingGap = 10.0;

        double left = targetX + (targetWidth / 2.0) - (flyoutWidth / 2.0);
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
        const double sideMargin = 12.0;

        if (left + flyoutWidth > monitorRight - sideMargin)
            left = monitorRight - flyoutWidth - sideMargin;
        if (left < monitorLeft + sideMargin)
            left = monitorLeft + sideMargin;

        Left = left;
        Top = top;

        UpdateActiveConnectionDisplay();
        _ = RefreshNetworksAsync();

        Show();
        Activate();
        Focus();
    }

    public void UpdateActiveConnectionDisplay()
    {
        var status = _networkService.GetNetworkStatus();

        if (status.NetworkType == "wifi")
        {
            StatusIconPath.Data = Geometry.Parse(GetWifiSignalPath(status.SignalPercent));
            StatusIconPath.Fill = _accentBrush;
            TxtStatusName.Text = string.IsNullOrEmpty(status.Ssid) ? "Wi-Fi" : status.Ssid;
            TxtStatusDetail.Text = $"Connected • {status.SignalPercent}% signal{(string.IsNullOrEmpty(status.LocalIp) ? "" : $" • {status.LocalIp}")}";
            BtnDisconnectWifi.Visibility = Visibility.Visible;
        }
        else if (status.NetworkType == "ethernet")
        {
            StatusIconPath.Data = Geometry.Parse(PATH_ETHERNET);
            StatusIconPath.Fill = _accentBrush;
            TxtStatusName.Text = string.IsNullOrEmpty(status.AdapterName) ? "Ethernet" : status.AdapterName;
            TxtStatusDetail.Text = $"Connected{(string.IsNullOrEmpty(status.LocalIp) ? "" : $" • {status.LocalIp}")}";
            BtnDisconnectWifi.Visibility = Visibility.Collapsed;
        }
        else
        {
            StatusIconPath.Data = Geometry.Parse(PATH_OFFLINE);
            StatusIconPath.Fill = new SolidColorBrush(MediaColor.FromRgb(0x9E, 0x9E, 0x9E));
            TxtStatusName.Text = "Not Connected";
            TxtStatusDetail.Text = "No internet access";
            BtnDisconnectWifi.Visibility = Visibility.Collapsed;
        }
    }

    public async Task RefreshNetworksAsync()
    {
        if (_isScanning) return;
        _isScanning = true;

        TxtScanningIndicator.Visibility = Visibility.Visible;
        BtnRefreshWifi.IsEnabled = false;

        try
        {
            var anim = new System.Windows.Media.Animation.DoubleAnimation(0, 360, new Duration(TimeSpan.FromMilliseconds(750)))
            {
                RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever
            };
            RefRotate.BeginAnimation(RotateTransform.AngleProperty, anim);
        }
        catch { }

        try
        {
            UpdateActiveConnectionDisplay();
            var networks = await _networkService.ScanWifiNetworksAsync();
            PopulateNetworksList(networks);
        }
        catch { }
        finally
        {
            _isScanning = false;
            TxtScanningIndicator.Visibility = Visibility.Collapsed;
            BtnRefreshWifi.IsEnabled = true;
            try
            {
                RefRotate.BeginAnimation(RotateTransform.AngleProperty, null);
                RefRotate.Angle = 0;
            }
            catch { }
        }
    }

    private void PopulateNetworksList(List<WifiNetworkItem> networks)
    {
        NetworksContainer.Children.Clear();

        bool isLight = string.Equals(_currentTheme, "light", StringComparison.OrdinalIgnoreCase);
        var cardBg = isLight ? new SolidColorBrush(MediaColor.FromRgb(0xFA, 0xFA, 0xFA)) : new SolidColorBrush(MediaColor.FromRgb(0x24, 0x24, 0x24));
        var cardBorder = isLight ? new SolidColorBrush(MediaColor.FromArgb(0x18, 0, 0, 0)) : new SolidColorBrush(MediaColor.FromRgb(0x34, 0x34, 0x34));
        var primaryText = isLight ? new SolidColorBrush(MediaColor.FromRgb(0x1A, 0x1A, 0x1A)) : new SolidColorBrush(MediaColor.FromRgb(0xFF, 0xFF, 0xFF));
        var secondaryText = isLight ? new SolidColorBrush(MediaColor.FromRgb(0x66, 0x66, 0x66)) : new SolidColorBrush(MediaColor.FromRgb(0x9E, 0x9E, 0x9E));

        if (networks.Count == 0)
        {
            var emptyTxt = new TextBlock
            {
                Text = "No Wi-Fi networks found in range",
                Foreground = secondaryText,
                FontSize = 11.5,
                HorizontalAlignment = WpfHorizontalAlignment.Center,
                Margin = new Thickness(0, 24, 0, 24)
            };
            NetworksContainer.Children.Add(emptyTxt);
            return;
        }

        foreach (var net in networks)
        {
            var itemBorder = new Border
            {
                Background = cardBg,
                BorderBrush = cardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 6)
            };

            var mainStack = new StackPanel();

            var topGrid = new Grid();
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // 1. Wi-Fi Signal Icon
            var iconPath = new Path
            {
                Data = Geometry.Parse(GetWifiSignalPath(net.SignalPercent)),
                Fill = net.IsConnected ? _accentBrush : primaryText,
                Width = 14,
                Height = 14,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = WpfHorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(iconPath, 0);
            topGrid.Children.Add(iconPath);

            // 2. Center Text (SSID + Details)
            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var titleRow = new StackPanel { Orientation = WpfOrientation.Horizontal };

            var txtSsid = new TextBlock
            {
                Text = net.Ssid,
                Foreground = primaryText,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 180
            };
            titleRow.Children.Add(txtSsid);

            if (net.IsSecured)
            {
                var lockPath = new Path
                {
                    Data = Geometry.Parse(PATH_LOCK),
                    Fill = secondaryText,
                    Width = 9,
                    Height = 9,
                    Stretch = Stretch.Uniform,
                    Margin = new Thickness(6, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                titleRow.Children.Add(lockPath);
            }

            textStack.Children.Add(titleRow);

            string bandStr = string.IsNullOrEmpty(net.Band) ? "" : $" • {net.Band}";
            var txtDetails = new TextBlock
            {
                Text = $"{net.SignalPercent}%{bandStr} • {(net.IsSecured ? (string.IsNullOrEmpty(net.Authentication) ? "Secured" : net.Authentication) : "Open")}",
                Foreground = secondaryText,
                FontSize = 10.5,
                Margin = new Thickness(0, 2, 0, 0)
            };
            textStack.Children.Add(txtDetails);

            Grid.SetColumn(textStack, 1);
            topGrid.Children.Add(textStack);

            // 3. Right Action
            if (net.IsConnected)
            {
                var connectedBadge = new Border
                {
                    Background = new SolidColorBrush(MediaColor.FromArgb(0x30, _accentBrush.Color.R, _accentBrush.Color.G, _accentBrush.Color.B)),
                    BorderBrush = _accentBrush,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 2, 6, 2),
                    VerticalAlignment = VerticalAlignment.Center
                };
                connectedBadge.Child = new TextBlock
                {
                    Text = "Connected",
                    Foreground = _accentBrush,
                    FontSize = 10,
                    FontWeight = FontWeights.Bold
                };
                Grid.SetColumn(connectedBadge, 2);
                topGrid.Children.Add(connectedBadge);
            }
            else
            {
                var btnConnect = new WpfButton
                {
                    Content = "Connect",
                    Background = new SolidColorBrush(MediaColor.FromArgb(0x18, 0xFF, 0xFF, 0xFF)),
                    Foreground = primaryText,
                    BorderBrush = cardBorder,
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(8, 3, 8, 3),
                    FontSize = 11,
                    FontWeight = FontWeights.Medium,
                    Cursor = WpfCursors.Hand,
                    VerticalAlignment = VerticalAlignment.Center
                };

                // Template for neat corners
                var btnTemplate = new ControlTemplate(typeof(WpfButton));
                var feBorder = new FrameworkElementFactory(typeof(Border));
                feBorder.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(WpfButton.BackgroundProperty));
                feBorder.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(WpfButton.BorderBrushProperty));
                feBorder.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(WpfButton.BorderThicknessProperty));
                feBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
                feBorder.SetValue(Border.PaddingProperty, new TemplateBindingExtension(WpfButton.PaddingProperty));
                var fePresenter = new FrameworkElementFactory(typeof(ContentPresenter));
                fePresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, WpfHorizontalAlignment.Center);
                fePresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
                feBorder.AppendChild(fePresenter);
                btnTemplate.VisualTree = feBorder;
                btnConnect.Template = btnTemplate;

                // Expandable password prompt row
                var passRow = new Grid
                {
                    Margin = new Thickness(0, 8, 0, 0),
                    Visibility = Visibility.Collapsed
                };
                passRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                passRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                passRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var passBox = new PasswordBox
                {
                    Background = isLight ? new SolidColorBrush(MediaColor.FromRgb(0xEE, 0xEE, 0xEE)) : new SolidColorBrush(MediaColor.FromRgb(0x1C, 0x1C, 0x1C)),
                    Foreground = primaryText,
                    BorderBrush = cardBorder,
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(6, 4, 6, 4),
                    FontSize = 11,
                    Margin = new Thickness(0, 0, 6, 0)
                };
                Grid.SetColumn(passBox, 0);
                passRow.Children.Add(passBox);

                var btnJoin = new WpfButton
                {
                    Content = "Join",
                    Background = _accentBrush,
                    Foreground = new SolidColorBrush(Colors.White),
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(10, 4, 10, 4),
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Cursor = WpfCursors.Hand,
                    Margin = new Thickness(0, 0, 4, 0),
                    Template = btnTemplate
                };
                Grid.SetColumn(btnJoin, 1);
                passRow.Children.Add(btnJoin);

                var btnCancel = new WpfButton
                {
                    Content = "Cancel",
                    Background = MediaBrushes.Transparent,
                    Foreground = secondaryText,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(6, 4, 6, 4),
                    FontSize = 11,
                    Cursor = WpfCursors.Hand,
                    Template = btnTemplate
                };
                Grid.SetColumn(btnCancel, 2);
                passRow.Children.Add(btnCancel);

                btnConnect.Click += async (s, e) =>
                {
                    if (net.IsKnownProfile || !net.IsSecured)
                    {
                        btnConnect.IsEnabled = false;
                        btnConnect.Content = "...";
                        await _networkService.ConnectWifiAsync(net.Ssid);
                        await Task.Delay(1000);
                        await RefreshNetworksAsync();
                    }
                    else
                    {
                        passRow.Visibility = passRow.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
                        if (passRow.Visibility == Visibility.Visible) passBox.Focus();
                    }
                };

                btnCancel.Click += (s, e) => passRow.Visibility = Visibility.Collapsed;

                btnJoin.Click += async (s, e) =>
                {
                    string pass = passBox.Password;
                    btnJoin.IsEnabled = false;
                    btnJoin.Content = "...";
                    await _networkService.ConnectWifiAsync(net.Ssid, pass);
                    await Task.Delay(1000);
                    await RefreshNetworksAsync();
                };

                Grid.SetColumn(btnConnect, 2);
                topGrid.Children.Add(btnConnect);
                mainStack.Children.Add(topGrid);
                mainStack.Children.Add(passRow);
            }

            if (mainStack.Children.Count == 0) mainStack.Children.Add(topGrid);
            itemBorder.Child = mainStack;
            NetworksContainer.Children.Add(itemBorder);
        }
    }

    private static string GetWifiSignalPath(int signal)
    {
        if (signal >= 70) return PATH_WIFI_FULL;
        if (signal >= 40) return PATH_WIFI_MED;
        if (signal >= 15) return PATH_WIFI_LOW;
        return PATH_WIFI_MIN;
    }

    private void OnRefreshClicked(object sender, RoutedEventArgs e)
    {
        _ = RefreshNetworksAsync();
    }

    private async void OnDisconnectWifiClicked(object sender, RoutedEventArgs e)
    {
        BtnDisconnectWifi.IsEnabled = false;
        await _networkService.DisconnectWifiAsync();
        await Task.Delay(800);
        UpdateActiveConnectionDisplay();
        await RefreshNetworksAsync();
        BtnDisconnectWifi.IsEnabled = true;
    }

    private void OnOpenSettingsClicked(object sender, RoutedEventArgs e)
    {
        NetworkService.OpenWindowsNetworkSettings();
    }

    private void OnOpenWindowsNetworksClicked(object sender, RoutedEventArgs e)
    {
        NetworkService.OpenWindowsAvailableNetworks();
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    public void ApplyAccentColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return;
        try
        {
            var color = (MediaColor)MediaColorConverter.ConvertFromString(hex);
            _accentBrush = new SolidColorBrush(color);
            Resources["AccentBrush"] = _accentBrush;
            if (HeaderIconPath != null) HeaderIconPath.Fill = _accentBrush;
            if (StatusIconPath != null) StatusIconPath.Fill = _accentBrush;
            if (TxtScanningIndicator != null) TxtScanningIndicator.Foreground = _accentBrush;
            if (BtnWindowsWifiSettings != null) BtnWindowsWifiSettings.Foreground = _accentBrush;
        }
        catch { }
    }

    public void ApplyTheme(string? theme)
    {
        _currentTheme = theme ?? "dark";
        bool isLight = string.Equals(theme, "light", StringComparison.OrdinalIgnoreCase);

        if (isLight)
        {
            OuterBorder.Background = new SolidColorBrush(MediaColor.FromRgb(0xF3, 0xF3, 0xF3));
            OuterBorder.BorderBrush = new SolidColorBrush(MediaColor.FromArgb(0x28, 0, 0, 0));
            TxtHeaderTitle.Foreground = new SolidColorBrush(MediaColor.FromRgb(0x1B, 0x1B, 0x1B));
            StatusCard.Background = new SolidColorBrush(MediaColor.FromRgb(0xFF, 0xFF, 0xFF));
            StatusCard.BorderBrush = new SolidColorBrush(MediaColor.FromArgb(0x18, 0, 0, 0));
            TxtStatusName.Foreground = new SolidColorBrush(MediaColor.FromRgb(0x1B, 0x1B, 0x1B));
            TxtStatusDetail.Foreground = new SolidColorBrush(MediaColor.FromRgb(0x66, 0x66, 0x66));
            TxtAvailableHeader.Foreground = new SolidColorBrush(MediaColor.FromRgb(0x44, 0x44, 0x44));
        }
        else
        {
            OuterBorder.Background = new SolidColorBrush(MediaColor.FromRgb(0x1E, 0x1E, 0x1E));
            OuterBorder.BorderBrush = new SolidColorBrush(MediaColor.FromRgb(0x3E, 0x3E, 0x3E));
            TxtHeaderTitle.Foreground = new SolidColorBrush(MediaColor.FromRgb(0xFF, 0xFF, 0xFF));
            StatusCard.Background = new SolidColorBrush(MediaColor.FromRgb(0x26, 0x26, 0x26));
            StatusCard.BorderBrush = new SolidColorBrush(MediaColor.FromRgb(0x38, 0x38, 0x38));
            TxtStatusName.Foreground = new SolidColorBrush(MediaColor.FromRgb(0xFF, 0xFF, 0xFF));
            TxtStatusDetail.Foreground = new SolidColorBrush(MediaColor.FromRgb(0xA0, 0xA0, 0xA0));
            TxtAvailableHeader.Foreground = new SolidColorBrush(MediaColor.FromRgb(0xCC, 0xCC, 0xCC));
        }

        if (HeaderIconPath != null) HeaderIconPath.Fill = _accentBrush;
        if (StatusIconPath != null) StatusIconPath.Fill = _accentBrush;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}
