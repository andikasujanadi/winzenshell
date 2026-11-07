# WinZenShell — Antigravity Session Handoff & Architecture Guide

> **Current State**: `v0.1.0-alpha`  
> **Platform**: Windows 11 (64-bit)  
> **Framework**: .NET 10 (WPF) + Microsoft.Web.WebView2 + Native Win32 APIs  
> **Frontend**: Vanilla Web Components (Custom Elements v1, Shadow DOM, CSS Custom Properties)

---

## 1. Executive Summary

**WinZenShell** is a modular, high-performance custom Windows 11 taskbar and community widget studio. It replaces the default Windows 11 taskbar with a **100% web-rendered taskbar** and companion floating flyouts, while respecting the Windows 11 Desktop Working Area (`SPI_SETWORKAREA`), supporting full-screen auto-suppression, and offering an interactive drag-and-drop widget arrangement system.

This document serves as the complete handoff for another Antigravity AI pair programming session on a different machine to continue development seamlessly.

---

## 2. Architecture & Core Concepts

```
┌────────────────────────────────────────────────────────────────────────┐
│                          Windows 11 OS                                │
│   ┌──────────────────────────┐     ┌──────────────────────────────┐    │
│   │ Native Taskbar Hidden    │     │ Working Area Adjusted        │    │
│   │ (Shell_TrayWnd hidden)   │     │ (SPI_SETWORKAREA + ABM_SETPOS)│   │
│   └─────────────▲────────────┘     └──────────────▲───────────────┘    │
└─────────────────┼─────────────────────────────────┼────────────────────┘
                  │ Win32 API Interop               │
┌─────────────────┴─────────────────────────────────┴────────────────────┐
│                    WinZenShell .NET 10 Host                            │
│                                                                        │
│  • WorkAreaAppBarService: Autohide-first tray hiding & work area sync  │
│  • SystemMetricsService: CPU/RAM/Battery metrics & window management  │
│  • AudioService: CoreAudio (IAudioEndpointVolume) master audio control │
│  • BrightnessService: WmiMonitorBrightness / DDC/CI controls           │
│  • VirtualDesktopService: IVirtualDesktopManager virtual desktop APIs  │
│  • AppLauncherService: Start Menu & shell app indexer + search engine  │
│  • ShellStateService: Unified JSON settings persistence & state events │
│  • WidgetManagerService: Manifest discovery, packaging & hot-reload   │
└─────────────────────────────────┬──────────────────────────────────────┘
                                  │ Direct WebView2 PostMessage Bridge
┌─────────────────────────────────┴──────────────────────────────────────┐
│                    WebView2 DirectComposition Windows                  │
│                                                                        │
│  1. TaskbarWebWindow (Docked taskbar with Left, Center, Right regions) │
│  2. SettingsWebWindow (Fluent 2 NavigationView settings studio)        │
│  3. Flyouts:                                                           │
│     - AppLauncherFlyoutWindow (ChromeOS-style launcher)                │
│     - ControlCenterFlyoutWindow (Quick toggles & compact widgets)      │
│     - ClockFlyoutWindow (Analog SVG clock + digital card)              │
│     - SystemMonitorFlyoutWindow (Telemetry meters & Task Manager)      │
│     - VolumeFlyoutWindow (Audio slider, mute & instant 0% snap)        │
│     - BrightnessFlyoutWindow (Display slider & instant 0% snap)        │
│                                                                        │
│  * Universal Theme Engine (universal-theme.css with Fluent 2 tokens)   │
│  * Shadow DOM isolated widgets with injected theme custom properties   │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Key Features in `v0.1.0-alpha`

### 3.1 Taskbar & Working Area
- **Autohide-First Tray Hiding**: Sets Windows 11 `Shell_TrayWnd` and `Shell_SecondaryTrayWnd` to `ABS_AUTOHIDE` before hiding (`ShowWindow(SW_HIDE)`). This guarantees Windows 11 releases the native work area without leaving dead space.
- **`SPI_SETWORKAREA` Synchronization**: Registers WinZenShell with `SHAppBarMessage` (`ABM_NEW`, `ABM_QUERYPOS`, `ABM_SETPOS`), cleanly updating the desktop working area so maximized windows snap flush against the custom taskbar.
- **Emergency Failsafe**: Global hotkey **`Ctrl + Shift + Alt + Z`** immediately unhides and restores the native Windows taskbar at any point. Clean exit handlers also restore the native shell on close.
- **Fullscreen Suppression**: Monitors `ABN_FULLSCREENAPP` and window z-order bounds. When a true fullscreen application is active (games, YouTube fullscreen, media players), the taskbar automatically conceals itself and restores upon exit.

### 3.2 4-Region Widget Arrangement
- Taskbar is split into four logical zones:
  1. `left`: Pinned to monitor left edge (default: App Launcher).
  2. `center`: Centered dynamically on screen (default: Active Window Switcher).
  3. `right`: System tray & status indicators (default: Clock, Brightness, Volume, Telemetry, Control Center).
  4. `inactive`: Disabled widgets that will not render on the taskbar.
- **Settings Drag & Drop**: Live interactive drag-and-drop preview in the Settings app allows grabbing widgets by full width, live reordering on the fly, and persisting region slots.

### 3.3 Flyout UX & Windowing Mechanics
- **Single-Click Switching (`WM_MOUSEACTIVATE`)**:
  - Overrides `WM_MOUSEACTIVATE` in `TaskbarWebWindow.xaml.cs` to return `MA_ACTIVATE` (1) instead of the default `MA_ACTIVATEANDEAT` (2).
  - Clicking from Volume icon to Brightness icon (or any other flyout icon) immediately dismisses Volume and opens Brightness in a **single click** without eating the mouse event.
- **Debounced Toggle Off**:
  - Flyout windows record `LastDeactivatedTime = DateTime.UtcNow`.
  - Clicking the same icon that owns the currently active flyout toggles it off smoothly (`TotalMilliseconds < 350` check prevents reopening race conditions).
- **Proportional Slider 0% Snap**:
  - `VolumeFlyoutWindow` and `BrightnessFlyoutWindow` intercept preview mouse events (`PreviewMouseLeftButtonDown`, `PreviewMouseMove`, `PreviewMouseLeftButtonUp`) with `IsMoveToPointEnabled="True"`.
  - Clicks at or near 0% immediately snap directly to `0%` rather than WPF's built-in `Slider.DecreaseLarge` (-10%).
  - Mouse wheel scrolling anywhere over the sliders adjusts volume/brightness smoothly in ±3% increments.
- **Aligned Floating Popups**:
  - Flyouts float 10px above (or below when taskbar is docked Top) the taskbar.
  - Horizontally align to `left`, `center`, or `right` with screen boundary clamping.

### 3.4 Official Widget Catalog (`Widgets/`)

| Widget Directory | Slot | Description | Flyout Companion |
| :--- | :--- | :--- | :--- |
| `zen-start-button` | `left` | Customizable Start / App launcher (ChromeOS, W11, W10, Apple) with dynamic theme tinting | `AppLauncherFlyoutWindow` (ChromeOS search) |
| `zen-active-windows` | `center` | Running apps switcher, thumbnails, badges & close (full Dark/Light theme) | Window thumbnails & Win11 context menu |
| `zen-virtual-desktops`| `left/center` | Windows 11 virtual desktop indicators | Native desktop switching |
| `zen-clock-pill` | `right` | Time & date pill | `ClockFlyoutWindow` (analog + digital) |
| `zen-brightness` | `right` | Brightness icon/meter with wheel scrolling | `BrightnessFlyoutWindow` |
| `zen-volume` | `right` | Master audio volume & mute state | `VolumeFlyoutWindow` |
| `zen-network` | `right` | Ethernet & Wi-Fi status, signal strength & IP | Network flyout with custom Fluent scrollbar |
| `zen-battery` | `right` | W11, W10, M27 (iOS 27) battery styles & charging states | Power & Sleep settings shortcut |
| `zen-system-monitor` | `right` | Real-time CPU & RAM usage | `SystemMonitorFlyoutWindow` |
| `zen-control-center` | `right` | Authentic 2-switch toggle icon (SF Symbols `switch.2`) | `ControlCenterFlyoutWindow` (modular grid) |

### 3.5 Battery & Power Studio (`zen-battery`)
- **Precision Vector SVG Geometry**:
  - Uses vector SVGs with SVG `clipPath` elements, preventing browser subpixel rounding shifts and keeping fill margins strictly symmetrical.
  - **M27 (iOS 27 / macOS)**: Translucent capsule track (`#30D158` / `#34C759` charging, `#FFD60A` energy saver, `#FF453A` low battery), straight vertical cut on partial charge, centered white bolt with drop shadow, and separate dome nub with authentic gap.
  - **W11 (Windows 11 Fluent)**: Rounded housing with 1.5px border, deep emerald fill in light mode (`#107C10`) / mint fill in dark mode (`#6CCB5F`), and a crisp solid bolt (black with white halo in light mode; white with dark halo in dark mode) piercing through the top housing.
  - **W10 (Windows 10 Metro)**: Sharp rectangular housing and inner fill bar.
