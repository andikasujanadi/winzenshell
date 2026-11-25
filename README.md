# WinZenShell

<div align="center">

[![Platform: Windows 11](https://img.shields.io/badge/Platform-Windows%2011%20(64--bit)-0078D4?logo=windows11&logoColor=white)](https://microsoft.com/windows)
[![Runtime: .NET 10](https://img.shields.io/badge/Runtime-.NET%2010%20(WPF)-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Frontend: WebView2](https://img.shields.io/badge/Frontend-WebView2%20%7C%20Web%20Components-008080?logo=microsoftedge&logoColor=white)](https://developer.microsoft.com/microsoft-edge/webview2/)
[![Release: v0.5.2](https://img.shields.io/badge/Release-v0.5.2-0078D4)](README.md#changelog)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

**WinZenShell** is a customizable, lightweight Windows 11 taskbar and widget studio.  
It pairs a modern web interface (HTML, CSS, and JavaScript) with a fast native Windows 11 host.

</div>

---

> [!IMPORTANT]
> **Release Notice (`v0.5.2`)**:  
> WinZenShell v0.5.2 fixes missing Store/UWP/PWA apps (such as **Camera**, **Clock**, **Photos**, **Paint**) in the App Launcher, eliminates the 1 px DPI gap above the taskbar in Full-Screen Start, and adds a flash-free staggered tile open animation.

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

### Windows 11 Light & Dark Themes & Icon Packs
* Matches the native **Windows 11 Dark** and **Windows 11 Light** appearances across the taskbar, flyout menus, widgets, and Settings window.
* **Custom Accent Colors & Transparency**: Choose your favorite highlight color and adjust taskbar height, floating margin, corner roundness, opacity, and background blur (including **Live Wallpaper** blur support for Lively Wallpaper / Wallpaper Engine).
* **GNOME-Look & Custom Icon Packs**: Import icon themes (`.zip`, `.tar.xz`, `.tar.gz`, `.tgz`, `.tar`, or extracted folders) from the dedicated **Icon Packs** page with automatic Linux symlink resolution, Windows-to-FreeDesktop app mapping, and automatic fallback to native Windows icons.
* **Flexible Icon Geometry**: Fine-tune **Icon Size** (`12px–56px`), **Icon Padding** (`0px–18px`), **App Launcher Icon** style, and **App Launcher Icon Scale** (`40%–100%`).

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
    * Opens a titleless Android 17-style 2×2 circular power flyout with Sleep, Lock, Power off, and Restart actions.
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

### v0.5.2 (2026-11-25)
* **Complete App Discovery (`shell:AppsFolder`)**:
  * Replaced broken `IEnumShellItems` COM marshaling in `UwpAppIconHelper` with `Shell.Application` enumeration (`10s` STA timeout), restoring missing UWP/Store apps (**Camera**, **Clock**, **Photos**, **Paint**, **Camo Studio**), Chrome/Edge PWAs, and Win32 `.exe` entries in `shell:AppsFolder`.
* **Full-Screen Start Staggered Open Animation & Pixel Alignment**:
  * Added a GPU-composited one-by-one staggered tile entrance animation (`win81TileOpenIn` + `win81-preopen` state) for Full-Screen Metro Start with zero pre-animation content flash.
  * Fixed the 1 px DPI rounding gap / white line between Full-Screen Start and the taskbar.

### v0.5.1 (2026-11-24)
* **Metro & ChromeOS App Launcher Styles**:
  * Renamed launcher variants to **ChromeOS** and **Metro** (Windows 8.1-style grid), featuring a *Start* / *Apps* header, custom profile name & picture, top-right search bar in the Apps view, type-to-search from Start, bold arrow toggle, and smooth view transitions.
  * Full-Screen Start now preserves the taskbar work area, aligns Recent and Pinned sections, and refines wide (`2×1`) tile padding and folder icon sizing.
* **High-Resolution 128×128 App Icons**:
  * Upgraded `AppLauncherService` icon extraction to 128×128 (`PrivateExtractIcons` and 128 px UWP extraction) and live-resolved crisp icons for existing pinned tiles and folders.
* **Layout Presets & Backdrop Fixes**:
  * Included full `AppLauncher` configuration and `OverrideWindowsKey` inside **Layout Presets**.
  * Fixed the App Launcher translucent/acrylic backdrop turning opaque black when resizing pinned tiles or folders.

### v0.5.0 (2026-11-24)
* **App Launcher Folders & Drag-and-Drop Management**:
  * Pinned apps can now be grouped into folders by dragging one app onto another with an intuitive 500ms hover delay that outlines the target folder border before arming, and only creates the folder upon mouse release.
  * Opened folder modal allows renaming folders, rearranging apps inside with fluid FLIP reordering, dragging apps outside the dialog container to remove them from the folder, and manual or automatic ungrouping (when 1 app remains).
  * Exiting the App Launcher automatically dismisses any opened folder.
  * Added pointer capture to ensure smooth, uninterrupted drag tracking across mouse speed and child elements, while keeping standard pointer cursors on hover.
* **Context Menu Refinements**:
  * In the **Pinned Apps** row and inside folders: removed "Hide app", changed unpin action to **"Remove"** positioned at the bottom in red (`#FF99A4`, danger action).
  * In the **All Apps** and **Recent Apps** sections: retained **"Hide"** at the bottom in red, with clear "Pin to launcher" or "Unpin from launcher" options.
  * Styled all **"Unpin"** actions (**Unpin from taskbar**, **Unpin from launcher**) in danger red across App Launcher, Taskbar, and Tray menus while keeping "Pin" actions standard.
  * Automatically dismisses open context menus when scrolling inside the App Launcher or mouse-wheeling outside the menu.

### v0.4.1 (2026-11-24)
* **Steam Game & `.url` Game Launcher Discovery (`AppLauncherService`)**:
  * Automatically discovers installed Steam games across all Steam libraries (`libraryfolders.vdf` + `appmanifest_*.acf`) even without Desktop/Start Menu shortcuts, while filtering out non-game utilities (*Steamworks Common Redistributables*, *SteamVR*, *Proton*, etc.).
  * Resolves high-resolution game icons from the Windows Registry (`Uninstall\Steam App <AppId>` `DisplayIcon`), `.url` `IconFile`, Steam `appcache\librarycache` + `steam\games\*.ico`, or the game's primary executable, and watches `appmanifest_*.acf` + `*.url` files for live install/uninstall updates.
* **Android 17-Style Power Flyout (`PowerFlyoutWindow`)**:
  * Redesigned the Power flyout into a titleless `26px`-rounded 2×2 grid of circular action buttons (**Sleep**, **Lock**, coral-red **Power off**, and **Restart**) with centered labels underneath.
* **Persistent Pin Lyrics Across Track Changes**:
  * Fixed **Pin Lyrics** turning off when the song changed or during instrumental intros, and ensured `metrics_update` ticks continue streaming to the Dynamic Island lyrics engine while collapsed so the pill automatically displays synced lyrics when the next track plays.
* **Settings UI Refinements**:
  * Added a 3-dots (`...`) menu button next to the **WinZenShell** header in the sidebar containing **Widgets Folder**, **Refresh**, **Restart**, and **Quit WinZenShell**.
  * Aligned **Accent Color** swatches and all **Shell** sliders (**Background Opacity**, **Background Blur**, **Flyout Opacity**, **Flyout Blur**, **Height**, **Floating Margin**, **Corner Radius**, **Icon Size**, **Icon Padding**, and **App Launcher Icon Scale**) into a clean two-column layout (title on the left, control on the right) with concise **Reset** buttons.

### v0.4.0 (2026-11-21)
* **GNOME-Look & Custom Icon Packs (`IconPackService`)**:
  * Added a dedicated **Icon Packs** page in Settings with an **Open GNOME-Look.org** shortcut, **Import Archive** (`.zip`, `.tar.xz`, `.tar.gz`, `.tgz`, `.tar`) and **Import Folder** actions, a live progress indicator, and an alphabetically sorted card grid for applying or deleting packs.
  * Automatically extracts `apps/` icons (`scalable` SVGs prioritized over `64x64` / `48x48` PNGs), resolves Linux tar symlink entries into real files on Windows, and maps Windows process names, `.lnk` target executables, and versioned app names (e.g. `Blender 5.2`, `Git for Windows`, `File Explorer`, `Microsoft Store`) to FreeDesktop icon names.
  * Instant in-memory icon pack switching across running taskbar apps, closed pinned apps, and the App Launcher with automatic fallback to native Windows icons.
* **Taskbar Icon Padding, Extended Size & Launcher Icon Scale**:
  * Increased maximum **Icon Size** to `56px` (`12px–56px`).
  * Added **Icon Padding** (`0px–18px`, default `9px`) to tighten or widen button padding around taskbar app icons.
  * Added **App Launcher Icon** style/color selectors and **App Launcher Icon Scale** (`40%–100%`) directly under **Settings → Shell → Taskbar Apps**.
* **Live Wallpaper Blur Support**:
  * Updated **Realtime GPU** taskbar blur (`RealtimeBlurService`) and widget flyout blur (`FlyoutBackdropHelper`) to detect and composite live wallpaper windows (such as **Lively Wallpaper**) hosted under `WorkerW` / `Progman`, throttled to 2 FPS on the taskbar for minimal GPU usage.
* **UWP / Microsoft Store Discovery & App Launcher Auto-Refresh**:
  * Added native UWP / Microsoft Store icon extraction via `GetApplicationUserModelId` and `IShellItemImageFactory` (`UwpAppIconHelper`) for both the App Switcher and App Launcher.
  * Added `shell:AppsFolder` packaged app scanning so Store/MSIX apps (e.g. **Lively Wallpaper**, **Microsoft Store**) appear and launch in the App Launcher.
  * Added debounced `FileSystemWatcher`s on Start Menu and Desktop shortcut directories plus on-open stale refresh so newly installed apps appear automatically without restarting the shell.

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
