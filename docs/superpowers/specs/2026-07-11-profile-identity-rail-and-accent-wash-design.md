# Profile identity rail and Source Window accent wash - design

## Goals

Address the accepted 2026-07-11 profile-selector review and the owner's follow-up direction:

- Every valid dark profile color and global app accent must produce a visible UI cue.
- Profile identity must read as one compact marker, not a filled chip or a colored control inside
  another control.
- The global app accent must reach beyond isolated buttons through a persistent, non-interactive
  Source Window chrome treatment.
- Persisted `#RRGGBB` values, the global-accent/profile-identity split, profile navigation, and
  profile settings behavior remain unchanged.

## Requirements served

- `REQ-UI-01`: visible chrome is intentionally themed and responds visibly to the app accent.
- `REQ-PROFILE-01`: profile color remains scoped identity metadata and never replaces the global
  app accent.
- Product spec section 20: non-text color cues remain discernible on the dark UI.
- Accepted findings in `docs/reviews/review-2026-07-11-profile-selector-frame.md`.

## Acceptance criteria

- Any valid profile/global `#RRGGBB` value remains accepted, normalized, and persisted unchanged.
- A presentation-only contrast policy preserves a requested color when it already reaches `3.0:1`
  against the active theme's `SurfaceHover`; otherwise it minimally mixes the color toward the
  higher-contrast black or white pole until it reaches `3.0:1`.
- `AccentPrimary` and its derived interactive states use the contrast-safe presentation color, so
  a dark global accent remains visible on buttons, toggles, selection, and chrome.
- A subtle `AccentShellTint` colors the left side of the Source Window title bar and fades to the
  normal `SurfaceBase` before the caption buttons. It is decorative, does not affect layout or hit
  testing, and reaches at least `1.20:1` against `SurfaceBase`.
- The profile selector has no selected-profile-colored outer frame and no filled profile-name chip.
- One 4-DIP leading profile rail is shared by the closed selected value and popup rows through the
  existing item template.
- Each valid profile color is rendered through the same `3.0:1` contrast policy against the live
  `SurfaceHover` resource.
- A null profile color leaves the rail transparent while retaining its alignment gutter. No selected
  profile retains the normal `Profiles` placeholder.
- Theme changes re-evaluate both global presentation tokens and already-realized profile rails.
- Profile selection changes neither the global accent nor the title-bar wash.
- Automated coverage includes surface-equal colors from all three presets, a passing bright color,
  null profile color, live surface replacement, and global/profile independence.
- The completed June 25 design and plan remain historical records; this dated pass owns the later
  selector correction and contrast/accent-wash work.

## Settled decisions

1. Global accent and profile identity remain separate.
   The global accent owns actions and the title-bar wash. Profile color owns only the profile rail.
2. The Source Window treatment is a restrained title-bar gradient.
   A wash carries color farther into the shell without creating another button, a toolbar separator,
   or an outer border that would regress the P1 borderless direction.
3. Profile identity uses one leading rail.
   The shared item template naturally renders it in both the closed selection and popup rows without
   layered-control styling.
4. Contrast correction is presentation-only.
   Stored colors remain exact. `ThemeColors.EnsureContrast` supplies the smallest necessary display
   adjustment for app-accent tokens and the profile-rail converter.
5. `SurfaceHover` is the contrast reference.
   It is the lightest relevant surface in the shipped dark themes, so a color that reaches 3:1 there
   also remains discernible on `SurfaceRaised`, `SurfaceBase`, and `AppBackground`.
6. Null profile color means no colored rail.
   The fixed gutter remains for row alignment; no fallback accent is invented.
7. Completed change-pass records are historical evidence.
   The June 25 files retain their original filled-chip decision and validation counts. Later
   corrections live in this July 11 record.

## Non-goals / out of scope

- No profile color override of `AccentPrimary` or other global app-accent resources.
- No filled profile-name chip, colored ComboBox frame, full title-bar fill, or new separator line.
- No active-profile Popout Player border.
- No persistence of derived presentation colors.
- No new theme preset, WebView2 hosting change, outer-window border, or corner architecture change.
- No release publish in this implementation pass; deployed-Stable visual QA remains the
  release-candidate lane.

## Testing approach

- Logic tests cover dark global accents across all presets, passing-color preservation, and the
  adaptive shell tint.
- Markup tests assert the title-bar gradient, neutral ComboBox frame, one 4-DIP rail, live
  `SurfaceHover` input, and absence of filled/nested identity controls.
- WPF tests assert rail contrast, null behavior, live surface re-resolution, and imperative
  Pin/Fade/PinnedHint use of the same safe presentation color.
- Main-window tests assert profile selection cannot change the global accent or shell tint.
- New or changed tests reference `REQ-PROFILE-01` and/or `REQ-UI-01`.
- Run focused logic/markup/WPF tests, the full Debug suite, the non-mutating build,
  `git diff --check`, and `scripts/Preflight-SpecGate.ps1`.

## Changes by file

| File | Change |
|---|---|
| `src/PiPlay/Theme/ThemeColors.cs` | Add contrast derivation and the adaptive shell tint. |
| `src/PiPlay/Theme/ThemeResourceApplier.cs` | Publish live contrast-safe accent/tint resources. |
| `src/PiPlay/Theme/Colors.xaml` | Add matching design-time shell-tint seed resources. |
| `src/PiPlay/Theme/ContrastBrushConverter.cs` | Convert profile color plus the live surface brush into a frozen contrast-safe brush. |
| `src/PiPlay/MainWindow.xaml` | Add the title-bar wash and use one contrast-safe profile rail. |
| `src/PiPlay/Theme/ControlStyles.xaml` | Remove the selected-profile ComboBox frame and register the converter. |
| `src/PiPlay/MainWindow.xaml.cs` | Keep imperative Source Window cues on the safe presentation color. |
| `src/PiPlay/PlayerWindow.xaml.cs` | Keep Popout Player Pin/Fade cues on the safe presentation color. |
| `src/PiPlay/Models/Profile.cs` | Replace obsolete profile-chip terminology. |
| UI/theme/profile tests | Cover contrast, live resources, one-rail geometry, and global/profile independence. |
| Current product/docs records | Restore June history and describe the July 11 behavior. |

## Docs & changelog impact

- Restore the June 25 design and plan to their pre-July-3 historical contents.
- Update the normative profile-color description and current implementation summary.
- Replace the Unreleased selector-frame entry with the contrast-safe rail/title-wash behavior.
- Keep historical release notes and earlier owner-roadmap documents unchanged.

## Unresolved decisions

- Deployed-Stable visual QA must confirm the gradient remains a restrained wash across all themes
  and DPI scales; that release-candidate evidence is deliberately not created from repo build output.
