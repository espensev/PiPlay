# Review - Source Window Return And Navigation

**Date:** 2026-07-15  
**Surface:** `main` at `d11eac5` / deployed Stable `v0.11.0-b34`, plus live window and portable-settings state  
**Spec source:** owner request; `docs/PiPlay_Product_Engineering_Spec.md`; `docs/PiPlay_UI_Priority_Improvements.md`  
**Standards sources:** `CLAUDE.md`; `docs/AGENTS.md`; `docs/QA_Checklist.md`  
**Verdict:** FAIL

## Findings

### High

- [axis: regression] `src/PiPlay/MainWindow.xaml.cs:1414` - Closing the Popout returns playback but does not restore or activate the Source Window.
  Evidence: `Player_OnClosed` clears the player, reveals the browser, and replays state without calling `Show`, restoring `WindowState.Minimized`, or activating the Source. Live Stable inspection found the Source minimized while the Popout was visible. The required restore/activate pattern already exists at `MainWindow.xaml.cs:1561` for second-instance activation.
  Impact: Focused Close, native X, and Alt+F4 can return audio/video into a minimized Source, leaving the user to recover it from the taskbar.
  Recommendation: add a user-return-specific Source activation coordinator that restores a minimized window, activates it, and preserves its persisted Pin state. Skip it during app shutdown. Add minimized-Source regressions for overlay Close, native close, and Bring back.

- [axis: regression] `src/PiPlay/MainWindow.xaml.cs:117` - Source placement restore can reopen below the Source's declared minimum size.
  Evidence: `MainWindow` passes raw saved placement to `WindowPlacementService.Restore`, while `PlayerWindow.xaml.cs:143` explicitly uses `PlacementMath.EnsureMinSize`. Live Stable had a Source normal size of about `821 x 321` DIP at 150% DPI despite `MainWindow.xaml:7` declaring `MinHeight="480"`; portable settings also contained a sub-minimum `569 x 307` placement. The screenshot matches the 321-DIP-high state.
  Impact: fixed 42 + 50 DIP app chrome leaves about 229 DIP for YouTube, so YouTube's own header consumes nearly the entire browsing surface.
  Recommendation: normalize Source placement through `EnsureMinSize` before native restore and preserve the minimum track size in the custom `WM_GETMINMAXINFO` path at `Services/BorderlessWindowHelper.cs:98`. Add stale-placement and native-resize regressions.

### Medium

- [axis: regression] `src/PiPlay/MainWindow.xaml.cs:1414` - The dock/undock action becomes available before asynchronous return work is complete.
  Evidence: `Player_OnClosed` clears `_player` and flips the action to `Pop out video` before awaiting return scripting. Different-video return continues later through `ReplayPendingReturnStateAsync` at `MainWindow.xaml.cs:247`. Neither manual nor Auto launch has a return-in-progress gate.
  Impact: a rapid new popout can overlap source seek/play/settings replay from the previous return.
  Recommendation: hold a return state through same-video scripting and different-video replay completion/timeout; disable the action or present a truthful Returning state during that interval.

- [axis: regression] `src/PiPlay/MainWindow.xaml.cs:1364` - Source navigation remains enabled while the Tier-1 placeholder hides the WebView.
  Evidence: `ShowSourcePlaceholder` changes only Browser and placeholder visibility. Back, Reload, Home, URL, and profile navigation remain live, while return decisions compare against the launch video rather than a Source URL that may have drifted invisibly.
  Impact: hidden navigation can make return seek or resume the wrong page and makes the Source toolbar appear usable when its result cannot be inspected.
  Recommendation: disable Source navigation/profile commands while the placeholder owns the content, or explicitly design and test concurrent Source browsing with drift-aware return logic.

- [axis: regression] `src/PiPlay/MainWindow.xaml.cs:1238` - Pin preferences are individually correct but z-order is not coordinated across the Source/Popout transition.
  Evidence: Source and Popout Topmost values correctly persist separately, and the Focused Pin action synchronizes native, WPF, and overlay state. However, a pinned Source can launch an unpinned Popout using only `_player.Show()`, and a pinned Popout can close into an unpinned, unfocused Source.
  Impact: the active playback surface can appear behind the placeholder Source, or the returned Source can disappear behind other applications.
  Recommendation: keep the two persisted Pin values separate, but define a temporary transition z-order policy and add the pinned-Source/unpinned-Popout matrix to runtime QA.

