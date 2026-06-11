# PiPlay Theme Review and Color Variant Definition

Source reviewed: `PiPlay-review-bundle-2026-06-11-v2-for-chatgpt.zip`  
Scope: current theme implementation, Settings appearance flow, color variants, rounding/theme definitions  
Status: implementation guide; no code changes applied in this review

> Note: I could not run `dotnet test` in this container because the .NET SDK is not installed here. This review is based on static inspection of the supplied bundle.

---

## 1. High-level review

The current bundle already has the first useful pieces of the theme system:

- `AppSettings.Theme` exists.
- `ThemeCatalog` exists with `sharp-dark`, `minimal`, and `soft-glass`.
- Settings is now scrollable and split into Privacy, Appearance, Playback, and Advanced.
- The separate old Pin/Fade color settings have effectively been replaced in the UI by one shared accent.
- `ThemeResourceApplier` applies `AccentPrimary` and `AccentPrimaryLight` through replaced resource entries.
- Tests already cover theme catalog uniqueness, accent normalization, accent contrast, and Settings/catalog drift.

That is a good foundation.

The important catch is that the current presets are not yet real visual themes. They mostly behave as **theme labels plus accent defaults**, not full theme variants. Rounding, surface colors, border strength, button shape, and most control radii are still fixed in XAML.

Recommended next pass:

```text
Keep current behavior stable → expand ThemeCatalog into visual tokens → migrate XAML to tokens → then add color wheel.
```

---

## 2. Main findings to address

### 2.1 Theme presets currently do not apply most theme defaults

`ThemeCatalog.ThemePreset` includes default accent, fade delay, strip auto-hide, and opacity defaults, but `SettingsWindow.ThemePreset_Click` currently only updates:

```csharp
ThemeId
AccentColor
```

It does **not** apply the preset opacity, fade delay, or strip auto-hide defaults.

That means selecting `Soft Glass` does not really turn the UI into the intended softer overlay preset unless the user also manually changes opacity/top-bar/fade controls.

Decision needed:

```text
When a user selects a theme preset, should PiPlay apply that theme's behavior defaults?
```

Recommendation:

- Yes, for a first explicit theme selection.
- Manual Advanced changes should become overrides.
- Do not silently reapply theme defaults every time if the user has custom overrides.

A clean model:

```csharp
public sealed class ThemeSettings
{
    public string ThemeId { get; set; } = "sharp-dark";
    public string AccentColor { get; set; } = "#00D4FF";

    public string? FadeDelayOverride { get; set; }
    public bool? StripAutoHideOverride { get; set; }
    public double? ActiveOpacityOverride { get; set; }
    public double? IdleOpacityOverride { get; set; }
}
```

Then effective values are:

```text
preset default → user override → normalized safe value
```

---

### 2.2 Theme switching resets the accent

Currently, selecting a theme sets the accent to the theme default. That is simple and understandable, but it can become annoying once users have a custom accent from a wheel.

Better behavior after the color wheel lands:

```text
If current accent == previous theme default:
    switching theme adopts new theme default accent
else:
    preserve custom accent
```

Optional UI later:

```text
[Use theme accent]
```

For now, preset resets are acceptable, but keep this in mind before the wheel lands.

---

### 2.3 Sharp muted accent cannot be the only accent fill without contrast logic

Earlier visual direction suggested a muted PiPlay/steel-blue accent such as:

```text
#2D6F8F
#244E66
#3B6173
```

Those look good as borders, hover glows, or subtle state colors, but they are too dark to use as a filled primary button with the current dark button text `#06141A`.

Use a brighter but still restrained steel accent for the actual primary fill:

```text
Recommended muted primary: #4A8FAB
```

Keep the darker colors as derived tokens:

```text
AccentMuted   = #2D6F8F
AccentSubtle  = #13242D / #102832
AccentBorder  = #3F86A2
```

This gives the sharp look without breaking readability.

---

