using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WinZenShell.Services;

public class NetworkStatusInfo
{
    public string NetworkType { get; set; } = "offline"; // "wifi", "ethernet", "offline"
    public string StatusText { get; set; } = "Disconnected";
    public string AdapterName { get; set; } = "";
    public string Ssid { get; set; } = "";
    public int SignalPercent { get; set; } = 0;
    public string LocalIp { get; set; } = "";
    public string GatewayIp { get; set; } = "";
    public bool IsConnected { get; set; } = false;
    public bool IsWifiHardwareOn { get; set; } = true;
    public bool IsWifiSoftwareOn { get; set; } = true;
}

public class WifiNetworkItem
{
    public string Ssid { get; set; } = "";
    public int SignalPercent { get; set; } = 0;
    public string Authentication { get; set; } = "";
    public string Encryption { get; set; } = "";
    public string Band { get; set; } = "";
    public bool IsConnected { get; set; } = false;
    public bool IsSecured { get; set; } = true;
    public bool IsKnownProfile { get; set; } = false;
}

public class NetworkService
{
    private readonly object _lock = new();
    private NetworkStatusInfo _cachedStatus = new();
    private DateTime _lastStatusFetch = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(1.5);

    public event EventHandler? NetworkChanged;

    public NetworkService()
    {
        try
        {
            NetworkChange.NetworkAvailabilityChanged += (_, _) => OnNetworkChanged();
            NetworkChange.NetworkAddressChanged += (_, _) => OnNetworkChanged();
        }
        catch { }
    }

    private void OnNetworkChanged()
    {
        lock (_lock)
        {
            _lastStatusFetch = DateTime.MinValue;
        }
        NetworkChanged?.Invoke(this, EventArgs.Empty);
    }

    public NetworkStatusInfo GetNetworkStatus()
    {
        lock (_lock)
        {
            if (DateTime.UtcNow - _lastStatusFetch < CacheDuration && _cachedStatus != null)
            {
                return _cachedStatus;
            }

            _cachedStatus = FetchStatusCore();
            _lastStatusFetch = DateTime.UtcNow;
            return _cachedStatus;
        }
    }

    private static NetworkStatusInfo FetchStatusCore()
    {
        var status = new NetworkStatusInfo();

        // 1. Inspect Wi-Fi interface via netsh
        string wifiOutput = RunNetsh("wlan show interfaces");
        bool wifiConnected = false;
        string wifiSsid = "";
        int wifiSignal = 0;

        foreach (var rawLine in wifiOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.Trim();
            if (line.StartsWith("State", StringComparison.OrdinalIgnoreCase) && line.IndexOf("connected", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                wifiConnected = true;
            }
            else if (line.StartsWith("SSID", StringComparison.OrdinalIgnoreCase) && !line.StartsWith("BSSID", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split(new[] { ':' }, 2);
                if (parts.Length > 1) wifiSsid = parts[1].Trim();
            }
            else if (line.StartsWith("Signal", StringComparison.OrdinalIgnoreCase))
            {
                var m = Regex.Match(line, @"(\d+)%");
                if (m.Success && int.TryParse(m.Groups[1].Value, out int s))
                {
                    wifiSignal = s;
                }
            }
            else if (line.StartsWith("Radio status", StringComparison.OrdinalIgnoreCase) || line.StartsWith("Hardware", StringComparison.OrdinalIgnoreCase))
            {
                if (line.IndexOf("Off", StringComparison.OrdinalIgnoreCase) >= 0) status.IsWifiHardwareOn = false;
            }
            else if (line.StartsWith("Software", StringComparison.OrdinalIgnoreCase))
            {
                if (line.IndexOf("Off", StringComparison.OrdinalIgnoreCase) >= 0) status.IsWifiSoftwareOn = false;
            }
        }

        // 2. Inspect active network interfaces via .NET
        NetworkInterface[] interfaces;
        try
        {
            interfaces = NetworkInterface.GetAllNetworkInterfaces();
        }
        catch
        {
            interfaces = Array.Empty<NetworkInterface>();
        }

        NetworkInterface? activeWifiIface = null;
        NetworkInterface? activeEthernetIface = null;

        foreach (var iface in interfaces)
        {
            if (iface.OperationalStatus != OperationalStatus.Up) continue;
            if (iface.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                iface.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;

            string desc = iface.Description.ToLowerInvariant();
            if (desc.Contains("virtual") || desc.Contains("hyper-v") || desc.Contains("vethernet") || desc.Contains("pseudo"))
                continue;

            var ipProps = iface.GetIPProperties();
            var ipv4Addr = ipProps.UnicastAddresses
                .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork &&
                                     !a.Address.ToString().StartsWith("169.254."));
            if (ipv4Addr == null) continue;

            var gateway = ipProps.GatewayAddresses
                .FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork);

            if (iface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
            {
                activeWifiIface ??= iface;
            }
            else if (iface.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
            {
                // Prefer an Ethernet interface that has a default gateway
                if (activeEthernetIface == null || gateway != null)
                {
                    activeEthernetIface = iface;
                }
            }
        }

        // Decision logic: Wi-Fi vs Ethernet vs Offline
        if (wifiConnected && !string.IsNullOrWhiteSpace(wifiSsid))
        {
            status.NetworkType = "wifi";
            status.Ssid = wifiSsid;
            status.SignalPercent = wifiSignal;
            status.StatusText = wifiSsid;
            status.IsConnected = true;

            if (activeWifiIface != null)
            {
                var ipProps = activeWifiIface.GetIPProperties();
                status.AdapterName = activeWifiIface.Name;
                var ip = ipProps.UnicastAddresses.FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);
                if (ip != null) status.LocalIp = ip.Address.ToString();
                var gw = ipProps.GatewayAddresses.FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork);
                if (gw != null) status.GatewayIp = gw.Address.ToString();
            }
        }
        else if (activeEthernetIface != null)
        {
            var ipProps = activeEthernetIface.GetIPProperties();
            status.NetworkType = "ethernet";
            status.AdapterName = activeEthernetIface.Name;
            status.StatusText = "Connected";
            status.IsConnected = true;

            var ip = ipProps.UnicastAddresses.FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);
            if (ip != null) status.LocalIp = ip.Address.ToString();
            var gw = ipProps.GatewayAddresses.FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork);
            if (gw != null) status.GatewayIp = gw.Address.ToString();
        }
        else
        {
            status.NetworkType = "offline";
            status.StatusText = "Disconnected";
            status.IsConnected = false;
        }

