# PiPlay b25 code review packet — thorough source-grounded review

Reviewed packet: `PiPlay-b25-code-review-packet-20260625-215921.zip`  
Packet base commit: `a4fe91c`  
Review focus: P4 bring-back implementation, return-state fidelity, doc/code contradictions, and the remaining UI priorities from the earlier reviews: border/inset, unused player space, soft/round behavior, opacity, and profile/accent behavior.

## 0. Scope and confidence

This review treats the **code in the packet as the ground truth** and calls out places where the docs disagree with the implementation.

I could not run `dotnet test` or compile locally because `dotnet` is not installed in this environment. The packet manifest records that automated QA passed at `a4fe91c` with `683/683` tests, but I did not independently verify that run here.

Also, the packet is not a complete repo snapshot. It contains the files needed to inspect the P4 implementation, but it omits important files such as `PlayerWindow.xaml`, theme resource XAML, and several services referenced by the tests. That means the extracted packet alone is not a buildable verification unit. For this review, I inspected the provided source, tests, and docs directly.

## 1. Executive verdict

The code is **much closer than the docs imply**. The earlier “Show popout only focuses the popout” problem has been materially changed in source.

The Source Window now has a real **Bring video back** action:

- the main popout button switches to `Bring video back` while a popout exists;
- the placeholder button is also `Bring video back`;
- both route into `BringVideoBackAsync()`;
- that method captures popout return state, closes the popout, and lets the normal close/return pipeline restore the Source Window.

So P4 is no longer just a placeholder/focus issue.

However, I would **not mark this push/RC-ready** yet. The implementation still has release-significant holes around return fidelity and stale state capture:

1. The code implements **popout live state wins when known**, but the product spec and parts of QA still say **source-was-playing-at-popout wins**.
2. Returning from a **different video** navigates the source to the new video, but does not replay the returned paused/volume/mute/speed state after navigation.
3. Fresh capture is only guaranteed for the explicit Bring-back path; closing the popout through the popout close button/window close relies on the latest timer sample.
4. The 250 ms sync timer can overlap asynchronous DOM reads and race with the final capture.
5. The packet/docs still overstate some areas: P3 opacity is honest now as “whole popout opacity,” but it is not video-safe transparency; P1 is still a 4-DIP inset compromise, not a truly trayless/borderless video surface.

Recommended gate:

```text
Hold RC.
Patch P4 state fidelity and update docs/spec/QA to match the code rule.
Then run automated tests plus Stable deployed manual smoke.
```

## 2. Priority status against the owner/UI stack

| Priority | Code review status | Notes |
|---|---:|---|
| **P1 — remove ugly border everywhere** | Improved, not complete | Main WebView now uses a 4-DIP resize inset, not the old 10-DIP band. That is better, but tests still preserve an intentional tray/inset. Manual visual QA decides whether 4 DIP is acceptable. |
| **P2 — profile color affects the full UI** | Not implemented against the original owner request | Code/docs still use profile color as identity, not full app accent. That may be a valid product decision, but it does not satisfy “profile colors need to do more than one button each.” |
| **P3 — transparency main/popout/both** | Not implemented | Settings now correctly labels the feature as **Whole popout opacity**. This is honest, but the video still fades/gets washed out. No main/popout/both chrome-only transparency exists. |
| **P4 — popout button brings video back** | Mostly implemented, but state fidelity incomplete | The button now closes/docks the popout through the return path. Same-video return is much improved. Different-video return and X-close freshness remain incomplete. |
| **P5 — use more player space** | Slightly improved only | The 4-DIP inset helps. There is still no real `Fit window` / `Fill/crop` / `Cinema padding` model. |
| **P6 — auto-hide chrome** | Not the focus of this packet | Popout strip auto-hide exists. Main-window chrome auto-hide is still not addressed here. |
| **P7 — round/soft distinction** | Still unresolved | The packet does not include the theme mapping code. The changelog still says `soft` and `round` both map to DWM `Round`, meaning the outer-window distinction is not real. |
| **P8 — consistent main/popout theming** | Partial | Accent, opacity, and corners propagate to the open popout in code, but profile color behavior, opacity semantics, and main/popout chrome behavior are still inconsistent against the original desired model. |

