# Product Spec vs Change Specs Review - 2026-06-25

## Scope

Review target: `docs/PiPlay_Product_Engineering_Spec.md` against the current dated specs, current
code, and active QA docs.

Focused surfaces:

- `docs/PiPlay_Product_Engineering_Spec.md`
- `docs/SPEC_GAPS_AND_OWNERSHIP.md`
- `docs/PiPlay_UI_Priority_Improvements.md`
- `docs/QA_Checklist.md`
- `docs/superpowers/specs/2026-06-25-p1-borderless-design.md`
- `docs/superpowers/specs/2026-06-25-popout-look-cleanup-and-drop-compact-design.md`
- `docs/superpowers/specs/2026-06-25-profile-color-identity-and-accent-fill-design.md`
- `src/PiPlay/Services/BorderlessResizeHitTestPolicy.cs`
- `src/PiPlay/Services/PlaybackModePolicy.cs`
- `src/PiPlay/Services/ProfileAccentService.cs`

## Verdict

Two real drift findings were present and were fixed in this pass.

No code behavior changed. The only code edit is a comment in `MainWindow.xaml.cs` that now matches the
existing compact-player kill switch.

## Findings And Disposition

### 1. Product spec still treated REQ-WINDOW-02 as a 10 DIP edge zone

Severity: High documentation drift.

`PiPlay_Product_Engineering_Spec.md` and `SPEC_GAPS_AND_OWNERSHIP.md` still described the resize target
as a 10 DIP edge zone. Current code and the v0.7.2 P1 borderless spec use a 4 DIP black resize band with
32 DIP corner acquisition:

- `BorderlessResizeHitTestPolicy.ResizeBorderDip = 4`
- `MainWindow.xaml` / `PlayerWindow.xaml` use `WindowChrome.ResizeBorderThickness="4"`
- WebView margins are `4,0,4,4`
- `docs/superpowers/specs/2026-06-25-p1-borderless-design.md` explicitly changed 10 -> 4 DIP

Disposition: fixed. The Product Engineering Spec, SPEC_GAPS, and QA wording now identify the current
P1 target as 4 DIP edge band plus 32 DIP corner length, while preserving the historical 10 DIP target as
an interim step.

### 2. Product spec and QA still treated Compact player as active

Severity: High documentation drift.

The latest popout cleanup spec and code make Compact player dormant:

- `PlaybackModePolicy.CompactPlayerEnabled = false`
- `ResolveEffectivePopoutMode(...)` forces `PlaybackMode.Normal` while the switch is false
- Settings no longer exposes the Compact player toggle
- tests assert the kill-switch behavior

But the Product Engineering Spec, SPEC_GAPS, and QA checklist still described compact as a user-facing
global/profile preference and asked release QA to validate active compact playback.

Disposition: fixed. Active docs now say compact settings/profile fields are reserved/migration data,
new popouts force Normal while the kill switch is false, and compact manual QA only returns if the mode
is deliberately re-enabled.

### 3. Profile color roadmap conflict is already labeled

Severity: none.

`docs/PiPlay_UI_Priority_Improvements.md` says Priority 2 would make profile color the global app
accent, but it also labels this as a conflict that reverses the v0.6.0 profile-identity split. The
Product Engineering Spec, SPEC_GAPS, current design spec, code, and tests consistently keep profile
color as identity-only and global accent as the app accent.

Disposition: no change. The conflict is intentionally recorded as roadmap input, not current product
truth.

## Evidence Commands

```powershell
rg -n -C 3 "REQ-WINDOW-02|10 DIP|4 DIP|ResizeBorderDip|Previous baseline" docs\PiPlay_Product_Engineering_Spec.md docs\SPEC_GAPS_AND_OWNERSHIP.md docs\superpowers\specs\2026-06-25-p1-borderless-design.md src\PiPlay\Services\BorderlessResizeHitTestPolicy.cs src\PiPlay\MainWindow.xaml src\PiPlay\PlayerWindow.xaml docs\CHANGELOG.md
rg -n -C 2 "Compact mode|Compact player|Player\.CompactMode|CompactPlayerEnabled|new popouts|Profile\.Mode" docs\QA_Checklist.md docs\PiPlay_Product_Engineering_Spec.md docs\SPEC_GAPS_AND_OWNERSHIP.md src\PiPlay\Services\PlaybackModePolicy.cs tests\PiPlay.Tests\PlaybackModePolicyTests.cs
rg -n -C 2 "profile color|global app accent|identity color|must not replace|CONFLICT|ResolvedAccentColor" docs\PiPlay_Product_Engineering_Spec.md docs\SPEC_GAPS_AND_OWNERSHIP.md docs\PiPlay_UI_Priority_Improvements.md docs\superpowers\specs\2026-06-25-profile-color-identity-and-accent-fill-design.md src\PiPlay\Services\ProfileAccentService.cs tests\PiPlay.Tests\ProfileAccentServiceTests.cs
```
