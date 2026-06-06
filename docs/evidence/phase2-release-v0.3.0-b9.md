# Phase 2 release evidence - v0.3.0 build 9

Date: 2026-06-06
Tester: Codex
Branch: `main`
Source commit: `99f86757da30ffb98dc8276d87571c15229ca3b3`
Publish label: `20260606-114243-v0.3.0-b9-stable`
Deploy root: `E:\Dev_test_implemenations\PiPlay`
Deployed exe: `E:\Dev_test_implemenations\PiPlay\PiPlay.exe`
Primary SHA256: `A393BC7B222133EB62F43C9933F4734DB83932261BDB230CB64FAFC9BF750458`

## Automated gates

| Gate | Result | Evidence |
|---|---:|---|
| `dotnet test PiPlay.sln --configuration Debug` | Pass, 221/221 | Console output from this run. |
| `.\Build-PiPlay.ps1 -Stage Build -NoVersionBump -NoBuildNumberBump` | Pass | Release build gate completed with version `0.3.0`, build `8`, channel `Default`. |
| `.\scripts\Publish-Stable.ps1` | Pass | Re-ran the deterministic test gate, built Stable build `9`, validated publish metadata, and deployed to `E:\Dev_test_implemenations\PiPlay`. |
| `pwsh -File scripts\Test-UiSmoke.ps1 -ExePath 'E:\Dev_test_implemenations\PiPlay\PiPlay.exe'` | Pass | `docs/evidence/phase2-stable-v0.3.0-b9-ui-smoke.png`. |

## Stable deploy checks

| Check | Result |
|---|---:|
| Stable channel baked into `build-info.json` | Pass |
| Stable title visible as `PiPlay — Stable v0.3.0 (b9)` | Pass |
| Publish metadata SHA256 and sizes validate | Pass |
| Deploy preserved the portable data folder path `E:\Dev_test_implemenations\PiPlay\PiPlayData` | Pass |
| `.piplay.publish.marker` written with version/build/source metadata | Pass |

## Manual/account-backed QA status

This pass did not claim the full account-backed manual checklist complete. The rows that require a
live YouTube playback/account state remain release-candidate checks in `docs/QA_Checklist.md`:

- Auto live playback/return loop on real `/watch` pages.
- Controls-fade/customization visual behavior while a real Popout Player is playing.
- Profile edit/delete via the live Source Window.
- Privacy sign-in invariant for Reset app state and sign-out invariant for Clear browser data.
- Long-running reliability, code signing, and distribution checks.

The deterministic tests cover the policy/schema/construction seams for Phase 2 (`AutoPopoutPolicy`,
`FadePolicy`, `PlayerAppearancePolicy`, `ProfileService`, `PrivacyService`, XAML invariants, and WPF
construction). The manual rows above remain the final release-candidate gate because they depend on
WebView2, live YouTube behavior, local account/session state, or distribution policy.