## 3. What the code actually implements for P4

### 3.1 The Source Window action really changed

`MainWindow.xaml` now defines the placeholder as a return action, not just a focus action:

```text
src/PiPlay/MainWindow.xaml:175-189
```

The placeholder text says:

```text
Playing in Video Popout
Ready to bring playback back here.
Bring video back
```

That is a good UI change. The wording now describes a useful action instead of leaving the user in a dead empty state.

The primary toolbar button also switches behavior in code:

```text
src/PiPlay/MainWindow.xaml.cs:781-789
```

```csharp
private async void PopOutButton_Click(object sender, RoutedEventArgs e)
{
    if (_player is not null) await BringVideoBackAsync();
    else await StartVideoPopoutAsync();
}

private async void PlaceholderBringBackButton_Click(object sender, RoutedEventArgs e) =>
    await BringVideoBackAsync();
```

The label/tooltip/UIA name update is centralized:

```text
src/PiPlay/MainWindow.xaml.cs:918-926
```

```csharp
var label = hasPlayer ? "Bring video back" : "Pop out video";
PopOutButtonText.Text = label;
AutomationProperties.SetName(PopOutButton, label);
```

That is the right direction. It means the user-visible button and accessibility name agree.

### 3.2 Bring-back closes through the return pipeline

The actual bring-back method is:

```text
src/PiPlay/MainWindow.xaml.cs:886-908
```

It does three important things:

1. guards `_player is null` and `_popoutInProgress`;
2. disables the popout button;
3. calls `player.CaptureReturnStateNowAsync()` and then `player.Close()`.

The close event then runs the standard return handler:

```text
src/PiPlay/MainWindow.xaml.cs:969-997
```

The return handler hides the placeholder, applies the return action, saves settings, and logs return.

This means the code is no longer “show/focus popout only.” P4 is real in code.

### 3.3 Tests now pin the visible P4 behavior

The tests cover the basic UI contract:

```text
tests/PiPlay.Tests/Ui/XamlInvariantTests.cs:257-268
tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs:657-690
```

They assert:

- placeholder button exists;
- content is `Bring video back`;
- style is `AccentButton`;
- tooltip contains return wording;
- primary button label/tooltip/UIA flip together.

That is useful regression coverage. The remaining issue is that the tests do **not** yet verify the full behavioral state transfer through WebView2.

## 4. Main blocker: code and spec disagree on the return rule

The code has chosen this rule:

```text
When popout paused state is known, popout live state wins.
When popout paused state is unknown, fall back to source-was-playing-at-popout.
```

The implementation is explicit:

```text
src/PiPlay/Services/ReturnPolicy.cs:30-35
```

```csharp
var shouldResume = returnedPaused.HasValue ? !returnedPaused.Value : sourceWasPlaying;
```

`MainWindow.ApplyReturnActionAsync()` passes the popout state into that policy:

```text
src/PiPlay/MainWindow.xaml.cs:1012-1016
```

```csharp
var action = ReturnPolicy.Decide(
    state.LastKnownSeconds,
    _sourceWasPlayingAtPopout,
    state.Paused,
    state.VideoId,
    _popoutSourceVideoId);
```

The unit tests also encode this rule:

```text
tests/PiPlay.Tests/ReturnPolicyTests.cs:20-30
```

```csharp
Popout_paused_state_overrides_source_launch_state_when_known
```

That is a coherent product rule. I recommend keeping it.

But the spec still says the opposite:

```text
docs/PiPlay_Product_Engineering_Spec.md:932-933
```

It still states that source playback resumes only if the source was playing when popout started, and that it should not be inferred later from close-time state.

`SPEC_GAPS_AND_OWNERSHIP.md` also repeats the old normative rule:

```text
docs/SPEC_GAPS_AND_OWNERSHIP.md:108
```

The QA checklist still has the old paused-source expectation:

```text
docs/QA_Checklist.md:27-28
```

This is not just documentation polish. If QA follows the current checklist while code follows the new rule, QA can mark correct behavior as a failure or miss a real failure.

### Recommendation

Make the product rule explicit and update the docs/tests accordingly:

```text
REQ-RETURN-01:
On return, the Source Window should mirror the Popout Player's current playback state when known.
If the popout reports paused, return paused.
If the popout reports playing, return playing.
If the popout state is unknown, fall back to sourceWasPlayingAtPopout.
```

This is the best rule for user intent. Once a video is popped out, the popout is the active playback surface. Bring-back should transfer the live session, not resurrect the source window’s old pre-popout intent.

## 5. P4 residual: different-video return loses playback settings

The code correctly detects a different returned video:

```text
src/PiPlay/Services/ReturnPolicy.cs:48-58
```

If `returnedVideoId != sourceVideoIdAtPopout`, the policy returns `ReturnAction.Navigate`.

`MainWindow.ApplyReturnActionAsync()` then navigates the Source Window:

```text
src/PiPlay/MainWindow.xaml.cs:1023-1031
```

```csharp
_autoLastHandledVideoId = state.VideoId;
NavigateInternal(YouTubeUrlHelper.BuildWatchUrl(
    new YouTubeTarget { VideoId = state.VideoId }, state.LastKnownSeconds));
```

That fixes the old corruption risk where PiPlay might seek the original video to a timestamp that belonged to a different video.

But this block also exposes the remaining gap. Playback settings are only applied before the switch when the action is **not** `Navigate`:

```text
src/PiPlay/MainWindow.xaml.cs:1018-1019
```

```csharp
if (action != ReturnAction.Navigate && core is not null)
    await YouTubeDomBridge.ApplyPlaybackSettingsAsync(core, state.Volume, state.Muted, state.PlaybackRate);
```

So for the different-video path, PiPlay:

- navigates to the returned video;
- includes the timestamp in the URL;
- arms `_autoLastHandledVideoId` to avoid immediate re-popout;
- but does **not** apply paused/playing state after the new YouTube page loads;
- does **not** apply volume/mute/speed after the new page loads.

That conflicts with the new P4 QA row:

```text
docs/QA_Checklist.md:80-81
```

The QA row says to change pause/volume/mute/speed in the popout, click Bring video back, and expect those values preserved where YouTube permits. The code only does that for the same-video/script path, not the navigate path.

### Recommendation

Add a pending replay path for `ReturnAction.Navigate`.

Suggested shape:

```text
1. Before NavigateInternal, store a pending PlayerReturnState clone.
2. Navigate to the returned target.
3. On Source Browser NavigationCompleted / DOM-ready for the returned watch page:
   - apply volume/mute/playbackRate;
   - seek to LastKnownSeconds;
   - pause or play based on state.Paused;
   - clear pending replay after success or timeout.
4. Use a retry loop because YouTube's video element may not exist at first NavigationCompleted.
```

Important: keep `_autoLastHandledVideoId = state.VideoId` before navigation, as the current code does. That part is correct.

## 6. P4 residual: final state capture can be stale or overwritten

The popout keeps return state fresh with a timer:

```text
src/PiPlay/PlayerWindow.xaml.cs:141-142
src/PiPlay/PlayerWindow.xaml.cs:467-482
```

`SyncTimer_Tick` is `async void` and does not guard against overlap:

```csharp
private async void SyncTimer_Tick(object? sender, EventArgs e)
{
    if (!_navCompleted || Player.CoreWebView2 is null) return;
    var state = await YouTubeDomBridge.ReadPlayerStateAsync(Player.CoreWebView2);
    ...
    ApplyReturnPlaybackState(state);
}
```

