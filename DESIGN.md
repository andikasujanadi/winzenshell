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

## 5. Control Center Grid & Modular Widget System
- **Grid Architecture**: 4-column squircle grid (`grid-template-columns: repeat(4, 1fr)`), 76px row unit height, 10px gap, 16px padding.
- **Card Corner Radius**: `16px` squircle curves with subtle inner border (`1px solid rgba(255, 255, 255, 0.08)` / `#e5e7eb` in Light theme).
- **Supported Widget Aspect Ratios / Spans**:
  - `1x1`: Squircle square (76px × 76px nominal), compact icon/telemetry tile.
  - `2x1`: Half-width horizontal banner (2 columns × 1 row).
  - `2x2`: Double square tile (2 columns × 2 rows).
  - `1x2`: Vertical tall card (1 column × 2 rows), optimized for vertical sliders.
  - `3x1`: Wide banner (3 columns × 1 row).
  - `4x1`: Full-width banner (4 columns × 1 row).
- **Empty Placeholders**: Soft gray squircle placeholders (`rgba(255, 255, 255, 0.04)` in Dark mode / `rgba(0, 0, 0, 0.06)` in Light mode) preserving spatial layout and user placement.
- **Edit Mode Affordances**:
  - Delete badge: `20px` circular red badge (`#ef4444`) top-right with `⊖` glyph.
  - Resize handle: `18px` circular glass badge (`rgba(255, 255, 255, 0.16)` / `rgba(0, 0, 0, 0.16)`) bottom-right with `⤢` diagonal resize glyph.
  - Action Bar: Bottom centered pill action buttons (**"Edit Controls"**, **"Done"**, and **"+ Add Widget"**).
- **Widget-Specific Layout Rules**:
  - `zen-battery`:
    - `1x1`: Vertical center stack with battery icon (`scale(1.25)`), bold 15px percentage, and centered 46px × 4px mini progress bar.
    - `2x1`, `3x1`, `4x1`: 2-row CSS grid with icon and percentage top-left, status text top-right, and 100% full-width progress bar bottom row.
  - `zen-clock-pill`: Bold time header with localized date subtitle across all spans.
  - `zen-system-monitor`: Multi-row or multi-column layout showing live CPU, RAM, and GPU meters without overflow.
  - `zen-volume` & `zen-brightness`: Vertical slider layout in `1x2` with high values at the top and anchored bottom icon.
  - `zen-bluetooth` & `zen-network`:
    - `1x1`: Centered circular or rounded square icon wrap with state fill / accent indicator.
    - `2x1`: Horizontal 2-column card with left icon wrap (34px × 34px) and right text column (bold 13px label and 11px muted sublabel).
    - `1x2`: Vertical centered capsule with 44px circular icon badge, bold 12px label, and 10px truncated status sublabel.
    - `3x1`, `4x1`, `2x2`: Expanded cards showing connected device details, signal/status badge, and quick action affordances.
