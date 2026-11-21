using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace WinZenShell.Native;

/// <summary>
/// Extracts native Windows 11 icons for packaged UWP / Microsoft Store apps via AUMID and IShellItemImageFactory.
/// </summary>
public static class UwpAppIconHelper
{
    public const string MicrosoftStoreAumid = "Microsoft.WindowsStore_8wekyb3d8bbwe!App";

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetApplicationUserModelId(
        IntPtr hProcess,
        ref uint applicationUserModelIdLength,
        StringBuilder applicationUserModelId);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        IntPtr pbc,
        [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);

    [DllImport("shell32.dll", EntryPoint = "SHCreateItemFromParsingName", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateShellItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        IntPtr pbc,
        [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItem ppv);

    private static readonly Guid BHID_EnumItems = new("94f60519-2850-4924-aa5a-d15e84868039");

    private enum SIGDN : uint
    {
        SIGDN_NORMALDISPLAY = 0x00000000,
        SIGDN_PARENTRELATIVEPARSING = 0x80018001
    }

    [ComImport]
    [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        [PreserveSig]
        int BindToHandler(IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid bhid, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IEnumShellItems ppv);
        void GetParent(out IShellItem ppsi);
        void GetDisplayName(SIGDN sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        void Compare(IShellItem psi, uint hint, out int piOrder);
    }

    [ComImport]
    [Guid("70629033-e363-4a28-a567-0db78006e6d7")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IEnumShellItems
    {
        [PreserveSig]
        int Next(uint celt, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] IShellItem[] rgelt, out uint pceltFetched);
        void Skip(uint celt);
        void Reset();
        void Clone(out IEnumShellItems ppenum);
    }

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);

    [ComImport]
    [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(SIZE size, SIIGBF flags, out IntPtr phbm);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE
    {
        public int cx;
        public int cy;
        public SIZE(int cx, int cy) { this.cx = cx; this.cy = cy; }
    }

    [Flags]
    private enum SIIGBF
    {
        SIIGBF_RESIZETOFIT = 0x00,
        SIIGBF_BIGGERSIZEOK = 0x01,
        SIIGBF_ICONONLY = 0x04
    }

    public static string? TryGetProcessAumid(uint pid)
    {
        if (pid == 0) return null;
        IntPtr hProc = Win32Api.OpenProcess(Win32Api.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (hProc == IntPtr.Zero) return null;

        try
        {
            uint len = 260;
            var sb = new StringBuilder((int)len);
            int res = GetApplicationUserModelId(hProc, ref len, sb);
            if (res == 0 && sb.Length > 0)
            {
                return sb.ToString();
            }
        }
        catch { }
        finally
        {
            Win32Api.CloseHandle(hProc);
        }

        return null;
    }

    public static string ExtractIconDataUrlByAumid(string aumid, int size = 48)
    {
        if (string.IsNullOrWhiteSpace(aumid)) return "";

        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            return ExtractIconDataUrlByAumidSta(aumid, size);
        }

        string result = "";
        var thread = new Thread(() =>
        {
            result = ExtractIconDataUrlByAumidSta(aumid, size);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        thread.Join(1500);
        return result;
    }

    public static System.Collections.Generic.List<(string Name, string Aumid)> EnumerateAppsFolderUwpApps()
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            return EnumerateAppsFolderUwpAppsSta();
        }

        var list = new System.Collections.Generic.List<(string Name, string Aumid)>();
        var thread = new Thread(() =>
        {
            list = EnumerateAppsFolderUwpAppsSta();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        thread.Join(3000);
        return list;
    }

    private static System.Collections.Generic.List<(string Name, string Aumid)> EnumerateAppsFolderUwpAppsSta()
    {
        var results = new System.Collections.Generic.List<(string Name, string Aumid)>();
        try
        {
            SHCreateShellItemFromParsingName(
                "shell:AppsFolder",
                IntPtr.Zero,
                typeof(IShellItem).GUID,
                out var appsFolder);

            if (appsFolder == null) return results;

            int hr = appsFolder.BindToHandler(IntPtr.Zero, BHID_EnumItems, typeof(IEnumShellItems).GUID, out var enumItems);
            if (hr != 0 || enumItems == null) return results;

            var items = new IShellItem[1];
            while (enumItems.Next(1, items, out uint fetched) == 0 && fetched == 1)
            {
                var item = items[0];
                if (item == null) continue;
                try
                {
                    item.GetDisplayName(SIGDN.SIGDN_PARENTRELATIVEPARSING, out string aumid);
                    item.GetDisplayName(SIGDN.SIGDN_NORMALDISPLAY, out string name);

                    // UWP / Microsoft Store packaged apps always have a '!' in their AUMID (PackageFamilyName!AppId)
                    if (!string.IsNullOrWhiteSpace(name) &&
                        !string.IsNullOrWhiteSpace(aumid) &&
                        aumid.Contains('!'))
                    {
                        results.Add((name.Trim(), aumid.Trim()));
                    }
                }
                catch { }
            }
        }
        catch { }

        return results;
    }

    private static string ExtractIconDataUrlByAumidSta(string aumid, int size)
    {
        IntPtr hBitmap = IntPtr.Zero;
        try
        {
            SHCreateItemFromParsingName(
                $"shell:AppsFolder\\{aumid}",
                IntPtr.Zero,
                typeof(IShellItemImageFactory).GUID,
                out var factory);

            if (factory == null) return "";

            int hr = factory.GetImage(new SIZE(size, size), SIIGBF.SIIGBF_ICONONLY | SIIGBF.SIIGBF_BIGGERSIZEOK, out hBitmap);
            if (hr != 0 || hBitmap == IntPtr.Zero)
            {
                hr = factory.GetImage(new SIZE(size, size), SIIGBF.SIIGBF_RESIZETOFIT | SIIGBF.SIIGBF_BIGGERSIZEOK, out hBitmap);
            }

            if (hr != 0 || hBitmap == IntPtr.Zero) return "";

            var source = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(size, size));
            source.Freeze();

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var ms = new MemoryStream();
            encoder.Save(ms);
            return "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
        }
        catch
        {
            return "";
        }
        finally
        {
            if (hBitmap != IntPtr.Zero)
            {
                DeleteObject(hBitmap);
            }
        }
    }
}
