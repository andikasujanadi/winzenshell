using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace WinZenShell.Services;

public class WidgetManifest
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("author")]
    public string Author { get; set; } = "Community";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("slot")]
    public string Slot { get; set; } = "right"; // "left", "center", "right"

    [JsonPropertyName("order")]
    public int Order { get; set; } = 10;

    [JsonPropertyName("entry")]
    public string Entry { get; set; } = "index.html";

    [JsonPropertyName("width")]
    public int Width { get; set; } = 160;

    [JsonPropertyName("autoWidth")]
    public bool AutoWidth { get; set; } = true;

    [JsonPropertyName("showAppName")]
    public bool ShowAppName { get; set; } = true;

    [JsonPropertyName("combineApps")]
    public bool CombineApps { get; set; } = false;

    [JsonPropertyName("showThumbnails")]
    public bool ShowThumbnails { get; set; } = true;

    [JsonPropertyName("startIcon")]
    public string StartIcon { get; set; } = "windows";

    [JsonPropertyName("startText")]
    public string StartText { get; set; } = "Start";

    [JsonPropertyName("showStartText")]
    public bool ShowStartText { get; set; } = false;

    [JsonPropertyName("batteryIconStyle")]
    public string BatteryIconStyle { get; set; } = "hyperos";

    [JsonPropertyName("batteryPercentMode")]
    public string BatteryPercentMode { get; set; } = "outside-right";

    [JsonPropertyName("displayMode")]
    public string DisplayMode { get; set; } = "widget"; // "icon" or "widget"

    [JsonPropertyName("aspectRatio")]
    public string AspectRatio { get; set; } = "1:1"; // "1:1" or "1:2"

    [JsonPropertyName("supportedModes")]
    public List<string> SupportedModes { get; set; } = new() { "widget" };

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("folderName")]
    public string FolderName { get; set; } = "";

    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    [JsonPropertyName("htmlContent")]
    public string HtmlContent { get; set; } = "";

    [JsonPropertyName("cssContent")]
    public string CssContent { get; set; } = "";

    [JsonPropertyName("jsContent")]
    public string JsContent { get; set; } = "";
}

public class WidgetManagerService
{
    private readonly string _widgetsRootPath;

    public string WidgetsRootPath => _widgetsRootPath;

    public WidgetManagerService()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string? repoRoot = FindRepoRoot(baseDir);
        _widgetsRootPath = repoRoot != null
            ? Path.Combine(repoRoot, "Widgets")
            : Path.Combine(baseDir, "Widgets");

