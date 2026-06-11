# PiPlay theme claim disposition post-merge review

Date: 2026-06-11
Scope: current `main` after PR #19 merge/pull, plus the pasted claim-by-claim disposition.
Status: verified. The disposition is accurate for the merged tree; the older root review notes are historical audits of a superseded draft.

## Bottom line

The pasted disposition is now the right interpretation of the theme pass. The earlier negative review in `historical-claim-response-review.md` was accurate for the dead draft it inspected, but it is no longer accurate for current `main`.

The strongest current claim is:

- PR #18 supplied the real Settings theme UI and runtime theme/accent plumbing.
- PR #19 supplied theme-owned radii, per-preset palettes, DWM corner ownership, preset behavior adoption, and the schema/resolver fixes.
- The addendum in `docs/superpowers/specs/2026-06-11-theme-corners-and-palettes-design.md` now records the disposition with evidence pointers.
- The remaining items are explicitly deferred design work, not missing pieces from the accepted pass.

Post-merge validation already run in this thread: `dotnet test` passed all 551 tests.

## Review of the pasted claim table

| Claim | Current verdict | Evidence |
| --- | --- | --- |
| R1 - resolver ignores preset defaults | Implemented. The resolver now falls back to preset defaults for strip auto-hide and both opacity levels when the theme block exists but the override is null. | `src/PiPlay/Theme/ThemePreferenceResolver.cs:26-39`; `tests/PiPlay.Tests/ThemePreferenceResolverTests.cs:15-64` |
| R2 - no schema 3 migration | Implemented. `CurrentSchemaVersion` is `3`, and schema `< 3` theme nulls are backfilled from the legacy Player fields once during sanitize. | `src/PiPlay/Models/AppSettings.cs:19`; `src/PiPlay/Services/SettingsService.cs:161-195`; `tests/PiPlay.Tests/SettingsServiceTests.cs:176-211` |
| R3 - `FromLegacy` does not copy behavior | Implemented. Legacy strip/active opacity/idle opacity are copied into explicit theme overrides during legacy seeding. | `src/PiPlay/Models/AppSettings.cs:128-137`; `tests/PiPlay.Tests/ThemePreferenceResolverTests.cs:68-85` |
| R4 - no preset-click path | Implemented in the merged lineage. Settings has preset buttons, `ThemePreset_Click`, and a pure `ThemeCatalog.AccentForThemeSwitch` helper so default accents adopt while custom accents survive. | `src/PiPlay/SettingsWindow.xaml:140-146`; `src/PiPlay/SettingsWindow.xaml.cs:128-151`; `src/PiPlay/Theme/ThemeCatalog.cs:244`; `tests/PiPlay.Tests/ThemeCatalogTests.cs:155`; `tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs:264-291` |
| R5 - overrides undocumented | Implemented. Nullable theme behavior fields have XML docs explaining override semantics while keeping JSON-compatible names. | `src/PiPlay/Models/AppSettings.cs:112-123` |
| R6 - disposition table missing | Implemented. The design spec addendum records the stale-draft audit, current fixes, evidence pointers, and deferred items. | `docs/superpowers/specs/2026-06-11-theme-corners-and-palettes-design.md:102-139` |
| R7 - MediaBackdrop misstated | The corrected disposition is right for current main. There is no active `MediaBackdrop` token in `src/PiPlay`; current media surfaces are still literal black, so the token/palette work is genuinely deferred. | `rg MediaBackdrop src tests` returns no source token; only the spec/review notes mention it. See spec line `138`. |
| Section 4 still-open list | Resolved by PR #18 plus PR #19 for the current pass. The old list was observing the dead draft tree, not merged main. | Runtime effective settings flow in `src/PiPlay/MainWindow.xaml.cs:487-633`; theme resource application in `src/PiPlay/Theme/ThemeResourceApplier.cs:27-46`; WPF tests around preset/default/runtime plumbing. |

## Important correction to the wording

The disposition should stop saying only "verified against the PR branch" now that PR #19 is merged. The safer wording is:

> Verified against current `main` after PR #19 merge/pull.

That matters because this repo now has multiple root-level review artifacts:

- `source-review-and-variants.md` - external/source review input.
- `historical-draft-end-pass-review.md` - historical audit of the old draft checkout.
- `historical-claim-response-review.md` - historical claim response against the old draft checkout.
- `post-merge-disposition-review.md` - this current-state review after merge.

Future reviewers should use this file and the design spec addendum for the current tree. The older two review files should be read as "why the old draft failed," not as "what current main still lacks."

## Technical notes

### Resolver and migration semantics are now coherent

The current model finally has one consistent meaning for nullable behavior fields:

- Existing legacy files without a theme block are seeded from `PlayerSettings` using `ThemeSettings.FromLegacy`.
- Schema `< 3` files with a theme block but null behavior fields are backfilled from `PlayerSettings` once, because those nulls previously meant "use Player."
- Schema `3` nulls mean "use preset default."
- Raw `PlayerSettings` fallback remains only for the defensive null-theme path.

That is the right split. It preserves old user looks while allowing a hand-edited `"themeId": "soft-glass"` with null overrides to actually become translucent.

### Preset switching now has the right accent rule

The pure helper `AccentForThemeSwitch` is the important design choice. It prevents a preset click from smashing a custom accent, while still letting theme-default users follow the new preset's default accent.

That is better than a blanket "preset click adopts everything" rule because accent is user identity/customization data, while fade/strip/opacity/corners are preset behavior defaults for this pass.

### Runtime paths now use effective theme settings

The old high-risk issue was tests proving resolver behavior while the app bypassed the resolver. Current `MainWindow` now feeds settings dialogs, player appearance, opacity, popout preferences, and corner mode from effective theme values. That closes the test-vs-runtime split that made the earlier draft risky.

Key evidence:

- `SettingsButton_Click` passes effective values into `SettingsWindow`.
- `ApplyPlayerPreferences` writes theme behavior overrides and reapplies resource/appearance state.
- `EffectivePlayerPreferencesForTests` exposes the resolved tuple for runtime tests.
- New player windows are created with effective accent, fade, opacity, strip, and DWM corner values.

## Still deferred by design

These are not regressions in the current pass:

- Generated Settings chips from catalog data.
- Media backdrop token/per-theme media surface variation.
- Media glow.
- Color wheel.
- Future accent token expansion such as pressed/muted/subtle/border/on-accent.
- Dropping compatibility radius aliases after one migration pass.

## Suggested cleanup before the next external review

1. Tell the reviewer to audit `main` after the PR #19 merge, not the old draft commit or stale local branch.
2. Include this file and the design spec addendum in the review bundle.
3. Either leave the older root review files with clear "historical" status, or rename/archive them so they do not look like current-state verdicts.
4. Keep `BUILD_NUMBER` and `VERSION` publish bumps separate from this theme review work; they are unrelated local edits and should stay untouched by cleanup.

## Final verdict

The pasted claim-by-claim disposition is substantively correct against the merged code. The only material improvement is to anchor it to post-merge `main` and call out the stale-root-review hazard explicitly. The implementation now satisfies R1 through R7 for this pass, and the remaining items are accurately marked as deferred rather than accidentally missing.