The timer interval is 250 ms. A slow WebView2 `ExecuteScriptAsync` call can exceed that. That means multiple timer ticks can be in flight at once.

The explicit Bring-back path calls:

```text
src/PiPlay/PlayerWindow.xaml.cs:484-489
```

```csharp
await CaptureCurrentPlaybackStateAsync();
CaptureReturnWindowState();
```

But any already-in-flight `SyncTimer_Tick` can still complete after the explicit final read and call `ApplyReturnPlaybackState(state)` with older data.

`CaptureReturnWindowState()` stops the timer:

```text
src/PiPlay/PlayerWindow.xaml.cs:875-889
```

But stopping the timer does not cancel an already-running asynchronous tick.

### Why this matters

The user-facing promise is that Bring-back captures the current popout state. With the current timer structure, a stale timer completion can theoretically overwrite the final capture. It is a classic async timer race.

### Recommendation

Make final capture authoritative.

Minimum patch:

```text
- Add _syncTickInProgress so timer reads never overlap.
- Stop _syncTimer before the final DOM read.
- Add _finalReturnCaptureInProgress or a monotonic capture version.
- After every awaited DOM read, check that the read is still allowed before applying it.
```

Better shape:

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
    await CaptureCurrentPlaybackStateAsync();
    CaptureReturnWindowState();
    return _returnState;
}
```

The exact implementation can differ, but the invariant should be:

```text
Final capture wins. Timer capture cannot overwrite it.
```

## 7. P4 residual: X-close does not do the same fresh capture as Bring-back

The Source Window’s Bring-back button explicitly captures current DOM state before closing:

```text
src/PiPlay/MainWindow.xaml.cs:894-896
```

But the popout close button itself is just:

```text
src/PiPlay/PlayerWindow.xaml.cs:511
```

```csharp
private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
```

The `Closing` handler only captures window state:

```text
src/PiPlay/PlayerWindow.xaml.cs:872-889
```

```csharp
private void PlayerWindow_Closing(object? sender, CancelEventArgs e)
    => CaptureReturnWindowState();
