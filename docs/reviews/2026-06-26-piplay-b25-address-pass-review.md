# PiPlay b25 — review of the address-pass (uncommitted working tree)

Reviews what was actually implemented in response to the b25 review cluster
(`...-addressed-findings.md`). Verified against the real source at the current working tree, full test
gate run locally.

## Gate

- **`dotnet test PiPlay.sln -c Debug` -> 687/687 passing, exit 0.** Clean compile.
  (The address-pass doc reported only a filtered 116/116; the full suite is also green.)
- **Caveat — the suite is headless.** It exercises `ReturnPolicy` and XAML/WPF construction seams, but does
  **not** drive WebView2: source suppression, mute-on-return, and the navigate-replay DOM behavior are
  unverified by tests. "687/687" means "nothing regressed in the headless contract," **not** "the new
  behaviors work" — which is exactly where residual #1 and the deferred runtime items live.

## Verdict

**Strong, comprehensive address pass.** It implements essentially the entire RC-gate list from the
meta-review plus the one issue the original review *missed* (persistence-on-throw). Docs are honestly
reconciled, tasks honestly tracked (X-close and richer-target left open, not faked). Two real residuals
remained — one a **new regression introduced by the suppression fix** — plus a missed doc spot and a minor
robustness nit.

## Addressed (this session - gate green at 687/687)

- **#1 source-returns-muted — FIXED.** Added `ReturnPolicy.ResolveReturnSettings(...)` (pure, unit-tested):
  return now applies popout value → else pre-suppression launch value → else **forced un-mute**, so
  `ApplyPlaybackSettingsAsync` always writes `muted` and suppression is always undone. Launch volume/mute/
  rate are captured at popout start (`_source{Volume,Muted,PlaybackRate}AtPopout`). New tests
  `ResolveReturnSettings_prefers_popout_then_launch_and_forces_unmute` and
  `Return_to_a_different_video_without_popout_sample_replays_launch_settings`.
- **#2 spec §13.4 — FIXED.** Race-prevention pseudocode now shows `await BringVideoBackAsync()` instead of
  the stale focus-only `_player.Activate()`.
- **#4 replay-loop robustness — FIXED.** `ReplayPendingReturnStateAsync` now re-validates its target every
  iteration (reference-equality + `IsCurrentSourceReturnReplayTarget` inside the loop) and clears the
  pending state on timeout.
- **#3 (X-close fresh capture) intentionally left open** — it's the deferred owner decision (plan Task 3d),
  not a silent re-architecture. With #1 fixed, the X-close audio gap is closed; the remaining X-close gap is
  only finer media-fidelity (last timer sample vs a fresh DOM read).

## What landed correctly (verified)

