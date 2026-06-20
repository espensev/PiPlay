# Profile accent wheel visual/manual verification

Date: 2026-06-20

Build under test: `src/PiPlay/bin/Debug/net10.0-windows/PiPlay.exe`

Data root: isolated temp `PIPLAY_DATA_ROOT` seeded with active profile `Violet` and profile accent `#A78BFA`.

## Result

PASS

- Active profile `Violet` restored on launch.
- Settings targets the active profile: `Editing accent for profile 'Violet'.`
- Entering unreadable `#787878` disables `Done`.
- The readability warning and `Use nearest readable` action are visible without scrolling.
- `Use nearest readable` repairs the value to `#828282`.
- `Done` applies the repaired per-profile accent; saved `Violet.accentColor` is `#828282`, not `#787878`.

## Screenshots

- `01-main-active-profile-violet.png`
- `02-settings-accent-picker-violet.png`
- `03-settings-unreadable-787878.png`
- `04-settings-nearest-readable.png`
- `05-main-after-readable-apply.png`

## Verification

- `dotnet build PiPlay.sln --configuration Debug`
- `dotnet test PiPlay.sln --configuration Debug --no-build --filter "FullyQualifiedName~WpfRuntimeTests|FullyQualifiedName~XamlInvariantTests"`: 155 passed
- `dotnet test PiPlay.sln --configuration Debug --no-build`: 660 passed
- `git diff --check`: clean, with only Git LF-to-CRLF normalization warnings