```

That stops timers and captures topmost/fade/placement, but it does not perform a final `ReadPlayerStateAsync`.

So there are currently two return paths with different freshness guarantees:

| Path | Capture freshness |
|---|---|
| Source Window **Bring video back** | Explicit DOM capture before close. |
| Popout close button / window close / Alt-F4 | Last timer sample only. |

The timer sample may usually be close enough, but it is not the same guarantee. If the user pauses, changes volume, or changes speed and immediately closes the popout, the returned state may be stale.

### Recommendation

Make close return as deterministic as Bring-back.

Possible implementation:

```text
Close button:
- disable close button / strip controls;
- await CaptureReturnStateNowAsync();
- Close();
```

For system close / Alt-F4, either:

1. implement an async-close guard in `Closing`:
   - first close request cancels;
   - run `CaptureReturnStateNowAsync()`;
   - call `Close()` again with a guard flag;

or:

2. document X-close as timer-sampled fallback and adjust QA.

For product quality, I recommend option 1. The app already treats Q-2 as a high-quality return requirement; X-close is a normal user path, not an edge case.

## 8. P4 residual: return completion races the button state

`BringVideoBackAsync()` calls `player.Close()` and then immediately reaches its `finally` block:

```text
src/PiPlay/MainWindow.xaml.cs:895-908
```

But the actual source return runs in `Player_OnClosed`, which is `async void`:

```text
src/PiPlay/MainWindow.xaml.cs:969-988
```

That method awaits `ApplyReturnActionAsync(state)`. Since it is `async void`, the caller does not await it.

This means `BringVideoBackAsync()` can re-enable the popout button before source return has completely finished, especially if return does a slow script call or later grows a post-navigation replay path.

### Recommendation

Add an explicit `_returnInProgress` state or a TaskCompletionSource so the Source Window remains in a stable disabled/returning state until `ApplyReturnActionAsync` completes.

This becomes more important once different-video navigate has a pending replay after navigation. Without a return-in-progress guard, a user can click Pop out again during an incomplete return.

## 9. Return target is too small: only video ID is stored

`PlayerReturnState` stores the final video ID only:

```text
src/PiPlay/Models/PlayerReturnState.cs:25-31
```

The navigate return reconstructs a plain watch URL from only that ID:

```text
src/PiPlay/MainWindow.xaml.cs:1028-1030
```

```csharp
new YouTubeTarget { VideoId = state.VideoId }
```

That drops playlist/list/index context.

The QA checklist still expects playlist context preservation in the general app flow:

```text
docs/QA_Checklist.md:32-33
```

The popout start path preserves a richer `YouTubeTarget`, but the return path compresses the final target down to just `VideoId`.

### Recommendation

Extend `PlayerReturnState` to carry one of these:

```text
- full validated YouTubeTarget, or
- canonical YouTube watch URL plus parsed VideoId, or
- VideoId + ListId + Index + StartSeconds fields.
```

Use the ID for equality comparison, but use the richer target for navigation.

## 10. Final capture should also re-read current/canonical URL

Normal-mode navigation tracking updates `VideoId` on SourceChanged:

```text
src/PiPlay/PlayerWindow.xaml.cs:302-306
```

But final capture reads only the video element state:

```text
src/PiPlay/PlayerWindow.xaml.cs:491-495
```

It does not re-read canonical/location URL during the final capture.

If a YouTube SPA navigation, auto-advance, or late SourceChanged event is lagging, it is possible to pair a fresh timestamp with an older video ID.

### Recommendation

During `CaptureReturnStateNowAsync()`, also read canonical URL:

```text
- YouTubeDomBridge.ReadCanonicalUrlAsync(Player.CoreWebView2)
- parse it with YouTubeUrlHelper.TryParse
- update return VideoId and richer return target before close
```

This complements the timer fix. Final capture should refresh both:

```text
media state + current target
```

## 11. DOM bridge review

The DOM bridge itself is generally solid.

It reads:

```text
currentTime
paused
duration
volume
muted
playbackRate
```

from the YouTube video element:

```text
src/PiPlay/Services/YouTubeDomBridge.cs:29-41
```

It parses defensively:

```text
src/PiPlay/Services/YouTubeDomBridge.cs:50-82
```

It applies volume/mute/rate with numeric/boolean values generated inside C#, not from user-provided JS strings:

```text
src/PiPlay/Services/YouTubeDomBridge.cs:103-127
```

Good details:

- volume is clamped to `0..1`;
- playback rate must be finite and `> 0`;
- numeric formatting uses invariant culture;
- failed scripts are logged and swallowed instead of crashing the app.

Remaining test gap:

- There are no pure tests around parsing odd WebView2 JSON results.
- There are no tests around generated apply-settings script behavior.

This is not a release blocker, but it is useful hardening once P4 is stabilized.

## 12. Compact mode caveat

Compact mode appears dormant in the product, but the code still contains compact bridge plumbing.

The compact shell updates timestamp and video ID:

```text
src/PiPlay/PlayerWindow.xaml.cs:328-340
```

But it explicitly does not report paused/volume/mute/rate:

```text
src/PiPlay/PlayerWindow.xaml.cs:332-335
```

That is acceptable while compact mode is disabled. It is **not** acceptable if compact is re-enabled and P4 state fidelity remains a quality requirement.

Recommendation:

```text
Keep compact dormant.
If compact is re-enabled later, shell protocol must report paused, volume, muted, and playbackRate.
```

## 13. Border/inset review against the previous visual complaints

The code now uses a 4-DIP resize border/inset in the Source Window:

```text
src/PiPlay/MainWindow.xaml:23-28
src/PiPlay/MainWindow.xaml:155-173
```

The key value is:

```xml
<Setter Property="Margin" Value="4,0,4,4" />
```

The tests intentionally pin that behavior:

```text
tests/PiPlay.Tests/Ui/XamlInvariantTests.cs:60-86
```

This is materially better than b24’s larger tray/inset. It addresses some of the “ugly border” feeling.

But P1 is still not truly finished if the visual target remains:

```text
No visible outer frame.
No visible WebView gutter.
No double-tray look.
Resize still works.
```

The current code still preserves a technical band as a layout contract. That may be acceptable if the deployed visual looks clean enough, but it is not the same thing as removing the tray concept entirely.

Also, the QA checklist is stale:

```text
docs/QA_Checklist.md:45
docs/QA_Checklist.md:63
```

It still says `10 DIP`. The current code/tests/changelog say `4 DIP`.

### Recommendation

Update QA wording from `10 DIP` to `4 DIP` immediately.

Then make a product call based on visual QA:

```text
If 4 DIP is visually acceptable:
  keep it as a known WebView2/resize compromise.

