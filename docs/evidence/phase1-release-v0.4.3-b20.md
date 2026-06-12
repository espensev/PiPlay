# Phase 1 release QA evidence - PiPlay v0.4.3 b20

## Identity

- Deployed exe: `E:\Dev_test_implemenations\PiPlay\PiPlay.exe`
- Version/build: `0.4.3` / `20`
- Source commit: `d628f27c6e07788bbd01d641480a5d4f0a516c5c`
- Stable tag: `stable-v0.4.3-b20`
- Publish label: `20260612-015827-v0.4.3-b20-stable`
- Exe SHA256: `F6D491E96D7DC6D9D338796C60F12E46C97C88037ADC8D37BDD329E184FDB3FA`
- Signed state: unsigned/internal. No `signtool` was found in PATH and no repo signing script was available; `Get-AuthenticodeSignature` returned `NotSigned`.
- Tester/date: Codex, 2026-06-12 Europe/Berlin
- OS/DPI/monitors: Windows 11 Pro 10.0.26200; detected video outputs 3840x2160 and 1920x1080; `Win32_DesktopMonitor` reported 144 logical DPI for two Generic PnP monitors.
- WebView2 runtime/package: deployed `Microsoft.Web.WebView2.Core.dll` file/product version `1.0.3967.48`.
- Handoff prompt files: root handoff files were preserved outside the release source at `D:\tmp\PiPlay-phase-handoffs-20260612-015745` so the release tree could publish from a clean exact-source commit.
- Follow-up hygiene: untracked evidence was temporarily moved out of the checkout, `Verify-StableDeploy.ps1` was rerun from clean `HEAD`/`stable-v0.4.3-b20`, and the UI smoke screenshot was copied to curated filename `docs\evidence\phase1-release-v0.4.3-b20-ui-smoke.png`.

## Release Gates

| Gate | Result | Evidence |
|---|---|---|
| git clean before publish | pass | `git status --short --branch` showed only `## main...origin/main [ahead 2]` before publish, with no dirty paths. |
| stable tag collision check | pass | Existing `stable-v0.4.3-b19` pointed at an older commit; `stable-v0.4.3-b20` did not exist before publish. `BUILD_NUMBER` was bumped to `20` and committed instead of moving an existing tag. |
| signing path available/used | unsigned/internal | `signtool` was not found in PATH; no signing script was present. Build manifest records `signing.enabled=false`. |
| `Publish-Stable.ps1` final verifier | pass | Exit 0. Final verifier printed `VERDICT: RELEASE VERIFIED`; v0.4.3 b20; commit `d628f27c6e07788bbd01d641480a5d4f0a516c5c`; tag `stable-v0.4.3-b20`; deploy root `E:\Dev_test_implemenations\PiPlay`. |
| `Verify-StableDeploy.ps1` rerun | pass | Exit 0. Printed `VERDICT: RELEASE VERIFIED`; artifact hashes, manifest, marker, `FileVersion`, `ProductVersion`, clean repo, source commit, and stable tag all agreed. Follow-up rerun was performed from clean release-source `HEAD` after temporarily moving untracked evidence out of the checkout. |
| `Test-UiSmoke.ps1` deployed exe | pass | Exit 0. UI Automation found Pop out video, URL/address box, Close caption button, Profiles dropdown, and Settings gear. Curated screenshot: `docs\evidence\phase1-release-v0.4.3-b20-ui-smoke.png`. |
| `dotnet test` Debug | pass | Run inside `Publish-Stable.ps1`; 561 passed, 0 failed, 0 skipped. |
| focused main-chrome UIA/screenshot follow-up | pass | UI Automation verified main chrome names for Settings, Minimize, Maximize/restore, Close, Back, Reload, YouTube home, URL/search, Profiles, Save profile, Pin, Auto, and Pop out video. Screenshot: `docs\evidence\phase1-release-v0.4.3-b20-main-chrome.png`. |
| Settings/Privacy UI follow-up | blocked | The Settings button was found and main chrome captured, but the Settings window did not open under UI Automation invoke or foreground coordinate click in this non-interactive run. No destructive privacy action was run. |

## Focused Manual QA

