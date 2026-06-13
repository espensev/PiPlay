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
| signing path available/used | blocked | Unsigned/internal: `signtool` was not found in PATH; no signing script was present. Build manifest records `signing.enabled=false`. Blocked for public distribution until signed (see lines below and Signing rows). |
| `Publish-Stable.ps1` final verifier | pass | Exit 0. Final verifier printed `VERDICT: RELEASE VERIFIED`; v0.4.3 b20; commit `d628f27c6e07788bbd01d641480a5d4f0a516c5c`; tag `stable-v0.4.3-b20`; deploy root `E:\Dev_test_implemenations\PiPlay`. |
| `Verify-StableDeploy.ps1` rerun | pass | Exit 0. Printed `VERDICT: RELEASE VERIFIED`; artifact hashes, manifest, marker, `FileVersion`, `ProductVersion`, clean repo, source commit, and stable tag all agreed. Follow-up rerun was performed from clean release-source `HEAD` after temporarily moving untracked evidence out of the checkout. |
| `Test-UiSmoke.ps1` deployed exe | pass | Exit 0. UI Automation found Pop out video, URL/address box, Close caption button, Profiles dropdown, and Settings gear. Curated screenshot: `docs\evidence\phase1-release-v0.4.3-b20-ui-smoke.png`. |
| `dotnet test` Debug | pass | Run inside `Publish-Stable.ps1`; 561 passed, 0 failed, 0 skipped. |
| focused main-chrome UIA/screenshot follow-up | pass | UI Automation verified main chrome names for Settings, Minimize, Maximize/restore, Close, Back, Reload, YouTube home, URL/search, Profiles, Save profile, Pin, Auto, and Pop out video. Screenshot: `docs\evidence\phase1-release-v0.4.3-b20-main-chrome.png`. |
| Settings/Privacy UI follow-up | blocked | The Settings button was found and main chrome captured, but the Settings window did not open under UI Automation invoke or foreground coordinate click in this non-interactive run. No destructive privacy action was run. |

## Direct-Observation Follow-Up

Tester/date: Codex, 2026-06-12 Europe/Berlin

Environment: Windows 11 Pro 10.0.26200; deployed target only `E:\Dev_test_implemenations\PiPlay\PiPlay.exe`; two detected video outputs 3840x2160 and 1920x1080; two Generic PnP monitors reported 144 logical DPI; `settings.json` recorded `dpiScale=1.5`, `player.compactMode=false`, `autoPopout=false`, and no saved profiles.