### 2.4 Current live theme support is accent-only

`ThemeResourceApplier` currently replaces:

```text
AccentPrimary
AccentPrimaryLight
```

Most surface, border, text, and radius values are still static resources or hardcoded values. That is fine for the current first pass, but a real theme switch needs more tokens.

If theme changes should apply live, migrate consumers to `DynamicResource` for theme-controlled values:

```text
ThemeAppBackground
ThemeSurfaceBase
ThemeSurfaceRaised
ThemeSurfaceHover
ThemeBorderSubtle
ThemeBorderStrong
ThemeTextPrimary
ThemeTextSecondary
ThemeControlRadius
ThemeButtonRadius
ThemePanelRadius
ThemeInputRadius
```

If full live switching is deferred, change the Settings copy so it does not promise more than accent live-switching.

---

### 2.5 Rounding is only partially tokenized and should become first-class theme data

Current tokens:

```xml
<CornerRadius x:Key="ControlCornerRadius">8</CornerRadius>
<CornerRadius x:Key="ButtonCornerRadius">10</CornerRadius>
```

But many controls still hardcode radii:

```text
Settings swatches      8
Settings presets       8
DangerButton           10
CloseIconButton        8
PinToggle              8
DarkTextBox            8
ScrollBar thumb        5
ComboBoxItem           6
ComboBox popup         8
ToolTip                6
```

Recommendation: promote rounding to the same level as palette and opacity. PiPlay should not have one universal radius. It should have semantic radius tokens, because a popout surface, a command button, a thumbnail, a tooltip, and a scrollbar thumb do not want the same shape.

Minimum useful token set:

```xml
<!-- Window / surface structure -->
<CornerRadius x:Key="RadiusMainWindowFrame">4</CornerRadius>
<CornerRadius x:Key="RadiusPopoutFrame">4</CornerRadius>
<CornerRadius x:Key="RadiusTitleBar">4,4,0,0</CornerRadius>

<!-- Controls -->
<CornerRadius x:Key="RadiusButton">5</CornerRadius>
<CornerRadius x:Key="RadiusIconButton">5</CornerRadius>
<CornerRadius x:Key="RadiusInput">5</CornerRadius>
<CornerRadius x:Key="RadiusPanel">4</CornerRadius>
<CornerRadius x:Key="RadiusPopup">6</CornerRadius>
<CornerRadius x:Key="RadiusThumbnail">3</CornerRadius>
<CornerRadius x:Key="RadiusSwatch">6</CornerRadius>
<CornerRadius x:Key="RadiusScrollbarThumb">4</CornerRadius>
<CornerRadius x:Key="RadiusToolTip">6</CornerRadius>
```

Compatibility aliases can stay for one pass:

```xml
<CornerRadius x:Key="ControlCornerRadius">5</CornerRadius>
<CornerRadius x:Key="ButtonCornerRadius">5</CornerRadius>
```

Do not depend on a resource-to-resource `CornerRadius` alias unless it is verified in the target dictionary. The safe first pass is to keep the old keys as separate entries and update both from `ThemeResourceApplier`.

Then themes can actually own rounding instead of only changing accent colors.

---

### 2.6 Native top-level window rounding is separate from XAML control rounding

There are two different rounding layers:

```text
Native/DWM rounding  = actual outer HWND/window corners
XAML rounding        = internal controls, panels, buttons, popups, settings chips
```

Because PiPlay hosts WebView2 and keeps `AllowsTransparency="False"`, arbitrary per-pixel transparent rounded WPF windows are not the right path. Keep `AllowsTransparency="False"` and keep `WindowChrome.CornerRadius="0"`. Let DWM own the real top-level window corner, and let XAML own internal control shape.

Top-level policy:

```text
WindowChrome.CornerRadius = 0
AllowsTransparency        = False
DWM corner preference     = theme/window policy
```

