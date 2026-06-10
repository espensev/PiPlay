# Session worklog — PR #13 + compact player Stage 4 fallback (2026-06-10)

Saved record of the working session that opened PR #13 (`feat/compact-player-stage1`) and produced
branch `feat/compact-player-stage4` (Stage 4 / Task 6: compact error states + normal-page fallback).

## Request

> "resume" → (asked) **"Open the PR now"** → "review the state" → (asked) **"One combined PR"** →
> "stage 4"

Three user decisions shaped the session: open the PR for what the branch already had (no new
features first), package borderless + compact Stages 1-3 as **one combined PR**, and then implement
**Stage 4** on top — following the documented Feature Workflow (dated design spec before code,
deterministic gate, tests in all lanes, docs updates).

## Part 1 — state review + PR #13

Re-verified rather than trusted the 2-day-old session records before opening the PR:

- `git status` clean; branch cleanly fast-forwardable on current `origin/main`.
- Full gate re-run: `dotnet test` **330/330, 0 skipped**; `Build-PiPlay.ps1 -Stage Build
  -NoVersionBump -NoBuildNumberBump` **0 warnings / 0 errors**.
- Pushed `feat/compact-player-stage1` and opened **PR #13** (combined borderless resize zones +
  compact player Stages 1-3), with the design-spec links, requirement trace, verification results
  (deterministic + the 2026-06-07 live smoke + an explicit NOT-run list), and docs impact in the
  body. Remaining merge gate: the required `Build and test (Windows)` check.

## Part 2 — Stage 4 (Task 6: error states + fallback)

**Branch strategy:** `feat/compact-player-stage4` stacked off `feat/compact-player-stage1` — PR #13's
scope was frozen by the user's "open the PR now" decision, so Stage 4 must not land on it.

**Design spec first:** `docs/superpowers/specs/2026-06-10-compact-stage4-fallback-design.md`.
Settled decisions (full rationale in the spec):

- **Native WPF error bar in its own grid row, not an overlay** — WebView2 is an HwndHost; WPF
  cannot render over it (airspace). An in-shell HTML error also can't cover the failed-load and
  IFrame-API-timeout cases, where the shell itself is what's broken.
- **In-place fallback** — the *same* `PlayerWindow` flips `_mode` to Normal, disposes the bridge,
  relaxes the minimums (480×270 → 320×180), and navigates to the watch URL. No window churn; the
  one-timestamp-source invariant holds because the DOM sync timer starts on the next
  `NavigationCompleted` in Normal mode.
- **User action, not auto-fallback** — a playlist can recover on its own (auto-advance past a dead
  entry), so the bar offers the action and auto-dismisses on a playing state instead of yanking the
  user out of compact mode.
- **Fallback URL built at click time** — `YouTubeUrlHelper.BuildWatchUrl(_fallbackTarget,
  _returnState.LastKnownSeconds)`, so it carries the best-known timestamp, not a stale launch-time
  snapshot.
- **Pure policy seam** — `PlayerShellErrorPolicy` (code→message map for 2/5/100/101/150 + generic,
  auto-dismiss rule, 20 s ready watchdog) follows the house `FadePolicy`/`PlaybackModePolicy`
  pattern.
- **Host-side ready watchdog** — the shell cannot self-report "the IFrame API never came up"; a
  20 s `DispatcherTimer` covers it, cancelled by any inbound bridge message.

**Implementation:** new `PlayerShellErrorPolicy` + `PlayerShellErrorPolicyTests` (17 logic tests);
`PlayerWindow.xaml` gains the `ErrorBar` row (message + accent **Open normal page** +
dismiss, both buttons tooltipped and UIA-named); `PlayerWindow.xaml.cs` wires
`Ready`/`StateReceived`/`ErrorReceived`, the watchdog, `ShowShellError`/`HideShellError`, and
`FallBackToNormalPage` (guarded, one-shot), with `*ForTests` internal seams; `MainWindow` threads
the `YouTubeTarget` through as the fallback handle. Markup invariants + 6 WPF runtime tests cover
the bar's default-collapsed state, the error/load-failure/auto-dismiss/normal-mode-ignores paths,
and the guarded no-op without a live `CoreWebView2`. User-facing strings say "normal page" /
"compact player" — never `embed` or `PlayerWindow`.

**Gate:** `dotnet test PiPlay.sln --configuration Debug` **354/354, 0 skipped** (330 baseline + 24
new); `Build-PiPlay.ps1 -Stage Build -NoVersionBump -NoBuildNumberBump` **0W/0E**.

## Live smoke (Stage 4 fallback, 2026-06-10)

Ran a Stage-4 variant of the `run-piplay` driver (settings backup → `compactMode=true`,
`autoPopout=false` → launch → UIA popout → UIA poll for `FallbackButton` → invoke → captures → log
tail → settings restored). Deterministic trigger: the **valid-shape nonexistent video id
`00000000000`**.

- **The IFrame API answered the nonexistent id with error `150`, not `100`.** Real-world data
  point: YouTube's error-code semantics blur (a nonexistent id surfaced as "embed disallowed"), so
  the per-code messages are best-effort and the generic mapping path matters. Log:
  `Compact shell reported a player error (code=150).`
- **The error bar rendered exactly as designed** (screenshot evidence): its own row between the
  chrome and the player surface — "This video doesn't allow embedded playback." left, accent
  **Open normal page** + dismiss right — with the iframe's own "Video unavailable" panel visible
  beneath it. The airspace decision held: the native bar and the WebView2 surface coexist without
  overlay tricks.
- **The fallback worked in place.** Invoking **Open normal page** navigated the *same* popout
  window to the real `youtube.com/watch` page (rendered signed-in: "This video isn't available any
  more" + Go to home), and the error bar was gone afterwards (`hidden=True` via UIA).
- **Logs stayed redacted.** `Compact player error shown (...) for https://piplay.local/player.html;
  normal-page fallback offered.` and `Compact fallback: reopening in normal page mode:
  https://www.youtube.com/watch` — no video id in either line.

**What this trigger could not exercise live** (now explicit release-candidate QA):

- The **timestamp-carrying fallback** — the error fired at `t=0` before any playback, so
  `LastKnownSeconds` was empty; carrying a real mid-playback timestamp into the fallback needs a
  video that plays and *then* errors (e.g. playlist advance into a dead entry).
- The **watchdog ready-timeout** — error 150 arrived in ~4 s, well inside the 20 s window.
- The **auto-dismiss on recovery** — needs a playlist that recovers past a dead entry.
- **Real** embed-disabled/restricted videos (this was a synthetic id) and account-backed playback.

**User follow-up (same day):** the user re-ran the app with a real video address and confirmed
compact playback rendered and "looked good" — an informal real-video datapoint on top of the
synthetic-id smoke. The formal account-backed release QA (Task 7) still stands.

## Held for the user

The Stage 4 PR (outward-facing; stacked on PR #13, whose scope is frozen). Branch is committed and
ready to push once #13 lands or the user asks for a stacked PR.
