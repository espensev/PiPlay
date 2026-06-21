# Popout button rendering fix - design

## Goals

Fix the Source Window `Pop out video` action text so the icon and label render legibly in the accent button after the Theme V2 density/accent changes. The fix is intentionally narrow: preserve the current command, tooltip, accessible name, profile/theme behavior, and popout lifecycle while correcting text rendering and height budget for the existing button.

## Requirements served

- `REQ-UI-01`: visual chrome remains readable and intentionally themed.
- `REQ-UI-02`: icon/text rendering does not degrade into malformed or unreadable chrome.
- `Q-7`: native-quality window chrome remains crisp across density/DPI-sensitive layouts.

## Acceptance criteria

- The `Pop out video` icon and label use pixel-aligned WPF text rendering on their actual nested `TextBlock` elements.
- The shared `AccentButton` style carries the same text-rendering options so nested content has a consistent default.
- The `PopOutButton` vertical margin leaves enough room in the 50 DIP toolbar row for the largest configured theme control height.
- Regression tests cover both markup invariants and runtime WPF property resolution.
- Full deterministic tests and the non-mutating Release build gate pass.

## Settled decisions

1. Apply text-rendering options directly to `PopOutButtonIcon` and `PopOutButtonText`.
   The accent button template hosts nested visual content, so setting the options only on the outer style can be bypassed by the nested elements.
2. Also set text-rendering options on `AccentButton` and its `ContentPresenter`.
   This keeps the shared style aligned with the control-specific fix and protects similar nested accent content.
3. Reduce the PopOutButton vertical margin from 9 DIP to 6 DIP.
   This preserves the 50 DIP toolbar row and gives the largest theme density enough vertical budget without redesigning the toolbar.
4. Keep this as a targeted regression fix instead of a broader theme/chrome redesign.
   The behavior, text, command wiring, and release channel are unchanged.

## Non-goals / out of scope

- No new theme preset or accent behavior.
- No Source Window toolbar redesign.
- No popout lifecycle, navigation, WebView2, or profile behavior changes.
- No release version bump in this pass.

## Testing approach

- Markup tests assert the `AccentButton` style, its `ContentPresenter`, and both named PopOutButton text elements carry the required `TextOptions`.
- Markup tests assert the PopOutButton height budget fits the maximum configured theme control height.
- WPF runtime tests construct `MainWindow` and assert the nested PopOutButton text elements resolve to `Display`, `Fixed`, and `Grayscale`.
- Full repo verification uses `dotnet test PiPlay.sln --configuration Debug`, `Build-PiPlay.ps1 -Stage Build -NoVersionBump -NoBuildNumberBump`, `git diff --check`, and the local spec preflight.

## Changes by file

| File | Change |
|---|---|
| `src/PiPlay/MainWindow.xaml` | Adjust PopOutButton vertical margin and apply text-rendering options to `PopOutButtonIcon` / `PopOutButtonText`. |
| `src/PiPlay/Theme/ControlStyles.xaml` | Apply text-rendering options to `AccentButton` and its `ContentPresenter`. |
| `tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs` | Add runtime coverage for nested PopOutButton text rendering options. |
| `tests/PiPlay.Tests/Ui/XamlInvariantTests.cs` | Add markup coverage for accent-button rendering options and PopOutButton height budget. |
| `docs/CHANGELOG.md` | Add the user-visible fix under `Unreleased`. |

## Docs & changelog impact

`docs/CHANGELOG.md` gets an `Unreleased` fix note because this is a visible chrome rendering correction. No ADR or product-spec change is required.

## Unresolved decisions

None.
