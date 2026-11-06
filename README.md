# WinZenShell — Windows 11 Full-Web Custom Taskbar & Community Widget Studio

**WinZenShell** is a customizable Windows 11 taskbar and shell studio featuring a **100% Web Frontend** (HTML / CSS / JavaScript) for the Settings UI, Custom Taskbar, and Community Widgets, backed by a lightweight .NET 10 + WebView2 Win32 interop host.

---

## Key Features (Workflow 1)

1. **Hide Native Windows 11 Taskbar (Autohide-First Sequence)**
   - Sets the native Windows taskbar (`Shell_TrayWnd`) to `ABS_AUTOHIDE` first via `SHAppBarMessage(ABM_SETSTATE)` so Windows 11 immediately releases the native working area.
   - Conceals `Shell_TrayWnd` and all secondary taskbars (`Shell_SecondaryTrayWnd`) via `ShowWindow(SW_HIDE)`.
   - Safely restores visibility and standard docked state when untoggled or when the application closes.

2. **Toggle WinZenShell Custom Taskbar**
   - Shows/hides the frameless, top-most custom Web Taskbar docked to the **Bottom** or **Top** edge of the primary monitor.

3. **Respected Desktop Working Area (`SPI_SETWORKAREA` + AppBar)**
   - Registers WinZenShell via `SHAppBarMessage(ABM_NEW, ABM_QUERYPOS, ABM_SETPOS)` and synchronizes `SystemParametersInfo(SPI_SETWORKAREA)`.
   - Maximized windows snap cleanly to the custom taskbar boundary without overlapping it or leaving dead space where the native taskbar used to be.
   - Includes a live **Desktop Working Area Visualizer** and a one-click **Test Maximize** button in the Settings UI.

4. **Community Widget Ecosystem (`Widgets/`)**
   - Every folder inside `Widgets/<widget-name>/` with a `manifest.json` and `index.html` is automatically discovered and rendered on the taskbar.
   - Includes `window.winzen` JavaScript bridge for live CPU/RAM metrics, active window switching, and triggering the Windows 11 Start Menu.
   - Includes a built-in **Community Widget Scaffolder** and **Hot-Reload** in the Settings UI.

5. **Safety & Failsafes**
   - Global emergency shortcut **`Ctrl + Shift + Alt + Z`** restores the native Windows 11 taskbar at any time.
   - Automatic native taskbar restoration on application exit or unhandled crash.

---

## Running WinZenShell

```powershell
dotnet run --project src\WinZenShell\WinZenShell.csproj
```
