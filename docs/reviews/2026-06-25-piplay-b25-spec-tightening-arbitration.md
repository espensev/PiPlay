# PiPlay b25 full source review - spec tightening and review arbitration

Reviewed packet: `PiPlay-b25-full-source-review-packet-20260625-221304.zip`
Source packet base commit recorded in manifest: `a4fe91c`
Review date: 2026-06-25
Reviewer stance: code is ground truth; docs and prior reviews are checked against the provided source.

## 0. Scope and verification limits

I reviewed the full source packet, the product/spec docs, QA docs, changelog, packet manifests, and the review artifacts included in the packet. I also compared the uploaded review documents against the source packet.

I could not independently compile or run tests because `dotnet` is not installed in this environment. The packet manifest records a prior automated gate at `a4fe91c` with `dotnet clean`, `dotnet test`, build, and `git diff --check`, and records `683/683` tests passing. I did not independently verify that result here.

Manual Stable smoke is also not recorded as completed in the manifest. That matters because the highest-risk issues here depend on WebView2 timing, YouTube SPA behavior, autoplay/ad behavior, and real DPI/window rendering.

## 1. Executive verdict

Hold push/RC for a real b25/b26 release candidate.

The current code has materially moved past the old focus-only popout behavior. The Source Window button now really becomes `Bring video back`; the placeholder also exposes `Bring video back`; both paths call `BringVideoBackAsync()`, which captures return state and closes the popout so the normal return pipeline restores the Source Window.

That verifies the strongest positive claim in the later reviews: P4 is no longer a fake or focus-only button.

However, P4 is still not complete enough to claim full return-state fidelity. The blocking issues are:

1. The code and tests implement `popout live state wins when known`, while the product spec and QA still describe the older `sourceWasPlayingAtPopout wins` rule.
2. Different-video return navigates the Source Window to the returned video/timestamp, but does not replay returned paused/playing, volume, mute, or playback-rate state after navigation.
3. The explicit Source Window `Bring video back` path takes a fresh DOM capture, but the popout close button, window X, and Alt-F4 path rely on the latest timer sample.
4. The 250 ms popout sync timer is `async void` and unguarded, so overlapping reads can race and stale data can overwrite final capture.
5. Retarget/navigation can pair a new video id with an old timestamp because `_navCompleted` is not reset before navigation/retarget, while the timer can continue sampling.
6. App shutdown closes the popout and can still enter the normal source-return path while the Source Window itself is closing.
7. The source suppression on popout launch is a single pause call, not a robust pause+mute guard against YouTube/ad/autoplay resume.
8. The docs still overstate or blur several UI areas: P1 is a 4-DIP inset compromise, P3 is whole-popout opacity that fades video, P2 profile color is identity-only by default, and P7 soft/round is not a true larger rounded-card model.

Recommended decision:

```text
Choose Option A: popout live session state wins when known.
Then patch the specs, QA, changelog, and code reliability gaps to match.
```

## 2. Gate status

| Gate                              |                              Status | Reason                                                                                                   |
| --------------------------------- | ----------------------------------: | -------------------------------------------------------------------------------------------------------- |
| Push docs/code as a WIP branch    | Possible after docs are made honest | Current spec/QA contradictions will mislead reviewers and QA.                                            |
| Push as P4-complete               |                             Blocked | Different-video replay, X-close freshness, final-capture race, and source suppression remain unresolved. |
| RC tag                            |                             Blocked | Requires automated gate on full repo plus deployed Stable smoke.                                         |
| Owner visual signoff for P1/P3/P7 |                 Blocked on evidence | 4-DIP inset, whole-popout opacity, and DWM corner modes need screenshots/manual QA.                      |

## 3. Main code findings

### 3.1 Verified: Bring video back is now real

The earlier review claim that the popout action was only `Show/focus popout` is stale for this packet.

Code verified:

* `src/PiPlay/MainWindow.xaml.cs:781-789`

  * `PopOutButton_Click` calls `BringVideoBackAsync()` when `_player is not null`.
  * `PlaceholderBringBackButton_Click` also calls `BringVideoBackAsync()`.
* `src/PiPlay/MainWindow.xaml.cs:886-908`

  * `BringVideoBackAsync()` calls `player.CaptureReturnStateNowAsync()` and then `player.Close()`.