| Fix | Evidence | Assessment |
|---|---|---|
| Option A return rule (docs) | Spec 931-944, QA, `SPEC_GAPS:108`, ADR row 1447 | Reconciled; **+ the two extra restatements (QA-table, ADR) the meta-review flagged as missed are now fixed** |
| Launch-from-paused gating (REQ-RETURN-07) | `PlayerWindow` ctor `nudgePlayOnInitialPause`; `MainWindow.xaml.cs:947` passes `_sourceWasPlayingAtPopout`; `SyncTimer_Tick` gate | Correct; default `true` preserves other callers/tests |
| Different-video replay | `_pendingReturnReplay` + `Core_NavigationCompleted` + `ReplayPendingReturnStateAsync` (12×250ms retry) + `ClearStalePendingReturnReplay` | Solid; closes the P4 navigate-path gap; pending replay now uses the same launch-setting fallback as same-video return and is pinned by WPF tests |
| Source suppression (double-audio) | `YouTubeDomBridge.SuppressPlaybackAsync` (mute+pause); `StartSourceSuppressionGuard` 1s reassert timer with reentrancy guard | Correct shape; the tracked double-audio bug moved `OPEN → NEEDS STABLE SMOKE`. **See residual #1.** |
| Final-capture authority (C8) | `_finalReturnPlaybackCaptured` set before the final read; re-checked after each `await` in `SyncTimer_Tick`; timer stopped first | Exactly the recommended one-flag fix; closes the timer-overwrite race |
| Retarget/nav sampling reset (§3.6) | `ResetPlaybackSamplingForNavigation` on nav-start; `RetargetTo` stops timer + `_navCompleted=false` + clears media; `Core_NavigationCompleted` now `_navCompleted = e.IsSuccess` | Fully closes the new-id/old-timestamp race |
| App-shutdown guard (§3.7) | `_mainWindowClosing` set in `MainWindow_Closing`; `Player_OnClosed` skips return scripting on shutdown | Clean; also gates the suppression tick and replay |
| Persistence-on-throw (**meta-review's "missed" finding**) | `Player_OnClosed:1125` saves settings *before* `ApplyReturnActionAsync` | Directly fixes the issue the original review didn't catch |

## Residuals

### 1. Source could return muted/silent after suppression. real-minor->moderate. FIXED.

Suppression now mutes the source synchronously at popout launch (`SuppressPlaybackAsync`:
`v.muted = true`). The unsafe shape was a no-sample return: if the popout closed before reporting
`Volume/Muted/PlaybackRate`, `ApplyPlaybackSettingsAsync` could skip the mute write and leave the source
silent.

The fix is now applied to both return paths:

- same-video return resolves popout settings -> launch settings -> forced unmute before scripting the
  source;
- different-video return stores the same resolved settings in `_pendingReturnReplay`, so the delayed
  post-navigation replay cannot carry all-null settings.

Pinned by `ResolveReturnSettings_prefers_popout_then_launch_and_forces_unmute` and
`Return_to_a_different_video_without_popout_sample_replays_launch_settings`. Still worth a 2-minute
`run-piplay` confirmation (pop out then immediately close; repeat from a paused source) because WebView2
DOM acceptance is outside the headless lane.

### 2. Doc gap — Spec §13.4 "Race prevention" still shows focus-only pseudocode. cosmetic. ✅ FIXED.

`PiPlay_Product_Engineering_Spec.md:886-890` still reads `if (_player is not null) { _player.Activate(); return; }`,
but the code now does `await BringVideoBackAsync()`. The thorough review's §18 explicitly called this out
("Update spec 13.4"); the address pass fixed REQ-RETURN-01 in the other four sites but missed this block.

### 3. X-close still timer-sampled (honestly deferred — plan Task 3d open). real-minor.

`PlayerWindow_Closing` sets the final-capture flag and captures window placement but does **no** fresh DOM
read, so X-close/Alt-F4 media fidelity is weaker than Bring-back. Acknowledged in the plan; combined with
#1 it's the weak path. Owner decision: add async close-deferral for a fresh capture, or document X-close as
timer-sampled fallback (and then #1 must be fixed for the no-sample case).

### 4. Replay loop: entry-only target validation + not cleared on timeout. nit. ✅ FIXED.

Two small robustness gaps in `ReplayPendingReturnStateAsync`:
- **Entry-only validation.** `IsCurrentSourceReturnReplayTarget` is checked **once before** the `for` loop;
  inside, iterations only check `current is not null`. The loop holds `state` in a local, so if the user
  navigates to *yet another* video during the 3s retry, the next iteration reads the new page and applies
  the **old** pending state (seek to old timestamp, old volume/mute/rate) to it — `ClearStalePendingReturnReplay`
  nulls the field but the running loop doesn't re-read it. Re-validate the target inside the loop.
- **No clear on timeout.** Cleared on success but not on the 12-attempt timeout (only
  `_pendingReturnReplayInProgress` resets in `finally`), so a later same-video `NavigationCompleted` could
  re-fire it; otherwise it sits inert until a different-video nav clears it. Clear pending on timeout too.

## Honestly deferred (per plan + addressed-findings) — not regressions

- Task 3d: X-close fresh capture (residual #3).
- Task 3e: richer return target (playlist/list/index; `PlayerReturnState` still `VideoId`-only).
- Task 8: test nits (pure `ParsePlayerState`/`BuildPlaybackSettingsScript` seams, clamp/locale tests,
  unused `*ForTests` scaffolding) + a plain X-close QA line.
- Runtime/Stable QA: duplicate-audio through ads/autoplay/SPA, live-page replay acceptance, deployed Stable
  manual smoke before any RC tag. **Note the suppression cadence is 1s** — up to ~1s of audio can leak
  through an ad / SPA element-swap before the guard re-mutes, so the smoke must watch for *brief* leaks at
  transitions, not just "does audio stop on popout."

## Recommendation

**#1, #2, #4 are now fixed (687/687 green).** What remains before RC is the deferred runtime/Stable QA pass
(duplicate-audio through ads/autoplay/SPA, live-page replay, deployed Stable smoke) and the owner-call items
(#3 X-close fresh capture, richer return target). Do not RC-tag before the Stable publish/verify + WebView2
smoke (the plan's own guard). The remaining headless-untestable behaviors — including the un-mute-on-return
this session added — still want a 2-minute `run-piplay` confirmation (pop out then immediately close; same
on a paused source).