- **Settings & Placement**:
  - Icon Style selector: `W11`, `W10`, `M27`.
  - Percentage Position selector: `None`, `Left`, `Right`.
- **Cross-Component Synchronization**:
  - Taskbar widget overrides (`batteryIconStyle`, `batteryPercentMode`) automatically pass through `ShellStateService.cs` (`ControlCenterItemPayload`) into `control-center.js` and render matching styles inside the Control Center flyout grid.

### 3.6 Universal Theme Engine (Windows 11 Fluent 2)
- Specified in `Widgets/universal-theme.css` and `src/WinZenShell/WebUI/universal-theme.css`.
- Injected directly into all widget shadow roots and flyout documents:
  - `--winzen-bg`: Solid backdrop (`#1e1e1e` dark / `#f3f3f3` light).
  - `--winzen-surface`: Inner cards (`#252525` dark / `#ffffff` light).
  - `--winzen-surface-hover` & `--winzen-surface-active`.
  - `--winzen-border`: Edge borders (`rgba(255,255,255,0.08)` / `rgba(0,0,0,0.08)`).
  - `--winzen-text` & `--winzen-text-secondary`.
  - `--winzen-accent`: Focus & highlights (`#60cdff` dark / `#0067c0` light).
- Complete Light Mode support across all surfaces:
  - Active Window Switcher window title text (`color: inherit;` with light mode `#1b1b1b`).
  - Settings studio: `Widgets & Layout` region arrangement containers, preview chips, and `Zen Control Center` preview tiles.
  - Dynamic icon tinting (`currentColor`) for start button launcher icons (Apple, ChromeOS, Windows 10).

