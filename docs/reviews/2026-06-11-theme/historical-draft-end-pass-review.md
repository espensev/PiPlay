> **HISTORICAL AUDIT — NOT A CURRENT-STATE VERDICT.** This review examined the superseded
> draft checkout at `6e843a2` (before the PR #18 and PR #19 merges). Its findings were
> accurate for that dead draft and are resolved on current `main` (merge `9e822d8`). Read it
> as "why the old draft failed". Current state: `post-merge-disposition-review.md`
> and `docs/superpowers/specs/2026-06-11-theme-corners-and-palettes-design.md`.

# PiPlay Theme End-Pass Review

Date: 2026-06-11  
Scope: live checkout in `D:\Development\DesktopApps\PiPlay`  
Review target: theme foundation, Settings appearance flow, accent resources, radius tokens, native corner behavior, tests  
Verdict: not ready as the full theme/radius variant pass. This is a useful foundation pass, but the actual app path still behaves mostly like the legacy Pin/Fade customization system.

Verification run:

```text
dotnet test
Passed: 507, Failed: 0, Skipped: 0
```

Working tree note at review time:

```text
M BUILD_NUMBER
M VERSION
?? piplay-theme-review-and-variants.md
```

---

## 1. Executive Summary

The current code has several good foundation pieces:

- `AppSettings.Theme` exists and is preserved through settings load/save.
- `ThemeCatalog` defines `sharp-dark`, `minimal`, and `soft-glass`.
- `ThemeResourceApplier` replaces derived `Theme.Accent*` resources before the first window parses.
- `AccentPalette` derives hover, pressed, dim, border, and foreground colors from one accent.
- Tests cover catalog IDs, accent normalization, accent contrast, resource replacement, and staged radius tokens.
- `dotnet test` is green.

The main problem is that the new theme model is not yet the model the running app actually uses. Startup theming reads `ThemeSettings`, but Settings still edits legacy `PlayerSettings` values, visible controls still use fixed `AccentCyan` resources, preset defaults are not applied through the resolver, and radius/native-corner work remains staged.

The result is a split-brain system:

```text
Startup resource pass: AppSettings.Theme -> ThemeResourceApplier -> Theme.Accent*
Interactive app path: SettingsWindow -> PlayerSettings -> AccentCyan/legacy brushes
```

That split needs to be resolved before adding color wheel, more variants, generated chips, or visual theme QA.

---

## 2. Blocking Findings

### F1. Theme settings are not the normal app path

Severity: P1

`App.OnStartup` applies theme resources from `LoadReadOnly()` before any window parses:

- `src/PiPlay/App.xaml.cs:59-62`

But `MainWindow` then loads settings and continues to drive the Settings dialog and popout construction from legacy `PlayerSettings`:

- `src/PiPlay/MainWindow.xaml.cs:58`
- `src/PiPlay/MainWindow.xaml.cs:479-487`
- `src/PiPlay/MainWindow.xaml.cs:557-573`
- `src/PiPlay/MainWindow.xaml.cs:695-700`

The Settings dialog constructor exposes `pinAccent`, `fadeAccent`, `fadeIdleDelayMs`, `constantWindowOpacity`, `idleWindowOpacity`, and `stripAutoHide`, but it does not expose `ThemeSettings.ThemeId`, `ThemeSettings.AccentColor`, or theme preset selection:

- `src/PiPlay/SettingsWindow.xaml.cs:38-51`
- `src/PiPlay/SettingsWindow.xaml.cs:119-156`

Why this matters:

- Users cannot select `sharp-dark`, `minimal`, or `soft-glass` through the UI.
- Users cannot edit the single theme accent through the UI.
- A manually edited `theme` block can affect startup resources while Settings continues saving legacy player fields.
- `ThemePreferenceResolver` is exercised in tests and startup, but most runtime app behavior bypasses it.

Recommended fix:

Create one effective appearance model and use it everywhere:

```csharp
public sealed record EffectiveThemePreferences(
    string ThemeId,
    string AccentColor,
    string FadeDelayPreset,
    bool StripAutoHide,
    double ActiveWindowOpacity,
    double IdleWindowOpacity);
```

Then have one resolver produce it:

```text
ThemeCatalog preset default -> ThemeSettings explicit override -> legacy fallback during migration -> normalized value
```

Use that effective model in:

- `MainWindow.ApplySourceAppearance`
- `MainWindow.SettingsButton_Click`
- `MainWindow.ApplyPlayerPreferences` or its replacement
- `PlayerWindow` constructor arguments
- `PlayerWindow.ApplyAppearance`
- `ThemeResourceApplier`

Keep legacy `PlayerSettings.PinAccent`, `FadeAccent`, and `FadeIdleDelayMs` readable for migration, but stop using them as the live source once `ThemeSettings` exists.

Tests to add:

- A settings file with only `theme.themeId = "soft-glass"` changes effective opacity and strip/fade defaults as expected.
- Settings edits persist into `settings.Theme`, not only `settings.Player`.
- Popout construction receives effective theme appearance, not raw legacy fields.
- A legacy settings file without `theme` still seeds theme once and remains backward-compatible.

---

### F2. Theme preset defaults are dead data

Severity: P1

`ThemeCatalog.ThemePreset` contains default behavior data:

- `src/PiPlay/Theme/ThemeCatalog.cs:5-13`
- `src/PiPlay/Theme/ThemeCatalog.cs:23-52`

But `ThemePreferenceResolver` does not use `ThemeCatalog.PresetFor(theme.ThemeId)` for effective behavior. It only reads explicit nullable values:

- `src/PiPlay/Theme/ThemePreferenceResolver.cs:14-26`

Current behavior:

```text
themeId = soft-glass
activeWindowOpacity = null
idleWindowOpacity = null

Effective result:
active opacity falls back to PlayerSettings.ConstantWindowOpacity
idle opacity falls back to PlayerSettings.IdleWindowOpacity
```

Expected theme behavior:

```text
themeId = soft-glass
activeWindowOpacity = null
idleWindowOpacity = null

Effective result:
active opacity = soft-glass default
idle opacity = soft-glass default
```

Why this matters:

- Selecting a preset would only change label/accent, not behavior.
- The `DefaultActiveWindowOpacity`, `DefaultIdleWindowOpacity`, and `DefaultStripAutoHide` fields look implemented but are not actually effective.
- Tests currently verify data existence and explicit overrides, but not preset-default resolution.

Recommended fix:

Change resolver methods to start with preset defaults:

```csharp
var preset = ThemeCatalog.PresetFor(theme?.ThemeId);
var active = theme?.ActiveWindowOpacity ?? preset.DefaultActiveWindowOpacity;
var idle = theme?.IdleWindowOpacity ?? preset.DefaultIdleWindowOpacity;
var strip = theme?.StripAutoHide ?? preset.DefaultStripAutoHide;
```

For legacy migration, only fall back to `PlayerSettings` when the `theme` block was missing and seeded from legacy, or while a compatibility flag says the user has not opted into theme-owned behavior.

Tests to add:

- `ThemePreferenceResolver` applies `soft-glass` defaults when override values are null.
- Explicit overrides still win over preset defaults.
- Invalid `themeId` falls back to `sharp-dark` defaults.

---

### F3. Derived theme accent resources are not wired to primary controls

Severity: P1

`ThemeResourceApplier` replaces these resources:

- `Theme.Accent`
- `Theme.AccentHover`
- `Theme.AccentPressed`
- `Theme.AccentDim`
- `Theme.AccentBorder`
- `Theme.AccentForeground`

See:

- `src/PiPlay/Theme/ThemeResourceApplier.cs:28-35`

But the most visible controls still use fixed legacy accent resources:

- `AccentButton` uses `AccentCyan`, `AccentCyanLight`, and `AccentCyanForeground`: `src/PiPlay/Theme/ControlStyles.xaml:57-71`
- Settings checked preset styling uses `AccentCyan`: `src/PiPlay/SettingsWindow.xaml:71-73`
- Main window pinned hint seeds from `AccentCyan`: `src/PiPlay/MainWindow.xaml:44-45`
- Source placeholder icon uses `AccentCyan`: `src/PiPlay/MainWindow.xaml:153-154`
- TextBox caret, selection, and focus border use `AccentCyan`: `src/PiPlay/Theme/ControlStyles.xaml:207-233`
- Pin/Fade toggles default attached brush is `AccentCyan`: `src/PiPlay/Theme/ControlStyles.xaml:169-186`

Why this matters:

- `ThemeResourceApplier` can successfully update `Theme.Accent*` while the UI still looks cyan.
- The default sharp-muted accent `#2D6F8F` is not visible on the primary CTA.
- Contrast tests include `Theme.AccentForeground`, but `AccentButton` does not consume it.

Recommended fix:

Migrate visible consumers first:

```text
AccentButton.Background -> Theme.Accent
AccentButton hover -> Theme.AccentHover
AccentButton pressed -> Theme.AccentPressed
AccentButton.Foreground -> Theme.AccentForeground
TextBox caret/selection/focus -> Theme.Accent or Theme.AccentBorder
Settings selected preset -> Theme.Accent / Theme.AccentBorder
PinnedHint seed -> Theme.Accent or Theme.AccentBorder
Placeholder icon -> Theme.Accent
Pin/Fade default checked brush -> Theme.Accent
```

Then decide whether the legacy fixed swatches stay only as color-palette options or are removed from the live control path entirely.

Naming improvement:

The code uses `Theme.AccentForeground`. The review guide used `OnAccent`. Either name is fine, but make the intent explicit in tests and style names:

```text
Theme.AccentForeground = text/icon color on Theme.Accent fill
```

Tests to add:

- Static XAML test: `AccentButton` does not reference `AccentCyan`, `AccentCyanLight`, or `AccentCyanForeground`.
- Static XAML test: primary CTA foreground is `Theme.AccentForeground`.
- Runtime resource test: after applying violet accent, `AccentButton` resolves violet fill before window creation.

---

### F4. Radius and DWM corner work is still staged, not implemented

Severity: P2

The review target calls for theme-owned radii and theme-owned native corner mode. Current code has only staged radius resources:

- `src/PiPlay/Theme/Colors.xaml:77-86`

The code explicitly says several radii are unwired and that many literals remain:

- `src/PiPlay/Theme/Colors.xaml:77-81`

Literal radii remain in control styles:

- `IconButton`: `src/PiPlay/Theme/ControlStyles.xaml:122`
- `CloseIconButton`: `src/PiPlay/Theme/ControlStyles.xaml:150`
- `PinToggle`: `src/PiPlay/Theme/ControlStyles.xaml:186`
- `DarkTextBox`: `src/PiPlay/Theme/ControlStyles.xaml:226`
- `ScrollBar` thumbs: `src/PiPlay/Theme/ControlStyles.xaml:259`, `src/PiPlay/Theme/ControlStyles.xaml:290`
- `DarkComboBoxItem`: `src/PiPlay/Theme/ControlStyles.xaml:323`
- `DarkComboBox` closed border: `src/PiPlay/Theme/ControlStyles.xaml:361`
- `ToolTip`: `src/PiPlay/Theme/ControlStyles.xaml:442`

Native rounded corners are still opacity-driven:

- `src/PiPlay/PlayerWindow.xaml.cs:602-608`
- `src/PiPlay/Services/WindowOpacityApplier.cs:106-116`

And `ThemePreset` has no radii or DWM corner mode fields:

- `src/PiPlay/Theme/ThemeCatalog.cs:5-13`

Why this matters:

- `Sharp Dark`, `Minimal`, and `Soft Glass` cannot visibly differ by corner shape.
- Soft-glass native rounding depends on opacity configuration, not the selected theme.
- There is no place to put `SmallRound`, `Round`, or `Square` as a preset-owned policy.
- Tests currently pin the staged values, including `Radius.MainWindow = 0` and `Radius.Popout = 0`: `tests/PiPlay.Tests/Ui/XamlInvariantTests.cs:508-521`.

Recommended fix:

Add first-class model types:

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
```

Then add them to `ThemePreset` and make `ThemeResourceApplier` replace radius resources from the active preset.

Recommended resource names:

```text
Radius.MainWindowFrame
Radius.PopoutFrame
Radius.TitleBar
Radius.Button
Radius.IconButton
Radius.Input
Radius.Panel
Radius.Popup
Radius.Thumbnail
Radius.Swatch
Radius.ScrollbarThumb
Radius.ToolTip
```

Do not reuse a generic `Radius.Settings` for both swatches and preset chips if the goal is visual theme personality. Split it into semantic tokens.

For native corners, replace:

```csharp
SetRoundedCorners(IntPtr hwnd, bool rounded)
```

with:

```csharp
SetCornerMode(IntPtr hwnd, DwmCornerMode mode)
```

Then call it from source/player/settings window initialization based on effective theme, not opacity.

Tests to add:

- Every preset has `ThemeRadii` and `DwmCornerMode`.
- Radius values are non-negative and within a sane max, for example `0..24`.
- `soft-glass` popout radius is greater than or equal to main radius.
- `sharp-dark` radii stay smaller than `soft-glass` radii.
- `ThemeResourceApplier` replaces every radius key.
- Static XAML test flags new literal `CornerRadius="8"` or `CornerRadius="10"` except whitelisted top-level `WindowChrome.CornerRadius="0"`.
- `WindowOpacityApplier` exposes mode-based test seams, not only boolean rounded state.

---

### F5. Settings UI still presents the old customization model

Severity: P2

Settings still has separate `Pin color` and `Fade color` sections:

- `src/PiPlay/SettingsWindow.xaml:119-156`

It has no theme preset selector and no single accent path. The code-behind still exposes:

- `PinAccent`
- `FadeAccent`
- `FadeIdleDelayMs`
- `ConstantWindowOpacity`
- `IdleWindowOpacity`
- `StripAutoHide`

See:

- `src/PiPlay/SettingsWindow.xaml.cs:23-31`

Why this matters:

- The product direction is "theme preset plus one accent", but the UI still teaches users that pin and fade are separate color systems.
- The `ThemeCatalog.AccentOptions` list is unused by Settings.
- Tests are still centered on player appearance input, not theme selection.

Recommended fix:

Replace Appearance controls in this order:

1. Theme preset segmented controls: Sharp Dark, Minimal, Soft Glass.
2. One accent swatch row generated from `ThemeCatalog.AccentOptions`.
3. Keep Playback controls separate: Compact player.
4. Keep Advanced controls separate: fade delay, window opacity, auto-hide.
5. When Advanced values differ from theme defaults, mark them as overrides in the model.

Model recommendation:

```csharp
public sealed class ThemeSettings
{
    public string ThemeId { get; set; } = "sharp-dark";
    public string AccentColor { get; set; } = "#2D6F8F";
    public string? FadeDelayOverride { get; set; }
    public bool? StripAutoHideOverride { get; set; }
    public double? ActiveOpacityOverride { get; set; }
    public double? IdleOpacityOverride { get; set; }
}
```

This makes the preset-default versus user-override relationship explicit.

Tests to add:

- Settings contains one accent selector, not separate Pin/Fade color sections.
- Settings has stable controls for every `ThemeCatalog.Presets` entry.
- Settings theme buttons and catalog cannot drift.
- Changing a theme preset updates `ThemeSettings.ThemeId`.
- Changing accent updates `ThemeSettings.AccentColor`.

---

### F6. Startup resource application is one-shot while later saves do not reapply resources

Severity: P2

`ThemeResourceApplier.Apply` runs once before `MainWindow` parses:

- `src/PiPlay/App.xaml.cs:59-62`

Its comment says live re-theming is deferred because window XAML is `StaticResource`-only:

- `src/PiPlay/Theme/ThemeResourceApplier.cs:8-18`

This is fine as a staged foundation, but it creates a product trap once Settings exposes theme selection:

```text
User changes theme in Settings
Settings saves settings
Already-open windows keep old StaticResource values
New windows may or may not reflect changed resources depending on whether the app-level dictionary was re-applied
```

Recommended fix:

Pick one of these and document it in code/tests:

Option A: live theme only for selected tokens

- Migrate theme-controlled consumers to `DynamicResource`.
- Re-run `ThemeResourceApplier.Apply(Application.Current.Resources, settings)` after a Settings save.
- Reapply non-resource behavior, such as DWM corner mode and popout opacity, through explicit window methods.

Option B: restart/new-window theme application for now

- Keep `StaticResource`.
- Make Settings copy honest: accent/theme changes apply after restart or next window creation.
- Still update `Application.Current.Resources` immediately so new popouts created after Settings close use the new resources.

Given PiPlay's current architecture, Option B is the safer first step. Option A can follow after the core theme model is not split-brain.

Tests to add:

- After saving theme settings and reapplying resources, newly constructed controls use the new resource values.
- Existing window behavior is either explicitly live-updated or explicitly documented as restart/new-window only.

---

### F7. Theme palette tokens exist but preset palettes do not

Severity: P2

`Colors.xaml` defines canonical theme palette tokens:

- `Theme.AppBackground`
- `Theme.SurfaceBase`
- `Theme.SurfaceRaised`
- `Theme.SurfaceHover`
- `Theme.BorderSubtle`
- `Theme.TextPrimary`
- `Theme.TextSecondary`
- `Theme.Danger`

See:

- `src/PiPlay/Theme/Colors.xaml:19-28`
- `src/PiPlay/Theme/Colors.xaml:48-57`

But `ThemePreset` does not contain a palette, and `ThemeResourceApplier` does not replace these tokens. Only accent/opacity/fade tokens are updated:

- `src/PiPlay/Theme/ThemeCatalog.cs:5-13`
- `src/PiPlay/Theme/ThemeResourceApplier.cs:28-42`

Why this matters:

- `sharp-dark`, `minimal`, and `soft-glass` are not real visual themes yet.
- Surface color, border strength, text tone, and danger color cannot vary by preset.
- A user selecting `minimal` or `soft-glass` would not get the quiet or translucent palette described by the design.

Recommended fix:

Add:

```csharp
public sealed record ThemePalette(
    string AppBackground,
    string SurfaceBase,
    string SurfaceRaised,
    string SurfaceHover,
    string BorderSubtle,
    string BorderStrong,
    string TextPrimary,
    string TextSecondary,
    string Danger,
    string TextOnDanger,
    string MediaBackdrop);
```

Then have the applier replace both color and brush entries for palette tokens, just as it currently does for accent tokens.

Do this after F1/F2, because palette work is only meaningful once the effective theme path is unified.

Tests to add:

- Every preset has a palette.
- Theme palette colors normalize to valid RGB/ARGB.
- Primary text contrast against base/raised surfaces stays above threshold.
- Danger text contrast stays safe.
- `ThemeResourceApplier` replaces palette resources in every dictionary that defines them.

---

### F8. Compatibility aliases can drift from theme tokens

Severity: P3

`Colors.xaml` has canonical `Theme.*` resources and legacy aliases such as `AppBackground`, `SurfaceBase`, and `DangerPin`:

- `src/PiPlay/Theme/Colors.xaml:91-115`

The comment explains that alias color keys duplicate hex literals and are pinned by tests:

- `src/PiPlay/Theme/Colors.xaml:91-99`

That is acceptable for this staged pass, but it becomes fragile once palette resources become preset-owned. If `ThemeResourceApplier` updates only `Theme.SurfaceBase` and not alias `SurfaceBase`, controls still using aliases will stay on stale colors.

Current consumers still use aliases heavily:

- `src/PiPlay/Theme/ControlStyles.xaml:23-25`
- `src/PiPlay/Theme/ControlStyles.xaml:207-212`
- `src/PiPlay/SettingsWindow.xaml:49-51`
- `src/PiPlay/MainWindow.xaml:12`

Recommended fix:

During the palette pass, either:

1. migrate all consumers to `Theme.*` keys, or
2. have `ThemeResourceApplier` update both canonical keys and compatibility aliases.

Avoid a mixed state where `Theme.SurfaceRaised` changes but `SurfaceRaised` does not.

Tests to add:

- Static XAML test tracks remaining legacy alias references.
- Runtime test verifies alias brushes match canonical theme brushes after applying a non-default palette.

---

## 3. Additional Improvements

### 3.1 Generate theme and accent controls from catalog data

Settings controls are currently hand-authored. Once the UI moves to theme presets and one accent, generate the controls from:

- `ThemeCatalog.Presets`
- `ThemeCatalog.AccentOptions`

Benefits:

- No drift between catalog and Settings.
- Adding `steel`, `rose`, or `media-glow` becomes data-only.
- Tests can assert the visual controls match catalog count/keys.

Recommended first step:

Keep generated controls simple. A `UniformGrid` or horizontal `ItemsControl` with a small view model is enough. Avoid a large MVVM rewrite for this pass.

### 3.2 Keep behavior overrides explicit

The current nullable fields on `ThemeSettings` are a good instinct:

- `StripAutoHide`
- `ActiveWindowOpacity`
- `IdleWindowOpacity`

But the names read like final values rather than overrides. Rename or document them as overrides before the Settings UI starts writing them.

Suggested names:

```text
StripAutoHideOverride
ActiveWindowOpacityOverride
IdleWindowOpacityOverride
FadeDelayPresetOverride
```

This avoids confusion when the resolver applies:

```text
preset default -> override -> normalized value
```

### 3.3 Decide custom accent preservation before color wheel

Theme switching behavior needs a rule before arbitrary accent selection lands:

```text
If current accent equals previous theme default:
    adopt the new theme default accent.
Else:
    preserve custom accent.
```

Without this, switching themes will either always erase the user's chosen accent or never show each theme's intended default. Both behaviors will feel wrong in different cases.

Tests to add:

- Switching from Sharp Dark with its default accent to Soft Glass adopts Soft Glass default accent.
- Switching from Sharp Dark with a custom accent preserves the custom accent.

### 3.4 Use theme accent for toggle active state, not separate pin/fade colors

`ToggleAccent` is useful and can stay. The issue is the source of the brush:

- today: `PlayerAppearancePolicy.BrushResourceKeyFor(pinAccent/fadeAccent)`
- target: effective theme accent brush, with optional role variants such as `Theme.AccentBorder`

Recommended roles:

```text
Pin active glyph: Theme.Accent
Pin active outline: Theme.AccentBorder
Fade active glyph: Theme.Accent
Fade active outline: Theme.AccentBorder
Focus border: Theme.AccentBorder
Primary button fill: Theme.Accent
Primary button text: Theme.AccentForeground
```

### 3.5 Separate media backdrop from app background intentionally

`Theme.MediaBackdrop` exists and is used by media surfaces:

- `src/PiPlay/MainWindow.xaml:130`
- `src/PiPlay/MainWindow.xaml:151`
- `src/PiPlay/PlayerWindow.xaml:11`

This is a good token. Keep it separate from app background because WebView/video surfaces want different treatment from chrome surfaces.

When adding palettes, define `MediaBackdrop` per theme rather than aliasing it to black forever.

### 3.6 Avoid making Settings visual copy overpromise live theming

Because current resources are mostly `StaticResource`, already-open windows will not fully restyle from a Settings change. Until consumers are migrated to `DynamicResource`, visible Settings copy should not imply full live theme switching.

Recommended copy stance:

```text
Accent and popout behavior apply to new windows immediately.
Full visual theme changes apply after restart.
```

Or, better, implement a small reapply path for new popouts and say:

```text
Applies to newly opened windows.
```

### 3.7 Treat `media-glow` as a later pass

Do not add `media-glow` until:

- base theme preset path is unified,
- accent roles are consumed by primary controls,
- palette tokens are preset-owned,
- radius tokens are preset-owned,
- screenshot QA exists.

Otherwise `media-glow` will add more data to an already split model.

---

## 4. Recommended Implementation Order

1. Unify effective theme resolution.
   - Add an `EffectiveThemePreferences` or equivalent DTO.
   - Resolve preset defaults plus user overrides in one place.
   - Stop passing raw legacy `PlayerSettings` appearance values into new popouts.

2. Update Settings to write `ThemeSettings`.
   - Add theme selector.
   - Replace separate Pin/Fade color rows with one accent row.
   - Keep Playback and Advanced sections as separate behavior controls.

3. Wire visible accent consumers to `Theme.Accent*`.
   - Start with `AccentButton`, Settings selected state, text input focus/caret, pinned hint, placeholder icon, and default toggle active brushes.

4. Decide and implement new-window versus live reapply behavior.
   - At minimum, re-run `ThemeResourceApplier` after Settings save so new windows see the new resources.
   - Clearly document if already-open controls require restart.

5. Add preset behavior tests.
   - `soft-glass` defaults must be effective without explicit overrides.
   - Overrides must win.
   - Legacy settings must still migrate safely.

6. Add `ThemeRadii` and `DwmCornerMode`.
   - Keep `WindowChrome.CornerRadius = 0`.
   - Make DWM corner preference theme-owned.
   - Replace XAML literal radii with semantic resources.

7. Add preset palettes.
   - Replace theme palette resources from `ThemeResourceApplier`.
   - Migrate aliases or update them in lockstep.

8. Generate Settings chips from catalog.
   - Add `steel` and `rose` once generation and tests exist.

9. Add color wheel last.
   - Store one accent value.
   - Derive contrast-safe accent roles.

---

## 5. Suggested Acceptance Criteria For The Next Pass

The next pass is acceptable when all of these are true:

- Settings exposes `sharp-dark`, `minimal`, and `soft-glass`.
- Settings persists theme selection into `AppSettings.Theme.ThemeId`.
- Settings exposes one accent selection and persists `AppSettings.Theme.AccentColor`.
- Separate Pin/Fade color controls are gone or clearly marked legacy-only during a transitional build.
- `ThemePreferenceResolver` applies preset defaults when overrides are null.
- `soft-glass` effective opacity differs from `sharp-dark` without manual slider changes.
- `AccentButton` consumes `Theme.Accent` and `Theme.AccentForeground`.
- Main placeholder, pinned hint, text input focus/caret, and selected Settings controls consume theme accent roles.
- New popouts use effective theme behavior, not raw `PlayerSettings`.
- Tests cover preset defaults, overrides, legacy migration, and Settings/catalog drift.
- Radius work is either explicitly still out of scope or implemented with `ThemeRadii` and `DwmCornerMode`; it should not sit halfway documented as a completed theme variant pass.

---

## 6. Non-Issues To Preserve

These parts look right and should not be casually undone:

- `AllowsTransparency="False"` on WebView-hosting windows.
- `WindowChrome.CornerRadius="0"` for top-level WPF chrome.
- DWM/native corners as the mechanism for actual top-level window corners.
- Avoiding WPF rounded clipping around WebView2.
- Applying initial resources before first window parse while most XAML uses `StaticResource`.
- Atomic settings writes and read-only startup settings load.
- Preserving unknown JSON extension data for forward compatibility.
- Keeping reset away from WebView2 user data and logs.

---

## 7. Bottom Line

This pass is a solid theme foundation, not the end of the theme variant implementation.

The highest-order fix is to remove the split-brain model. Make `ThemeSettings` the source of truth for appearance, make `ThemeCatalog` preset defaults effective, and then wire visible controls to `Theme.Accent*`. Radius, palette, native corners, generated chips, and color wheel should follow after that core path is coherent.
