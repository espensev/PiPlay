# Profile color identity and accent fill - implementation plan

**Spec:** `docs/superpowers/specs/2026-06-25-profile-color-identity-and-accent-fill-design.md`

**Goal:** make profile colors visible without hijacking the app accent, make app accent buttons filled, and accept any valid RGB color.

## Tasks

- [x] **Task 1 - Inspect current contracts.**
  - Confirm active profile colors currently override `ResolvedAccentColor`.
  - Confirm `AccentButton` currently uses dark fill plus accent border.
  - Confirm free colors are blocked by `AccentReadabilityPolicy`.

- [x] **Task 2 - Widen color acceptance.**
  - Change `AccentReadabilityPolicy` to accept any valid hex.
  - Change nearest-readable/fallback behavior to default only invalid hex.
  - Make `PickReadableForeground` return the best dark/white foreground instead of throwing.
  - Update picker warning/copy and profile validation tests.

- [x] **Task 3 - Fill primary accent buttons.**
  - Make `AccentButton` fill with `AccentPrimary`, `AccentHover`, and `AccentPressed`.
  - Use `OnAccent` / `OnAccentPressed` foreground resources.
  - Bind nested Popout action text/icon foreground to the parent button foreground.
  - Update style/runtime tests.

- [x] **Task 4 - Reverse profile color behavior.**
  - Make `ResolvedAccentColor` equal the global app accent.
  - Make `CommitAccent` write global theme accent.
  - Make Settings always edit global accent.
  - Keep profile color edits in the profile editor only.

- [x] **Task 5 - Make profile color visible.**
  - Fill the profile selector name chip with the profile color.
  - Add foreground conversion so chip text remains as readable as possible.
  - Update markup/runtime tests.

- [x] **Task 6 - Docs and validation.**
  - Update changelog and any review status notes.
  - Run focused UI/theme/profile tests.
  - Run full tests and `git diff --check`.

## Validation

- `dotnet test PiPlay.sln --configuration Debug --filter 'FullyQualifiedName~AccentReadabilityPolicyTests|FullyQualifiedName~ThemeColorsTests|FullyQualifiedName~ProfileServiceTests|FullyQualifiedName~ProfileAccentServiceTests|FullyQualifiedName~MainWindowProfileAccentTests|FullyQualifiedName~XamlInvariantTests|FullyQualifiedName~WpfRuntimeTests|FullyQualifiedName~AccentColorPickerTests|FullyQualifiedName~SettingsWindowAppearanceTests|FullyQualifiedName~SettingsServiceTests' --nologo`: passed, 238 tests.
- `dotnet test PiPlay.sln --configuration Debug --nologo`: passed, 682 tests.
- `git diff --check`: passed. PowerShell/Git reported LF-to-CRLF normalization warnings for touched files.

## Deferred follow-ups

- Optional active-profile popout border.
- Richer active-profile chip/control.
- Large rounded-card WebView2/corner architecture.