| Area | Scenario | Result | Evidence / notes |
|---|---|---|---|
| Release identity | deployed candidate identity | pass | `Test-Path` returned `True`. SHA256 was `F6D491E96D7DC6D9D338796C60F12E46C97C88037ADC8D37BDD329E184FDB3FA`, matching the archived evidence and `build-info.json`. |
| Release identity | source/tag/build metadata | pass | `build-info.json` reports v0.4.3 b20, publish label `20260612-015827-v0.4.3-b20-stable`, source commit `d628f27c6e07788bbd01d641480a5d4f0a516c5c`, and `releaseEvidence=true`; `stable-v0.4.3-b20` resolves to that source commit. |
| Signing | public distribution decision | blocked | `Get-AuthenticodeSignature` returned `NotSigned`. This candidate remains usable only as an unsigned internal QA build; public distribution needs a new signed publish before the manifest hash is captured. |
| Chrome | main chrome visual rendering | pass | `docs\evidence\phase1-release-v0.4.3-b20-main-chrome.png` shows the deployed main window with toolbar icons, caption buttons, URL box, Profiles control, Pin/Auto controls, Pop out video button, and WebView content rendered without empty icon boxes. |
| Chrome | main chrome accessibility names | pass | UI Automation exposed names for Settings, Minimize, Maximize/restore, Close, Back, Reload, YouTube home, URL/search, Profiles, Save profile, Pin, Auto, and Pop out video. |
| Chrome | URL/address field at current DPI | pass | Main-chrome screenshot at the recorded 144 logical DPI / `dpiScale=1.5` shows the URL/search field text and toolbar spacing legible within the deployed window. |
| Chrome | Profiles closed dropdown appearance | pass | Main-chrome screenshot shows the closed Profiles control rendered in the dark toolbar without a light-theme rectangle. |
| Chrome | Profiles open dropdown appearance | blocked | UI Automation ExpandCollapse was attempted and `docs\evidence\phase1-release-v0.4.3-b20-profiles-dropdown.png` was captured, but the screenshot still showed the control closed or an empty list not directly visible; the open dropdown state was not proven. |
| Chrome | UI-CHK-1 through UI-CHK-7 full review | blocked | Partial visual/UIA coverage passed for the main chrome, but tooltip timing, full screen-reader review, and every reachable focus/visual state were not completed under direct human observation. |
| Settings | Settings opens from deployed Stable | blocked | Settings button was visible, enabled, and keyboard-focusable, but UIA InvokePattern, keyboard Enter/Space, and foreground coordinate click did not open the Settings window in this synthetic/non-interactive run. No human click path was available. |
| Privacy | Reset app state clears app settings without signing out YouTube | blocked | Requires Settings access plus a controlled signed-in YouTube state. Settings did not open through the available synthetic interaction path, and no destructive reset was run. |
| Privacy | Clear browser data signs out YouTube | blocked | Requires Settings access, a controlled signed-in YouTube state, and consent to run the destructive browser-data action. These dependencies were unavailable. |
| Privacy | Reset app state and Clear browser data are clearly distinct | blocked | Requires opening Settings and observing both confirmation/action surfaces. Settings could not be opened in this environment. |
| Persistence | settings changes persist across restart | blocked | Requires interactive settings mutation and restart observation. Settings could not be opened, and runtime `settings.json` was not hand-edited for QA. |
| Compact | compact mode entry state | blocked | Deployed `settings.json` records `player.compactMode=false`; Settings could not be opened to enable compact mode, and no config hand-edit was performed. |
| Compact | recommendation/end-screen click retargets same popout | blocked | Requires compact-player mode plus live YouTube recommendation/end-screen interaction and timestamp observation. These dependencies were unavailable. |
| Compact | retargeted fallback opens the new target | blocked | Requires compact-player mode and a known restricted/embed-disabled or fallback target to induce the retargeted fallback path. No suitable controlled media was available. |
| Compact | video-aware return navigates source to current popout URL/timestamp | blocked | Requires compact-player popout playback, source/popout URL capture, and direct timestamp observation before and after return. Compact mode was unavailable. |
| Compact | compact YouTube fullscreen expands/restores | blocked | Requires compact-player mode, live playback, and direct fullscreen/restore observation. Compact mode was unavailable. |
| Compact | playlist and playlist-only URL behavior | blocked | Requires compact-player mode and controlled playlist/playlist-only YouTube URLs. Compact mode and controlled media were unavailable. |
| Compact | restricted/embed-disabled fallback clarity | blocked | Requires known restricted/embed-disabled media to induce fallback. No controlled test media was available. |
| Compact | watchdog timeout/error bar behavior | blocked | Requires inducing IFrame API timeout or recovery, likely through special media or network control. No safe induction path was available. |
| Compact | timestamp-carrying fallback | blocked | Requires compact-player mode, fallback induction, and direct timestamp comparison. Compact mode and controlled fallback media were unavailable. |
| Playback/account | logged-out playback | blocked | Requires a controlled logged-out WebView2/browser state and direct playback observation. Browser/account state was not reset for this pass. |
| Playback/account | logged-in playback | blocked | Requires controlled credentials/session and direct playback observation. No credentialed test account was supplied. |
| Playback/account | autoplay allowed | blocked | Requires controlled browser policy/user gesture/account state and direct playback observation. These dependencies were unavailable. |
| Playback/account | autoplay blocked | blocked | Requires controlled browser policy/user gesture/account state and a reproducible blocked-autoplay condition. These dependencies were unavailable. |
| Playback/account | returning from Auto does not immediately re-pop same video | blocked | Requires live playback with Auto enabled and direct source/popout observation. This was not run. |
| DPI/resize | current-DPI main chrome smoke | pass | Main-chrome screenshot was captured at the current environment's 144 logical DPI / `dpiScale=1.5`, with main controls visible and not overlapping. |
| DPI/resize | 100/125/150 percent and mixed-monitor matrix | blocked | The environment exposed current 144 logical DPI and two outputs, but system DPI switching and mixed-monitor movement were not performed. |
| DPI/resize | auto-hide reveal then resize | blocked | Requires direct pointer/resize observation against the deployed app. Synthetic QA did not provide a reliable interactive pointer path for this row. |
| DPI/resize | source window resize over WebView2 | blocked | Requires direct interactive resizing and hit-testing over WebView2 content. This was not run. |
| DPI/resize | popout resize over WebView2 | blocked | Requires opening popout playback and directly resizing over WebView2 content. Popout/compact playback was not established in this pass. |
| DPI/resize | resize zones do not swallow caption buttons or popout controls | blocked | Requires direct pointer hit-test observation across caption and popout controls. This was not run. |
| Diagnostics | Settings-attempt log check | pass | Tail of `E:\Dev_test_implemenations\PiPlay\PiPlayData\logs\piplay.log` during the synthetic Settings attempts showed startup/source-browser initialization lines and no recorded exception for Settings. |