        Directory.CreateDirectory(_widgetsRootPath);
    }

    private static string? FindRepoRoot(string startPath)
    {
        var dir = new DirectoryInfo(startPath);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "src")) &&
                Directory.Exists(Path.Combine(dir.FullName, "Widgets")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        return null;
    }

    public List<WidgetManifest> DiscoverWidgets(Dictionary<string, WidgetStateOverride>? overrides = null)
    {
        var result = new List<WidgetManifest>();
        if (!Directory.Exists(_widgetsRootPath))
            return result;

        foreach (string dir in Directory.GetDirectories(_widgetsRootPath))
        {
            string manifestPath = Path.Combine(dir, "manifest.json");
            if (!File.Exists(manifestPath)) continue;

            try
            {
                string json = File.ReadAllText(manifestPath);
                var manifest = JsonSerializer.Deserialize<WidgetManifest>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (manifest == null) continue;

                string folderName = Path.GetFileName(dir);
                manifest.FolderName = folderName;
                if (string.IsNullOrWhiteSpace(manifest.Id))
                    manifest.Id = folderName;

                manifest.Url = $"https://widgets.winzenshell.local/{folderName}/{manifest.Entry}";

                // Load HTML, CSS, and JS directly for instant Shadow DOM rendering
                string htmlPath = Path.Combine(dir, string.IsNullOrWhiteSpace(manifest.Entry) ? "index.html" : manifest.Entry);
                string cssPath = Path.Combine(dir, "style.css");
                string jsPath = Path.Combine(dir, "script.js");

                if (File.Exists(htmlPath))
                {
                    string rawHtml = File.ReadAllText(htmlPath);
                    manifest.HtmlContent = ExtractBodyHtml(rawHtml);
                }

                if (File.Exists(cssPath))
                {
                    manifest.CssContent = File.ReadAllText(cssPath);
                }

                if (File.Exists(jsPath))
                {
                    manifest.JsContent = File.ReadAllText(jsPath);
                }

                if (overrides != null && (overrides.TryGetValue(manifest.Id, out var ov) ||
                    overrides.TryGetValue(folderName, out ov) ||
                    overrides.TryGetValue($"community.{folderName}", out ov)))
                {
                    manifest.Enabled = ov.Enabled;
                    if (!string.IsNullOrWhiteSpace(ov.Slot))
                        manifest.Slot = ov.Slot;
                    if (ov.Order.HasValue)
                        manifest.Order = ov.Order.Value;
                    if (ov.Width > 32)
                        manifest.Width = ov.Width;
                    manifest.AutoWidth = ov.AutoWidth;
                    manifest.ShowAppName = ov.ShowAppName;
                    manifest.CombineApps = ov.CombineApps;
                    manifest.ShowThumbnails = ov.ShowThumbnails;
                    if (!string.IsNullOrWhiteSpace(ov.StartIcon))
                        manifest.StartIcon = ov.StartIcon;
                    if (ov.StartText != null)
                        manifest.StartText = ov.StartText;
                    manifest.ShowStartText = ov.ShowStartText;
                    if (!string.IsNullOrWhiteSpace(ov.DisplayMode))
                        manifest.DisplayMode = ov.DisplayMode;
                    if (!string.IsNullOrWhiteSpace(ov.AspectRatio))
                        manifest.AspectRatio = ov.AspectRatio;
                    if (!string.IsNullOrWhiteSpace(ov.BatteryIconStyle))
                        manifest.BatteryIconStyle = ov.BatteryIconStyle;
                    if (!string.IsNullOrWhiteSpace(ov.BatteryPercentMode))
                        manifest.BatteryPercentMode = ov.BatteryPercentMode;
                }

                result.Add(manifest);
            }
            catch
            {
                // Ignore malformed community widget manifest
            }
        }

        return result.OrderBy(w => w.Order).ThenBy(w => w.Name).ToList();
    }

    private static string ExtractBodyHtml(string html)
    {
        var match = Regex.Match(html, @"<body[^>]*>([\s\S]*?)</body>", RegexOptions.IgnoreCase);
        string body = match.Success ? match.Groups[1].Value : html;
        // Strip <script> tags since script.js is executed directly with widget root scope
        body = Regex.Replace(body, @"<script\b[^<]*(?:(?!</script>)<[^<]*)*</script>", "", RegexOptions.IgnoreCase);
        return body.Trim();
    }

    public WidgetManifest CreateCommunityWidgetScaffold(string name, string author, string slot, int width)
    {
        string safeSlug = string.Join("-", name.ToLowerInvariant()
            .Split(new[] { ' ', '_', '-', '.', '/' }, StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(safeSlug))
            safeSlug = "custom-widget-" + DateTime.Now.ToString("HHmmss");

        string folderPath = Path.Combine(_widgetsRootPath, safeSlug);
        Directory.CreateDirectory(folderPath);

        var manifest = new WidgetManifest
        {
            Id = $"community.{safeSlug}",
            Name = name,
            Author = string.IsNullOrWhiteSpace(author) ? "Community Creator" : author,
            Version = "1.0.0",
            Description = $"Custom community web widget ({name})",
            Slot = string.IsNullOrWhiteSpace(slot) ? "right" : slot,
            Order = 50,
            Entry = "index.html",
            Width = width > 40 ? width : 170,
            Enabled = true,
            FolderName = safeSlug,
            Url = $"https://widgets.winzenshell.local/{safeSlug}/index.html"
        };

        string manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(folderPath, "manifest.json"), manifestJson);

        string html = $$"""
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8" />
  <link rel="stylesheet" href="style.css" />
</head>
<body>
  <button class="widget-pill" id="widgetRoot" title="Click to interact with {{name}}">
    <span class="dot"></span>
    <span class="label" id="titleText">{{name}}</span>
    <span class="value" id="metricVal">Ready</span>
  </button>
  <script src="script.js"></script>
</body>
</html>
""";
        File.WriteAllText(Path.Combine(folderPath, "index.html"), html);

        string css = """
:host {
  display: flex;
  align-items: center;
  height: 34px;
}
* {
  box-sizing: border-box;
  margin: 0;
  padding: 0;
  user-select: none;
}
.widget-pill {
  height: 34px;
  width: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  padding: 0 12px;
  border-radius: 7px;
  background: rgba(30, 41, 59, 0.85);
  border: 1px solid rgba(148, 163, 184, 0.25);
  color: #f8fafc;
  font-family: 'Segoe UI Variable Display', 'Segoe UI', sans-serif;
  cursor: pointer;
  transition: all 0.16s ease;
}
.widget-pill:hover {
  background: rgba(56, 189, 248, 0.22);
  border-color: rgba(56, 189, 248, 0.55);
}
.dot {
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: #38bdf8;
  box-shadow: 0 0 8px #38bdf8;
  flex-shrink: 0;
}
.label {
  font-size: 12px;
  font-weight: 600;
  white-space: nowrap;
  color: #ffffff;
}
.value {
  font-size: 11px;
  color: #93c5fd;
  font-variant-numeric: tabular-nums;
}
""";
        File.WriteAllText(Path.Combine(folderPath, "style.css"), css);

        string js = """
const valEl = root.getElementById('metricVal');
const btnEl = root.getElementById('widgetRoot');
let clicks = 0;

window.addEventListener('winzen:metrics', (e) => {
  const m = e.detail;
  if (m && clicks === 0 && valEl) {
    valEl.textContent = `CPU ${m.cpuPercent}%`;
  }
});

if (btnEl) {
  btnEl.addEventListener('click', () => {
    clicks++;
    if (valEl) valEl.textContent = `Clicked ${clicks}x`;
  });
}
""";
        File.WriteAllText(Path.Combine(folderPath, "script.js"), js);

        return manifest;
    }

    public void OpenWidgetsFolderInExplorer()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{_widgetsRootPath}\"",
                UseShellExecute = true
            });
        }
        catch
        {
            // Ignore
        }
    }
}

public class WidgetStateOverride
{
    public bool Enabled { get; set; } = true;
    public string Slot { get; set; } = "right";
    public int? Order { get; set; }
    public int Width { get; set; } = 160;
    public bool AutoWidth { get; set; } = true;
    public bool ShowAppName { get; set; } = true;
    public bool CombineApps { get; set; } = false;
    public bool ShowThumbnails { get; set; } = true;
    public string StartIcon { get; set; } = "windows";
    public string StartText { get; set; } = "Start";
    public bool ShowStartText { get; set; } = false;
    public string DisplayMode { get; set; } = "widget";
    public string AspectRatio { get; set; } = "1:1";
    public string BatteryIconStyle { get; set; } = "hyperos";
    public string BatteryPercentMode { get; set; } = "outside-right";
}

public class WidgetOrderUpdate
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("slot")]
    public string Slot { get; set; } = "right";

    [JsonPropertyName("order")]
    public int Order { get; set; }

    [JsonPropertyName("enabled")]
    public bool? Enabled { get; set; }
}
