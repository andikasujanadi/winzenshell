using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;

namespace WinZenShell.Services;

public static class WebViewEnvironmentManager
{
    private static Task<CoreWebView2Environment>? _sharedEnvTask;
    private static Task<CoreWebView2Environment>? _compositionEnvTask;
    private static readonly object _lock = new();

    /// <summary>
    /// Microsoft WebView2 requires all windowed WebView2 instances in the same process
    /// to share the exact same CoreWebView2Environment and User Data Folder.
    /// </summary>
    public static Task<CoreWebView2Environment> GetSharedEnvironmentAsync()
    {
        lock (_lock)
        {
            if (_sharedEnvTask == null)
            {
                string userDataDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "WinZenShell",
                    "WebView2_SharedProfile");
                Directory.CreateDirectory(userDataDir);

                _sharedEnvTask = CoreWebView2Environment.CreateAsync(null, userDataDir);
            }
            return _sharedEnvTask;
        }
    }

    /// <summary>
    /// Dedicated environment for WebView2CompositionControl (Visual Hosting mode),
    /// as WebView2 does not allow mixing CompositionController and HWND Controller on the same User Data Folder.
    /// </summary>
    public static Task<CoreWebView2Environment> GetCompositionEnvironmentAsync()
    {
        lock (_lock)
        {
            if (_compositionEnvTask == null)
            {
                string userDataDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "WinZenShell",
                    "WebView2_CompositionProfile");
                Directory.CreateDirectory(userDataDir);

                _compositionEnvTask = CoreWebView2Environment.CreateAsync(null, userDataDir);
            }
            return _compositionEnvTask;
        }
    }
}
