# PR 25 Theme Accent Audit

Date: 2026-06-17
Repository: `espensev/PiPlay`
PR: https://github.com/espensev/PiPlay/pull/25
Reviewed scope:

- Isolated PR delta: `0622347..ee462d5`
- Current local main: `14f946e18bb24ca72ffddac07956a38133b6fd51`
- GitHub merge commit: `d72fb90d599b056848d8b880cba6a2874a3f06d7`

## Verdict

PR 25 correctly moved the accent token model forward: `ThemeResourceApplier` derives and applies
accent state tokens, `Colors.xaml` seeds match the default derived set, and the unit/WPF gate is
green. I found one still-actionable visual/runtime issue in the first real `AccentButton` consumer:
the new foreground tokens are set on the `Button`, but nested `TextBlock` content in the main
pop-out button can keep the implicit `TextBlock` foreground instead of rendering `OnAccent` /
`OnAccentPressed`.

## Findings

### P2 - `AccentButton` foreground does not reliably reach nested text content

Evidence:

- `src/PiPlay/Theme/ControlStyles.xaml:19` defines an implicit `TextBlock` style with
  `Foreground="{DynamicResource TextPrimary}"`.
- `src/PiPlay/Theme/ControlStyles.xaml:61` defines `AccentButton`.
- `src/PiPlay/Theme/ControlStyles.xaml:63` sets `Button.Foreground` to `{DynamicResource OnAccent}`.
- `src/PiPlay/Theme/ControlStyles.xaml:81` changes `Button.Foreground` to
  `{DynamicResource OnAccentPressed}` while pressed.
- `src/PiPlay/Theme/ControlStyles.xaml:70` uses a plain `ContentPresenter` without
  `TextElement.Foreground="{TemplateBinding Foreground}"`.
- `src/PiPlay/MainWindow.xaml:117` applies `AccentButton` to `PopOutButton`.
- `src/PiPlay/MainWindow.xaml:121` and `src/PiPlay/MainWindow.xaml:124` put explicit nested
  `TextBlock`s inside that button.

Why it matters:

The PR's contrast fix is meant to make the primary action text/icons follow `OnAccent` and
`OnAccentPressed`. For nested visual content, the implicit `TextBlock` foreground can win over
inherited button foreground, so the main pop-out button may continue rendering `TextPrimary`
instead of the derived foreground. That is especially visible for dim accents where the pressed
state intentionally flips to white.

Coverage gap:

- `tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs:331` to `:345` verifies `btn.Foreground` on a synthetic
  `Button`, but does not realize nested `TextBlock` content or inspect `PopOutButtonIcon` /
  `PopOutButtonText`.
- Existing tests around `PopOutButton` verify naming/type/label state, not foreground propagation.

Recommended fix:

- Add `TextElement.Foreground="{TemplateBinding Foreground}"` to the `AccentButton` template's
  `ContentPresenter`, matching the pattern already used by the combo-box selection presenter at
  `src/PiPlay/Theme/ControlStyles.xaml:392`.
- Add a WPF runtime regression test that realizes either:
  - `PopOutButton` and verifies `PopOutButtonIcon` / `PopOutButtonText` inherit the accent
    foreground, or
  - a synthetic `AccentButton` containing nested `TextBlock`s.

This matches the inline Codex PR review comment on
`src/PiPlay/Theme/ControlStyles.xaml` line 81.

## Audit Notes

- The PR's derived-token implementation is otherwise coherent. `ThemeResourceApplier` now applies
  `AccentPrimary`, `AccentHover`, `AccentPressed`, `AccentBorder`, `OnAccent`, `OnAccentPressed`,
  and companion `*Color` entries together.
- The default design-time seeds in `Colors.xaml` are pinned to `ThemeColors.DeriveAccentSet`, which
  is the right drift guard for fresh-launch/pre-apply visuals.
- `PickReadableForeground` intentionally fails closed for a valid mid-tone hex where neither dark
  nor white reaches 4.5:1. Today the Settings UI only exposes safe catalog chips, so I do not treat
  this as a PR-25 blocker. Before a color wheel or arbitrary accent import ships, settings load/save
  should reject or repair unreadable valid hex values instead of letting live apply throw.

## Verification

- GitHub PR checks: all passing.
  - `Build and test (Windows)` pass
  - `Require design spec` pass
- Local verification:
  - `dotnet test PiPlay.sln --configuration Debug`
  - Result: `617` passed, `0` failed, `0` skipped.
- Working tree before this report already had one untracked file:
  `PHASE1_HUMAN_QA_AND_SIGNING_HANDOFF.md`.