If 4 DIP still reads as a border/tray:
  stop shaving pixels and change architecture.
```

Likely architecture options:

- separate resize hit testing from visible layout;
- use a non-visible native hit-test band outside the WebView surface;
- investigate WebView2 composition/windowless hosting for true rounded-card/window overlay behavior.

## 14. Opacity review

The settings UI now calls the feature exactly what it is:

```text
src/PiPlay/SettingsWindow.xaml:218-250
```

It says:

```text
Whole popout opacity
Active whole popout opacity
Idle whole popout opacity
```

That is honest and good.

The implementation applies opacity to the top-level popout HWND:

```text
src/PiPlay/PlayerWindow.xaml.cs:728-745
```

So the last opacity screenshot showing washed-out video is expected behavior with this implementation.

This is not the requested “transparency on chrome/background while video stays solid.” It is whole-window opacity.

### Recommendation

Keep the current feature, but classify it correctly:

```text
Implemented:
- Whole popout opacity.

Not implemented:
- Video-safe transparency.
- Main/popout/both transparency scope.
- Chrome/background-only opacity.
```

If true transparency is still wanted, model it explicitly:

```text
Transparency scope:
- Off
- Main only
- Popout only
- Both

Transparency target:
- Chrome/background only
- Whole window, advanced
```

Default should be chrome/background only once technically feasible. Whole-window should stay advanced/explicit because it fades the video.

## 15. Soft vs round review

The current packet does not include the theme/corner mapping source, so I cannot fully verify the live implementation from this packet alone.

However, the changelog still states:

```text
docs/CHANGELOG.md:17-21
```

that `soft` and `round` both map to DWM `Round` and preserve a framed feel.

That matches the earlier review finding: the options are not meaningfully different at the outer-window level.

### Recommendation

Do not expose two outer-window choices that produce the same native silhouette.

Either:

```text
Simplify:
- Default / Native
- Square
- Rounded
```

or make them genuinely different:

```text
Soft:
- subtle native rounding, minimal shadow, low-radius inner surfaces