Do not try to clip WebView2 with a WPF rounded `Border` to create real window corners. WebView2 is an HWND-backed child surface, so WPF clipping will be unreliable and can create airspace/resize artifacts. The safe model is:

```text
Outer window shape: DWM preference
Internal chrome:    XAML radius tokens
WebView surface:    rectangular child content inside the window
```

Suggested theme token:

```csharp
public enum DwmCornerMode
{
    Default,
    Square,      // DWMWCP_DONOTROUND when supported
    SmallRound,  // DWMWCP_ROUNDSMALL when supported
    Round        // DWMWCP_ROUND when supported
}
```

Current code rounds DWM corners when active/idle opacity is below `1.0`. For real themes, this should become explicit:

```text
Sharp Dark   → SmallRound or Default
Minimal      → Default or SmallRound
Soft Glass   → Round
Media Glow   → Round
Square/None  → optional advanced override
```

Opacity and rounding are visually related, but they should not be permanently coupled. A fully opaque `Sharp Dark` window may still want small rounded native corners, and a translucent window may still need a square/technical override.

---

### 2.7 Settings chips are hand-written

Current tests prevent drift between `ThemeCatalog` and the hardcoded Settings chips. That is useful.

Later, generate the chip controls from the catalog instead. That will make adding `media-glow`, `rose`, `muted-cyan`, etc. easier and less error-prone.

First pass can keep the hand-written chips.

---

## 3. Recommended theme architecture

The current `ThemePreset` record is too small for real theming. Expand it in stages rather than doing a huge rewrite.

Suggested target shape:

```csharp
public sealed record ThemePreset(
    string Id,
    string DisplayName,
    string Description,
    string DefaultAccentKey,
    ThemePalette Palette,
    ThemeRadii Radii,
    ThemeBehavior Behavior);

public sealed record ThemePalette(
    string AppBackground,
    string SurfaceBase,
    string SurfaceRaised,
    string SurfaceHover,
    string BorderSubtle,
    string BorderStrong,
    string TextPrimary,
    string TextSecondary,
    string Danger);

public sealed record ThemeRadii(
    double MainWindowFrame,
    double PopoutFrame,
    double TitleBar,
    double Button,
    double IconButton,
    double Input,
    double Panel,
    double Popup,
    double Thumbnail,
    double Swatch,
    double ScrollbarThumb,
    double ToolTip);

public sealed record ThemeBehavior(
    string FadeDelayPreset,
    int FadeDurationMs,
    bool StripAutoHide,
    double ActiveOpacity,
    double IdleOpacity,
    DwmCornerMode DwmCorners);
```

If this feels too large for the immediate pass, start with:

```csharp
public sealed record ThemePreset(
    string Id,
    string DisplayName,
    string Description,
    string DefaultAccentColor,
    string DefaultFadeDelayPreset,
    bool DefaultStripAutoHide,
    double DefaultActiveWindowOpacity,
    double DefaultIdleWindowOpacity,
    DwmCornerMode DwmCorners,
    double MainWindowRadius,
    double PopoutRadius,
    double ButtonRadius,
    double InputRadius,
    double PanelRadius,
    double PopupRadius);
```

That is enough to make the presets visually different.

---

## 4. Accent variant model

Use one selected accent, but generate several derived tokens.

Recommended token set:

```text
AccentPrimary     filled primary button / active strong state
AccentHover       primary hover fill
AccentPressed     primary pressed fill
AccentMuted       restrained accent for sharp borders/glyphs
AccentSubtle      dark tinted background / low glow
AccentBorder      focus border / active outline
AccentGlow        optional low-alpha glow source
OnAccent          text/icon color on AccentPrimary
```

Current code has only:

```text
AccentPrimary
AccentPrimaryLight
```

That is enough for first pass, but the next theme pass should add the other tokens before adding a color wheel.

---

## 5. Recommended accent variants

All `Primary` values below are chosen to work on dark UI and to remain usable as a filled primary color. The darker values should be treated as muted/subtle derivatives, not as the main filled button color unless `OnAccent` is contrast-adjusted.