## Human-Assisted QA Follow-Up

Tester/date: Human tester coordinated by Codex, 2026-06-12 Europe/Berlin

Environment: deployed target only `E:\Dev_test_implemenations\PiPlay\PiPlay.exe`; archived SHA256 rechecked as `F6D491E96D7DC6D9D338796C60F12E46C97C88037ADC8D37BDD329E184FDB3FA`; signature remained `NotSigned`; current run used the visible deployed Stable window.

| Area | Scenario | Result | Evidence / notes |
|---|---|---|---|
| Release identity | deployed candidate still matches archived SHA256 | pass | `Test-Path` returned `True`; SHA256 matched `F6D491E96D7DC6D9D338796C60F12E46C97C88037ADC8D37BDD329E184FDB3FA`; `Get-AuthenticodeSignature` returned `NotSigned`. |
| Settings | Settings opens from deployed Stable by real click | pass | Human tester clicked the Settings gear in the visible deployed Stable window and Settings opened. Screenshot was observed during the pass but not retained at tester request. |
| Privacy | Reset app state and Clear browser data are visibly distinct | pass | Human-observed Settings view showed separate `Reset app state` and `Clear browser data` actions with distinct explanatory text: reset clears PiPlay settings/profiles/placement while staying signed in, and clear browser data signs out of YouTube and clears browsing data. |
| Privacy | Reset app state clears app settings without signing out YouTube | not run | Destructive reset action was not performed in this pass. Requires explicit tester consent plus a controlled signed-in YouTube state and post-action observation. |
| Privacy | Clear browser data signs out YouTube | not run | Destructive browser-data action was not performed in this pass. Requires explicit tester consent plus a controlled signed-in YouTube state and post-action observation. |
| Persistence | settings changes persist across restart | not run | A harmless setting change and restart cycle were not performed yet. |
| Profiles | open Profiles dropdown by hand | pass | Human tester opened the Profiles dropdown from the visible deployed Stable window. The open menu used the dark theme, was readable, and showed `No saved profiles yet`. Screenshot was observed during the pass but not retained at tester request. |
| Chrome | UI-CHK-4 main chrome tooltips | pass | Human tester hovered the main icon buttons and confirmed tooltips appeared with expected labels. |
| Chrome | profile edit/delete icon meaning and enabled state | pass | Code labels confirm the pencil icon is `Edit profile` and the trash can icon is `Delete profile`. Human tester observed the profile action icons become active after a profile was added. |
| Chrome | UI-CHK-1 through UI-CHK-7 full human review | blocked | Tooltip observation is complete, but full screen-reader/accessibility review for every icon-only control is still pending. |
| Compact | compact-player live rows | not run | Compact mode and live YouTube compact-player scenarios have not been exercised in the human-assisted pass. |
| Playback/account | logged-in/logged-out/autoplay matrix | not run | Controlled account/browser/autoplay states have not been established in the human-assisted pass. |
| DPI/resize | DPI, mixed-monitor, resize, and hit-test rows | not run | DPI switching, mixed-monitor movement, and direct resize/hit-test checks have not been performed in the human-assisted pass. |
| Signing | public distribution decision | blocked | Candidate remains unsigned/internal. Public distribution requires a new signed publish with `.\scripts\Publish-Stable.ps1 -SignScript <path>` before manifest hashes are written. |

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

## Automation Re-Verification

Tester/date: Claude Code (automation), 2026-06-13 Europe/Berlin

Environment: deployed target only `E:\Dev_test_implemenations\PiPlay\PiPlay.exe`, the same byte-identical
release candidate (SHA256 re-confirmed below). Installed WebView2 Evergreen runtime `149.0.4022.69`
(distinct from the bundled SDK DLL `1.0.3967.48` recorded under Identity — both expected). Reused the
`run-piplay` UIAutomation scripts against the running deployed instance; no destructive action, no
account login, no setting changed (Settings was opened read-only and closed without persisting).

This pass re-verifies integrity and the automated gate, and **retains the Settings/Appearance
screenshot the human-assisted pass observed but did not keep** (see the `not retained at tester
request` notes above) — closing that evidence gap on the theme feature surface for this version.

