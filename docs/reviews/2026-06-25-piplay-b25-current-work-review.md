# PiPlay b25 current work review - 2026-06-25

> Address note: the findings in this review were followed by an address pass recorded in
> `docs/reviews/2026-06-25-piplay-b25-addressed-findings.md`. Read that note before treating this file
> as current blocker status.

## Scope

Reviewed the current local `main` work against `origin/main` after the audit packet prep.

Current branch state during review:

- `main` is 6 commits ahead of `origin/main`.
- Committed head: `a4fe91c`.
- Current uncommitted changes are docs/review packet material; product source is unchanged since the
  last recorded QA pass at `a4fe91c`.
- Reviewed source and docs directly, not only the generated zip.

## Findings

### 1. Blocking - REQ-RETURN-01 still contradicts the implemented return rule

`ReturnPolicy.Decide(...)` now lets the popout's live paused state override the source launch snapshot
when `returnedPaused` is known (`src/PiPlay/Services/ReturnPolicy.cs:30-35`). `MainWindow` feeds that
value for every return path, including explicit bring-back and normal X-close
(`src/PiPlay/MainWindow.xaml.cs:1015-1016`).

The living spec still says return resumes only if the source was playing when Video Popout started and
explicitly says not to infer this from close-time state (`docs/PiPlay_Product_Engineering_Spec.md:932-933`,
`docs/PiPlay_Product_Engineering_Spec.md:1439`). The current QA checklist also still pins the paused-source
case to "stays paused" (`docs/QA_Checklist.md:27-28`).

This is the main release gate. Either bless Option A and update the normative docs/QA, or scope the new
`state.Paused` behavior to the explicit bring-back action only.

### 2. High - P4 is still incomplete for different-video return

For same-video return, the new path captures paused, volume, mute, and playback rate and applies them before
seek/play (`src/PiPlay/MainWindow.xaml.cs:1018-1039`).

For different-video return, `ReturnPolicy` returns `Navigate` before the paused-state decision can matter
(`src/PiPlay/Services/ReturnPolicy.cs:48-57`). `ApplyReturnActionAsync(...)` then explicitly skips
`ApplyPlaybackSettingsAsync(...)` for `ReturnAction.Navigate` and only builds a watch URL with the returned
video/timestamp (`src/PiPlay/MainWindow.xaml.cs:1018-1030`).

Impact: if the popout moved to a recommendation or playlist item and the user paused or changed volume/mute/
speed there, the Source Window navigates to the right video/timestamp but does not replay those settings after
navigation. The current test only proves the queued URL and auto de-dup key (`tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs:1547-1559`).

### 3. High - Source audio suppression is still a single pause call

Popout launch pauses the source once and then hides the source WebView
(`src/PiPlay/MainWindow.xaml.cs:828-832`). There is no source mute and no reassertion guard while the popout is
active.

This keeps the existing duplicate-audio risk alive through ads, autoplay-next, and SPA re-renders. It is
properly tracked in `docs/SPEC_GAPS_AND_OWNERSHIP.md:9`, but that row's file:line references are stale.

### 4. Medium - Final capture is not fully authoritative on all close paths

The explicit Source Window bring-back path calls `CaptureReturnStateNowAsync()` before closing
(`src/PiPlay/MainWindow.xaml.cs:886-896`; `src/PiPlay/PlayerWindow.xaml.cs:484-488`), which is the right
direction.

Two gaps remain:

- X-close/system close only runs `CaptureReturnWindowState()` from `PlayerWindow_Closing`; it does not do the
  same fresh async DOM read (`src/PiPlay/PlayerWindow.xaml.cs:872-889`).
- An already-running `SyncTimer_Tick` can await `ReadPlayerStateAsync(...)` and then write return state after
  a final explicit capture unless a generation/final-capture guard is added
  (`src/PiPlay/PlayerWindow.xaml.cs:467-482`).

This is not as severe as the return-rule mismatch, but it can make a "fresh capture" claim overconfident.

