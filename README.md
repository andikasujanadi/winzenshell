# WinZenShell (Alpha)

<div align="center">

[![Platform: Windows 11](https://img.shields.io/badge/Platform-Windows%2011%20(64--bit)-0078D4?logo=windows11&logoColor=white)](https://microsoft.com/windows)
[![Runtime: .NET 10](https://img.shields.io/badge/Runtime-.NET%2010%20(WPF)-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Frontend: WebView2](https://img.shields.io/badge/Frontend-WebView2%20%7C%20Web%20Components-008080?logo=microsoftedge&logoColor=white)](https://developer.microsoft.com/microsoft-edge/webview2/)
[![Release: Alpha](https://img.shields.io/badge/Release-v0.1.0--alpha-FF9800)](ANTIGRAVITY_SESSION_HANDOFF.md)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

**WinZenShell** is a modular, high-performance Windows 11 custom taskbar and community widget studio.  
It combines a **100% Web Frontend** (HTML5, Modern CSS, Web Components, Shadow DOM) with a lightweight **.NET 10 + WebView2** Win32 interop host.

</div>

---

> [!IMPORTANT]
> **Alpha Release Notice (`v0.1.0-alpha`)**:  
> WinZenShell is currently in active Alpha development. Core taskbar replacement, working area management, drag-and-drop layout configuration, theme engine, and flyout systems are fully operational. Multi-monitor mirroring and native notification tray proxying are planned for subsequent beta milestones.

---

## Key Features

### 🪟 Windows 11 Shell Integration
* **Autohide-First Concealment**: Automatically places the default Windows 11 taskbar (`Shell_TrayWnd`) into `ABS_AUTOHIDE` before hiding, guaranteeing that Windows immediately relinquishes the desktop work area without leaving dead screen margins.
* **Desktop Work Area Sync (`SPI_SETWORKAREA`)**: Automatically synchronizes `SHAppBarMessage` boundaries so maximized windows snap cleanly against the custom taskbar without overlapping or creating dead space.
* **True Fullscreen Suppression**: Automatically conceals the custom taskbar when a fullscreen game, video (YouTube, Netflix), or presentation is running, and restores it instantly upon exiting fullscreen.
* **Emergency Failsafe**: Press **`Ctrl + Shift + Alt + Z`** at any time to immediately restore and unhide the default Windows 11 taskbar.

### 🎨 Modular 4-Region Drag-and-Drop Layout
* Split into **Left**, **Center**, **Right**, and **Inactive** zones.
* **Interactive Arranger in Settings**: Drag and drop widgets with smooth on-the-fly ghost previews. Changes to widget order on the taskbar take effect cleanly upon mouse release.
* **Inactive Zone**: Park widgets you don't need without deleting them.

### 🌓 Universal Fluent 2 Theme Engine
* Official **Windows 11 Fluent 2 Design Specification** color palettes.
* Supports both **Windows 11 Dark** and **Windows 11 Light** themes across the entire taskbar, widgets, flyouts, and Settings app.
* Seamless token sharing: Widgets reference `--winzen-*` CSS custom properties, automatically injected into each widget's isolated Shadow DOM.

### 🚀 Floating Flyouts & Official Widgets
1. **ChromeOS-Style App Launcher (`zen-start-button`)**:
   * Quick-launch search bar with instant prefix/fuzzy filtering across all installed Start Menu apps.
   * Recent applications row and full keyboard navigation (<kbd>↑</kbd> <kbd>↓</kbd> <kbd>Enter</kbd> <kbd>Esc</kbd>).
   * Dynamically aligns to Left, Center, or Right depending on start button placement.
2. **Active Window Switcher (`zen-active-windows`)**:
   * Live window list, thumbnail previews, active focus indicators, pin/unpin toggles, and right-click context menu.
3. **Windows 11 Virtual Desktop Switcher (`zen-virtual-desktops`)**:
   * Displays all active virtual desktops with indicator pills and one-click native desktop switching (`IVirtualDesktopManager`).
4. **Clock & Calendar Flyout (`zen-clock-pill`)**:
   * High-contrast digital time and date pill on the taskbar.
   * Flyout features an animated analog clock, bold digital time, localized date, and timezone badge.
5. **System Telemetry & Performance (`zen-system-monitor`)**:
   * Live real-time CPU & RAM utilization bars and percentages.
   * Direct shortcut to Windows Task Manager.
6. **Master Volume Flyout (`zen-volume`)**:
   * CoreAudio master volume control with mute toggle.
   * Mouse wheel scrolling anywhere over the icon/slider for fluid adjustment.
   * Proportional click tracking: clicking 0% snaps directly to **0%** (no accidental 10% decrements).
7. **Display Brightness Flyout (`zen-brightness`)**:
   * WMI-powered brightness slider with mouse wheel scrolling and instant 0% snap.
8. **Control Center (`zen-control-center`)**:
   * Unified quick-settings card grid containing brightness, volume, telemetry, and settings launcher.

### ⚡ Flyout UX Enhancements
* **Single-Click Switching**: Seamlessly click from Volume to Brightness to Clock without Windows discarding clicks (`WM_MOUSEACTIVATE -> MA_ACTIVATE`).
* **Debounced Toggle**: Clicking the active flyout's own icon toggles it off smoothly.

---

## Architecture Overview

```
WinZenShell (Host)
  ├── WorkAreaAppBarService    -> SHAppBarMessage + SPI_SETWORKAREA
  ├── SystemMetricsService     -> CPU, RAM, Battery & Window enumeration
  ├── AudioService             -> CoreAudio IAudioEndpointVolume
  ├── BrightnessService        -> WmiMonitorBrightness / DDC-CI
  ├── VirtualDesktopService    -> IVirtualDesktopManager
  ├── AppLauncherService       -> Start Menu indexer & icon extractor
  └── WidgetManagerService     -> Manifest discovery & hot-reload
        │
        ▼ (Edge WebView2 DirectComposition)
Taskbar & Flyouts (Web Frontend)
  ├── universal-theme.css      -> Fluent 2 Light & Dark design tokens
  ├── Shadow DOM Isolation     -> Injected theme variables
  └── window.winzen Bridge     -> Bidirectional IPC
```

---

## Getting Started

### Prerequisites
* **Windows 11** (64-bit).
* **[.NET 10 SDK](https://dotnet.microsoft.com/download)**.
* **Microsoft Edge WebView2 Runtime** (installed by default on Windows 11).

### Build & Run

```powershell
# Clone repository
git clone https://github.com/your-username/winzenshell.git
cd winzenshell

# Run in Development mode
dotnet run --project src\WinZenShell\WinZenShell.csproj

# Or publish a standalone release
dotnet publish src\WinZenShell\WinZenShell.csproj -c Release -o publish\WinZenShell
Start-Process "publish\WinZenShell\WinZenShell.exe"
```

---

## Community Widget Development

Creating a custom widget is as simple as creating a folder inside `Widgets/`:

```
Widgets/
└── my-custom-widget/
    ├── manifest.json
    ├── index.html
    ├── script.js
    └── style.css
```

### Sample `manifest.json`
```json
{
  "id": "community.my-custom-widget",
  "name": "My Custom Widget",
  "version": "1.0.0",
  "author": "Your Name",
  "description": "A lightweight custom widget",
  "defaultSlot": "right",
  "width": 120,
  "height": 40
}
```

### Using the Bridge & Theme Tokens
Widgets are rendered inside Shadow DOMs with automatic access to `window.winzen` and Fluent 2 CSS variables:

```css
/* style.css */
:host {
  display: flex;
  align-items: center;
  background: var(--winzen-surface);
  color: var(--winzen-text);
  border-radius: var(--winzen-radius-sm);
  padding: 4px 10px;
}

:host(:hover) {
  background: var(--winzen-surface-hover);
}
```

```javascript
// script.js
window.winzen.shell.onMetricsUpdate((metrics) => {
  console.log('CPU:', metrics.cpuPercent, 'RAM:', metrics.ramPercent);
});
```

---

## Keyboard Shortcuts

| Shortcut | Action |
| :--- | :--- |
| **`Ctrl + Shift + Alt + Z`** | **Emergency Failsafe**: Restore and unhide default Windows taskbar |
| **`Esc`** | Dismiss active flyout or clear App Launcher search |
| **`↑` / `↓` / `Enter`** | Navigate and open apps in ChromeOS App Launcher |

---

## Handoff & Session Continuation

For developers or Antigravity AI pair programming sessions picking up this project on another machine, see:
📄 [**ANTIGRAVITY_SESSION_HANDOFF.md**](ANTIGRAVITY_SESSION_HANDOFF.md)

---

## License

This project is licensed under the [MIT License](LICENSE).