| Area | Scenario | Result | Evidence / notes |
|---|---|---|---|
| Release identity | deployed exe still matches archived SHA256 | pass | `Get-FileHash` = `F6D491E96D7DC6D9D338796C60F12E46C97C88037ADC8D37BDD329E184FDB3FA`; signature `NotSigned`; marker version 0.4.3 / build 20 / commit `d628f27` / channel Stable / releaseEvidence=true / sourceDirty=false. |
| Release identity | `Verify-StableDeploy.ps1` from main HEAD | pass (binary) | All 21 deployed artifacts re-hash clean; identity checks pass; commit carries tag `stable-v0.4.3-b20`. The verifier's overall `FAIL` here is the documented source-drift only (HEAD ahead of the tag — doc/evidence archival, empty `src/`/`*.xaml`/`VERSION`/`BUILD_NUMBER` diff — plus a dirty tree of evidence/QA docs); not a deployed-binary failure (handoff lines 56-63). |
| Release identity | `Verify-StableDeploy.ps1` from clean tag worktree | pass | Re-run from a detached worktree at `stable-v0.4.3-b20` (the handoff's pristine-proof path): every check OK incl. "built from the CURRENT HEAD (d628f27)" and "working tree is clean" → `VERDICT: RELEASE VERIFIED — you are testing v0.4.3 b20 @ d628f27…, current with HEAD and tagged.` Confirms the main-HEAD `FAIL` was purely source-drift. |
| Test gate | `dotnet test` on HEAD | pass | 561 passed, 0 failed, 0 skipped (same source the binary was built from). |
| Functional | youtube.com loads and a watch video plays | pass | `phase1-release-v0.4.3-b20-deployed-main-reverify.png` shows the deployed main window with a YouTube video actively playing in WebView2, URL box reading youtube.com, no crash. |
| Chrome | main chrome renders, zero empty icon boxes (UI-CHK-1) | pass | Same screenshot: all caption/nav/profile/Pin/Auto/Pop-out icons present and rendered; closed Profiles combo dark (UI-CHK-2); consistent icon weight/style (UI-CHK-6). |
| Settings | Settings opens from deployed Stable under UIAutomation | pass | Opened via UIAutomation InvokePattern on the deployed instance (advances the direct-observation row that had blocked on synthetic input). `phase1-release-v0.4.3-b20-deployed-settings.png` retained. |
| Appearance | theme preset row renders (Sharp Dark / Minimal / Soft Glass) | pass | Settings screenshot shows the three named presets; Minimal selected. |
| Appearance | single accent row with six chips incl. steel #4A8FAB | pass | Six chips: cyan, steel blue, steel (muted #4A8FAB), violet, green, amber (amber selected/active). |
| Appearance | corner-style override row (Theme/Square/Small/Soft/Round) | pass | All five options render; Theme selected (default). |
| Appearance | close-to-apply hint copy is accurate (PR P3 fix) | pass | Reads "Theme, accent, and corners apply to every open window when you close Settings - no restart needed. A preset sets the accent; the chips fine-tune it." |
| Appearance | custom accent preserved across theme state | pass | Active accent is amber while Minimal's own default is steel blue — the custom-accent-preservation rule holds live on the deployed binary. |
| Privacy | Reset app state and Clear browser data render distinct/worded | pass | Both actions present with their distinct explanatory text; no destructive action run. |
| Settings | section order + scrollability | pass | Privacy → Appearance → Playback order; vertical scrollbar present. |

Not advanced by this pass (still require interactive/account/env conditions, see Release Readiness):
live popout-player rows, destructive Privacy execution, Auto/compact matrix, account/autoplay matrix,
DPI sweep, recovery/durability rows, and the full screen-reader (UI-CHK-7) review.

## Screenshots And Logs

- UI smoke screenshot: `docs\evidence\phase1-release-v0.4.3-b20-ui-smoke.png`
- Main chrome screenshot: `docs\evidence\phase1-release-v0.4.3-b20-main-chrome.png`
- Profiles dropdown attempt screenshot: `docs\evidence\phase1-release-v0.4.3-b20-profiles-dropdown.png` (inconclusive for open-dropdown state).
- Automation re-verify — deployed main (video playing): `docs\evidence\phase1-release-v0.4.3-b20-deployed-main-reverify.png`
- Automation re-verify — deployed Settings/Appearance (theme surface, retained): `docs\evidence\phase1-release-v0.4.3-b20-deployed-settings.png`
- Chrome screenshots: UI smoke, main chrome, profiles dropdown attempt, deployed main re-verify, deployed Settings.
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
