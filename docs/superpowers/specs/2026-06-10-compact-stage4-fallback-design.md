# Compact player Stage 4 — error states and normal-mode fallback — design

## Goals

Close the last functional gap in the Phase 3 compact-player sweep (Task 6 of
`docs/superpowers/plans/2026-06-07-compact-player-sweep.md`): a compact popout whose video cannot
play — embed-disabled, unavailable, a failed shell load, or an IFrame API that never comes up —
must show a clear in-app error state with a one-click fallback that reopens the same target in
normal page mode, instead of stranding the user on a dead player.

What is NOT changing: normal page mode stays the default and the systemic fallback; the shell, the
`PlayerShellProtocol` wire contract, and the bridge transport are untouched (the `error` message
already flows shell → host end to end — today it is only logged). The tuned shell
Content-Security-Policy stays deferred to live QA (it must be enumerated against the real
IFrame-API requests before a restrictive policy can ship without breaking playback).

## Requirements served

- `Q-6` — compact player failure produces understandable fallback behavior (the sweep design's
  acceptance row: "Restricted/unavailable/embed-disabled videos show a compact in-app error with a
  clear fallback to normal mode").
- Spec §10.3 — shell error messages are part of the versioned host↔shell contract.
- `Q-2` / spec §14 — the fallback preserves the best-known timestamp.
- `Q-5` — the error surface adds PiPlay chrome only; YouTube's own in-player error UI stays
  visible and untouched.
- Spec §17 — failures degrade gracefully; logs carry redacted target info only.

## Acceptance criteria

- A compact popout that receives a shell `error` message (IFrame API codes 2, 5, 100, 101, 150, or
  unknown) shows a native error bar with a code-specific, user-facing message and an
  **Open normal player** action.
- A compact popout whose shell navigation fails (`NavigationCompleted.IsSuccess == false`) shows
  the same error bar with a load-failure message.
- A compact popout whose shell loads but whose IFrame API never reports ready/state within the
  policy timeout shows the same error bar with a timeout message.
- Activating **Open normal player** re-navigates the same Popout Player to the normal watch URL for
  the same target at the best-known timestamp; from that point the window behaves as a normal-mode
  player (DOM sync timer drives the return timestamp; the shell bridge is disposed; the window
  minimum relaxes to the normal 320×180 floor).
- The error bar auto-dismisses if playback recovers (the shell later reports a playing state — e.g.
  a playlist auto-advances past a dead entry).
- Normal-mode popouts are unaffected: no error bar, no timer, no behavior change.
- Logs record the error code and the redacted target URL only — never full query strings.

## Settled decisions

1. **Native WPF error bar in its own grid row, not an overlay and not in-shell HTML.** WebView2 is
   an HwndHost, so WPF content cannot render on top of it (airspace); a separate auto-height row
   needs no airspace fight. An in-shell HTML error could not cover the failed-shell-load or
   API-timeout cases where the shell itself is broken. The bar also leaves YouTube's own in-player
   error message visible (Q-5) rather than papering over it.
2. **Fallback re-navigates the same window in place; no close/relaunch.** Closing would fire the
   return lifecycle (source resume + possible auto re-popout) — a churny, surprising path. In-place
   navigation keeps `PlayerWindow` the owner of the playback surface; the window flips its
   mode-dependent behavior (timer source, minimum size) to Normal at the same time, preserving the
   one-timestamp-source invariant (`PlaybackModePolicy.UsesDomSyncTimer`).
3. **Fallback is a user action, not automatic.** An auto-navigation would be wrong for playlists
   (the IFrame player can auto-advance past a dead entry — the bar auto-dismisses instead) and
   would surprise users mid-error-read. The plan's wording asks for "a clear fallback action".
4. **The fallback URL is rebuilt at click time from the threaded `YouTubeTarget` +
   `lastKnownSeconds`,** not precomputed at launch — so an error after minutes of playback (e.g.
   HTML5 error 5) falls back to the current position, not the original start (Q-2). `MainWindow`
   stays a thin caller: it passes the target it already resolved; URL building stays in
   `YouTubeUrlHelper.BuildWatchUrl`.
5. **Error→message mapping, the auto-dismiss predicate, and the ready timeout live in a new pure
   `PlayerShellErrorPolicy`,** following the house policy-seam pattern (`FadePolicy`,
   `PlaybackModePolicy`), so every decision is unit-testable without WebView2.
6. **The ready timeout is host-side, single-shot, and generous (20 s).** It must catch "the
   iframe_api script never loaded" (offline, blocked) without false-firing on a slow cold start;
   any inbound bridge message (ready, state, or error) cancels it.

## Non-goals / out of scope

- The tuned shell CSP (stays a live-QA follow-up; tracked in the sweep plan's Task 6 note and the
  `player.html` security comment).
- Auto-falling-back without user action, or remembering the fallback as a saved mode preference
  (`Profile.Mode` / `PlayerSettings.CompactMode` are untouched).
- The reserved direct-embed fallback tier (`BuildEmbedUrl` — sweep design unresolved decision #1).
- Shell-side (JS) changes — the protocol already carries everything Stage 4 needs.
- Retry-in-compact action; close-and-give-up is already served by the existing chrome Close.

## Testing approach

- **Logic tests (`PlayerShellErrorPolicyTests`):** the code→message map (101/150 embed-disabled,
  100 unavailable, 2 invalid, 5 player error, null/unknown generic), `ShouldAutoDismiss` (playing
  only), and the timeout constant's sane range.
- **Markup tests (`XamlInvariantTests`):** the error bar and its named controls exist, are
  collapsed by default, and carry tooltips/automation names.
- **WPF runtime tests (`WpfRuntimeTests`):** a compact `PlayerWindow` constructs with the bar
  collapsed; the internal shell-error/shell-state handlers show the bar with the policy message,
  auto-dismiss on a playing state, and ignore errors in normal mode; the fallback click without a
  live CoreWebView2 is a guarded no-throw.
- **Manual live QA:** drive compact with a valid-shape but nonexistent video id (deterministic
  IFrame error 100) via the `run-piplay` driver: error bar appears; fallback opens the normal
  watch page; return state still captured. Embed-disabled (101/150) with a real restricted video
  stays a release-candidate row.

## Changes by file

| File | Change |
|---|---|
| `src/PiPlay/Services/PlayerShellErrorPolicy.cs` | New pure policy: code→message, auto-dismiss predicate, ready timeout. |
| `src/PiPlay/PlayerWindow.xaml` | Error bar row (collapsed by default): message text + Open normal player + dismiss. |
| `src/PiPlay/PlayerWindow.xaml.cs` | Subscribe bridge `Ready`/`ErrorReceived`; ready-timeout timer; failed-load hook; show/dismiss bar; in-place fallback navigation + mode flip. |
| `src/PiPlay/MainWindow.xaml.cs` | Pass the resolved `YouTubeTarget` to `PlayerWindow` for the fallback URL. |
| `tests/PiPlay.Tests/PlayerShellErrorPolicyTests.cs` | New logic lane coverage. |
| `tests/PiPlay.Tests/Ui/XamlInvariantTests.cs` | Error-bar markup invariants. |
| `tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs` | Error-bar lifecycle + fallback guard coverage. |
| `docs/CHANGELOG.md` | Phase 3 fallback entry. |
| `docs/superpowers/plans/2026-06-07-compact-player-sweep.md` | Task 6 status (fallback done; CSP still live-QA). |
| `docs/QA_Checklist.md` | Compact fallback rows. |

## Docs & changelog impact

`docs/CHANGELOG.md` Phase 3 section gains the fallback entry. The sweep plan's Task 6 is updated
(fallback shipped; CSP remains the live-QA remainder). `docs/QA_Checklist.md` gains
restricted/unavailable fallback rows. No ADR impact (no architecture, window-policy, or WebView2
platform change).

## Unresolved decisions

- None for the fallback itself. The shell CSP remains deliberately deferred to live QA (recorded
  in the sweep plan and `player.html`).