        return status;
    }

    public async Task<List<WifiNetworkItem>> ScanWifiNetworksAsync()
    {
        return await Task.Run(() =>
        {
            var knownProfiles = GetKnownProfiles();
            var currentStatus = GetNetworkStatus();
            string connectedSsid = currentStatus.NetworkType == "wifi" ? currentStatus.Ssid : "";

            string output = RunNetsh("wlan show networks mode=bssid");
            var map = new Dictionary<string, WifiNetworkItem>(StringComparer.OrdinalIgnoreCase);

            string currentSsid = "";
            string currentAuth = "";
            string currentEnc = "";
            string currentBand = "";
            int maxSignal = 0;

            foreach (var rawLine in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string line = rawLine.Trim();

                var ssidMatch = Regex.Match(line, @"^SSID\s+\d+\s*:\s*(.*)$");
                if (ssidMatch.Success)
                {
                    // Save previous SSID if exists
                    if (!string.IsNullOrWhiteSpace(currentSsid))
                    {
                        AddOrUpdateNetwork(map, currentSsid, currentAuth, currentEnc, currentBand, maxSignal, connectedSsid, knownProfiles);
                    }

                    currentSsid = ssidMatch.Groups[1].Value.Trim();
                    currentAuth = "";
                    currentEnc = "";
                    currentBand = "";
                    maxSignal = 0;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(currentSsid)) continue;

                if (line.StartsWith("Authentication", StringComparison.OrdinalIgnoreCase))
                {
                    var p = line.Split(new[] { ':' }, 2);
                    if (p.Length > 1) currentAuth = p[1].Trim();
                }
                else if (line.StartsWith("Encryption", StringComparison.OrdinalIgnoreCase))
                {
                    var p = line.Split(new[] { ':' }, 2);
                    if (p.Length > 1) currentEnc = p[1].Trim();
                }
                else if (line.StartsWith("Band", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(currentBand))
                {
                    var p = line.Split(new[] { ':' }, 2);
                    if (p.Length > 1) currentBand = p[1].Trim();
                }
                else if (line.StartsWith("Signal", StringComparison.OrdinalIgnoreCase))
                {
                    var sigMatch = Regex.Match(line, @"(\d+)%");
                    if (sigMatch.Success && int.TryParse(sigMatch.Groups[1].Value, out int s))
                    {
                        if (s > maxSignal) maxSignal = s;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(currentSsid))
            {
                AddOrUpdateNetwork(map, currentSsid, currentAuth, currentEnc, currentBand, maxSignal, connectedSsid, knownProfiles);
            }

            // Return sorted: connected first, then highest signal to lowest
            return map.Values
                .OrderByDescending(n => n.IsConnected)
                .ThenByDescending(n => n.SignalPercent)
                .ThenBy(n => n.Ssid)
                .ToList();
        });
    }

    private static void AddOrUpdateNetwork(
        Dictionary<string, WifiNetworkItem> map,
        string ssid,
        string auth,
        string enc,
        string band,
        int signal,
        string connectedSsid,
        HashSet<string> knownProfiles)
    {
        if (string.IsNullOrWhiteSpace(ssid)) return;

        bool isConnected = string.Equals(ssid, connectedSsid, StringComparison.OrdinalIgnoreCase);
        bool isSecured = !string.Equals(auth, "Open", StringComparison.OrdinalIgnoreCase);
        bool isKnown = knownProfiles.Contains(ssid);

        if (map.TryGetValue(ssid, out var existing))
        {
            if (signal > existing.SignalPercent)
            {
                existing.SignalPercent = signal;
            }
            if (string.IsNullOrEmpty(existing.Band) && !string.IsNullOrEmpty(band))
            {
                existing.Band = band;
            }
        }
        else
        {
            map[ssid] = new WifiNetworkItem
            {
                Ssid = ssid,
                Authentication = auth,
                Encryption = enc,
                Band = band,
                SignalPercent = signal,
                IsConnected = isConnected,
                IsSecured = isSecured,
                IsKnownProfile = isKnown
            };
        }
    }

    public HashSet<string> GetKnownProfiles()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string output = RunNetsh("wlan show profiles");
        foreach (var rawLine in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.Trim();
            var m = Regex.Match(line, @"All User Profile\s*:\s*(.*)$", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                string name = m.Groups[1].Value.Trim();
                if (!string.IsNullOrEmpty(name)) set.Add(name);
            }
        }
        return set;
    }

    public async Task<bool> ConnectWifiAsync(string ssid, string? password = null)
    {
        return await Task.Run(() =>
        {
            if (string.IsNullOrWhiteSpace(ssid)) return false;

            var known = GetKnownProfiles();
            if (known.Contains(ssid) || string.IsNullOrEmpty(password))
            {
                string res = RunNetsh($"wlan connect name=\"{EscapeQuotes(ssid)}\"");
                return res.IndexOf("Connection request was completed successfully", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       res.IndexOf("completed successfully", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            // Create XML profile for password
            string tempFile = Path.Combine(Path.GetTempPath(), $"winzen_wifi_{Guid.NewGuid():N}.xml");
            try
            {
                string xml = GenerateWlanProfileXml(ssid, password);
                File.WriteAllText(tempFile, xml, Encoding.UTF8);

                RunNetsh($"wlan add profile filename=\"{tempFile}\" user=all");
                string res = RunNetsh($"wlan connect name=\"{EscapeQuotes(ssid)}\"");
                return res.IndexOf("completed successfully", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch
            {
                return false;
            }
            finally
            {
                try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
            }
        });
    }

    public async Task<bool> DisconnectWifiAsync()
    {
        return await Task.Run(() =>
        {
            string res = RunNetsh("wlan disconnect");
            return res.IndexOf("completed successfully", StringComparison.OrdinalIgnoreCase) >= 0;
        });
    }

    public static void OpenWindowsNetworkSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:network-wifi") { UseShellExecute = true });
        }
        catch
        {
            try
            {
                Process.Start(new ProcessStartInfo("ms-settings:network") { UseShellExecute = true });
            }
            catch { }
        }
    }

    public static void OpenWindowsAvailableNetworks()
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-availablenetworks:") { UseShellExecute = true });
        }
        catch { }
    }

    public static void OpenNetworkConnections()
    {
        try
        {
            Process.Start(new ProcessStartInfo("ncpa.cpl") { UseShellExecute = true });
        }
        catch { }
    }

    private static string GenerateWlanProfileXml(string ssid, string password)
    {
        string safeSsid = System.Security.SecurityElement.Escape(ssid);
        string safePass = System.Security.SecurityElement.Escape(password);

        return $@"<?xml version=""1.0""?>
<WLANProfile xmlns=""http://www.microsoft.com/networking/WLAN/profile/v1"">
    <name>{safeSsid}</name>
    <SSIDConfig>
        <SSID>
            <name>{safeSsid}</name>
        </SSID>
    </SSIDConfig>
    <connectionType>ESS</connectionType>
    <connectionMode>manual</connectionMode>
    <MSM>
        <security>
            <authEncryption>
                <authentication>WPA2PSK</authentication>
                <encryption>AES</encryption>
                <useOneX>false</useOneX>
            </authEncryption>
            <sharedKey>
                <keyType>passPhrase</keyType>
                <protected>false</protected>
                <keyMaterial>{safePass}</keyMaterial>
            </sharedKey>
        </security>
    </MSM>
</WLANProfile>";
    }

    private static string RunNetsh(string args)
    {
        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "netsh.exe",
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                }
            };
            proc.Start();
            string outText = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(3000);
            return outText;
        }
        catch
        {
            return "";
        }
    }

    private static string EscapeQuotes(string val)
    {
        return val.Replace("\"", "\\\"");
    }
}