* `src/PiPlay/MainWindow.xaml.cs:918-926`

  * the label, tooltip, and UIA name flip between `Pop out video` and `Bring video back`.
* `src/PiPlay/MainWindow.xaml:175-189`

  * the placeholder copy now says `Playing in Video Popout`, `Ready to bring playback back here.`, and the action button says `Bring video back`.

Verdict: the focus-only P4 finding from earlier b25 reviews is refuted for this packet. It remains useful historical context only.

### 3.2 Verified: the code implements popout-live-state-wins

`src/PiPlay/Services/ReturnPolicy.cs:30-35`:

```csharp
var shouldResume = returnedPaused.HasValue ? !returnedPaused.Value : sourceWasPlaying;
```

That means:

```text
Known popout paused state -> authoritative.
Unknown popout paused state -> fall back to sourceWasPlayingAtPopout.
```

The tests also encode this rule in `tests/PiPlay.Tests/ReturnPolicyTests.cs:20-30` with `Popout_paused_state_overrides_source_launch_state_when_known`.

Verdict: the implementation is internally coherent. The specs and QA are stale, not the policy code.

### 3.3 Verified: different-video return drops playback-state replay

`src/PiPlay/Services/ReturnPolicy.cs:48-57` correctly returns `ReturnAction.Navigate` if the returned video id differs from the video id at popout launch.

`src/PiPlay/MainWindow.xaml.cs:1018-1031` only applies playback settings when the action is not `Navigate`:

```csharp
if (action != ReturnAction.Navigate && core is not null)
    await YouTubeDomBridge.ApplyPlaybackSettingsAsync(core, state.Volume, state.Muted, state.PlaybackRate);
```

For `ReturnAction.Navigate`, the code does this:

```csharp
_autoLastHandledVideoId = state.VideoId;
NavigateInternal(YouTubeUrlHelper.BuildWatchUrl(
    new YouTubeTarget { VideoId = state.VideoId }, state.LastKnownSeconds));
```

So the source navigates to the returned video and timestamp, but it does not replay:

* paused/playing state;
* volume;
* mute;
* playback speed;
* playlist/list/index context.

Verdict: the other reviews are correct. This is a P4 residual, not P2.

### 3.4 Verified: final capture can race with timer capture

`src/PiPlay/PlayerWindow.xaml.cs:467-482` uses an `async void` 250 ms sync timer without an in-flight guard.

`src/PiPlay/PlayerWindow.xaml.cs:484-489` final capture does:

```csharp
await CaptureCurrentPlaybackStateAsync();
CaptureReturnWindowState();
```

`CaptureReturnWindowState()` stops the timer, but it is called after the awaited DOM read. That means an already-running timer tick can still complete later and call `ApplyReturnPlaybackState(state)` with stale data.

Verdict: the timer/final-capture race finding is valid. Fix this before claiming reliable return-state preservation.

### 3.5 Verified: X-close is weaker than Source Window Bring-back

`src/PiPlay/PlayerWindow.xaml.cs:511`:

```csharp
private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
```

`src/PiPlay/PlayerWindow.xaml.cs:872-889`:

```csharp
private void PlayerWindow_Closing(object? sender, CancelEventArgs e)
    => CaptureReturnWindowState();
```

That captures window placement/fade/topmost, but not a fresh DOM playback state. X-close and Alt-F4 therefore rely on the last timer sample.

Verdict: the other reviews are correct. Product-wise, X-close and Bring-back should use the same return semantics unless the spec intentionally defines X-close as a weaker timer-sampled fallback.

### 3.6 Additional issue: retarget/navigation can mix old media state with new video id

In `src/PiPlay/PlayerWindow.xaml.cs:269-293`, `RetargetTo()` updates `_returnState.VideoId` and navigates to the new URL. It resets `LastKnownSeconds` and `_nudgedPlay`, but does not reset `_navCompleted` or stop the sync timer.

`Core_NavigationCompleted` sets `_navCompleted = true`, but `Core_NavigationStarting` does not reset it.

Since `SyncTimer_Tick` samples whenever `_navCompleted` is true, the timer can continue reading during a retarget/navigation transition. That can pair a new `VideoId` with an old page/video timestamp or stale paused/volume values.

Recommended invariant:

```text
During retarget/navigation, return media state is unknown until the new YouTube video element is loaded and sampled.
```

Implementation direction:

```text
- On RetargetTo and NavigationStarting: set _navCompleted = false and stop/suspend the sync timer.
- Clear media fields or mark them unknown for the new target.
- On NavigationCompleted + DOM-ready: resume sampling after a valid video element is found.
```

### 3.7 Additional issue: app shutdown can enter the normal source-return path

`src/PiPlay/MainWindow.xaml.cs:1077-1084` closes `_player` during MainWindow closing.

`src/PiPlay/PlayerWindow.xaml.cs:891-897` raises `PlayerClosed?.Invoke(this, _returnState)` on popout close.

`src/PiPlay/MainWindow.xaml.cs:969-997` then runs `Player_OnClosed`, which hides the placeholder, applies return action, saves settings, and logs returned playback.

During app shutdown, that means the app can try to seek/play/navigate the Source Window while the Source Window itself is closing.

Recommended invariant:

```text
App shutdown should persist popout placement/window state but should not apply source playback return.
```

Implementation direction:

```text
- Add _appClosing or _mainWindowClosing.
- In MainWindow_Closing, set the flag before closing the popout.
- In Player_OnClosed, if _appClosing is true, skip ApplyReturnActionAsync and source placeholder/UI restoration; only persist settings/state that should survive shutdown.
```

### 3.8 Verified: return target is too small

`src/PiPlay/Models/PlayerReturnState.cs:25-31` stores `VideoId`, but not a full `YouTubeTarget`, playlist/list/index, or canonical URL.

Different-video return reconstructs a target using only:

```csharp
new YouTubeTarget { VideoId = state.VideoId }
```

Verdict: playlist/list/index context can be lost on return. This is not always visible on simple watch URLs, but it contradicts playlist-context preservation expectations.

### 3.9 Verified: source suppression is weak against duplicate audio

On popout launch, `src/PiPlay/MainWindow.xaml.cs:802-805` captures source state, then `src/PiPlay/MainWindow.xaml.cs:831-832` calls `YouTubeDomBridge.PauseAsync(core)` and shows the placeholder.

That is a single pause call. There is no mute guard and no short reassertion guard against YouTube SPA/ad/autoplay behavior.

Verdict: if the double-audio bug is reproduced in Stable smoke, it should block release. The source surface should be paused and muted/guarded while the popout is active.

### 3.10 Verified caveat: popout launch can nudge a paused source into playing

`src/PiPlay/PlayerWindow.xaml.cs:473-479` nudges the popout to play if the first sampled video state is paused and `_nudgedPlay` is false.

Under Option A, this creates a visible product case:

```text
Source paused -> open popout -> popout is nudged to play -> return -> source plays.
```

That is not necessarily wrong, but it must be a deliberate product rule. My recommendation is not to auto-play a popout that was launched from a paused source. Pass the initial source playback intent to `PlayerWindow` and only apply the play nudge when the source was playing at popout launch.

### 3.11 Verified: compact mode return fidelity is incomplete if re-enabled

`src/PiPlay/PlayerWindow.xaml.cs:328-340` updates timestamp and video id from compact shell state, but explicitly does not report paused/volume/mute/rate.

Verdict: acceptable only if compact remains dormant. If compact is re-enabled, it must carry full return session state.

## 4. UI and product-priority status

| Priority                   |                                     My status | Advice                                                                                                                                                       |
| -------------------------- | --------------------------------------------: | ------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| P1 border/inset            |                        Improved, not complete | 4-DIP inset is a compromise. Do deployed visual QA at 100/125/150% DPI. Do not call it trayless until screenshots prove it reads as video canvas, not frame. |
| P2 profile/accent          | Not implemented as full profile-driven accent | Keep global app accent as default and add a future explicit `active profile overrides accent` option if the owner still wants it.                            |
| P3 transparency            |                     Whole-popout opacity only | Keep current label honest. Do not call this chrome-only transparency; it fades video.                                                                        |
| P4 bring-back              |                           Real but incomplete | Fix spec rule, final capture, X-close, different-video replay, and source suppression.                                                                       |
| P5 unused player space     |                                   Mostly open | 4-DIP helps but does not create Fit/Fill/Cinema modes.                                                                                                       |
| P6 auto-hide               |                        Partial/popout-focused | Main-window cinema/mini modes should be separate future work.                                                                                                |
| P7 soft/round              |                              Still unresolved | Do not expose `soft` and `round` as meaningfully different outer-window modes if both map to DWM Round.                                                      |
| P8 main/popout consistency |                                       Partial | Depends on P2/P3/P4/P5/P7 decisions.                                                                                                                         |

