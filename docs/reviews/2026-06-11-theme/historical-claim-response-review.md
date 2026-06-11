> **HISTORICAL AUDIT — NOT A CURRENT-STATE VERDICT.** This review examined the superseded
> draft checkout at `6e843a2` (before the PR #18 and PR #19 merges). Its findings were
> accurate for that dead draft and are resolved on current `main` (merge `9e822d8`). Read it
> as "why the old draft failed". Current state: `post-merge-disposition-review.md`
> and `docs/superpowers/specs/2026-06-11-theme-corners-and-palettes-design.md`.

# PiPlay Theme Claim Response Review

Date: 2026-06-11  
Scope: review of the quoted implementation response against the current checkout in `D:\Development\DesktopApps\PiPlay`  
Review target: claims about F2, schema 3 migration, preset-click accent preservation, override documentation, and deferred theme work  
Verdict: the quoted response is not supported by the current repo state. The implementation it describes is either on another branch/bundle or has not been applied here.

Verification run:

```text
dotnet test
Passed: 507, Failed: 0, Skipped: 0
```

Working tree note at review time:

```text
M BUILD_NUMBER
M VERSION
?? piplay-theme-end-pass-review.md
?? piplay-theme-review-and-variants.md
```

No code, test, or docs changes under `src/`, `tests/`, or `docs/` were present in the current working tree during this review.

---

## 1. Summary

The quoted answer claims several follow-up fixes landed:

- `ThemePreferenceResolver` now resolves preset default -> explicit override -> normalized.
- A hand-edited `"themeId": "soft-glass"` with null opacity overrides is actually translucent.
- Migration is protected by `FromLegacy` copying behavior values as explicit overrides and by a schema 3 bump for schema <=2 theme blocks.
- Preset clicks preserve custom accents while adopting the new theme default only when the old accent was the previous theme default.
- Nullable `ThemeSettings` fields are documented as overrides.
- A full disposition table exists in the design spec addendum.

The current checkout does not contain those changes.

The code still matches the earlier foundation state:

```text
ThemePreferenceResolver:
  null StripAutoHide -> PlayerSettings.StripAutoHide
  null ActiveWindowOpacity -> PlayerSettings.ConstantWindowOpacity
  null IdleWindowOpacity -> PlayerSettings.IdleWindowOpacity

AppSettings.CurrentSchemaVersion:
  2

ThemeSettings.FromLegacy:
  copies AccentColor and FadeDelayPreset only

Settings UI:
  no theme preset selector
  no ThemePreset_Click path
  no single accent theme path
```

This means F2 is still open in this checkout, and the response should not be accepted as evidence of implementation.

---

## 2. Claim Disposition

| Claim | Current checkout result | Disposition |
|---|---|---|
| `ThemePreferenceResolver` resolves preset default -> explicit override -> normalized for strip/opacity | Resolver still uses nullable override -> `PlayerSettings` fallback. It does not call `ThemeCatalog.PresetFor`. | Not implemented |
| Hand-edited `"themeId": "soft-glass"` is translucent | With null opacity fields and default player opacity, resolver returns player defaults of `1.0`, not soft-glass `0.92/0.78`. | Not implemented |
| `FromLegacy` copies legacy behavior as explicit overrides | `FromLegacy` copies only accent and fade delay. It does not copy strip auto-hide or opacities. | Not implemented |
| Schema 3 bump backfills schema <=2 theme-block nulls from player fields once | `CurrentSchemaVersion` is still `2`; no schema <=2 backfill code exists. | Not implemented |
| Custom accent survives preset switches | No theme selector or `ThemePreset_Click` path exists in current Settings code. | Not implemented |
| Behavior defaults still adopt on preset click | No preset-click path exists. | Not implemented |
| Nullable `ThemeSettings` fields are documented as overrides | The properties are still bare nullable values with no XML comments explaining override semantics. | Not implemented |
| Generated chips, media-glow, color wheel remain deferred | Generated chips, media-glow, and color wheel are still deferred. | Confirmed |
| `MediaBackdrop` token deferred | `Theme.MediaBackdrop` already exists and is used. What remains deferred is per-theme palette/media-backdrop variation. | Claim is imprecise |
| Full disposition table is in design spec addendum | The design spec has the older plan-vs-code addendum and Task 9 implementation record; no disposition table for this response was found. | Not found |

---

## 3. Findings

### R1. F2 is still open: preset defaults do not participate in effective behavior

Severity: P1

Current code:

- `src/PiPlay/Theme/ThemePreferenceResolver.cs:19-26`

```csharp
public static bool StripAutoHide(ThemeSettings? theme, PlayerSettings player) =>
    theme?.StripAutoHide ?? player.StripAutoHide;

public static double ActiveWindowOpacity(ThemeSettings? theme, PlayerSettings player) =>
    WindowOpacityPolicy.Normalize(theme?.ActiveWindowOpacity ?? player.ConstantWindowOpacity);

public static double IdleWindowOpacity(ThemeSettings? theme, PlayerSettings player) =>
    WindowOpacityPolicy.Normalize(theme?.IdleWindowOpacity ?? player.IdleWindowOpacity);
```

`ThemeCatalog` defines soft-glass behavior defaults:

- `src/PiPlay/Theme/ThemeCatalog.cs:44-51`

But the resolver does not use them. A settings file like:

```json
{
  "theme": {
    "themeId": "soft-glass",
    "accentColor": "#A78BFA",
    "fadeDelayPreset": "normal"
  }
}
```

will not produce soft-glass opacity if `PlayerSettings` is still at the default `1.0/1.0`.

Recommended fix:

```csharp
var preset = ThemeCatalog.PresetFor(theme?.ThemeId);
var strip = theme?.StripAutoHide ?? preset.DefaultStripAutoHide;
var active = theme?.ActiveWindowOpacity ?? preset.DefaultActiveWindowOpacity;
var idle = theme?.IdleWindowOpacity ?? preset.DefaultIdleWindowOpacity;
```

Then normalize the resolved value.

Tests to add:

- `ThemePreferenceResolver` with `ThemeId = "soft-glass"` and null override fields returns `false`, `0.92`, `0.78` from the preset.
- Explicit override fields still win over preset defaults.
- Invalid theme ID falls back to the default preset's behavior.

---

### R2. The claimed schema 3 migration does not exist

Severity: P1

Current schema:

- `src/PiPlay/Models/AppSettings.cs:14`

```csharp
public const int CurrentSchemaVersion = 2;
```

Current sanitize logic:

- `src/PiPlay/Services/SettingsService.cs:188-210`

There is no schema 3 bump, no schema <=2 branch, and no one-time backfill for theme blocks whose nullable behavior fields meant "use Player" under schema 2.

Why this matters:

If F2 is fixed naively, existing schema 2 files with a `theme` block and null behavior fields may change effective behavior from player-owned values to preset-owned values. The quoted response correctly identifies this semantic break, but the code does not implement the mitigation.

Recommended fix:

1. Bump `AppSettings.CurrentSchemaVersion` to `3`.
2. During load, capture the original schema version before sanitize.
3. If `schemaVersion <= 2` and a theme block exists, convert null behavior fields into explicit overrides from `PlayerSettings`.
4. Then normalize and set schema version to 3.

Important distinction:

- Missing theme block: seed from legacy player settings.
- Schema <=2 theme block with null behavior fields: backfill behavior overrides from player settings because old null meant "use Player".
- New schema 3 null behavior fields: use preset defaults.

Tests to add:

- Schema 2 file with `theme.themeId = "soft-glass"` and player opacity `0.82/0.44` loads with theme override opacity `0.82/0.44`.
- Schema 3 file with the same theme and null override opacity resolves to soft-glass preset opacity.
- Saving upgrades schema to 3 without dropping unknown JSON extension data.
- `LoadReadOnly` follows the same effective migration semantics without writing to disk.

---

### R3. `FromLegacy` does not copy behavior values as explicit overrides

Severity: P1

Current code:

- `src/PiPlay/Models/AppSettings.cs:105-109`

```csharp
public static ThemeSettings FromLegacy(PlayerSettings player) => new()
{
    AccentColor = ThemeCatalog.AccentColorForLegacyAccent(player.PinAccent),
    FadeDelayPreset = ThemeCatalog.FadeDelayPresetForMilliseconds(player.FadeIdleDelayMs),
};
```

It does not copy:

- `player.StripAutoHide`
- `player.ConstantWindowOpacity`
- `player.IdleWindowOpacity`

The existing tests also pin this older behavior:

- `tests/PiPlay.Tests/SettingsServiceTests.cs:170-178`

They assert migrated theme behavior fields are null, then resolver falls through to `PlayerSettings`.

Recommended fix:

If the new model makes null mean "use preset default", `FromLegacy` should probably become:

```csharp
public static ThemeSettings FromLegacy(PlayerSettings player) => new()
{
    AccentColor = ThemeCatalog.AccentColorForLegacyAccent(player.PinAccent),
    FadeDelayPreset = ThemeCatalog.FadeDelayPresetForMilliseconds(player.FadeIdleDelayMs),
    StripAutoHide = player.StripAutoHide,
    ActiveWindowOpacity = WindowOpacityPolicy.Normalize(player.ConstantWindowOpacity),
    IdleWindowOpacity = WindowOpacityPolicy.Normalize(player.IdleWindowOpacity),
};
```

Or equivalent, after deciding whether fade delay remains an always-explicit value or also becomes a nullable preset override.

Tests to update:

- Replace the old null assertions with explicit override assertions for migrated legacy behavior.
- Keep a separate test proving old files preserve the configured look after the schema upgrade.

---

### R4. Preset-click accent preservation is not present because there is no preset-click path

Severity: P2

Search results:

- No `ThemePreset_Click`
- No theme preset selector in `SettingsWindow.xaml`
- No code path that compares current accent to the previous theme default

Current Settings path still passes and edits legacy player appearance fields:

- `src/PiPlay/MainWindow.xaml.cs:479-487`
- `src/PiPlay/MainWindow.xaml.cs:557-573`
- `src/PiPlay/SettingsWindow.xaml.cs:23-31`

The old separate Pin/Fade color controls are still present:

- `src/PiPlay/SettingsWindow.xaml:119-156`

Recommended fix:

When Task 10 lands the theme selector, implement the preservation rule as a pure helper first:

```csharp
public static string AccentForThemeSwitch(
    string currentAccent,
    ThemePreset previousPreset,
    ThemePreset nextPreset)
{
    return ThemeCatalog.NormalizeAccentColor(currentAccent) ==
           ThemeCatalog.NormalizeAccentColor(previousPreset.DefaultAccentColor)
        ? nextPreset.DefaultAccentColor
        : currentAccent;
}
```

Tests to add:

- Sharp Dark default accent -> Soft Glass adopts Soft Glass default accent.
- Sharp Dark custom amber -> Soft Glass preserves amber.
- Invalid/custom lowercase accent is normalized before comparison.

---

### R5. Nullable theme fields are not documented as overrides

Severity: P3

Current `ThemeSettings` has nullable behavior fields but no comments explaining their semantics:

- `src/PiPlay/Models/AppSettings.cs:93-100`

This matters more after F2 changes because null will no longer mean "use PlayerSettings"; it will mean "use preset default" for schema 3 files.

Recommended fix:

Add XML comments or rename in a future schema:

```csharp
/// <summary>
/// Optional override for the selected theme's strip auto-hide default.
/// Null means use the selected theme preset default in schema 3+ settings.
/// </summary>
public bool? StripAutoHide { get; set; }
```

Because names are persisted in `settings.json`, keeping the current property names is reasonable for back-compat. The comments and migration tests are the important part.

---

### R6. The design spec does not contain the claimed disposition table

Severity: P2

The design spec contains:

- `Review addendum - plan-vs-code findings (2026-06-10)`
- `Task 9 implementation record (2026-06-11)`

See:

- `docs/superpowers/specs/2026-06-10-ui-overhaul-stabilization-design.md`

I did not find a disposition table for the quoted response, nor entries for:

- F2 implementation disposition
- schema 3 migration disposition
- custom accent preservation disposition
- nullable override documentation disposition

Recommended fix:

Add a short dated addendum, for example:

```markdown
## Review addendum - theme follow-up disposition (2026-06-11)

| Review item | Disposition | Code/test evidence |
|---|---|---|
| F2 preset defaults | Implemented / deferred / rejected | file:line + tests |
```

Do not mark an item implemented until the code and tests are in the same branch.

---

### R7. Deferral list is partly accurate but one item is misstated

Severity: P3

Confirmed still deferred:

- generated Settings chips
- `media-glow`
- color wheel

But `Theme.MediaBackdrop` is not deferred as a token. It already exists and is used:

- `src/PiPlay/Theme/Colors.xaml:28`
- `src/PiPlay/Theme/Colors.xaml:57`
- `src/PiPlay/MainWindow.xaml:130`
- `src/PiPlay/MainWindow.xaml:151`
- `src/PiPlay/PlayerWindow.xaml:11`

What remains deferred is per-theme palette ownership of `Theme.MediaBackdrop`, not the token itself.

Recommended wording:

```text
Already deferred: generated chips, per-theme MediaBackdrop palette variation, media-glow, color wheel.
```

---

## 4. Still-Open Issues From The Previous Review

The quoted response addresses only a subset of the earlier review and does not affect these larger gaps in the current checkout:

- `ThemeSettings` is still not the normal Settings/runtime path; Settings edits `PlayerSettings`.
- Visible primary controls still use fixed `AccentCyan` resources rather than `Theme.Accent*`.
- Theme presets still do not own palettes.
- Radius and DWM corner mode remain staged/unimplemented.
- Startup resource application is still one-shot and not reapplied from Settings.

These remain the higher-order theme system risks.

---

## 5. Recommended Next Implementation Slice

Do not start with UI chips or color wheel. First make the semantics real and testable:

1. Add effective theme behavior resolution that uses preset defaults.
2. Add schema 3 migration so old theme-block nulls preserve current appearance.
3. Update `FromLegacy` to copy behavior values as explicit overrides.
4. Add tests for schema 2, schema 3, missing theme block, explicit overrides, and invalid theme ID.
5. Add XML comments for nullable override fields.
6. Only then add the Settings preset-click path and custom-accent preservation helper.
7. Record the disposition table in the design spec with code/test evidence.

Suggested minimum test names:

```text
Soft_glass_theme_id_without_overrides_uses_preset_opacity_for_schema3
Schema2_theme_block_null_behavior_fields_backfill_from_player_values
Missing_theme_block_seeds_legacy_behavior_as_explicit_overrides
Theme_behavior_overrides_win_over_preset_defaults
Theme_switch_adopts_default_accent_only_when_current_accent_was_previous_default
```

---

## 6. Bottom Line

The response is directionally reasonable, but it is not implemented in the current checkout. F2 remains open, the schema-3 compatibility story is absent, accent preservation cannot exist without a theme selector path, override semantics are not documented, and the claimed disposition table is not in the checked-in design spec.

Treat this as a failed evidence check, not as a completed implementation review. The next pass should land code, tests, and the spec addendum together.