| Key | Display | Primary | Hover | Pressed | Muted | Subtle | Best use |
|---|---:|---:|---:|---:|---:|---:|---|
| `cyan` | PiPlay cyan | `#00D4FF` | `#5BE6FF` | `#00A9CC` | `#2B8EA3` | `#102832` | Existing PiPlay identity, high-energy CTA |
| `steel` | Steel cyan | `#4A8FAB` | `#6BB5D1` | `#3F86A2` | `#2D6F8F` | `#13242D` | Sharp Dark / utility look |
| `sky` | Sky blue | `#5AA9E6` | `#8BC7F1` | `#3C82B8` | `#3B6173` | `#12283A` | Clean daily/default alternative |
| `violet` | Violet | `#A78BFA` | `#C4B5FD` | `#8A70E5` | `#5E4D9A` | `#231E3C` | Soft Glass / modern overlay |
| `emerald` | Emerald | `#38D996` | `#78F0BD` | `#22A870` | `#2C7A5B` | `#143428` | Calm active state, good on dark |
| `amber` | Amber | `#FFC857` | `#FFD985` | `#D99F2A` | `#9D742E` | `#3B2B12` | Warm/media/glow look |
| `rose` | Rose | `#E45D75` | `#F28BA0` | `#D05268` | `#914454` | `#351B22` | Alternative danger-adjacent accent, not destructive |

First implementation recommendation:

```text
Keep current five chips: cyan, sky/steel-blue, violet, emerald/green, amber.
Add steel and rose after chips are generated from catalog.
```

Naming note: the current `steel-blue` chip uses `#5AA9E6`, which reads more like “sky blue”. If you add `#4A8FAB`, call that one `steel` or `muted-cyan`.

---

## 6. Color wheel policy

The color wheel should store only one user value:

```json
{
  "accentColor": "#4A8FAB"
}
```

Do not store every derived brush.

When arbitrary colors are allowed, generate contrast-safe derived tokens:

```csharp
var onAccent = Contrast(accent, DarkText) >= 4.5 ? DarkText : Colors.White;
```

If the chosen color is too dark to work as a filled primary color with dark text, either:

1. switch `OnAccent` to white, or
2. lift/lighten the primary fill while using the raw color as `AccentMuted`.

Recommendation for PiPlay:

```text
Use the chosen wheel color as AccentMuted.
Generate AccentPrimary by lifting it until OnAccent contrast passes.
```

That keeps dark/muted user choices visually faithful without breaking buttons.

---

## 7. Theme definitions

### 7.1 Sharp Dark

Purpose: compact, serious, utility-first. Best match for the current PiPlay shell and the sharp non-popout screenshot.

Recommended accent pairing:

```text
Default: cyan or steel
For the screenshot style: steel (#4A8FAB)
```

Palette:

```text
AppBackground  #07090B
SurfaceBase    #0D1014
SurfaceRaised  #141920
SurfaceHover   #202833
BorderSubtle   #2A3441
BorderStrong   #3A4655
TextPrimary    #F2F5F7
TextSecondary  #A8B0BA
Danger         #E45D75
```

Radii:

```text
MainWindow      4
Popout          4
Button          5
IconButton      5
Input           5
Panel           4
Popup           6
Thumbnail       3
ScrollbarThumb  4
```

Behavior:

```text
Fade delay      normal
Fade duration   180 ms
Active opacity  1.00
Idle opacity    0.92
Top bar hide    false
DWM corners     default or square
```

Button style:

```text
Primary CTA     AccentPrimary fill
Normal buttons  dark surface + subtle border
Pinned/active   AccentMuted glyph + AccentBorder outline
Hover           SurfaceHover, not a neon wash
```

---

### 7.2 Minimal

Purpose: quiet, daily-use, low-distraction theme.

Palette:

```text
AppBackground  #0B0D0E
SurfaceBase    #111316
SurfaceRaised  #1A1E22
SurfaceHover   #252B31
BorderSubtle   #30363D
BorderStrong   #414A55
TextPrimary    #F3F5F7
TextSecondary  #A7ADB4
Danger         #FF4B55
```

Radii:

```text
MainWindow      6
Popout          8
Button          6
IconButton      6
Input           6
Panel           8
Popup           8
Thumbnail       4
ScrollbarThumb  5
```

Behavior:

```text
Fade delay      normal
Fade duration   220 ms
Active opacity  1.00
Idle opacity    1.00
Top bar hide    false
DWM corners     default
```

Button style:

```text
Primary CTA     AccentPrimary fill, restrained
Normal buttons  dark surface
Hover           SurfaceHover only
Glow            none
```

---

### 7.3 Soft Glass

Purpose: translucent overlay/player look. Good for desktop popouts and secondary monitor usage.

Palette:

```text
AppBackground  #090B0F
SurfaceBase    #10141B
SurfaceRaised  #171C26
SurfaceHover   #242B38
BorderSubtle   #384255
BorderStrong   #526179
TextPrimary    #F7F8FA
TextSecondary  #C0C6CF
Danger         #E45D75
```

Radii:

```text
MainWindow      10
Popout          16
Button          10
IconButton      10
Input           10
Panel           14
Popup           14
Thumbnail       8
ScrollbarThumb  5
```

Behavior:

```text
Fade delay      normal or long
Fade duration   300 ms
Active opacity  0.90
Idle opacity    0.78
Top bar hide    false initially; true is a good advanced option
DWM corners     round
```

Button style:

```text
Primary CTA     AccentPrimary fill
Normal buttons  translucent dark fill + border
Active states   AccentBorder + soft glow
Hover           accent-tinted surface, low opacity
```

---

### 7.4 Media Glow

Purpose: expressive display/player mode. Good for music visual usage, but should probably land after the first real token pass.

Palette:

```text
AppBackground  #04070A
SurfaceBase    #0B1118
SurfaceRaised  #111A24
SurfaceHover   #1C2A38
BorderSubtle   #2A4356
BorderStrong   #3E6782
TextPrimary    #F8FBFF
TextSecondary  #B6C7D6
Danger         #F05F75
```

Radii:

```text
MainWindow      8
Popout          12
Button          8
IconButton      8
Input           8
Panel           12
Popup           12
Thumbnail       6
ScrollbarThumb  5
```

Behavior:

```text
Fade delay      long
Fade duration   340 ms
Active opacity  0.92
Idle opacity    0.72
Top bar hide    true
DWM corners     round
```

Button style:

```text
Primary CTA     AccentPrimary fill
Normal buttons  dark surface + accent border when active
Glow            allowed but capped
Hover           stronger than Soft Glass
```

Recommendation: defer this theme until `AccentGlow`, shadow/glow policy, and screenshot QA exist.

---

## 8. Proper corner rounding implementation plan

This is the recommended path to turn rounding on properly now, while keeping the WebView2 constraints safe.

### 8.1 User-facing rule

Rounding should be theme-owned by default.

Do not expose every radius as a separate setting in the first implementation. The settings page should present this as part of the theme personality:

```text
Sharp Dark   = compact, small corners
Minimal      = calm, moderate corners
Soft Glass   = floating, larger corners
Media Glow   = display/player mode, medium-large corners
```

Optional advanced override later:

```text
Corner style: Theme / Square / Small / Soft / Round
```

That override should change the corner profile, not individual per-control values.

### 8.2 Rounding profiles

Recommended values, in WPF device-independent pixels:

| Token | Sharp Dark | Minimal | Soft Glass | Media Glow |
|---|---:|---:|---:|---:|
| `RadiusMainWindowFrame` | 4 | 6 | 10 | 8 |
| `RadiusPopoutFrame` | 4 | 8 | 16 | 12 |
| `RadiusTitleBar` | 4 | 6 | 10 | 8 |
| `RadiusButton` | 5 | 6 | 10 | 8 |
| `RadiusIconButton` | 5 | 6 | 10 | 8 |
| `RadiusInput` | 5 | 6 | 10 | 8 |
| `RadiusPanel` | 4 | 8 | 14 | 12 |
| `RadiusPopup` | 6 | 8 | 14 | 12 |
| `RadiusThumbnail` | 3 | 4 | 8 | 6 |
| `RadiusSwatch` | 6 | 8 | 10 | 8 |
| `RadiusScrollbarThumb` | 4 | 5 | 5 | 5 |
| `RadiusToolTip` | 6 | 6 | 8 | 8 |
| DWM corner mode | `SmallRound`/`Default` | `Default`/`SmallRound` | `Round` | `Round` |

Notes:

- `Sharp Dark` should not go fully square by default. Tiny rounding keeps it modern without making it soft.
- `Soft Glass` gets the largest popout radius because it is the floating overlay theme.
- `Media Glow` is expressive, but not as soft as `Soft Glass`.
- Scrollbar thumbs should not become huge pills unless the scrollbar track is also redesigned.

### 8.3 Theme model additions

Add radii and native corner mode to the preset model:

```csharp
public enum DwmCornerMode
{
    Default,
    Square,
    SmallRound,
    Round
}

public sealed record ThemeRadii(
    double MainWindowFrame,
    double PopoutFrame,
    double TitleBar,
    double Button,
    double IconButton,
    double Input,
    double Panel,
    double Popup,
    double Thumbnail,
    double Swatch,
    double ScrollbarThumb,
    double ToolTip);

public sealed record ThemePreset(
    string Id,
    string DisplayName,
    string Description,
    string DefaultAccentColor,
    string DefaultFadeDelayPreset,
    bool DefaultStripAutoHide,
    double DefaultActiveWindowOpacity,
    double DefaultIdleWindowOpacity,
    DwmCornerMode DwmCorners,
    ThemeRadii Radii);
```

If adding full `ThemePalette` at the same time is too much, radii can land first as a focused pass. That gives immediate visual payoff and reduces hardcoded XAML values.

### 8.4 Resource keys to add

Add these to `Colors.xaml` or move them into a dedicated `Theme/Radii.xaml` merged dictionary:

```xml
<CornerRadius x:Key="RadiusMainWindowFrame">4</CornerRadius>
<CornerRadius x:Key="RadiusPopoutFrame">4</CornerRadius>
<CornerRadius x:Key="RadiusTitleBar">4,4,0,0</CornerRadius>
<CornerRadius x:Key="RadiusButton">5</CornerRadius>
<CornerRadius x:Key="RadiusIconButton">5</CornerRadius>
<CornerRadius x:Key="RadiusInput">5</CornerRadius>
<CornerRadius x:Key="RadiusPanel">4</CornerRadius>
<CornerRadius x:Key="RadiusPopup">6</CornerRadius>
<CornerRadius x:Key="RadiusThumbnail">3</CornerRadius>
<CornerRadius x:Key="RadiusSwatch">6</CornerRadius>
<CornerRadius x:Key="RadiusScrollbarThumb">4</CornerRadius>
<CornerRadius x:Key="RadiusToolTip">6</CornerRadius>

<!-- Temporary compatibility aliases while styles migrate. -->
<CornerRadius x:Key="ControlCornerRadius">5</CornerRadius>
<CornerRadius x:Key="ButtonCornerRadius">5</CornerRadius>
```

`ThemeResourceApplier` should replace these entries when the active theme changes.

### 8.5 XAML migration checklist

Replace hardcoded radii with semantic tokens:

```text
Generic button styles        → RadiusButton
AccentButton                 → RadiusButton
DangerButton                 → RadiusButton
CloseIconButton              → RadiusIconButton
PinToggle                    → RadiusIconButton or RadiusButton
DarkTextBox                  → RadiusInput
ComboBox outer border        → RadiusInput
ComboBox item hover          → RadiusPopup or 6-ish item token if added later
ComboBox popup border        → RadiusPopup
ToolTip                      → RadiusToolTip
ScrollBar thumb              → RadiusScrollbarThumb
Settings theme chips         → RadiusPanel
Settings accent swatches     → RadiusSwatch
Settings section cards       → RadiusPanel
Player top strip background  → RadiusTitleBar
Player/main shell border     → RadiusPopoutFrame / RadiusMainWindowFrame
```

Avoid a generic `RadiusControl` everywhere. That makes themes look mathematically consistent but visually flat.

### 8.6 MainWindow, PlayerWindow, and SettingsWindow guidance

For `MainWindow.xaml` and `PlayerWindow.xaml`:

```text
WindowChrome.CornerRadius stays 0
DWM corner preference controls the real outer corner
Root/shell Border may use RadiusMainWindowFrame or RadiusPopoutFrame for internal border/background consistency
Do not enable AllowsTransparency
Do not depend on WPF clipping to shape WebView2
```

For `SettingsWindow.xaml`:

```text
Use RadiusPanel for the settings container
Use RadiusTitleBar or explicit top-only radius for the header area
Use RadiusButton, RadiusInput, RadiusSwatch in controls
Settings can visually round more safely than WebView windows because it does not host the main video WebView surface
```

The Settings window can adopt theme radii immediately. The WebView windows should use DWM/native rounding for the true outer shape.

### 8.7 Native corner policy implementation

Change `WindowOpacityApplier.SetRoundedCorners(hwnd, bool rounded)` into a mode-based API:

```csharp
public static void SetCornerMode(IntPtr hwnd, DwmCornerMode mode)
{
    if (hwnd == IntPtr.Zero) return;

    var pref = mode switch
    {
        DwmCornerMode.Square => DWMWCP_DONOTROUND,
        DwmCornerMode.Round => DWMWCP_ROUND,
        DwmCornerMode.SmallRound => DWMWCP_ROUNDSMALL,
        _ => DWMWCP_DEFAULT
    };

    _ = DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));
}
```

Keep the call no-op tolerant on older Windows versions.

Then call it from:

```text
MainWindow SourceInitialized / theme apply
PlayerWindow SourceInitialized / theme apply
SettingsWindow SourceInitialized / theme apply, if needed
```

Do not tie the corner mode only to opacity. Theme selection should be enough to set the desired native corner behavior.

### 8.8 Testing suggestions

Add tests that check theme data, not visual pixels:

```text
ThemeRadii values are non-negative and within sane max, e.g. 0..24
Popout radius is never smaller than main window radius for Soft Glass
Sharp Dark radii stay below Minimal/Soft Glass radii
Every theme has a DwmCornerMode
ThemeResourceApplier writes all radius resource keys
Settings chips and ThemeCatalog do not drift
Accent button foreground uses OnAccent, not a hardcoded dark foreground
```

Optional static/XAML test:

```text
Fail or warn on new hardcoded CornerRadius="8" / "10" / etc.
Allow only deliberate exceptions listed in a whitelist.
```

### 8.9 First rounding pass acceptance criteria

The rounding pass is good enough when:

- `Sharp Dark`, `Minimal`, and `Soft Glass` visibly differ by corner shape.
- Buttons, inputs, chips, panels, popups, and settings swatches no longer use scattered hardcoded radii.
- Popout native corners are controlled by theme, not by opacity side effects.
- WebView2 remains stable during resize and does not depend on WPF rounded clipping.
- The theme selector can change radius resources without restarting, or the UI clearly documents that full visual theme changes apply on next window creation.
- The sharp screenshot style can be represented by `Sharp Dark + steel accent`, not by another near-duplicate theme.

---

## 9. First-pass implementation order