## 5. Arbitration against the other reviews

| Review artifact                                     | My arbitration                                              | Notes                                                                                                                                                                                                                             |
| --------------------------------------------------- | ----------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `PiPlay-b25-code-review-thorough.md`                | Mostly verified; strengthen with two extra lifecycle issues | Its core blockers are correct: spec rule mismatch, different-video replay gap, X-close freshness, timer race, 4-DIP compromise, and whole-popout opacity. Add retarget `_navCompleted` reset issue and app-shutdown return guard. |
| `PiPlay-b25-opinion-packet-full-review.md`          | Mostly verified                                             | Correct to choose Option A, update REQ-RETURN-01/QA/SPEC_GAPS/changelog, and treat cross-video replay as P4 blocker. Add launch-from-paused product decision and shutdown guard.                                                  |
| `2026-06-25-v0.7.2-b25-full-review.md`              | Partly superseded                                           | P4 focus-only finding is stale for this packet. P1/P2/P3/P5/P7 findings remain directionally valid.                                                                                                                               |
| `2026-06-25-v0.7.2-b25-full-review-evaluation.md`   | Needs chronology banner                                     | It should state that the early P4 focus-only assessment was for pre-follow-up HEAD and is superseded by current Bring-back code.                                                                                                  |
| `2026-06-25-b25-full-review-followup.md`            | Good plan; add tasks                                        | Add X-close fresh capture, retarget capture guard, app shutdown guard, Blackout/theme preset tracking, and double-audio guard.                                                                                                    |
| `2026-06-25-b25-followup-package-review.md`         | Finding verified; heading wrong                             | Rename `P2 - Returned-video navigation...` to `P4 residual - Returned-video navigation...`.                                                                                                                                       |
| `2026-06-25-codex-p4-bring-video-back-review.md`    | Verified                                                    | Correctly reframes the problem as a return-rule decision, not simply missing implementation.                                                                                                                                      |
| `2026-06-25-codex-opinion-b25-return-rule.md`       | Verified with caveat                                        | Option A is right. Add the launch-from-paused caveat so auto-play does not accidentally rewrite user intent.                                                                                                                      |
| `2026-06-25-product-spec-vs-change-specs-review.md` | Mostly superseded by current spec                           | Current product spec mostly uses 4 DIP in normative sections. QA checklist still has stale 10-DIP rows.                                                                                                                           |
| `2026-06-25-v0.7.2-b25-code-audit.md`               | Verified directionally                                      | The 4-DIP hit-test/inset model exists and is test-pinned. Still needs deployed visual QA.                                                                                                                                         |
| `2026-06-25-claude-dependency-audit.md`             | Locally inspected only                                      | I verified local package versions in csproj files, but did not recheck external latest NuGet versions here. Treat external update recommendations as needing fresh package verification before acting.                            |

## 6. Tightened product/spec language

### 6.1 Replace REQ-RETURN-01

Recommended final wording:

```text
[REQ-RETURN-01] Single active playback session return
PiPlay treats the Source Window and Popout Player as two surfaces for one active playback session. While playback is detached, the Popout Player owns the live session state. On any normal return from the Popout Player to the Source Window, PiPlay must restore the latest known Popout Player session state: returned target, timestamp, play/pause, volume, mute, and playback rate where YouTube exposes them. If live popout playback state is unavailable, PiPlay must fall back to the source playback intent captured before popout launch. In that fallback case only, playback resumes if the source was playing when Video Popout started; otherwise it returns paused.
```

### 6.2 Add REQ-RETURN-02: final capture wins

```text
[REQ-RETURN-02] Authoritative final capture
Before a normal return, PiPlay must perform one authoritative final capture of the Popout Player media state and current target when WebView2/YouTube state is available. Periodic timer samples must not overlap this final capture or overwrite it after completion. A return-state capture is valid only when media state and current target are from the same loaded YouTube target.
```

### 6.3 Add REQ-RETURN-03: close paths are equivalent

```text
[REQ-RETURN-03] Equivalent normal close paths
The Source Window Bring video back action, the Popout Player close button, window X, and Alt-F4 are all normal return paths and must use the same return-state rule and final-capture behavior. App shutdown is not a normal return path; it may persist popout placement/state but must not attempt to seek/play/navigate the closing Source Window.
```

