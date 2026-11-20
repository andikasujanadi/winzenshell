# WinZenShell

<div align="center">

[![Platform: Windows 11](https://img.shields.io/badge/Platform-Windows%2011%20(64--bit)-0078D4?logo=windows11&logoColor=white)](https://microsoft.com/windows)
[![Runtime: .NET 10](https://img.shields.io/badge/Runtime-.NET%2010%20(WPF)-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Frontend: WebView2](https://img.shields.io/badge/Frontend-WebView2%20%7C%20Web%20Components-008080?logo=microsoftedge&logoColor=white)](https://developer.microsoft.com/microsoft-edge/webview2/)
[![Release: v0.3.4](https://img.shields.io/badge/Release-v0.3.4-0078D4)](README.md#changelog)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

**WinZenShell** is a customizable, lightweight Windows 11 taskbar and widget studio.  
It pairs a modern web interface (HTML, CSS, and JavaScript) with a fast native Windows 11 host.

</div>

---

> [!IMPORTANT]
> **Release Notice (`v0.3.4`)**:  
> WinZenShell v0.3.4 introduces **Separated Taskbar Style** with per-island blur clipping, **Gnome 3×3** launcher icon and **Highlight Active App Icon** toggle, **concentric corner radius & dynamic padding**, cached **flyout blur** with smooth cross-fade, persistent **Pin Lyrics** and **Wide Pill** settings, and a cleaner **Settings** layout with card-based **Layout Presets**.

---

## Screenshots
<img width="1919" height="1079" alt="Screenshot 2026-11-17 112731" src="https://github.com/user-attachments/assets/86f51950-454a-4876-bbc0-9ab2c88b49ad" />
<img width="1919" height="1079" alt="Screenshot 2026-11-17 112805" src="https://github.com/user-attachments/assets/804c763a-086b-4970-9d89-8142aeb8ab18" />
<img width="1919" height="1079" alt="Screenshot 2026-11-17 112816" src="https://github.com/user-attachments/assets/8b6699ef-0b6f-40b1-8c8c-d024a59e7b42" />
<img width="1919" height="1079" alt="Screenshot 2026-11-17 112822" src="https://github.com/user-attachments/assets/9291dcee-b60a-43c8-a659-506d0836f35f" />

## Key Features

### Windows 11 Desktop Integration
* **Clean Taskbar Replacement**: Hides the default Windows 11 taskbar cleanly without leaving empty gaps at the bottom of your screen, and remembers your preference across app restarts.
* **Auto Start on Logon**: Optionally start WinZenShell quietly in the background whenever you sign in to Windows.
* **Windows Key Override**: Optionally open the WinZen App Launcher with a single tap of the Windows key while preserving all standard `Win + key` shortcuts.
* **Smart Screen Space Management**: Automatically adjusts your desktop work area so maximized windows fit neatly against the WinZenShell taskbar (or top-center Dynamic Island) without overlapping.
* **Automatic Fullscreen Hiding**: Hides the taskbar automatically when you play fullscreen games, watch fullscreen videos, or present slides, and brings it right back when you exit.
* **Emergency Restore Shortcut**: Press **`Ctrl + Shift + Alt + Z`** at any time to immediately bring back the standard Windows 11 taskbar.

### Dynamic Island & Music Island
* **Clock & Battery Island**: A sleek pitch-black pill displaying the current time (with optional seconds and date). Click it to expand a larger card showing the time, battery percentage, day of the week, and full date.
* **Music Dynamic Island**: Appears automatically next to the clock pill whenever music or media is playing.
  * **Standby**: Compact pill with album art and a live animated audio waveform.
  * **Hover**: Hold your cursor over the music pill to smoothly reveal the current song title.
  * **Expanded Media Card**: Click the music pill to expand a full media player with large album cover art, song title, artist name, interactive progress bar, and Previous / Play-Pause / Next controls.
* **Natural Grow, Shrink & Merge Motion**: Clicking either pill expands the card directly from that pill's position toward the center while the other pill smoothly merges behind it. Closing the card shrinks it back to its spot while the secondary pill unmerges into standby position.
* **Flexible Placement**: Dock the Dynamic Island inside the taskbar (left, center, or right) or float it at the top-center of your desktop.

### Drag-and-Drop Taskbar Layout & Presets
* **Layout Presets**: Save, apply, rename, update, and delete complete layout presets that store your taskbar regions, Control Center grid, Shell appearance/geometry, and Dynamic Island placement.
* **Four Layout Zones**: Arrange items across **Left**, **Center**, **Right**, and **Unused** sections.
* **Live Visual Editor**: Drag and drop widgets in Settings to reorder your taskbar in real time.
* **Unused Drawer**: Temporarily disable widgets you do not need without deleting them.

### Windows 11 Light & Dark Themes
* Matches the native **Windows 11 Dark** and **Windows 11 Light** appearances across the taskbar, flyout menus, widgets, and Settings window.
* **Custom Accent Colors & Transparency**: Choose your favorite highlight color and adjust taskbar height, floating margin, corner roundness, opacity, and background blur.

### Built-In Widgets & Quick Menus
1. **App Launcher (`zen-start-button`)**:
   * Search and launch any installed app with instant filtering, a recent apps row, and full keyboard navigation (<kbd>Up</kbd> <kbd>Down</kbd> <kbd>Enter</kbd> <kbd>Esc</kbd>).
2. **Open Apps & Window Switcher (`zen-active-windows`)**:
   * Shows your running and pinned apps with live window thumbnails on hover, multi-window indicators, pin/unpin options, and clean active-window highlights.
3. **Virtual Desktop Switcher (`zen-virtual-desktops`)**:
   * View and switch between your Windows 11 virtual desktops with a single click.
4. **Clock & Calendar (`zen-clock-pill`)**:
   * Displays the time and date on the taskbar and opens a flyout with an analog clock, digital time, and calendar date.
5. **System Monitor (`zen-system-monitor`)**:
   * Shows live CPU, memory (RAM), and GPU usage with a quick button to open Windows Task Manager.
6. **Volume Control (`zen-volume`)**:
   * Adjust system volume with a slider or by scrolling your mouse wheel over the icon, plus one-click mute.
7. **Display Brightness (`zen-brightness`)**:
   * Adjust screen brightness using the slider, mouse wheel, or your keyboard brightness keys.
8. **Control Center (`zen-control-center`)**:
   * A customizable quick-settings panel where you can add, remove, reorder, and resize widgets across a 4-column grid.
9. **Wi-Fi & Network (`zen-network`)**:
   * View available Wi-Fi networks, signal strength, and connection status.
10. **Bluetooth & Devices (`zen-bluetooth`)**:
    * Turn Bluetooth on or off, view paired headphones, phones, and accessories, and connect or disconnect with one click.
11. **Battery Status (`zen-battery`)**:
    * Shows battery percentage, charging status, and estimated remaining time.
12. **Media Player Widget (`zen-media-player`)**:
    * Controls your currently playing music or video from any app (Spotify, browser, media players) with album art, track scrubbing, and multiple card sizes in Control Center.
13. **Power Menu (`zen-power`)**:
    * Opens a compact power flyout with Sleep, Lock, Restart, and Shutdown actions.
14. **Lock Screen (`zen-lock-button`)**:
    * One-click button to lock your Windows workstation (`Win + L`).

---

## Architecture Overview

```
WinZenShell (Host)
  ├── WorkAreaAppBarService    -> Desktop work area & taskbar bounds
  ├── SystemMetricsService     -> CPU, RAM, Battery, GPU & open windows
  ├── MediaService             -> System media playback & timeline controls
  ├── AudioService             -> System master volume & mute
  ├── BrightnessService        -> Display brightness & hardware key sync
  ├── NetworkService           -> Wi-Fi scanning & network status
  ├── BluetoothService         -> Bluetooth radio & paired device controls
  ├── VirtualDesktopService    -> Windows 11 virtual desktop switching
  ├── AppLauncherService       -> Start Menu app search & icons
  └── WidgetManagerService     -> Widget discovery & live reload
        │
        ▼ (Edge WebView2)
Taskbar, Dynamic Island & Flyouts (Web Frontend)
  ├── universal-theme.css      -> Light & Dark theme tokens
  ├── Shadow DOM Isolation     -> Scoped widget styles
  └── window.winzen Bridge     -> Two-way host communication
```

---

## Getting Started

### Requirements
* **Windows 11** (64-bit)
* **[.NET 10 SDK](https://dotnet.microsoft.com/download)**
* **Microsoft Edge WebView2 Runtime** (included with Windows 11)

### Build & Run

```powershell
# Clone the repository
git clone https://github.com/your-username/winzenshell.git
cd winzenshell

# Run in development mode
dotnet run --project src\WinZenShell\WinZenShell.csproj

# Or publish a standalone release + single-file portable package
powershell -ExecutionPolicy Bypass -File .\build-portable.ps1
Start-Process "publish\WinZenShell\WinZenShell.exe"
```

---

## Creating Custom Widgets

You can create your own widget by adding a folder inside `Widgets/`:

```
Widgets/
└── my-custom-widget/
    ├── manifest.json
    ├── index.html
    ├── script.js
    └── style.css
```

### Example `manifest.json`
```json
{
  "id": "community.my-custom-widget",
  "name": "My Custom Widget",
  "version": "1.0.0",
  "author": "Your Name",
  "description": "A simple custom taskbar widget",
  "defaultSlot": "right",
  "width": 120,
  "height": 40
}
```

### Styling & System Data
Widgets automatically receive theme colors and live system data through `window.winzen`:

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
/* script.js */
window.winzen.shell.onMetricsUpdate((metrics) => {
  console.log('CPU:', metrics.cpuPercent, 'RAM:', metrics.ramPercent);
});
```

---

## Keyboard Shortcuts

| Shortcut | Action |
| :--- | :--- |
| **`Ctrl + Shift + Alt + Z`** | **Emergency Restore**: Immediately unhide and restore the default Windows taskbar |
| **`Esc`** | Close the open flyout menu, collapse Dynamic Island, or clear App Launcher search |
| **`Up` / `Down` / `Enter`** | Navigate and launch apps in the App Launcher |

---

## Changelog

### v0.3.4 (2026-11-20)
* **Separated Taskbar Style & Adaptive Geometry**:
  * Added **Separated** taskbar style alongside **Unified**, splitting Left, Center, and Right regions into independent floating islands with accurate per-island blur clipping and auto-hiding empty right-region controls.
  * Added dynamic horizontal padding and concentric inner button radius synced with the taskbar corner radius, plus a **Highlight Active App Icon** toggle and a **Gnome** 3×3 dots launcher icon option.
* **Flyout Blur & Backdrop Reliability**:
  * Cached decoded wallpaper backdrops and added asynchronous live-window capture with smooth cross-fade while excluding the flyout itself from capture.
  * Fixed missing blur on first open for auto-sized flyouts (Bluetooth, Power, etc.) and transparent WebView2 background initialization.
* **Dynamic Island Persistence**:
  * Added **Pin Lyrics** and **Wide Pill** toggles to **Settings → Dynamic Island** with full persistence across restarts and two-way sync with the Music Island card, and fixed Wide Pill width on startup.
* **Settings & Layout Presets Redesign**:
  * Reorganized **Shell** and **Dynamic Island** settings into cleaner, focused groups.
  * Updated **Layout** active widget cards with a quick `×` button to move widgets to Inactive, and redesigned **Layout Presets** into an alphabetically sorted card grid with compact icon actions.

### v0.3.3 (2026-11-18)
* **Music Dynamic Island - Live Lyrics & Wide Pill**:
  * **Synchronized Live Lyrics**: Toggle **pin lyrics** on the expanded media card to view scrolling LRC-synced lyrics smoothly positioned under playback controls, with auto-fetch and smart fuzzy matching for YouTube/streaming track titles.
  * **Adaptive Lyrics Pill**: When lyrics are pinned, the small resting music pill replaces the track title with the live lyric line, smoothly animating its width to fit the exact text.
  * **Wide Pill Mode**: Toggle between standard compact pill and wide pill mode to keep the track title permanently visible on the taskbar.
  * **Automatic Flushing**: Automatically clears pinned lyrics and reverts pill width when playback stops or skips to the next track.
* **App Launcher Type-to-Search**:
  * Typing anywhere when the App Launcher opens automatically focuses and writes directly into the search bar.
  * Enhanced keyboard navigation and focus management across WebView2 flyouts.

### v0.3.2 (2026-11-17)
* **Layout Presets**:
  * Save, apply, rename, update, and delete layout presets directly from the **Layouts** tab.
  * Presets store taskbar and Control Center widget arrangements along with Shell settings (position, height, corner radius, floating margin, opacity, blur, app labels, window combining, icon size, and native taskbar hiding) and Dynamic Island placement.
* **Shell & Startup Improvements**:
  * **Remember Hide Windows Taskbar**: Your preference to hide the default Windows 11 taskbar is now saved across app restarts and reinforced so system overlays such as Snipping Tool (`Win + Shift + S`) do not cause the native taskbar to reappear.
  * **Auto Start on Logon**: Added a toggle in Shell settings to launch WinZenShell automatically in the background when signing in to Windows.
  * **Override Windows Key**: Added a toggle in Shell settings to open the WinZen App Launcher when pressing the Windows key while keeping `Win + key` shortcuts working normally.
  * **Global Date Format**: Added a customizable date template in Shell settings (`YYYY`, `YY`, `MMMM`, `MMM`, `MM`, `M`, `dddd`, `ddd`, `DD`, `D`) shared by Dynamic Island and Clock widgets.
* **Power & Lock Widgets**:
  * Added a **Power** widget with a flyout menu for Sleep, Lock, Restart, and Shutdown.
  * Added a **Lock Screen** widget (`Win + L` equivalent).
* **App Launcher & Dynamic Island Refinements**:
  * Resized the App Launcher to `728 × 800` with a 6-column icon grid, cleaner borderless search bar and icon tiles, restored built-in app icons, and filtered out administrative and SDK utility clutter.
  * Added smooth appear and disappear transitions for the Music Island pill and synced the media seeker timeline with active audio and video playback.

### v0.3.1 (2026-11-16)
* **Music Dynamic Island**:
  * Added a companion music pill next to the clock island whenever media is playing, complete with album art and an animated 5-bar audio waveform.
  * Added a brief hover delay before expanding the music pill to show the song title, avoiding accidental triggers when moving the mouse across the screen.
  * Clicking the music pill opens a full media card with large album art, song title, artist name, seekable progress bar, and playback controls (Previous, Play/Pause, Next).
  * Added a toggle in Settings under Dynamic Island to turn the Music Island on or off.
* **Refined Dynamic Island Animations & Layout**:
  * **Grow & Shrink from Pill Position**: Clicking either the clock pill or the music pill now animates the card directly from that pill's starting position into the centered expanded card while the other pill smoothly merges behind it. Closing the card shrinks it back to its original spot while the secondary pill unmerges.
  * Eliminated visual flickering when collapsing the Dynamic Island back into the taskbar or top-center bar.
  * Matched the Dynamic Island proportions, typography, battery indicator, and compact top-center padding 1:1 with the reference design.
  * Added an **Island Placement** selector directly on the **Shell** settings page in addition to the Dynamic Island page for quicker access.
  * Prevented the expanded Dynamic Island card from being clipped when switching between Top Center and Inside Taskbar modes.
* **Taskbar Window Indicators**:
  * Cleaned up multi-window app icons on the taskbar so the card highlight and side tab appear only when the app is active or hovered, keeping inactive apps transparent on translucent taskbars.

### v0.3.0 (2026-11-15)
* **Dynamic Island System HUD**:
  * Added the pitch-black Dynamic Island capsule with smooth expand and collapse animations.
  * Supports placement inside the taskbar (left, center, or right) or floating at the top-center of the desktop with optional screen-space reservation.
  * Expanded card view shows large digital time, battery level, day of the week, and full date.
* **Instant Volume & Brightness Response**:
  * Volume and brightness sliders now respond immediately as you drag them or use keyboard brightness keys.

### v0.2.2 (2026-11-14)
* **Media Player Widget**:
  * Added the built-in Media Player widget showing live song title, artist, album art, playback progress, and Play/Pause/Next/Previous controls.
  * Supports six card sizes in Control Center (`1x1`, `2x1`, `3x1`, `4x1`, `1x2`, `2x2`) plus a compact taskbar pill with optional album art.
* **Control Center Improvements**:
  * Replaced the small add-widget popup with a full-page Add Widget drawer.
  * Allowed widgets to be placed freely across empty grid spots without shifting neighboring cards unexpectedly.

### v0.2.1 (2026-11-13)
* **Customizable Control Center Grid**:
  * Added drag-to-resize handles on Control Center cards and empty slot placeholders for flexible 4-column layouts.
  * Exiting the Control Center automatically finishes Edit Mode so it reopens cleanly next time.
* **New Bluetooth Menu & Widget Polish**:
  * Added a native Bluetooth flyout menu with a main On/Off switch, paired device list with device-type icons, one-click Connect/Disconnect buttons, and quick links to add or manage devices.
  * Improved responsive layouts for Clock, Battery, System Monitor, Volume, and Brightness cards across all Control Center sizes.

### v0.2.0 (2026-11-13)
* **Control Center & Settings Redesign**:
  * Introduced the 4-column Control Center grid with Edit Mode for adding, removing, and rearranging quick-settings cards.
  * Redesigned the Settings sidebar with quick actions to open the Widgets folder, refresh widgets, restart the shell, or quit.
  * Added live GPU usage monitoring alongside CPU and RAM in the System Monitor widget.

### v0.1.0-alpha
* **Initial Alpha Release**:
  * Custom Windows 11 taskbar powered by .NET 10 and WebView2.
  * Automatic hiding of the default Windows taskbar and desktop work area adjustment.
  * Four-zone drag-and-drop widget layout, App Launcher, Active Window Switcher, Virtual Desktop Switcher, Light/Dark themes, and the `Ctrl + Shift + Alt + Z` emergency restore shortcut.

---

## License

This project is licensed under the [MIT License](LICENSE).
