# Accent reach default and profile-routing fixes — design

## Goals

Make the new reach preference preserve the deployed v0.9.0 appearance at its default, and repair the
Settings commit boundary so profile-owned and global accents cannot overwrite each other. Preserve the
0–100 control, exact stored RGB values, profile-driven P2 behavior, and the existing preset-default rule
for global accents.

This follow-up is grounded in the owner's earlier direction that profile selection must visibly re-tint
PiPlay and that custom colors survive preset changes. Those decisions remain current context; they are
not reopened merely because the bug was found in a later session.

## Requirements served

- `REQ-PROFILE-01` — a profile-owned accent overrides but never destroys the global fallback.
- `REQ-UI-01` — the default reach preserves the shipped, contrast-safe P2 presentation.
- Product spec §17 — truthful profile/global accent editing and exact stored colors.

## Acceptance criteria

- Default reach 50 produces the v0.9.0 Sharp Dark defaults exactly: full `#2BAED0` presentation glyphs
  and title wash `#12343F` (the 1.45 target).
- The wash remains linear from no wash at 0, through 1.45 at 50, to the restrained 1.90 ceiling at 100.
- Glyph reach runs from neutral at 0 to full accent at 50 and remains full through 100.
- With a colored profile active, Settings → Done writes the effective accent only to that profile and
  leaves `Theme.AccentColor` unchanged, even when another Appearance control triggered the save.
- A full theme repaint after Done still renders the active profile accent in `AccentPrimary`,
  `AccentChromeGlyph`, and `AccentShellTint`.
- A preset switch never rewrites a profile-owned accent, even when it equals the previous preset default;
  the existing global-default adoption rule remains unchanged.
- Current-code docs describe the new resources, persistence field, routing, and superseded history with
  no live identity-only/global-split contradiction.

## Settled decisions

1. Keep the wash's existing `intensity / 100` scalar, because that already anchors 50 at the shipped 1.45.
2. Give glyphs a separate `min(intensity / 50, 1)` scalar, so the midpoint reproduces v0.9.0 and the
   upper half controls only wash depth.
3. Route the Settings payload through `ProfileAccentService.CommitAccent` inside the pure settings
   writer, replacing the unconditional global assignment and removing the duplicate MainWindow commit.
4. After applying the full preset palette, explicitly reapply `ResolvedAccentColor`; a full theme apply
   derives from the global theme block and cannot by itself know the active profile override.
5. Pass accent ownership to Settings as behavior, not by parsing the user-facing context string. Apply
   `AccentForThemeSwitch` only to the global value: a profile target stays exact, while an untouched
   hidden global fallback may still advance to the new preset default.
6. Preserve completed v0.9.0 design records as history. This pass gets its own dated spec and plan, while
   current normative docs are folded forward.

## Non-goals / out of scope

- No new reach endpoints, easing curve, borders, fills, caption-row accents, or Popout chrome treatment.
- No settings schema bump; `theme.accentIntensity` already defaults and normalizes safely.
- No release publish, version bump, tag, or deployed-Stable visual signoff in this implementation pass.

## Testing approach

- Logic: literal default-color/wash compatibility, split-curve anchors, all-intensity contrast, and the
  profile-aware `ThemeSettingsWriter` transaction.
- WPF: the exact post-Settings apply seam keeps the global fallback while repainting dynamic resources
  from the active profile; profile-owned preset switching preserves the stored color.
- Markup/runtime resources: startup seeds and `AccentChromeGlyph` brush/color companion stay aligned.
- Final gate: full Debug suite, non-mutating build, spec preflight, and `git diff --check`. Deployed visual
  QA remains a later Stable-copy gate under `UI-CHK-10`/`UI-CHK-11`.

## Changes by file

| File | Change |
|---|---|
| `src/PiPlay/Theme/ThemeColors.cs` | Split glyph reach from the full-range wash scalar. |
| `src/PiPlay/Theme/Colors.xaml` | Seed the default glyph at the v0.9.0 full accent. |
| `src/PiPlay/Theme/ThemeCatalog.cs` | Document the midpoint compatibility contract. |
| `src/PiPlay/Theme/ThemeSettingsWriter.cs` | Route accent commits through the profile-aware owner. |
| `src/PiPlay/MainWindow.xaml.cs` | Remove duplicate commit and restore the resolved accent after full theme apply. |
| `src/PiPlay/SettingsWindow.xaml.cs` | Distinguish global preset-following from profile-owned accent editing. |
| `tests/PiPlay.Tests/**` | Add logic, WPF transaction, resource, and preset-switch regressions. |
| `docs/CHANGELOG.md`, product/QA/theme/ownership docs | Fold forward the user-visible fixes and retire stale live contracts. |

## Docs & changelog impact

Add Unreleased entries for reach compatibility and both accent-routing fixes. Update the product spec and
QA rows as normative behavior; update `Theme_Preset_Differences.md` as the current-code map; mark the older
identity-only direction in `SPEC_GAPS_AND_OWNERSHIP.md` as historical and superseded.

## Unresolved decisions

- None