Round:
- larger card-like radius, stronger silhouette, intentional shadow
```

The second option likely requires an ADR because WPF + WebView2 child HWND hosting limits true rounded/translucent composition.

## 16. Profile color / accent review

The Source Window uses profile color for the profile identity chip:

```text
src/PiPlay/MainWindow.xaml:101-106
```

The main runtime accent is resolved through:

```text
src/PiPlay/MainWindow.xaml.cs:617-645
```

The included changelog says the product decision is:

```text
docs/CHANGELOG.md:73-76
```

```text
Profile color is identity, not app accent.
```

That is internally coherent if the desired model is “global app accent + per-profile identity chip.”

But it does not satisfy the earlier owner request that profile colors should affect more than one button and should replace the standard blue/accent tokens more broadly.

### Recommendation

Make the decision explicit rather than accidental.

Best model:

```text
Accent source:
- Global app accent
- Active profile color
- Active profile color when available, otherwise global
```

Default can stay global app accent if stability matters, but the owner-requested behavior needs an available mode. Otherwise P2 should remain open, not marked done.

## 17. Test coverage review

### Good coverage

The packet has useful deterministic coverage for:

- `ReturnPolicy` timestamp/paused decision;
- different-video navigation decision;
- placeholder/button text and accessibility;
- WebView margin/inset invariants;
- border token/transparent control-border invariants;
- WPF construction seams for UI labels.

Notable tests:

```text
tests/PiPlay.Tests/ReturnPolicyTests.cs:8-56
tests/PiPlay.Tests/Ui/XamlInvariantTests.cs:60-86
tests/PiPlay.Tests/Ui/XamlInvariantTests.cs:257-268
tests/PiPlay.Tests/Ui/XamlInvariantTests.cs:937-990
tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs:657-690
tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs:1546-1573
```

### Missing coverage

The tests do not yet cover the hardest P4 behavior:

1. **Different-video post-navigation replay**
   - There is a test proving the URL queues, but not proving paused/volume/mute/rate are replayed after navigation.

2. **Final capture wins over timer capture**
   - No test catches overlapping `SyncTimer_Tick` reads.

3. **X-close fresh capture**
   - No test distinguishes Bring-back from popout close button/window close.

4. **Same-video return applies volume/mute/speed before seek/play**
   - The code does it, but there is no strong seam proving call ordering.

5. **Profile color full-accent mode**
   - Not implemented, therefore not tested.

6. **Opacity semantics**
   - Tests should assert it is labeled whole-popout opacity, but not claim video-safe transparency.

### Recommendation

Add a thin abstraction seam around source return actions so tests can observe:

```text
ApplyPlaybackSettings
SeekAndPause
SeekAndPlay
Play
Navigate
PendingReplayAfterNavigate
```

That will let tests validate behavior without live WebView2.

## 18. Documentation contradictions that should be fixed now

The docs are not trustworthy enough yet for release guidance.

### Stale focus-only pseudocode

Spec still says an existing player should be activated:

```text
docs/PiPlay_Product_Engineering_Spec.md:878-891
```

```csharp
if (_player is not null)
{
    _player.Activate();
    return;
}
```

But the code now brings video back:

```text
src/PiPlay/MainWindow.xaml.cs:781-789
```

Update spec 13.4.

### Stale REQ-RETURN-01

Spec and gaps still say source-was-playing-at-popout is normative:

```text
docs/PiPlay_Product_Engineering_Spec.md:932-940
docs/SPEC_GAPS_AND_OWNERSHIP.md:108
```

Code and tests say popout state wins when known.

Update the product rule.

### Stale QA 10-DIP wording

QA still says `10 DIP`:

```text
docs/QA_Checklist.md:45
docs/QA_Checklist.md:63
```

Code/tests/changelog say `4 DIP`.

Update QA.

### Changelog overstatement risk

Changelog says Bring-back captures timestamp, paused state, volume, mute, and playback speed:

```text
docs/CHANGELOG.md:8-11
```

That is true for same-video paths where DOM state is captured and applied. It is not fully true for different-video navigate until post-navigation replay exists.

Rewrite either as:

```text
captures fresh popout state and preserves it on same-video return; different-video state replay remains a follow-up
```

or implement the replay before release.

### AGENTS wording nit

`AGENTS.md` says Fade includes optional whole-window opacity:

```text
docs/AGENTS.md:28
```

The UI now says whole-popout opacity. Use that term consistently.

## 19. Suggested implementation order

### Step 1 — Decide and document the return rule

Recommended decision:

```text
Popout live state wins when known.
Fallback to sourceWasPlayingAtPopout only when popout state is unknown.
```

Patch:

- `PiPlay_Product_Engineering_Spec.md` section 13.4 and 14;
- `SPEC_GAPS_AND_OWNERSHIP.md` close/return row;
- `QA_Checklist.md` functional rows;
- test names/comments that still imply old `REQ-RETURN-01`.

### Step 2 — Make final capture authoritative

Patch:

- prevent overlapping sync timer reads;
- stop timer before final read;
- ensure stale timer reads cannot overwrite final capture;
- re-read canonical/current target during final capture.

### Step 3 — Make X-close equivalent to Bring-back

Patch:

- popout close button awaits final capture before close;
- system close/Alt-F4 either does async guarded capture or is documented as timer-sampled fallback.

Preferred: async guarded capture.

### Step 4 — Add post-navigation replay for different-video return

Patch:

- store pending returned state;
- navigate to returned target;
- on DOM-ready, apply volume/mute/rate;
- seek and pause/play according to returned `Paused`;
- retry with timeout;
- clear pending state.

### Step 5 — Update QA and changelog truthfully

Patch:

- remove 10-DIP references;
- add explicit same-video and different-video Bring-back rows;
- add X-close rows;
- ensure changelog does not claim preservation that is not implemented.

### Step 6 — Run gates on deployed Stable

Required before RC:

```text
dotnet clean PiPlay.sln --configuration Debug --nologo
dotnet test PiPlay.sln --configuration Debug --nologo
scripts/Build-PiPlay.ps1 -Stage Build -NoVersionBump -NoBuildNumberBump
scripts/Publish-Stable.ps1
scripts/Verify-StableDeploy.ps1
manual Stable smoke from docs/QA_Checklist.md
```

Manual smoke matters here because WebView2 timing and YouTube SPA behavior are exactly where the remaining bugs live.

## 20. Open questions — advice

### 20.1 Return resume rule

Choose:

```text
Option A: popout live state wins when known.
```

That is what the code already implements. It is also the least surprising user model.

Example:

```text
Source was playing -> popout opens -> user pauses in popout -> Bring video back.
```

Expected result should be paused, because the user just paused the active player.

### 20.2 Should X-close and Bring-back behave the same?

Yes.

Both are “return the active playback session to source” events. The close button should not have weaker state fidelity than the Source Window button.

### 20.3 Should different-video return preserve pause/volume/mute/speed?

Yes, if the changelog and QA continue to claim it.

If this is too much for the current push, then explicitly mark it as a follow-up and reduce the release claim. Do not leave the docs claiming behavior the code does not fully implement.

### 20.4 Should profile color drive the full UI accent?

Against the original owner ask, yes — at least as an option.

My advised product model:

```text
Default: global app accent.
Optional: active profile overrides app accent.
```

This preserves the current clean global theme behavior while still allowing the requested “profile color changes the actual UI” behavior.

### 20.5 Is the 4-DIP inset acceptable?

Only visual QA can decide.

The code is now better than b24, but the architecture still intentionally reserves a band. If that band still reads as a border/tray in the deployed Stable build, the fix should be architectural, not more pixel shaving.

### 20.6 Should opacity stay?

Yes, but with honest labeling:

```text
Whole popout opacity = implemented.
Video-safe transparency = not implemented.
```

Do not call current opacity “transparency” in the original requested sense because it fades the video.

### 20.7 Should soft and round both remain?

Not unless they look meaningfully different.

For now, either reduce the choices to honest native DWM modes or write an ADR for a proper rounded-card/composition implementation.

## 21. Release recommendation

I would not tag RC from this exact packet unless the release goal is explicitly “WIP bring-back preview.”

For a real b25/b26 push candidate, I would require:

```text
1. Spec/QA updated to match popout-live-state-wins.
2. Final capture race fixed.
3. X-close path either fresh-captures or is explicitly documented/tested as timer-sampled fallback.
4. Different-video return either replays paused/volume/mute/speed after navigation, or docs/changelog reduce the claim.
5. 10-DIP QA references corrected to 4 DIP.
6. Automated tests pass on the full repo.
7. Stable deployed smoke confirms no duplicate audio and no obvious return-state loss.
```

Bottom line:

```text
The code is no longer lying about the button: Bring video back is real.
The docs are still lying about the return rule.
The implementation still needs one reliability pass before it can claim full P4 state preservation.
```