---

## 4. Repository Structure

```
winzenshell/
├── Widgets/                                # Community & Official Widgets
│   ├── bridge.js                           # Standard host bridge for standalone testing
│   ├── universal-theme.css                 # Universal Fluent 2 design tokens
│   ├── zen-active-windows/                 # Task switcher widget
│   ├── zen-brightness/                     # Brightness widget
│   ├── zen-clock-pill/                     # Clock & date widget
│   ├── zen-control-center/                 # Control center button widget
│   ├── zen-start-button/                   # Start launcher widget
│   ├── zen-system-monitor/                 # CPU/RAM telemetry widget
│   ├── zen-virtual-desktops/               # Windows 11 virtual desktop switcher
│   └── zen-volume/                         # Audio volume widget
│
├── src/WinZenShell/                        # .NET 10 WPF Application
│   ├── App.xaml / App.xaml.cs              # Entry point, single-instance mutex, failsafe hotkeys
│   ├── WinZenShell.csproj                  # Project config (net10.0-windows, v0.1.0-alpha)
│   ├── Native/
│   │   └── Win32Api.cs                     # P/Invoke definitions (User32, Shell32, DwmApi, Ole32)
│   ├── Services/
│   │   ├── AppLauncherService.cs           # Start Menu app scanner & icon extractor
│   │   ├── AudioService.cs                 # CoreAudio endpoint volume & mute
│   │   ├── BrightnessService.cs            # WMI monitor brightness
│   │   ├── ShellStateService.cs            # State persistence (settings.json)
│   │   ├── SystemMetricsService.cs         # Performance counters & window enumeration
│   │   ├── VirtualDesktopService.cs        # IVirtualDesktopManager interop
│   │   ├── WidgetManagerService.cs         # Widget discovery & scaffolding
│   │   └── WorkAreaAppBarService.cs        # AppBar & SPI_SETWORKAREA sync
│   ├── WebUI/                              # Frontend HTML/CSS/JS for Host Windows
│   │   ├── universal-theme.css             # Synced theme tokens
│   │   ├── taskbar.html / .css / .js       # Taskbar shell & widget host
│   │   ├── settings.html / .css / .js      # Settings studio & layout arranger
│   │   ├── app-launcher.html / .css / .js  # ChromeOS-style launcher
│   │   ├── control-center.html / .css / .js# Control Center UI
│   │   └── bridge.js                       # Web-to-C# message broker
│   └── Windows/                            # WPF Host Windows & Flyouts
│       ├── TaskbarWebWindow.xaml[.cs]      # Main custom taskbar window
│       ├── SettingsWebWindow.xaml[.cs]     # Settings window
│       ├── AppLauncherFlyoutWindow.xaml[.cs]
│       ├── ControlCenterFlyoutWindow.xaml[.cs]
│       ├── ClockFlyoutWindow.xaml[.cs]
│       ├── SystemMonitorFlyoutWindow.xaml[.cs]
│       ├── VolumeFlyoutWindow.xaml[.cs]
│       └── BrightnessFlyoutWindow.xaml[.cs]
│
├── publish/WinZenShell/                    # Published binary output directory
├── ANTIGRAVITY_SESSION_HANDOFF.md          # This handoff reference
└── README.md                               # User-facing README
```