| Area | Scenario | Result | Evidence / notes |
|---|---|---|---|
| Release identity | verifier release proof | pass | `Verify-StableDeploy.ps1` rerun printed `VERDICT: RELEASE VERIFIED` for the deployed Stable copy. |
| Release identity | deployed exe path | pass | Verified path is exactly `E:\Dev_test_implemenations\PiPlay\PiPlay.exe`. |
| Release identity | version/build/source/tag agree | pass | v0.4.3 b20, commit `d628f27c6e07788bbd01d641480a5d4f0a516c5c`, tag `stable-v0.4.3-b20`. |
| Release identity | signing state recorded | pass | Recorded as unsigned/internal; not a public distribution candidate until signed. |
| Compact | recommendation retarget | blocked | Requires interactive YouTube compact-player playback and direct observation; not executed by automation in this pass. |
| Compact | retargeted fallback | blocked | Requires forcing an embed-disabled/restricted recommendation target after retarget; not executed by automation in this pass. |
| Compact | video-aware return | blocked | Requires playback timestamp observation across source and popout; not executed by automation in this pass. |
| Compact | compact YouTube fullscreen | blocked | Requires interactive compact player and fullscreen/restore observation; not executed by automation in this pass. |
| Compact | playlists and playlist-only URLs | blocked | Requires live YouTube playlist playback; not executed by automation in this pass. |
| Compact | restricted/embed-disabled fallback | blocked | Requires known restricted/embed-disabled test video and interactive fallback observation; not executed by automation in this pass. |
| Compact | watchdog timeout/error auto-dismiss | blocked | Requires inducing IFrame API timeout or recovery condition; not executed by automation in this pass. |
| Compact | timestamp-carrying fallback | blocked | Requires live timestamp observation; not executed by automation in this pass. |
| Playback | logged-out | blocked | Requires a controlled browser/account state; not executed by automation in this pass. |
| Playback | logged-in | blocked | Requires YouTube account credentials/session; not executed by automation in this pass. |
| Playback | autoplay allowed | blocked | Requires controlled browser policy/user gesture state; not executed by automation in this pass. |
| Playback | autoplay blocked | blocked | Requires controlled browser policy/user gesture state; not executed by automation in this pass. |
| Playback | Auto return does not re-pop same video | blocked | Requires live playback with Auto enabled and direct observation; not executed by automation in this pass. |
| Window | auto-hide reveal then resize | blocked | Requires interactive pointer/resize observation against the deployed app; not executed by automation in this pass. |
| Window | DPI resize smoke | blocked | Environment reported 144 DPI, but 100/125/150 percent switching and mixed-monitor movement were not executed. |
| Window | resize zones do not swallow controls | blocked | Requires interactive pointer hit-test observation; not executed by automation in this pass. |
| Chrome | main chrome UIA names and screenshot | pass | Follow-up UIA run verified accessible names for main chrome/navigation/profile/pin/auto/popout controls and saved `docs\evidence\phase1-release-v0.4.3-b20-main-chrome.png`. |
| Chrome | UI-CHK-1 through UI-CHK-7 full review | blocked | UI smoke and main chrome screenshots exist, but full visual inspection, tooltip review, and screen-reader pass were not completed. |
| Privacy | reset vs clear browser data | blocked | Settings/Privacy could not be opened under UI automation in this run; controlled signed-in YouTube state and destructive browser-data action were also unavailable. |
| Persistence | settings persist across restart | blocked | Requires interactive settings changes and restart observation; not executed by automation in this pass. |

## Screenshots And Logs

- UI smoke screenshot: `docs\evidence\phase1-release-v0.4.3-b20-ui-smoke.png`
- Main chrome screenshot: `docs\evidence\phase1-release-v0.4.3-b20-main-chrome.png`
- Chrome screenshots: UI smoke and main chrome screenshots only.
- DPI screenshots: not produced.
- Compact fallback screenshots: not produced.
- Redacted logs: none produced.

## Issues Found

- No release-gate failures were found.
- Follow-up UI automation could not open Settings from the visible deployed window. Because this was synthetic input rather than a human click, it is recorded as a blocked Settings/Privacy observation rather than an app defect.
- Manual focused QA remains incomplete. The release candidate is not approved for broader release testing until the blocked YouTube playback, account, privacy, compact-player, and DPI rows are executed with direct observation.
- The deployed executable is unsigned. This is acceptable only as an internal candidate; it is blocked for public distribution until signed through the pre-manifest signing path.

## Release Readiness

- Verdict: blocked pending focused manual QA and signing decision.
- Remaining risks: compact-player live behavior, account/autoplay matrix, privacy reset/clear distinction, multi-DPI resize behavior, and full chrome/screen-reader visual review remain unverified.
- Recommended next phase: run the focused manual QA rows from this note against `E:\Dev_test_implemenations\PiPlay\PiPlay.exe`; use a signed publish path if the intended audience is public distribution.