### 5. Medium - Launch-from-paused behavior changed by the interaction of play nudge and Option A

The source launch snapshot is captured before pause (`src/PiPlay/MainWindow.xaml.cs:802-805`), but the popout
nudges playback once if it comes up paused (`src/PiPlay/PlayerWindow.xaml.cs:473-479`). With the new
popout-state-wins rule, a user can pop out a paused source, PiPlay can nudge the popout to play, and return
can resume the Source Window.

That may be a valid product choice, but it is a choice. It should be specified and QA'd, or the play nudge
should depend on source launch intent.

### 6. Low - Current review packet docs still contain stale or malformed references

Confirmed doc drift:

- `docs/QA_Checklist.md:45` and `docs/QA_Checklist.md:63` still say `10 DIP`; current code/spec/changelog
  say `4 DIP`.
- At review time, `docs/AGENTS.md:28` still said optional `whole-window opacity`; the address pass changed
  this to `whole-popout opacity`.
- At review time, `docs/SPEC_GAPS_AND_OWNERSHIP.md:9` still cited old source line numbers for the
  double-audio row; the address pass replaced this with current source-suppression status.
- At review time, `docs/SPEC_GAPS_AND_OWNERSHIP.md:73`,
  `docs/superpowers/plans/2026-06-25-b25-full-review-followup.md:79`, and
  `docs/superpowers/plans/2026-06-25-b25-full-review-followup.md:83` referenced "QA row 1233"; the address
  pass removed those current-plan references.
- At review time, `docs/PiPlay b25 full source review - spec tightening and review arbitration.md:1` and
  `docs/reviews/2026-06-25-piplay-b25-spec-tightening-arbitration.md:1` had malformed first headings; the
  address pass repaired them.
- `docs/reviews/2026-06-25-spec-review-v2.md` is retained for provenance but appears to review TerminalHQ/THQ,
  not PiPlay.

These are not product-code blockers, but they should be cleaned before committing/pushing the review packet.

## Non-findings

- The old focus-only P4 complaint is superseded for current source. The toolbar and placeholder now route to
  `BringVideoBackAsync()` rather than only focusing the popout (`src/PiPlay/MainWindow.xaml.cs:781-788`,
  `src/PiPlay/MainWindow.xaml.cs:886-896`).
- The button label, tooltip, and UIA name are updated together and covered by WPF/XAML tests
  (`tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs:658-672`,
  `tests/PiPlay.Tests/Ui/XamlInvariantTests.cs:258-266`).
- Whole-popout opacity wording in Settings is now honest about the current layered-window behavior
  (`src/PiPlay/SettingsWindow.xaml:215-251`).
- No merge conflict markers were found in `docs`, `src`, `tests`, `README.md`, `CLAUDE.md`, or
  `Build-PiPlay.ps1`.

## Review Verdict

Do not push or RC-tag this stack yet.

The code moved meaningfully forward, especially for the explicit bring-back path. The remaining release gate
is not whether P4 exists; it does. The gate is that the implementation, spec, and QA still disagree about the
return rule and that different-video return does not yet replay the full captured playback state.

Recommended next move:

1. Decide Option A vs Option B for `REQ-RETURN-01`.
2. Patch the normative spec/QA/docs to match that decision.
3. Add different-video playback-state replay or reduce P4 claims before release.
4. Clean the review-packet doc drift above.
5. Rerun automated QA, then publish/verify Stable and do real WebView2/YouTube manual smoke.

## Verification Performed

- Inspected `origin/main..HEAD` commit list and changed-file set.
- Reviewed the touched return/source files directly.
- Checked current QA/spec/gap docs for contradiction.
- Ran `git diff --check`; no whitespace errors, only Git's CRLF notice for the follow-up plan.
- Checked conflict markers over active docs/source/test surfaces; none found.

Tests were not rerun in this review pass. Last recorded automated QA remains the `a4fe91c` pass documented in
the packet manifest and audit prep.