---

## 5. Prerequisites & Getting Started on Another Machine

### 5.1 Prerequisites
1. **Windows 11** (x64 or ARM64).
2. **.NET 10 SDK** (verify with `dotnet --version`).
3. **Microsoft Edge WebView2 Runtime** (preinstalled on Windows 11).
4. **Git**.

### 5.2 Build & Publish
Always publish or sync changes so `publish/WinZenShell/` has the compiled binary and the web assets:

```powershell
# 1. Kill any existing instance
Stop-Process -Name WinZenShell -Force -ErrorAction SilentlyContinue

# 2. Publish Release build
dotnet publish src/WinZenShell/WinZenShell.csproj -c Release -o publish/WinZenShell

# 3. Synchronize WebUI and Widgets
Copy-Item -Path "Widgets" -Destination "publish/WinZenShell/Widgets" -Recurse -Force
Copy-Item -Path "src/WinZenShell/WebUI" -Destination "publish/WinZenShell/WebUI" -Recurse -Force
```

### 5.3 Running the Application
```powershell
# Run from publish output
Start-Process "publish\WinZenShell\WinZenShell.exe"

# Or run via dotnet CLI for rapid debugging
dotnet run --project src/WinZenShell/WinZenShell.csproj
```

---

## 6. Crucial Implementation Gotchas

1. **WebView2 DirectComposition Captures**:
   - WebView2 uses DirectComposition. Standard GDI `PrintWindow(hWnd, hdc, 2)` renders black for web contents.
   - For automated verification screenshots of WebView2 surfaces, use `Graphics.CopyFromScreen(rect.Left, rect.Top, 0, 0, rect.Size)`.
2. **`WM_MOUSEACTIVATE` on Taskbar**:
   - `WndProc` in `TaskbarWebWindow.xaml.cs` intercepts `WM_MOUSEACTIVATE` (0x0021) and returns `MA_ACTIVATE` (1). **Do not remove this hook**, otherwise Windows will eat clicks on taskbar icons when a flyout is open.
3. **Shadow DOM Theming**:
   - Widgets are rendered inside Shadow Roots for style encapsulation. Because Shadow Roots block outer CSS stylesheets, `taskbar.js` automatically clones and injects the `:root[data-theme="..."]` CSS variables directly into each widget's shadow root.
4. **Slider Proportional Clamping**:
   - Standard WPF `Slider` handles clicks on its `Track` with `Slider.DecreaseLarge` and `Slider.IncreaseLarge`.
   - In `VolumeFlyoutWindow` and `BrightnessFlyoutWindow`, this is overridden with `PreviewMouseLeftButtonDown` which calculates `(pt.X - thumbOffset) / trackWidth` directly, snapping cleanly to `0%`.

---

## 7. Next Steps & Roadmap for Beta (`v0.2.0-beta`)

1. **Multi-Monitor Support**:
   - Extend `TaskbarWebWindow` to spawn child appbar taskbars on secondary displays (`Screen.AllScreens`), mirroring the native multi-monitor experience.
2. **Widget Store / Package Manager**:
   - Add `.zenwidget` zip package import/export in the Settings app with one-click installation and digital signature verification.
3. **System Tray Overflow Overflow (Notification Area)**:
   - Implement shell icon notification callbacks (`Shell_NotifyIcon`) or proxy native notification tray icons into the web frontend.
4. **Smooth Window Animations**:
   - Add subtle entrance/exit scale and blur animations (using Windows 11 Composition / Mica backdrops where supported).

---

*Handoff document prepared for Antigravity AI pair programming sessions.*
