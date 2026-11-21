using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace WinZenShell.Services;

public class IconPackManifest
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("author")]
    public string Author { get; set; } = "Community";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("iconCount")]
    public int IconCount { get; set; }
}

public class IconPackService
{
    private readonly string _userPacksDir;
    private readonly List<IconPackManifest> _packs = new();
    private readonly Dictionary<string, string> _activeIconFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _packAliases = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _dataUrlCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    private string _activePackId = "native";

    public string UserPacksDirectory => _userPacksDir;
    public string ActivePackId => _activePackId;

    public event Action? ActivePackChanged;

    // Built-in Windows process/app name -> FreeDesktop / GNOME-Look candidate icon stems
    private static readonly Dictionary<string, string[]> BuiltinWindowsToFreedesktopAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["explorer"] = new[] { "explorer", "file-manager", "system-file-manager", "file-manager-blue", "org.gnome.Nautilus", "nautilus", "nautilus-alt", "dolphin", "thunar", "folder" },
        ["file explorer"] = new[] { "explorer", "file-manager", "system-file-manager", "file-manager-blue", "org.gnome.Nautilus", "nautilus", "nautilus-alt", "dolphin", "folder" },
        ["file-explorer"] = new[] { "explorer", "file-manager", "system-file-manager", "file-manager-blue", "org.gnome.Nautilus", "nautilus", "nautilus-alt", "dolphin", "folder" },
        ["winstore.app"] = new[] { "microsoft-store", "gnome-software", "org.gnome.Software", "system-software-install", "software-store", "pamac", "muon-discover" },
        ["microsoft store"] = new[] { "microsoft-store", "gnome-software", "org.gnome.Software", "system-software-install", "software-store", "pamac", "muon-discover" },
        ["microsoft-store"] = new[] { "microsoft-store", "gnome-software", "org.gnome.Software", "system-software-install", "software-store", "pamac", "muon-discover" },
        ["ms-windows-store"] = new[] { "microsoft-store", "gnome-software", "org.gnome.Software", "system-software-install", "software-store" },
        ["systemsettings"] = new[] { "settings", "preferences-system", "gnome-control-center", "org.gnome.Settings", "systemsettings" },
        ["settings"] = new[] { "settings", "preferences-system", "gnome-control-center", "org.gnome.Settings", "systemsettings" },
        ["ms-settings"] = new[] { "settings", "preferences-system", "gnome-control-center", "org.gnome.Settings", "systemsettings" },
        ["windowsterminal"] = new[] { "windows-terminal", "terminal", "utilities-terminal", "org.gnome.Terminal", "konsole", "alacritty", "kitty" },
        ["windows terminal"] = new[] { "windows-terminal", "terminal", "utilities-terminal", "org.gnome.Terminal", "konsole", "alacritty" },
        ["windows-terminal"] = new[] { "windows-terminal", "terminal", "utilities-terminal", "org.gnome.Terminal", "konsole", "alacritty" },
        ["wt"] = new[] { "windows-terminal", "terminal", "utilities-terminal", "org.gnome.Terminal", "konsole" },
        ["powershell"] = new[] { "powershell", "terminal", "utilities-terminal", "org.gnome.Terminal" },
        ["pwsh"] = new[] { "powershell", "terminal", "utilities-terminal", "org.gnome.Terminal" },
        ["cmd"] = new[] { "terminal", "utilities-terminal", "org.gnome.Terminal" },
        ["notepad"] = new[] { "notepad", "accessories-text-editor", "org.gnome.TextEditor", "gedit", "text-editor", "kate", "notepadqq" },
        ["notepad++"] = new[] { "notepadqq", "notepad", "accessories-text-editor", "text-editor" },
        ["calc"] = new[] { "calculator", "accessories-calculator", "org.gnome.Calculator", "gnome-calculator", "kcalc" },
        ["calculator"] = new[] { "calculator", "accessories-calculator", "org.gnome.Calculator", "gnome-calculator", "kcalc" },
        ["calculatorapp"] = new[] { "calculator", "accessories-calculator", "org.gnome.Calculator", "gnome-calculator", "kcalc" },
        ["taskmgr"] = new[] { "task-manager", "utilities-system-monitor", "org.gnome.SystemMonitor", "gnome-system-monitor", "ksysguard" },
        ["task manager"] = new[] { "task-manager", "utilities-system-monitor", "org.gnome.SystemMonitor", "gnome-system-monitor", "ksysguard" },
        ["task-manager"] = new[] { "task-manager", "utilities-system-monitor", "org.gnome.SystemMonitor", "gnome-system-monitor", "ksysguard" },
        ["snippingtool"] = new[] { "snipping-tool", "accessories-screenshot", "org.gnome.Screenshot", "applets-screenshooter", "flameshot", "spectacle" },
        ["snipping tool"] = new[] { "snipping-tool", "accessories-screenshot", "org.gnome.Screenshot", "applets-screenshooter", "flameshot", "spectacle" },
        ["snipping-tool"] = new[] { "snipping-tool", "accessories-screenshot", "org.gnome.Screenshot", "applets-screenshooter", "flameshot" },
        ["screenclippinghost"] = new[] { "snipping-tool", "accessories-screenshot", "org.gnome.Screenshot", "applets-screenshooter" },
        ["mspaint"] = new[] { "mspaint", "kolourpaint", "org.gnome.Drawing", "drawing", "gimp" },
        ["paint"] = new[] { "mspaint", "kolourpaint", "org.gnome.Drawing", "drawing", "gimp" },
        ["photos"] = new[] { "photos", "org.gnome.Photos", "eog", "org.gnome.Loupe", "gwenview", "multimedia-photo-viewer" },
        ["microsoft.photos"] = new[] { "photos", "org.gnome.Photos", "eog", "org.gnome.Loupe", "gwenview", "multimedia-photo-viewer" },
        ["chrome"] = new[] { "google-chrome", "chrome", "com.google.Chrome", "chromium", "web-browser" },
        ["google chrome"] = new[] { "google-chrome", "chrome", "com.google.Chrome", "chromium", "web-browser" },
        ["msedge"] = new[] { "microsoft-edge", "com.microsoft.Edge", "msedge", "edge", "web-browser" },
        ["microsoft edge"] = new[] { "microsoft-edge", "com.microsoft.Edge", "msedge", "edge", "web-browser" },
        ["firefox"] = new[] { "firefox", "org.mozilla.firefox", "mozilla-firefox", "firefox-esr" },
        ["mozilla firefox"] = new[] { "firefox", "org.mozilla.firefox", "mozilla-firefox", "firefox-esr" },
        ["brave"] = new[] { "brave", "brave-browser", "com.brave.Browser" },
        ["brave browser"] = new[] { "brave", "brave-browser", "com.brave.Browser" },
        ["vivaldi"] = new[] { "vivaldi", "com.vivaldi.Vivaldi" },
        ["opera"] = new[] { "opera", "com.opera.Opera" },
        ["zen"] = new[] { "zen-browser", "app.zen_browser.zen", "zen", "firefox" },
        ["zen browser"] = new[] { "zen-browser", "app.zen_browser.zen", "zen", "firefox" },
        ["code"] = new[] { "visual-studio-code", "vscode", "code", "com.visualstudio.code", "code-oss", "vscodium" },
        ["visual studio code"] = new[] { "visual-studio-code", "vscode", "code", "com.visualstudio.code", "code-oss" },
        ["devenv"] = new[] { "visual-studio", "visualstudio", "code" },
        ["discord"] = new[] { "discord", "com.discordapp.Discord", "vesktop", "webcord" },
        ["vesktop"] = new[] { "vesktop", "discord", "com.discordapp.Discord" },
        ["spotify"] = new[] { "spotify", "com.spotify.Client", "spotify-client", "multimedia-audio-player" },
        ["steam"] = new[] { "steam", "com.valvesoftware.Steam" },
        ["steamwebhelper"] = new[] { "steam", "com.valvesoftware.Steam" },
        ["vlc"] = new[] { "vlc", "org.videolan.VLC" },
        ["vlc media player"] = new[] { "vlc", "org.videolan.VLC" },
        ["obs64"] = new[] { "obs", "com.obsproject.Studio", "obs-studio" },
        ["obs studio"] = new[] { "obs", "com.obsproject.Studio", "obs-studio" },
        ["telegram"] = new[] { "telegram", "org.telegram.desktop", "telegram-desktop" },
        ["whatsapp"] = new[] { "whatsapp", "whatsapp-desktop", "com.whatsapp.WhatsApp", "io.github.mimbrero.WhatsAppDesktop" },
        ["slack"] = new[] { "slack", "com.slack.Slack" },
        ["obsidian"] = new[] { "obsidian", "md.obsidian.Obsidian" },
        ["notion"] = new[] { "notion", "notion-app", "notion-app-enhanced" },
        ["figma"] = new[] { "figma", "figma-linux" },
        ["blender"] = new[] { "blender", "org.blender.Blender" },
        ["gimp"] = new[] { "gimp", "org.gimp.GIMP" },
        ["inkscape"] = new[] { "inkscape", "org.inkscape.Inkscape" },
        ["krita"] = new[] { "krita", "org.kde.krita" },
        ["audacity"] = new[] { "audacity", "org.audacityteam.Audacity" },
        ["7zfm"] = new[] { "7zip", "peazip", "org.gnome.FileRoller", "archive-manager", "ark" },
        ["7-zip file manager"] = new[] { "7zip", "peazip", "org.gnome.FileRoller", "archive-manager", "ark" },
        ["winrar"] = new[] { "winrar", "org.gnome.FileRoller", "archive-manager", "ark" },
        ["qbittorrent"] = new[] { "qbittorrent", "org.qbittorrent.qBittorrent" },
        ["winword"] = new[] { "ms-word", "microsoft-word", "word", "wps-office-wpsmain", "libreoffice-writer", "org.libreoffice.LibreOffice.writer" },
        ["word"] = new[] { "ms-word", "microsoft-word", "word", "wps-office-wpsmain", "libreoffice-writer" },
        ["excel"] = new[] { "ms-excel", "microsoft-excel", "excel", "wps-office-et", "libreoffice-calc", "org.libreoffice.LibreOffice.calc" },
        ["powerpnt"] = new[] { "ms-powerpoint", "microsoft-powerpoint", "powerpoint", "wps-office-wpp", "libreoffice-impress", "org.libreoffice.LibreOffice.impress" },
        ["powerpoint"] = new[] { "ms-powerpoint", "microsoft-powerpoint", "powerpoint", "wps-office-wpp", "libreoffice-impress" },
        ["outlook"] = new[] { "ms-outlook", "microsoft-outlook", "outlook", "thunderbird", "org.mozilla.Thunderbird", "internet-mail" },
        ["olk"] = new[] { "ms-outlook", "microsoft-outlook", "outlook", "thunderbird", "internet-mail" },
        ["teams"] = new[] { "teams", "microsoft-teams", "com.microsoft.Teams" },
        ["ms-teams"] = new[] { "teams", "microsoft-teams", "com.microsoft.Teams" },
        ["onedrive"] = new[] { "onedrive", "folder-cloud", "nextcloud" },
        ["lively"] = new[] { "lively", "preferences-desktop-wallpaper", "wallpaper", "cs-backgrounds", "preferences-desktop-theme" },
        ["lively.ui.winui"] = new[] { "lively", "preferences-desktop-wallpaper", "wallpaper", "cs-backgrounds" },
        ["lively wallpaper"] = new[] { "lively", "preferences-desktop-wallpaper", "wallpaper", "cs-backgrounds", "preferences-desktop-theme" },
        ["git"] = new[] { "git", "git-gui", "gitg", "git-cola", "git-dag", "utilities-terminal" },
        ["git bash"] = new[] { "git", "git-gui", "gitg", "git-cola", "utilities-terminal" },
        ["git-bash"] = new[] { "git", "git-gui", "gitg", "git-cola", "utilities-terminal" },
        ["git cmd"] = new[] { "git", "git-gui", "gitg", "git-cola", "utilities-terminal" },
        ["git-cmd"] = new[] { "git", "git-gui", "gitg", "git-cola", "utilities-terminal" },
        ["git gui"] = new[] { "git-gui", "git", "gitg", "git-cola" },
        ["git-gui"] = new[] { "git-gui", "git", "gitg", "git-cola" },
        ["git for windows"] = new[] { "git", "git-gui", "gitg", "git-cola", "git-dag" },
        ["git-for-windows"] = new[] { "git", "git-gui", "gitg", "git-cola", "git-dag" },
        ["wintoast"] = new[] { "git", "git-gui", "gitg", "git-cola" },
        ["mintty"] = new[] { "git", "utilities-terminal", "terminal", "org.gnome.Terminal" },
        ["githubdesktop"] = new[] { "github-desktop", "io.github.shiftey.Desktop", "github", "git" },
        ["github desktop"] = new[] { "github-desktop", "io.github.shiftey.Desktop", "github", "git" },
        ["postman"] = new[] { "postman", "com.getpostman.Postman" },
        ["docker desktop"] = new[] { "docker", "docker-desktop" }
    };

    public IconPackService()
    {
        _userPacksDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WinZenShell",
            "IconPacks");
        Directory.CreateDirectory(_userPacksDir);

        RefreshPacks();
    }

    public List<IconPackManifest> GetInstalledPacks()
    {
        lock (_lock)
        {
            return _packs.ToList();
        }
    }

    public void RefreshPacks()
    {
        var discovered = new List<IconPackManifest>();

        if (Directory.Exists(_userPacksDir))
        {
            foreach (var dir in Directory.EnumerateDirectories(_userPacksDir))
            {
                try
                {
                    var manifest = TryLoadOrCreateManifest(dir);
                    if (manifest != null && manifest.IconCount > 0)
                    {
                        discovered.Add(manifest);
                    }
                }
                catch { }
            }
        }

        discovered.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        lock (_lock)
        {
            _packs.Clear();
            _packs.AddRange(discovered);
        }

        LoadActivePack(_activePackId, notify: false);
    }

    public void SetActivePack(string? packId, bool notify = true)
    {
        string normalized = string.IsNullOrWhiteSpace(packId) ? "native" : packId.Trim();
        LoadActivePack(normalized, notify);
    }

    public bool DeletePack(string? packId)
    {
        if (string.IsNullOrWhiteSpace(packId) ||
            string.Equals(packId, "native", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string safeName = Path.GetFileName(packId.Trim());
        string packDir = Path.Combine(_userPacksDir, safeName);
        if (!Directory.Exists(packDir))
            return false;

        try
        {
            Directory.Delete(packDir, recursive: true);
            bool wasActive = string.Equals(_activePackId, safeName, StringComparison.OrdinalIgnoreCase);
            RefreshPacks();
            if (wasActive)
            {
                SetActivePack("native", notify: true);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void LoadActivePack(string packId, bool notify)
    {
        lock (_lock)
        {
            _activePackId = packId;
            _activeIconFiles.Clear();
            _packAliases.Clear();
            _dataUrlCache.Clear();

            if (!string.Equals(packId, "native", StringComparison.OrdinalIgnoreCase))
            {
                string packDir = Path.Combine(_userPacksDir, packId);
                if (!Directory.Exists(packDir))
                {
                    // Fallback to native if pack was removed
                    _activePackId = "native";
                }
                else
                {
                    IndexPackIcons(packDir);
                }
            }
        }

        if (notify)
        {
            ActivePackChanged?.Invoke();
        }
    }

    private void IndexPackIcons(string packDir)
    {
        string aliasesPath = Path.Combine(packDir, "aliases.json");
        if (File.Exists(aliasesPath))
        {
            try
            {
                var map = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(aliasesPath));
                if (map != null)
                {
                    foreach (var kv in map)
                    {
                        if (!string.IsNullOrWhiteSpace(kv.Key) && !string.IsNullOrWhiteSpace(kv.Value))
                        {
                            _packAliases[kv.Key.Trim()] = kv.Value.Trim();
                        }
                    }
                }
            }
            catch { }
        }

        var searchDirs = new List<string>();
        string appsDir = Path.Combine(packDir, "apps");
        if (Directory.Exists(appsDir))
            searchDirs.Add(appsDir);
        else
            searchDirs.Add(packDir);

        foreach (var sDir in searchDirs)
        {
            foreach (var file in Directory.EnumerateFiles(sDir, "*.*", SearchOption.AllDirectories))
            {
                string ext = Path.GetExtension(file);
                if (!ext.Equals(".svg", StringComparison.OrdinalIgnoreCase) &&
                    !ext.Equals(".png", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string stem = Path.GetFileNameWithoutExtension(file);
                if (string.IsNullOrWhiteSpace(stem)) continue;

                // Prefer SVG over PNG if both exist
                if (!_activeIconFiles.TryGetValue(stem, out string? existing) ||
                    (ext.Equals(".svg", StringComparison.OrdinalIgnoreCase) && !existing.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)))
                {
                    _activeIconFiles[stem] = file;
                }
            }
        }
    }

    public string ResolveAppIconDataUrl(string? appId, string? processName, string? title, string? exeOrTargetPath = null)
    {
        lock (_lock)
        {
            if (string.Equals(_activePackId, "native", StringComparison.OrdinalIgnoreCase) || _activeIconFiles.Count == 0)
                return "";

            var keysToTry = new List<string>();

            void AddCandidateKey(string? raw)
            {
                if (string.IsNullOrWhiteSpace(raw)) return;
                string trimmed = raw.Trim();
                keysToTry.Add(trimmed);

                string lower = trimmed.ToLowerInvariant();
                string hyphenated = Regex.Replace(lower, @"[\s_]+", "-");
                if (hyphenated != lower) keysToTry.Add(hyphenated);

                string compact = Regex.Replace(lower, @"[\s_\-\.]+", "");
                if (compact != lower) keysToTry.Add(compact);

                // Strip trailing version numbers, bitness, or edition suffixes (e.g. "Blender 5.2" -> "blender")
                string withoutVersion = Regex.Replace(
                    lower,
                    @"(\s+v?\d+([\.\-]\d+)*|\s+\([^)]*\)|\s+x64|\s+x86|\s+64-bit|\s+32-bit)+$",
                    "",
                    RegexOptions.IgnoreCase).Trim();
                if (!string.IsNullOrWhiteSpace(withoutVersion) && withoutVersion != lower)
                {
                    keysToTry.Add(withoutVersion);
                    string wvHyphen = Regex.Replace(withoutVersion, @"[\s_]+", "-");
                    if (wvHyphen != withoutVersion) keysToTry.Add(wvHyphen);
                }

                // Strip common executable wrapper suffixes (e.g. "blender-launcher" -> "blender", "obs64" -> "obs")
                string withoutLauncher = Regex.Replace(
                    hyphenated,
                    @"(-launcher|-desktop|-client|-app|-bin|-gui|-win64|-win32|-x64|-x86|64|32)$",
                    "",
                    RegexOptions.IgnoreCase).Trim('-');
                if (!string.IsNullOrWhiteSpace(withoutLauncher) && withoutLauncher != hyphenated)
                {
                    keysToTry.Add(withoutLauncher);
                }
            }

            if (!string.IsNullOrWhiteSpace(exeOrTargetPath))
            {
                if (exeOrTargetPath.StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase))
                {
                    string aumid = exeOrTargetPath["shell:AppsFolder\\".Length..];
                    int bangIdx = aumid.IndexOf('_');
                    if (bangIdx > 0)
                    {
                        string pkgPrefix = aumid[..bangIdx];
                        AddCandidateKey(pkgPrefix);
                        int lastDot = pkgPrefix.LastIndexOf('.');
                        if (lastDot >= 0 && lastDot + 1 < pkgPrefix.Length)
                        {
                            AddCandidateKey(pkgPrefix[(lastDot + 1)..]);
                        }
                    }
                }
                else
                {
                    try
                    {
                        string fileStem = Path.GetFileNameWithoutExtension(exeOrTargetPath.TrimEnd(':'));
                        AddCandidateKey(fileStem);
                    }
                    catch { }
                }
            }

            AddCandidateKey(appId);
            AddCandidateKey(processName);
            AddCandidateKey(title);

            var stemsToProbe = new List<string>();
            foreach (var k in keysToTry)
            {
                if (_packAliases.TryGetValue(k, out string? customStem))
                    stemsToProbe.Add(customStem);

                if (BuiltinWindowsToFreedesktopAliases.TryGetValue(k, out var builtinStems))
                    stemsToProbe.AddRange(builtinStems);

                stemsToProbe.Add(k);
            }

            foreach (var stem in stemsToProbe)
            {
                if (_activeIconFiles.TryGetValue(stem, out string? iconFilePath))
                {
                    return GetOrLoadDataUrl(iconFilePath);
                }
            }

            return "";
        }
    }

    private string GetOrLoadDataUrl(string filePath)
    {
        if (_dataUrlCache.TryGetValue(filePath, out string? cached))
            return cached;

        try
        {
            byte[] bytes = File.ReadAllBytes(filePath);
            if (bytes.Length == 0) return "";

            string mime = filePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
                ? "image/svg+xml"
                : "image/png";

            string dataUrl = $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
            _dataUrlCache[filePath] = dataUrl;
            return dataUrl;
        }
        catch
        {
            return "";
        }
    }

    private static IconPackManifest? TryLoadOrCreateManifest(string packDir)
    {
        string manifestPath = Path.Combine(packDir, "manifest.json");
        string folderName = Path.GetFileName(packDir);
        IconPackManifest? manifest = null;

        if (File.Exists(manifestPath))
        {
            try
            {
                manifest = JsonSerializer.Deserialize<IconPackManifest>(File.ReadAllText(manifestPath));
            }
            catch { }
        }

        // Count icons in apps/ or packDir
        string appsDir = Path.Combine(packDir, "apps");
        string countDir = Directory.Exists(appsDir) ? appsDir : packDir;
        int iconCount = Directory.EnumerateFiles(countDir, "*.*", SearchOption.AllDirectories)
            .Count(f => f.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".png", StringComparison.OrdinalIgnoreCase));

        if (iconCount == 0 && Directory.Exists(packDir))
        {
            // Check if user dropped a raw GNOME-Look folder directly into IconPacks/
            int imported = NormalizeRawThemeDirectoryInPlace(packDir);
            if (imported > 0)
            {
                iconCount = imported;
            }
        }

        manifest ??= new IconPackManifest
        {
            Id = SanitizePackId(folderName),
            Name = ReadThemeNameFromIndexTheme(packDir) ?? PrettifyPackName(folderName),
            Author = "Community",
            Version = "1.0"
        };

        manifest.Id = folderName;
        if (string.IsNullOrWhiteSpace(manifest.Name))
            manifest.Name = PrettifyPackName(folderName);
        manifest.IconCount = iconCount;

        if (!File.Exists(manifestPath) && iconCount > 0)
        {
            try
            {
                File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        return manifest;
    }

    /// <summary>
    /// Imports a GNOME-Look archive (.zip, .tar.xz, .tar.gz, .tgz, .tar) or an extracted theme folder
    /// into %LocalAppData%\WinZenShell\IconPacks\&lt;pack-id&gt;\apps\ and resolves Linux symlinks.
    /// </summary>
    public bool ImportIconPack(string sourcePath, out string importedPackId, out string statusMessage, Action<int, string>? onProgress = null)
    {
        importedPackId = "";
        statusMessage = "";

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            statusMessage = "No path specified.";
            return false;
        }

        string? tempExtractDir = null;
        try
        {
            string sourceRoot;
            string rawName;

            if (Directory.Exists(sourcePath))
            {
                sourceRoot = sourcePath;
                rawName = Path.GetFileName(sourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                onProgress?.Invoke(20, $"Scanning folder '{rawName}'...");
            }
            else if (File.Exists(sourcePath))
            {
                rawName = StripArchiveExtensions(Path.GetFileName(sourcePath));
                onProgress?.Invoke(12, $"Extracting '{Path.GetFileName(sourcePath)}'...");
                tempExtractDir = Path.Combine(Path.GetTempPath(), "WinZenShell_IconPack_" + Guid.NewGuid().ToString("N")[..8]);
                Directory.CreateDirectory(tempExtractDir);

                if (sourcePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    ZipFile.ExtractToDirectory(sourcePath, tempExtractDir, overwriteFiles: true);
                }
                else
                {
                    // Use Windows 11 built-in bsdtar (tar.exe) for .tar.xz, .tar.gz, .tgz, .tar
                    // Exclude non-app folders and do NOT redirect stderr without reading (prevents 4KB pipe deadlock on symlink warnings)
                    const string excludes =
                        "--exclude=\"*/panel/*\" --exclude=\"*/places/*\" --exclude=\"*/status/*\" " +
                        "--exclude=\"*/mimetypes/*\" --exclude=\"*/actions/*\" --exclude=\"*/devices/*\" " +
                        "--exclude=\"*/emblems/*\" --exclude=\"*/categories/*\" --exclude=\"*/animations/*\" " +
                        "--exclude=\"*@2*\"";
                    var psi = new ProcessStartInfo
                    {
                        FileName = "tar.exe",
                        Arguments = $"{excludes} -xf \"{sourcePath}\" -C \"{tempExtractDir}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    using var proc = Process.Start(psi);
                    proc?.WaitForExit(30000);
                }

                sourceRoot = tempExtractDir;
            }
            else
            {
                statusMessage = "Selected file or folder does not exist.";
                return false;
            }

            // Locate theme root (either sourceRoot itself or a top-level subdirectory containing index.theme / apps)
            onProgress?.Invoke(45, "Locating theme root and app icons...");
            string themeRoot = LocateThemeRoot(sourceRoot);
            string displayName = ReadThemeNameFromIndexTheme(themeRoot) ?? PrettifyPackName(rawName);
            string packId = SanitizePackId(displayName);
            if (string.IsNullOrWhiteSpace(packId))
                packId = "custom-pack";

            string destPackDir = Path.Combine(_userPacksDir, packId);
            string destAppsDir = Path.Combine(destPackDir, "apps");
            Directory.CreateDirectory(destAppsDir);

            int copied = ExtractAppsFromThemeTree(themeRoot, destAppsDir, onProgress);
            if (File.Exists(sourcePath) && !sourcePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                onProgress?.Invoke(88, "Resolving Linux icon symlinks...");
                copied += ResolveTarSymlinksIntoAppsDir(sourcePath, destAppsDir);
            }
            if (copied == 0)
            {
                statusMessage = "No app icons (.svg or .png) were found in that archive/folder.";
                return false;
            }

            var manifest = new IconPackManifest
            {
                Id = packId,
                Name = displayName,
                Author = "GNOME-Look / Community",
                Version = "1.0",
                IconCount = copied
            };

            File.WriteAllText(
                Path.Combine(destPackDir, "manifest.json"),
                JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));

            importedPackId = packId;
            onProgress?.Invoke(96, $"Activating '{displayName}'...");
            RefreshPacks();
            SetActivePack(packId, notify: true);
            statusMessage = $"Imported '{displayName}' ({copied} app icons)";
            onProgress?.Invoke(100, statusMessage);
            return true;
        }
        catch (Exception ex)
        {
            statusMessage = $"Import failed: {ex.Message}";
            return false;
        }
        finally
        {
            if (!string.IsNullOrEmpty(tempExtractDir) && Directory.Exists(tempExtractDir))
            {
                try { Directory.Delete(tempExtractDir, recursive: true); } catch { }
            }
        }
    }

    private static int NormalizeRawThemeDirectoryInPlace(string packDir)
    {
        try
        {
            string tempApps = Path.Combine(Path.GetTempPath(), "WinZenShell_Apps_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(tempApps);
            try
            {
                int copied = ExtractAppsFromThemeTree(packDir, tempApps);
                if (copied > 0)
                {
                    string destApps = Path.Combine(packDir, "apps");
                    Directory.CreateDirectory(destApps);
                    foreach (var file in Directory.EnumerateFiles(tempApps))
                    {
                        File.Copy(file, Path.Combine(destApps, Path.GetFileName(file)), overwrite: true);
                    }
                }
                return copied;
            }
            finally
            {
                try { Directory.Delete(tempApps, recursive: true); } catch { }
            }
        }
        catch
        {
            return 0;
        }
    }

    private static string LocateThemeRoot(string root)
    {
        if (File.Exists(Path.Combine(root, "index.theme")) ||
            File.Exists(Path.Combine(root, "manifest.json")))
        {
            return root;
        }

        // Check immediate subdirectories (prefer non-dark/non-variant base folder or the one with the most app icons)
        var subDirs = Directory.GetDirectories(root);
        if (subDirs.Length == 1)
        {
            return LocateThemeRoot(subDirs[0]);
        }

        string? bestSub = null;
        int bestCount = -1;
        foreach (var sub in subDirs)
        {
            if (File.Exists(Path.Combine(sub, "index.theme")) || Directory.Exists(Path.Combine(sub, "apps")))
            {
                int count = Directory.EnumerateFiles(sub, "*.*", SearchOption.AllDirectories)
                    .Take(500)
                    .Count(f => f.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
                if (count > bestCount)
                {
                    bestCount = count;
                    bestSub = sub;
                }
            }
        }

        return bestSub ?? root;
    }

    private static int ExtractAppsFromThemeTree(string themeRoot, string destAppsDir, Action<int, string>? onProgress = null)
    {
        // Collect all candidate app icon files across the theme tree, prioritizing:
        // 1. scalable/apps or apps/scalable (.svg)
        // 2. 64x64/apps or apps/64
        // 3. 48x48/apps or apps/48
        // 4. any apps/ folder or top-level icons
        var allIconFiles = Directory.EnumerateFiles(themeRoot, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Build a global filename lookup for resolving broken/text-stub Linux symlinks
        var realFilesByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in allIconFiles)
        {
            if (IsRealIconFile(file))
            {
                string fn = Path.GetFileName(file);
                if (!realFilesByName.ContainsKey(fn) ||
                    (file.Contains("scalable", StringComparison.OrdinalIgnoreCase) && !realFilesByName[fn].Contains("scalable", StringComparison.OrdinalIgnoreCase)))
                {
                    realFilesByName[fn] = file;
                }
            }
        }

        // Filter to app icons (if the theme has 'apps' directories; otherwise accept all icons)
        bool hasAppsFolder = allIconFiles.Any(f =>
            f.Contains($"{Path.DirectorySeparatorChar}apps{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
            f.Contains($"{Path.AltDirectorySeparatorChar}apps{Path.AltDirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));

        var appCandidateFiles = hasAppsFolder
            ? allIconFiles.Where(f =>
                (f.Contains($"{Path.DirectorySeparatorChar}apps{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
                 f.Contains($"{Path.AltDirectorySeparatorChar}apps{Path.AltDirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)) &&
                !f.Contains("@2x", StringComparison.OrdinalIgnoreCase) &&
                !Path.GetFileNameWithoutExtension(f).EndsWith("-symbolic", StringComparison.OrdinalIgnoreCase))
              .ToList()
            : allIconFiles;

        // Sort so scalable (.svg) > 64 > 48 > others
        int ScorePath(string p)
        {
            int s = 0;
            if (p.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)) s += 100;
            if (p.Contains("scalable", StringComparison.OrdinalIgnoreCase)) s += 50;
            else if (p.Contains("64", StringComparison.OrdinalIgnoreCase)) s += 30;
            else if (p.Contains("48", StringComparison.OrdinalIgnoreCase)) s += 20;
            else if (p.Contains("128", StringComparison.OrdinalIgnoreCase)) s += 15;
            return s;
        }

        var ordered = appCandidateFiles
            .OrderByDescending(ScorePath)
            .ToList();

        var writtenStems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int copied = 0;
        int totalCandidates = Math.Max(1, ordered.Count);
        int processed = 0;

        foreach (var file in ordered)
        {
            processed++;
            if (onProgress != null && (processed % 250 == 0 || processed == totalCandidates))
            {
                int pct = 50 + (int)Math.Round((processed / (double)totalCandidates) * 35.0);
                onProgress(Math.Clamp(pct, 50, 85), $"Copying app icons ({copied} extracted)...");
            }

            string stem = Path.GetFileNameWithoutExtension(file);
            if (string.IsNullOrWhiteSpace(stem) || writtenStems.Contains(stem))
                continue;

            string? resolvedFile = ResolveSymlinkOrRealFile(file, realFilesByName);
            if (string.IsNullOrEmpty(resolvedFile) || !File.Exists(resolvedFile))
                continue;

            string ext = Path.GetExtension(resolvedFile);
            string destFile = Path.Combine(destAppsDir, stem + ext);
            try
            {
                File.Copy(resolvedFile, destFile, overwrite: true);
                writtenStems.Add(stem);
                copied++;
            }
            catch { }
        }

        return copied;
    }

    /// <summary>
    /// Reads symlink entries ('l...' lines containing '/apps/' and ' -> ') from a tar archive via 'tar.exe -tvf'
    /// and materializes them as real files in destAppsDir (since Windows blocks unprivileged tar symlink creation).
    /// </summary>
    private static int ResolveTarSymlinksIntoAppsDir(string tarFilePath, string destAppsDir)
    {
        var symlinkMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "tar.exe",
                Arguments = $"-tvf \"{tarFilePath}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return 0;

            using var reader = proc.StandardOutput;
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Length == 0 || line[0] != 'l') continue;
                int arrowIdx = line.IndexOf(" -> ", StringComparison.Ordinal);
                if (arrowIdx <= 0) continue;

                string leftPart = line[..arrowIdx].TrimEnd();
                string targetPart = line[(arrowIdx + 4)..].Trim();

                int spaceIdx = leftPart.LastIndexOf(' ');
                string linkPath = spaceIdx >= 0 ? leftPart[(spaceIdx + 1)..] : leftPart;

                if (!linkPath.Contains("/apps/", StringComparison.OrdinalIgnoreCase) &&
                    !linkPath.Contains("\\apps\\", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (linkPath.Contains("@2", StringComparison.OrdinalIgnoreCase))
                    continue;

                string linkStem = Path.GetFileNameWithoutExtension(linkPath);
                string targetStem = Path.GetFileNameWithoutExtension(targetPart);
                if (string.IsNullOrWhiteSpace(linkStem) ||
                    string.IsNullOrWhiteSpace(targetStem) ||
                    linkStem.EndsWith("-symbolic", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                symlinkMap.TryAdd(linkStem, targetStem);
            }
            proc.WaitForExit(15000);
        }
        catch
        {
            return 0;
        }

        if (symlinkMap.Count == 0) return 0;

        var existingByStem = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(destAppsDir))
        {
            string stem = Path.GetFileNameWithoutExtension(file);
            if (!string.IsNullOrWhiteSpace(stem))
            {
                existingByStem[stem] = file;
            }
        }

        int resolvedCount = 0;
        foreach (var (linkStem, _) in symlinkMap)
        {
            if (existingByStem.ContainsKey(linkStem)) continue;

            string current = linkStem;
            for (int depth = 0; depth < 6; depth++)
            {
                if (!symlinkMap.TryGetValue(current, out string? next))
                    break;
                current = next;
                if (existingByStem.TryGetValue(current, out string? realFile))
                {
                    string ext = Path.GetExtension(realFile);
                    string destFile = Path.Combine(destAppsDir, linkStem + ext);
                    try
                    {
                        File.Copy(realFile, destFile, overwrite: false);
                        existingByStem[linkStem] = destFile;
                        resolvedCount++;
                    }
                    catch { }
                    break;
                }
            }
        }

        return resolvedCount;
    }

    private static bool IsRealIconFile(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length < 16) return false;
            if (info.Length > 300) return true;

            byte[] head = new byte[(int)Math.Min(info.Length, 128)];
            using var fs = File.OpenRead(path);
            int read = fs.Read(head, 0, head.Length);
            if (read >= 4 && head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47)
                return true; // PNG magic

            string text = Encoding.UTF8.GetString(head, 0, read).TrimStart();
            return text.StartsWith("<svg", StringComparison.OrdinalIgnoreCase) ||
                   text.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) ||
                   text.StartsWith("<!--", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string? ResolveSymlinkOrRealFile(string file, Dictionary<string, string> realFilesByName, int depth = 0)
    {
        if (depth > 4) return null;
        if (IsRealIconFile(file)) return file;

        try
        {
            var info = new FileInfo(file);
            if (info.Exists && info.Length > 0 && info.Length < 300)
            {
                string linkTarget = File.ReadAllText(file).Trim();
                if (!string.IsNullOrWhiteSpace(linkTarget) && !linkTarget.Contains('\n'))
                {
                    string relPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file) ?? "", linkTarget.Replace('/', Path.DirectorySeparatorChar)));
                    if (File.Exists(relPath))
                    {
                        var resolved = ResolveSymlinkOrRealFile(relPath, realFilesByName, depth + 1);
                        if (resolved != null) return resolved;
                    }

                    string targetName = Path.GetFileName(linkTarget);
                    if (realFilesByName.TryGetValue(targetName, out string? byName))
                        return byName;
                }
            }
        }
        catch { }

        return null;
    }

    private static string? ReadThemeNameFromIndexTheme(string dir)
    {
        string indexTheme = Path.Combine(dir, "index.theme");
        if (!File.Exists(indexTheme)) return null;

        try
        {
            foreach (var line in File.ReadLines(indexTheme))
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("Name=", StringComparison.OrdinalIgnoreCase))
                {
                    string val = trimmed["Name=".Length..].Trim();
                    if (!string.IsNullOrWhiteSpace(val))
                        return val;
                }
            }
        }
        catch { }
        return null;
    }

    private static string StripArchiveExtensions(string fileName)
    {
        string[] exts = { ".tar.xz", ".tar.gz", ".tar.bz2", ".tgz", ".txz", ".tar", ".zip" };
        foreach (var ext in exts)
        {
            if (fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
            {
                return fileName[..^ext.Length];
            }
        }
        return Path.GetFileNameWithoutExtension(fileName);
    }

    private static string SanitizePackId(string raw)
    {
        string lower = raw.Trim().ToLowerInvariant();
        string slug = Regex.Replace(lower, @"[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "icon-pack" : slug;
    }

    private static string PrettifyPackName(string raw)
    {
        string cleaned = raw.Replace('-', ' ').Replace('_', ' ').Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "Custom Icon Pack" : cleaned;
    }
}
