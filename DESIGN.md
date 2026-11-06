# DESIGN.md — WinZenShell Design System

## 1. Domain & Aesthetic Direction
- **Domain**: Native Windows 11 Desktop Shell & Taskbar Utility
- **Aesthetic Persona**: Authentic Windows 11 Dark Theme (WinUI 3 / Fluent Design System)
- **Brand Inspirations**: Windows 11 System Settings (`ms-settings:`), WinUI 3 Gallery, PowerToys Settings

## 2. Color Palette (Windows 11 Dark Theme Tokens)
- **Settings Page Background (`SolidBackgroundFillColorBase`)**: `#202020`
- **Taskbar Base Surface (`SolidBackgroundFillColorSecondary`)**: `#1C1C1C`
- **Card / Expander Background (`CardBackgroundFillColorDefault`)**: `#2B2B2B`
- **Card Secondary / Expanded Tray**: `#272727`
- **Control Fill (`ControlFillColorDefault`)**: `#2D2D2D`
- **Control Hover (`ControlFillColorSecondary`)**: `#323232`
- **Subtle Hover (`SubtleFillColorSecondary`)**: `rgba(255, 255, 255, 0.0605)`
- **Subtle Active (`SubtleFillColorTertiary`)**: `rgba(255, 255, 255, 0.0419)`
- **Card Stroke (`CardStrokeColorDefault`)**: `rgba(255, 255, 255, 0.08)` (`#303030`)
- **Control Stroke (`ControlStrokeColorDefault`)**: `rgba(255, 255, 255, 0.09)` (`#3A3A3A`)
- **Text Primary (`TextFillColorPrimary`)**: `#FFFFFF`
- **Text Secondary (`TextFillColorSecondary`)**: `#C5C5C5`
- **Text Tertiary (`TextFillColorTertiary`)**: `#8A8A8A`
- **Accent Primary (`SystemAccentColorLight2`)**: `#60CDFF`
- **Accent Hover (`SystemAccentColorLight1`)**: `#4CC2FF`
- **Semantic Status**:
  - Success: `#6CCB5F`
  - Warning: `#FCE100`
  - Danger: `#FF99A4`

## 3. Typography
- **UI Body & Headings (`--font-sans`)**: `'Segoe UI Variable Text'`, `'Segoe UI Variable Display'`, `'Segoe UI'`, system-ui, sans-serif
- **Monospace & Telemetry (`--font-mono`)**: `'Cascadia Code'`, `'Consolas'`, monospace (`font-variant-numeric: tabular-nums`)
- **Scale**:
  - Page Header: `28px`, weight `600`, letter-spacing `-0.015em`
  - Section Header: `14px`, weight `600`, color `#FFFFFF`
  - Card Title: `14px`, weight `400`, color `#FFFFFF`
  - Card Description: `12px`, weight `400`, color `#C5C5C5`
  - Taskbar Item Text: `12px`, weight `400`, line-height `1`

## 4. Layout, Geometry & Elevation
- **Settings Layout**: Authentic Windows 11 single-column `SettingsExpander` stack (`max-width: 1000px`, `padding: 28px 36px 48px`)
- **Settings Card Radius**: `4px` (`ControlCornerRadius` / `OverlayCornerRadius` in WinUI 3) with `1px solid #303030` border
- **Controls (`ToggleSwitch`)**: `40px × 20px` pill track (`#60CDFF` when checked with `#000000` thumb; `#232323` with `#8A8A8A` border when unchecked)
- **Taskbar Height**: `48px` total (`38px` interactive item height, `4px` border radius, `16px × 3px` `#60CDFF` bottom active indicator pill)
- **Widget Dynamic Sizing**: Widgets default to `autoWidth: true` (`width: max-content`), expanding and contracting automatically to fit their child elements.