Recommended order from the current code state:

1. Keep behavior bug work first: popout resize, scroll, compact navigation, compact expand path.
2. Add `ThemeRadii` and `DwmCornerMode` to `ThemeCatalog`.
3. Add radius resources: `RadiusButton`, `RadiusIconButton`, `RadiusInput`, `RadiusPanel`, `RadiusPopup`, `RadiusSwatch`, `RadiusScrollbarThumb`, `RadiusToolTip`, `RadiusMainWindowFrame`, and `RadiusPopoutFrame`.
4. Update `ThemeResourceApplier` to replace radius resources from the active preset.
5. Replace hardcoded radii in `ControlStyles.xaml`, `SettingsWindow.xaml`, `MainWindow.xaml`, and `PlayerWindow.xaml` with radius tokens.
6. Change popout/main native corner handling from opacity-driven rounded yes/no to theme-driven `DwmCornerMode`.
7. Add `AccentPressed`, `AccentMuted`, `AccentSubtle`, `AccentBorder`, and `OnAccent` resources.
8. Change `AccentButton.Foreground` from hardcoded `#FF06141A` to dynamic `OnAccent`.
9. Update `ThemeResourceApplier` to replace all theme-controlled resources, not only `AccentPrimary`.
10. Decide whether theme switching preserves custom accent or adopts preset accent.
11. Make theme selection apply preset defaults, unless a user override exists.
12. Add `steel` and `rose` chips once Settings chips are generated from catalog.
13. Add the color wheel last.

---

## 10. Specific small cleanup items

These are safe follow-up items I noticed while reviewing the bundle:

- `MainWindow.xaml` still has `AccentCyan` on the placeholder icon. Move it to `AccentPrimary` or an `AccentMuted` token.
- `PinnedHint` has `AccentCyan` in XAML but is recolored in code; use `DynamicResource AccentPrimary` as the seed to avoid the pre-apply flash.
- Many `CornerRadius="8"`, `"10"`, `"6"`, and `"5"` values remain in `ControlStyles.xaml` and `SettingsWindow.xaml`; migrate them to semantic radius tokens rather than one catch-all radius.
- `MainWindow.xaml` and `PlayerWindow.xaml` root shell borders still use `CornerRadius="0"`; decide whether they should use `RadiusMainWindowFrame` / `RadiusPopoutFrame` for internal border consistency while DWM owns the true outer shape.
- `WindowOpacityApplier.SetRoundedCorners(hwnd, bool)` should become `SetCornerMode(hwnd, DwmCornerMode)` so theme rounding is not coupled to opacity.
- The current `ThemePreset` defaults for Soft Glass are not applied by the Settings theme button path.
- If the Settings text says themes apply right away, either make surface/radius resources dynamic or clarify that accent applies immediately while broader theme styling affects new windows / next restart.
- `ThemeCatalog.DefaultAccentColor = "#00D4FF"` is correct for current PiPlay identity, but for the sharp muted screenshot look use the steel accent variant, not a separate theme.

---

## 11. Recommended immediate definitions

For the next implementation pass, I would define these as the stable first set:

```text
Themes:
  sharp-dark   default / compact utility shell
  minimal      quiet daily shell
  soft-glass   translucent overlay shell

Deferred:
  media-glow   expressive display/player mode
```

```text
Accent variants:
  cyan      #00D4FF
  steel     #4A8FAB
  sky       #5AA9E6
  violet    #A78BFA
  emerald   #38D996
  amber     #FFC857
  rose      #E45D75
```

Recommended default combinations:

```text
Fresh install:             Sharp Dark + cyan
Sharp screenshot style:    Sharp Dark + steel
Daily neutral:             Minimal + sky
Floating overlay:          Soft Glass + violet
Music/display mode later:  Media Glow + cyan or amber
```

This keeps PiPlay identity intact while giving the sharper, less-neon look a clean path through accent variants instead of fragmenting the theme list.
