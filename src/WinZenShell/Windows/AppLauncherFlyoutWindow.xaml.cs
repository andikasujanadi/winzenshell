using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using WinZenShell.Native;
using WinZenShell.Services;

namespace WinZenShell.Windows;

public partial class AppLauncherFlyoutWindow : Window
{
    private readonly AppLauncherService _appLauncherService;
    private readonly Action _openSettingsCallback;
    private readonly ShellStateService? _shellState;
    private bool _webViewReady;
    public DateTime LastDeactivatedTime { get; private set; } = DateTime.MinValue;

    public AppLauncherFlyoutWindow(
        AppLauncherService appLauncherService,
        Action openSettingsCallback,
        ShellStateService? shellState = null)
    {
        InitializeComponent();
        _appLauncherService = appLauncherService;
        _openSettingsCallback = openSettingsCallback;
        _shellState = shellState;

        Deactivated += (_, _) =>
        {
            LastDeactivatedTime = DateTime.UtcNow;
            Hide();
        };
        Loaded += OnLoaded;

        _appLauncherService.AppsLoaded += OnAppsLoaded;
        if (_shellState != null)
        {
            _shellState.StateChanged += payload => Dispatcher.Invoke(() => ApplyTheme(payload.Settings.Theme, payload.Settings.AccentColor));
        }
    }

    public void ApplyTheme(string? theme, string? accentColor = null)
    {
        bool isLight = string.Equals(theme, "light", StringComparison.OrdinalIgnoreCase);
        if (isLight)
        {
            OuterBorder.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF3, 0xF3, 0xF3));
            OuterBorder.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x28, 0x00, 0x00, 0x00));
        }
        else
        {
            OuterBorder.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x1E, 0x1E));
            OuterBorder.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3E, 0x3E, 0x3E));
        }

        if (_webViewReady && LauncherWebView.CoreWebView2 != null)
        {
            string th = isLight ? "light" : "dark";
            string acc = accentColor ?? _shellState?.Settings.AccentColor ?? "#3684E3";
            LauncherWebView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new
            {
                type = "theme_update",
                theme = th,
                accentColor = acc
            }));
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var env = await WebViewEnvironmentManager.GetSharedEnvironmentAsync();
            await LauncherWebView.EnsureCoreWebView2Async(env);
            LauncherWebView.DefaultBackgroundColor = System.Drawing.Color.Transparent;

            var core = LauncherWebView.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDevToolsEnabled = true;

            string webUiDir = TaskbarWebWindow.ResolveWebUiDirectory();
            core.SetVirtualHostNameToFolderMapping(
                "app.winzenshell.local",
                webUiDir,
                CoreWebView2HostResourceAccessKind.Allow);

            core.WebMessageReceived += OnWebMessageReceived;
            core.NavigationCompleted += (_, _) =>
            {
                _webViewReady = true;
                ApplyTheme(_shellState?.Settings.Theme, _shellState?.Settings.AccentColor);
                PushAppsData();
            };

            core.Navigate("https://app.winzenshell.local/app-launcher.html");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AppLauncher WebView2 init error: {ex.Message}");
        }
    }

    private void OnAppsLoaded()
    {
        Dispatcher.Invoke(() =>
        {
            if (_webViewReady)
            {
                PushAppsData();
            }
        });
    }

    public void PushAppsData()
    {
        if (!_webViewReady || LauncherWebView.CoreWebView2 == null) return;

        try
        {
            var payload = _appLauncherService.GetAppsPayload();
            string theme = _shellState?.Settings.Theme ?? "dark";
            string accentColor = _shellState?.Settings.AccentColor ?? "#3684E3";
            string json = JsonSerializer.Serialize(new
            {
                type = "apps_data",
                payload,
                theme,
                accentColor
            }, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            LauncherWebView.CoreWebView2.PostWebMessageAsJson(json);
        }
        catch { }
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var doc = JsonDocument.Parse(e.WebMessageAsJson);
            var root = doc.RootElement;
            string action = root.TryGetProperty("action", out var actProp) ? actProp.GetString() ?? "" : "";

            switch (action)
            {
                case "request_apps":
                    PushAppsData();
                    break;

                case "launch_app":
                    string target = root.TryGetProperty("target", out var tProp) ? tProp.GetString() ?? "" : "";
                    string name = root.TryGetProperty("name", out var nProp) ? nProp.GetString() ?? "" : "";
                    if (!string.IsNullOrEmpty(target))
                    {
                        _appLauncherService.Launch(target, name);
                        Hide();
                    }
                    break;

                case "close_launcher":
                    Hide();
                    break;

                case "open_settings":
                    Hide();
                    _openSettingsCallback.Invoke();
                    break;
            }
        }
        catch { }
    }

    public void ShowAt(
        double anchorScreenX,
        double anchorWidth,
        double taskbarTop,
        double taskbarHeight,
        string dockEdge,
        string region = "left")
    {
        double flyoutWidth = Width;
        double flyoutHeight = Height;

        var mon = WorkAreaAppBarService.GetPrimaryMonitorInfo().rcMonitor;
        double monitorLeft = mon.Left;
        double monitorRight = mon.Right;
        double monitorTop = mon.Top;
        double monitorBottom = mon.Bottom;
        double monitorWidth = monitorRight - monitorLeft;

        const double sideMargin = 12.0;
        double left;

        switch (region.ToLowerInvariant())
        {
            case "center":
                // Really center: perfectly in the middle of the display
                left = monitorLeft + ((monitorWidth - flyoutWidth) / 2.0);
                break;

            case "right":
                // Float right: docked to the right edge with clean margin
                left = monitorRight - flyoutWidth - sideMargin;
                break;

            case "left":
            default:
                // Float left: docked to the left edge with clean margin
                left = monitorLeft + sideMargin;
                break;
        }

        // Keep strictly within screen horizontal bounds
        if (left + flyoutWidth > monitorRight - sideMargin)
            left = monitorRight - flyoutWidth - sideMargin;
        if (left < monitorLeft + sideMargin)
            left = monitorLeft + sideMargin;

        const double floatingGap = 10.0;
        double top;
        if (string.Equals(dockEdge, "Top", StringComparison.OrdinalIgnoreCase))
        {
            top = taskbarTop + taskbarHeight + floatingGap;
        }
        else
        {
            top = taskbarTop - flyoutHeight - floatingGap;
        }

        // Keep within screen vertical bounds
        if (top < monitorTop + 12)
            top = monitorTop + 12;
        if (top + flyoutHeight > monitorBottom - 12)
            top = monitorBottom - flyoutHeight - 12;

        Left = left;
        Top = top;

        ApplyTheme(_shellState?.Settings.Theme, _shellState?.Settings.AccentColor);
        Show();
        Activate();
        Focus();

        if (_webViewReady && LauncherWebView.CoreWebView2 != null)
        {
            PushAppsData();
            LauncherWebView.CoreWebView2.PostWebMessageAsJson("{\"type\":\"focus_search\"}");
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}