### 6.4 Add REQ-RETURN-04: different-video replay

```text
[REQ-RETURN-04] Returned target navigation replay
If the returned popout target differs from the source target at popout launch, PiPlay must navigate the Source Window to the returned target and timestamp, then replay captured volume, mute, playback rate, and play/pause after the returned YouTube video element is ready. Replay must use bounded retry and must clear itself after success, timeout, or a newer user navigation.
```

### 6.5 Add REQ-RETURN-05: target fidelity

```text
[REQ-RETURN-05] Return target fidelity
Return state must carry enough target information to restore the user's returned context. Video id alone is insufficient when playlist/list/index or canonical URL context is known. Equality comparisons may use video id, but navigation should use the richer validated target.
```

### 6.6 Add REQ-RETURN-06: source suppression while popped out

```text
[REQ-RETURN-06] Source suppression while popout is active
While a Popout Player owns the playback session, the Source Window WebView must not continue audible playback. PiPlay must pause and, where practical, mute or guard the source player against YouTube SPA/ad/autoplay resume until the popout returns, fails, or is closed.
```

### 6.7 Add REQ-RETURN-07: launch-from-paused behavior

Recommended choice:

```text
[REQ-RETURN-07] Initial popout playback intent
When Video Popout is launched, the Popout Player should preserve the source's current play/pause intent. If the source was playing, the popout may nudge playback to start after load. If the source was paused, the popout should open paused unless the user explicitly starts playback in the popout. This avoids turning a paused source into playing merely because it was popped out.
```

Alternative if the owner wants popout to always play:

```text
Opening a popout always starts playback. Under REQ-RETURN-01, returning that playing popout will return the source playing. QA must expect this behavior even when the source was paused before popout launch.
```

I recommend the first version.

### 6.8 Tighten border/inset wording

```text
[REQ-WINDOW-BORDER-01] Borderless visual target with technical resize inset
PiPlay should not show a grey/double outer frame or a decorative tray around the video surface. The current implementation may reserve a 4-DIP WebView inset and 32-DIP corner hit-test length for native resize behavior. That inset is acceptable only if deployed visual QA at 100%, 125%, and 150% DPI shows it reads as video canvas/cinema padding, not as an app border. QA and docs must not refer to the old 10-DIP inset as current behavior.
```

### 6.9 Tighten opacity wording

```text
[REQ-OPACITY-01] Whole-popout opacity semantics
The current opacity controls apply to the whole Popout Player window, including the video surface. This feature must be labeled as whole-popout opacity. It must not be described as chrome-only transparency or video-safe transparency.
```

Future request:

```text
[REQ-OPACITY-FUTURE] Chrome/background transparency
A future transparency feature may offer Main only, Popout only, or Both scopes, and Chrome/background-only versus Whole-window targets. Video should remain opaque by default.
```

### 6.10 Tighten profile/accent wording

```text
[REQ-ACCENT-01] Global accent default
The app accent is a global appearance setting by default. Profile color is profile identity by default and should appear in identity surfaces such as chips, dots, labels, or profile markers.
```

Future option:

```text
[REQ-ACCENT-FUTURE] Active profile accent override
A future explicit setting may let the active profile color override the global app accent while that profile is active. This should be a named user choice, not an accidental side effect of selecting a profile.
```

### 6.11 Tighten corner-mode wording

```text
[REQ-CORNERS-01] Honest native corner modes
Until PiPlay has a composition/windowless architecture capable of clipping WebView2 and rendering a custom rounded card, user-facing outer-window corner modes must describe actual native DWM behavior. Do not expose `soft` and `round` as two visibly different outer-window modes if both map to the same DWM Round preference.
```

## 7. QA checklist replacements/additions

### 7.1 Replace old source-anchored return rows

Use these rows after choosing Option A:

```text
- [ ] Playing source -> open popout -> leave popout playing -> Bring video back returns source playing at the captured timestamp.
- [ ] Playing source -> open popout -> pause in popout -> Bring video back returns source paused at the captured timestamp.
- [ ] Paused source -> open popout -> popout remains paused -> Bring video back returns source paused at the captured timestamp.
- [ ] Paused source -> open popout -> user starts playback in popout -> Bring video back returns source playing at the captured timestamp.
- [ ] Unknown popout state fallback: if popout state cannot be read, return uses sourceWasPlayingAtPopout.
```

