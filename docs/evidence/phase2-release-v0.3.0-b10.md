# Phase 2 stable replacement evidence - v0.3.0 build 10

Date: 2026-06-07
Tester: Codex
Branch: `main`
Source commit: `df2ac9f1823d85bdc844ae9ab94c359bd8fb9ee2`
Publish label: `20260607-111151-v0.3.0-b10-stable`
Deploy root: `E:\Dev_test_implemenations\PiPlay`
Deployed exe: `E:\Dev_test_implemenations\PiPlay\PiPlay.exe`
Primary SHA256: `C0A404EF7406CEB2F0D355829E696FC62084AC0CFE08DB426C44DF5C4E563301`

This build replaces the previous deployed Stable build 9. It is built from the Phase 2 landing
commit, so the deployed Stable copy now includes the build-number/evidence/docs bookkeeping that
was added after the build 9 artifact was cut.

## Automated gates

| Gate | Result | Evidence |
|---|---:|---|
| `dotnet test PiPlay.sln --configuration Debug` | Pass, 221/221 | Console output from this run. |
| `.\Build-PiPlay.ps1 -Stage Build -NoVersionBump -NoBuildNumberBump` | Pass | Release build gate completed with version `0.3.0`, build `9`, channel `Default`. |
| `.\scripts\Publish-Stable.ps1` | Pass | Re-ran the deterministic test gate, built Stable build `10`, validated publish metadata, and deployed to `E:\Dev_test_implemenations\PiPlay`. |
| `pwsh -File scripts\Test-UiSmoke.ps1 -ExePath 'E:\Dev_test_implemenations\PiPlay\PiPlay.exe'` | Pass | `docs/evidence/phase2-stable-v0.3.0-b10-ui-smoke.png`. |
| UI Automation title check | Pass | Deployed window title read as `PiPlay — Stable v0.3.0 (b10)`. |

## Stable deploy checks

| Check | Result |
|---|---:|
| Stable channel baked into `build-info.json` | Pass |
| `build-info.json` source commit matches `df2ac9f1823d85bdc844ae9ab94c359bd8fb9ee2` | Pass |
| Publish metadata SHA256 and sizes validate | Pass |
| Deploy replaced binaries at `E:\Dev_test_implemenations\PiPlay` | Pass |
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
