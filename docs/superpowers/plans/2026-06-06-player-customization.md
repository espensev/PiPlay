# Popout control customization - implementation plan

**Spec:** `docs/superpowers/specs/2026-06-06-player-customization-design.md`

**Goal:** Let the user customize the active colors for Pin and Fade plus the controls-fade idle delay,
while preserving current defaults and keeping whole-window opacity / click-through transparency out of
this phase.

**Result:** Implemented on `docs/player-customization-spec` after the spec pass. Product code now
contains the fixed swatch palette, persisted Pin/Fade accents, fade-delay presets, Settings UI, and
focused regression coverage.

**Phase 2 landing note:** This slice closes the Pin/Fade customization code path. Phase 2 release
evidence was recorded separately; compact-mode placement was later resolved for Phase 3 in
`docs/superpowers/specs/2026-06-07-compact-player-sweep-design.md`.

## Tasks

- [x] **Task 1 - Model and palette policy.**
  - Add appearance settings under `PlayerSettings`: `PinAccent = "cyan"`, `FadeAccent = "cyan"`,
    and `FadeIdleDelayMs = 2500`.
  - Add a small `PlayerAppearancePolicy` helper that owns allowed accent keys, display names, brush
    resource keys, defaults, and normalization.
  - Allowed accents for this slice: `cyan`, `violet`, `green`, `amber`.
  - Allowed fade-delay values: 1500, 2500, 4000 ms.
  - Keep persistence JSON readable with string keys, not numeric enum values.
  - Update `SettingsService.Sanitize` to repair unknown accent keys and out-of-range delay values.
  - Verified by `SettingsServiceTests` and `PlayerAppearancePolicyTests`.

- [x] **Task 2 - Theme tokens and toggle accent plumbing.**
  - Add `AccentViolet`, `AccentGreen`, and `AccentAmber` to `Theme/Colors.xaml`; keep existing
    `AccentCyan` as the default and keep `DangerPin` reserved for danger / close states.
  - Do not use current `AccentPurple` for toggle customization unless a contrast test proves it meets
    the active-glyph threshold on hover surfaces.
  - Refactor the `PinToggle` style so checked foreground/border can be supplied per control instead
    of hardcoded to `AccentCyan`.
  - Prefer a tiny attached property or equivalent local WPF seam over duplicating one style per color.
  - Apply the configured Pin accent to `MainWindow.PinToggle` and `PlayerWindow.PinToggle`.
  - Apply the configured Fade accent to `PlayerWindow.FadeToggle`.
  - Verified by markup contrast/resource tests and WPF runtime tests.

- [x] **Task 3 - Settings window Appearance section.**
  - Add an `Appearance` section below Privacy in `SettingsWindow`.
  - Use swatches for `Pin color` and `Fade color`, with stable names/tooltips and visible selected
    state. Do not use free-form color text input.
  - Add constrained fade-delay presets: Short = 1500 ms, Normal = 2500 ms, Long = 4000 ms.
    Normal maps to the current `FadePolicy.IdleDelayMs` behavior.
  - Keep `SettingsWindow` input-only: it reports changes to `MainWindow`; `MainWindow` copies them
    into `_settings`, saves through `SettingsService`, and updates any open player/source controls.
  - Verified by targeted WPF runtime tests for construction and selected-value behavior.

- [x] **Task 4 - Fade delay application.**
  - Thread the configured fade idle delay from `MainWindow` into `PlayerWindow`.
  - Keep `FadePolicy` as the pure decision layer; use configured timing for the timer interval, not
    for the hide/show truth table.
  - Preserve the invariant that disabling Fade makes controls always visible immediately.
  - Verified by WPF construction tests that the player accepts configured delay without showing the window.

- [x] **Task 5 - Reset, docs, and manual QA hooks.**
  - Ensure `ApplyResetState` and `SettingsService.Reset` restore default accents and fade delay.
  - Update `docs/CHANGELOG.md` under Phase 2 customization.
  - Update `docs/QA_Checklist.md` with a short visual pass: default colors, alternate Pin color,
    alternate Fade color, and fade after idle at a fractional DPI.
  - Confirm transparency wording stays separate: whole-window opacity remains Phase 4 and click-through
    remains out of scope.
  - Verified by `dotnet test PiPlay.sln --configuration Debug`.

- [x] **Task 6 - Full gate and review.**
  - Run the deterministic gate:

    ```powershell
    dotnet test PiPlay.sln --configuration Debug
    .\Build-PiPlay.ps1 -Stage Build -NoVersionBump -NoBuildNumberBump
    ```

  - Run a review pass focused on settings persistence, WPF style/resource drift, and `Q-8`
    transparency boundaries.
  - If releasing a shareable build, run `scripts/Test-UiSmoke.ps1` and capture manual evidence under
    `docs/evidence/`.
  - Commit or PR summary should call out that whole-window opacity and click-through were not added.
  - Verified: `dotnet test PiPlay.sln --configuration Debug` passed 221/221; `.\Build-PiPlay.ps1
    -Stage Build -NoVersionBump -NoBuildNumberBump` passed with 0 warnings / 0 errors.

## Self-review

- Requirements -> tasks: model/persistence in Task 1; active-color behavior in Tasks 2 and 3;
  fade-delay behavior in Task 4; reset/docs/QA in Task 5; deterministic validation in Task 6.
- Ownership: settings persistence stays in `SettingsService`; input surface stays in `SettingsWindow`;
  Source Window policy and saving stay in `MainWindow`; Popout Player visuals stay in `PlayerWindow`;
  fade truth-table logic stays in `FadePolicy`.
- Risk: highest risk is WPF styling drift and confusing Fade with opacity. Markup/WPF tests cover
  resource/style behavior; docs and review keep opacity/click-through separate.
- Verified: full implementation pass; `dotnet test PiPlay.sln --configuration Debug` passed 221/221
  and `.\Build-PiPlay.ps1 -Stage Build -NoVersionBump -NoBuildNumberBump` passed with 0 warnings /
  0 errors.