- [axis: spec] `src/PiPlay/MainWindow.xaml:76` - The Source has no responsive control hierarchy comparable to the Focused overlay.
  Evidence: the Source permanently reserves 92 DIP and places navigation, a flexible URL box, fixed 150-DIP profile selector, three profile commands, Pin, Auto, and a text transfer action in one row. The Focused surface groups a smaller action set and withdraws its native strip after the overlay handshake. Main chrome auto-hide remains an open owner priority in `docs/PiPlay_UI_Priority_Improvements.md:150`.
  Impact: the URL field and YouTube viewport collapse first at compact sizes, making routine browsing and search difficult.
  Recommendation: fix minimum restore first, then apply the overlay's hierarchy rather than literally overlaying WPF on WebView2: keep Back/Home and the omnibox primary, move profile CRUD behind one adjacent menu, keep Auto beside Popout, expose distinct Show Popout and Bring back commands, and use an adaptive compact/top-edge-reveal mode.

## Control Evaluation

- **Bring back:** playback-state capture and the label/tooltip/UIA flip are sound. Missing Source activation, a return busy state, an inward glyph, and a separate Show/Focus Popout recovery action make the full transition incomplete.
- **Pin:** Focused and native Popout paths correctly converge on `Topmost`; Source and Popout preferences correctly remain separate. Cross-window z-order during transfer is the missing policy.
- **Pop out:** the single-player guard and source suppression are sound. Hidden Source navigation and immediate re-entry during return are the remaining lifecycle risks.

## Verification

- `dotnet test tests\PiPlay.Tests\PiPlay.Tests.csproj --configuration Debug --nologo --filter "FullyQualifiedName~ReturnPolicyTests|FullyQualifiedName~PopoutPresentationPolicyTests|FullyQualifiedName~BorderlessWindowHelperTests|FullyQualifiedName~PlacementMathTests|Name~Popout_action_state_flips_label_tooltip_and_uia_name_together|Name~Focused_pin_action_updates_native_and_overlay_state"` - pass, 66/66.
- Live Stable window enumeration - Source minimized while Popout was normal; Source normal bounds below the XAML minimum.
- Portable Stable settings/log inspection - confirmed Focused presentation, separate Pin values, sub-minimum saved Source bounds, successful return events, and one immediate-close return with an unknown timestamp.

## Coverage Notes

- Deep-reviewed: Source/Popout XAML and lifecycle code, placement/minimum handling, Focused overlay action bridge, return replay, Pin persistence, current specs, QA checklist, live Stable windows/settings/log.
- Excluded: unrelated local-CI working-tree changes; no live UI actions were sent to the owner's running PiPlay process.

## Open Questions

- Should the Source remain visible as a placeholder during Popout, or minimize/withdraw automatically and restore on return? The answer determines the cleanest z-order policy.
- Should Source compact chrome be automatic below a height threshold, a user preference, or part of the existing Fade behavior?

## Remediation Disposition

**Status:** Addressed in the working tree on 2026-07-15; deployed Stable remains b34 until a separate
sanctioned publish.

- Saved and interactive Source sizing now enforce the DPI-scaled 760 x 480 DIP floor.
- Popout close/Bring restores and activates Source, holds a single-flight Returning state through
  replay, and cancels/revalidates replay if browser clearing starts.
- Actual Source Topmost is captured before suspension and restored on return, including profile-derived
  Pin; Source/Popout preferences remain separate.
- Hidden Source navigation/profile commands are disabled; Show Popout and Bring back are distinct.
- Profile CRUD uses one dark menu and transfer text collapses below the compact-width threshold.

Verification: focused integrated suite 224/224; full Debug suite 959/959; no-bump Release build with 0
warnings/errors; independent final review found no blocker, high, or medium issue. Deployed/manual QA
rows remain open in `docs/QA_Checklist.md`.
