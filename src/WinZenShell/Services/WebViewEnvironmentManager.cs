using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;

namespace WinZenShell.Services;

public static class WebViewEnvironmentManager
{
    private static Task<CoreWebView2Environment>? _sharedEnvTask;
    private static readonly object _lock = new();

    /// <summary>
    /// Microsoft WebView2 requires all WebView2 instances in the same process
    /// to share the exact same CoreWebView2Environment and User Data Folder.
    /// Attempting to create separate environments in the same process causes 0x800700AA (Resource in use).
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
}