### 7.2 Add X-close equivalence rows

```text
- [ ] Popout close button after pausing in popout returns source paused at the captured timestamp.
- [ ] Popout close button after resuming in popout returns source playing at the captured timestamp.
- [ ] Window X / Alt-F4 uses the same return semantics as Bring video back.
- [ ] Immediate close after changing volume/mute/speed does not lose the last user-visible popout state.
```

### 7.3 Add different-video return rows

```text
- [ ] In popout, navigate to a different YouTube video, pause it, change volume/mute/speed, then Bring video back. Source navigates to the returned video/timestamp and replays paused/volume/mute/speed where YouTube permits.
- [ ] Repeat the previous test with popout X-close.
- [ ] With Auto popout enabled, returning a different video does not immediately re-pop the same returned video.
- [ ] Playlist/list context is preserved where available.
```

### 7.4 Add capture-race rows

```text
- [ ] Rapid pause/play/seek/volume changes followed immediately by Bring video back return the latest visible popout state.
- [ ] Rapid pause/play/seek/volume changes followed immediately by X-close return the latest visible popout state.
- [ ] Retargeting the popout to a new video does not return an old video's timestamp paired with the new video id.
```

### 7.5 Add source-suppression rows

```text
- [ ] While popout is active, the Source Window produces no duplicate audio.
- [ ] Source duplicate audio remains suppressed through YouTube ad transitions, autoplay-next, and SPA navigation while popout is active.
- [ ] If popout launch fails, source audio/playback returns to the prior source intent without duplicate audio.
```

### 7.6 Fix P1 visual rows

Replace 10-DIP language with 4-DIP language:

```text
- [ ] Source and popout show no grey/double outer border at 100%, 125%, and 150% DPI.
- [ ] Any 4-DIP edge area reads as black video canvas/cinema padding, not as an app tray or decorative frame.
- [ ] Resize still works at borders/corners, including 32-DIP corner hit-test behavior.
- [ ] Wheel/pointer behavior over the 4-DIP resize band is acceptable and does not hijack page interaction.
```

## 8. Suggested implementation order

### Step 1 - Decide and patch specs

Choose Option A and update:

* `docs/PiPlay_Product_Engineering_Spec.md`
* `docs/QA_Checklist.md`
* `docs/SPEC_GAPS_AND_OWNERSHIP.md`
* `docs/CHANGELOG.md`
* review/follow-up docs that still quote pre-follow-up P4 behavior as current

Also decide `REQ-RETURN-07` for launch-from-paused behavior.

### Step 2 - Make final capture authoritative

Implementation shape:

```csharp
private bool _syncTickInProgress;
private bool _finalReturnCaptureInProgress;
private int _captureGeneration;

private async void SyncTimer_Tick(object? sender, EventArgs e)
{
    if (_syncTickInProgress || _finalReturnCaptureInProgress) return;
    if (!_navCompleted || Player.CoreWebView2 is null) return;

    _syncTickInProgress = true;
    var generation = _captureGeneration;
    try
    {
        var state = await YouTubeDomBridge.ReadPlayerStateAsync(Player.CoreWebView2);
        if (state is null) return;
        if (_finalReturnCaptureInProgress || generation != _captureGeneration) return;
        ApplyReturnPlaybackState(state);
    }
    finally
    {
        _syncTickInProgress = false;
    }
}

internal async Task<PlayerReturnState> CaptureReturnStateNowAsync()
{
    _finalReturnCaptureInProgress = true;
    _captureGeneration++;
    _syncTimer.Stop();
    try
    {
        await CaptureCurrentTargetAndPlaybackStateAsync();
        CaptureReturnWindowState();
        return _returnState;
    }
    finally
    {
        _finalReturnCaptureInProgress = false;
    }
}
```

The exact implementation can differ, but the invariant must be:

```text
Final capture wins. Timer capture cannot overwrite it.
```

### Step 3 - Reset navigation sampling during retarget

Implementation shape:

```text
- In RetargetTo and Core_NavigationStarting:
  - _navCompleted = false
  - suspend/stop sync timer
  - clear LastKnownSeconds or mark media state unknown
- In Core_NavigationCompleted:
  - wait for/confirm a video element for the new target
  - _navCompleted = true
  - resume sync timer
```

### Step 4 - Make X-close equivalent

Implementation shape:

```text
- Popout close button: disable controls, await CaptureReturnStateNowAsync(), then Close().
- Window Closing / Alt-F4: first close request cancels, runs async final capture, then closes again with a guard flag.
- App shutdown: use a separate shutdown flag and skip source playback return.
```

### Step 5 - Add pending post-navigation replay

Implementation shape:

```text
- Add _pendingReturnReplayState in MainWindow.
- For ReturnAction.Navigate:
  - store a cloned PlayerReturnState / rich target
  - navigate to returned target/timestamp
  - keep _autoLastHandledVideoId guard
- After source navigation completes and video element is ready:
  - apply volume/mute/playbackRate
  - seek to timestamp if needed
  - pause/play according to returned Paused
  - retry with bounded timeout
  - clear pending state on success, timeout, or user navigation
```

### Step 6 - Strengthen source suppression

Implementation shape:

```text
- On popout start:
  - capture source state
  - pause source
  - mute source or apply a temporary source-suppression guard
  - reassert pause/mute on a short timer and after likely SPA/ad transitions
- On return/failure:
  - stop guard
  - restore according to REQ-RETURN-01 or fallback source state
```

### Step 7 - Add app-shutdown guard

Implementation shape:

```text
private bool _appClosing;

private void MainWindow_Closing(object? sender, CancelEventArgs e)
{
    _appClosing = true;
    _autoPopoutTimer.Stop();
    _player?.Close();
}

private async void Player_OnClosed(object? sender, PlayerReturnState state)
{
    if (_appClosing)
    {
        _player = null;
        SaveSettings();
        return;
    }

    // normal return behavior
}
```

### Step 8 - Update tests

Add tests/seams for:

* ReturnPolicy known paused overrides source launch state.
* Unknown popout state falls back to sourceWasPlayingAtPopout.
* Different-video return queues post-navigation replay.
* X-close invokes final capture path.
* Timer sample cannot overwrite final capture.
* Retarget clears old media state until new video is ready.
* App shutdown skips source return action.
* QA XAML strings use 4 DIP, not 10 DIP.

## 9. Changelog language until cross-video replay lands

Use this safer wording if replay is not implemented yet:

```text
Bring video back (P4): while a popout exists, the Source Window primary action and placeholder action now return playback to the Source Window instead of only focusing the popout. Same-video return captures fresh popout timestamp, paused state, volume, mute, and playback speed where the YouTube DOM exposes them. Different-video return navigates the source to the returned video/timestamp; replay of paused/volume/mute/speed after that navigation remains a follow-up until the pending replay path lands.
```

After replay is implemented and QA passes, the stronger claim can be restored.

## 10. Release checklist

Before RC, require:

```text
- dotnet clean PiPlay.sln --configuration Debug --nologo
- dotnet test PiPlay.sln --configuration Debug --nologo
- scripts/Build-PiPlay.ps1 -Stage Build -NoVersionBump -NoBuildNumberBump
- git diff --check
- scripts/Publish-Stable.ps1
- scripts/Verify-StableDeploy.ps1
- manual Stable smoke from updated QA_Checklist.md
```

Manual smoke must explicitly include:

```text
- same-video Bring-back
- same-video X-close
- different-video Bring-back
- different-video X-close
- immediate return after pause/volume/mute/speed changes
- duplicate-audio/ad/autoplay guard
- 100/125/150% DPI border screenshots
- whole-popout opacity visual behavior
- profile/accent and corner-mode visual sanity
```

## 11. Bottom line

Verified:

```text
Bring video back is real now.
Popout live state wins in code/tests.
The other reviews correctly identify the main spec/code mismatch and cross-video replay gap.
```

Refuted or superseded:

```text
The old claim that the current button only focuses the popout is stale.
The old/current-doc implication that 10 DIP is the active inset is stale for code and product spec, though still stale in QA checklist rows.
```

Added beyond the other reviews:

```text
Retarget/navigation should reset sampling so old timestamps cannot pair with new video ids.
App shutdown needs a guard so popout close does not attempt normal source playback return while the Source Window is closing.
Launch-from-paused behavior must be specified because the current popout play nudge can turn a paused source into a playing returned source under Option A.
```

Recommendation:

```text
Hold RC.
Patch the spec to Option A, make final capture deterministic, make X-close equivalent, add different-video replay, strengthen source suppression, then run automated and Stable manual QA.
```
