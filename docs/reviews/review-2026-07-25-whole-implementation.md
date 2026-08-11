# Review — Whole-implementation pass (2026-07-25)

- Scope: the PiPlay implementation as a whole at `main` (`28f51af`, v0.12.1 b36), post deep-audit remediation.
- Method: full line-read of the core lifecycle and risk-surface files; targeted pattern sweeps (`async void`, blocking waits, `e.Source` validation, WebView settings); build + test validation.
- Validation: `dotnet build PiPlay.sln` — 0 warnings/0 errors; `dotnet test` — 985/985 pass.
- Prior state: `.audit/deep-audit/piplay-runtime-2026-07-16` fixed F-001..F-005 (all Medium/Low) at `99f9834`; no Critical/High was outstanding when this pass began.

## Verdict

The implementation is in strong shape: race-prone async paths are generation-guarded, failure paths are bounded, persistence is atomic, the C#↔JS contracts are pinned by tests, and the Win32 interop cleans up after itself. Two Medium findings — one trust-boundary inconsistency in the compact-shell bridge, one unbounded WebView2 script await that can wedge lifecycle flags — plus a handful of Low items. Nothing here blocks a release; M-1 and M-2 are the recommended next fixes.

## Findings

### M-1 (Medium) — `PlayerShellBridge` accepts WebMessages from any frame

`src/PiPlay/Services/PlayerShellBridge.cs:50-56`

`OnWebMessageReceived` parses whatever arrives without checking `CoreWebView2WebMessageReceivedEventArgs.Source`. In compact mode the popout hosts `https://piplay.local/player.html`, which embeds the cross-origin YouTube player iframe (`src/PiPlay/PlayerShell/player.html:32`). WebView2 injects `chrome.webview` into *every* frame, so the YouTube iframe — or any sub-frame YouTube embeds — can post the shell protocol:

- `{type:"request", action:"close"|"pinToggle"|"fullscreenToggle"}` → drives real window handlers (`PlayerWindow.xaml.cs:575-604`);
- `{type:"state", currentTime:N, videoId:"..."}` → overwrites `_returnState` (`PlayerWindow.xaml.cs:494,501`), corrupting the return timestamp / return-video identity;
- `{type:"error"}` → spawns the error bar and invites fallback churn.

Impact is bounded (the parser shape-validates the video id; no code execution; navigation stays on YouTube), but it bypasses the trust model the codebase itself established: both sibling bridges validate `e.Source` + nonce + per-document token (`PlayerSurfaceDragBridge.cs:151-165`, `PlayerFirstSurfaceBridge.cs:346-373`). The comment in `PlayerWindow.xaml.cs:499` says "the parse IS the trust boundary" — parse validates *shape*, not *origin*.

Suggestion: drop messages whose `e.Source` does not start with `WebViewEnvironmentService.ShellOrigin` (ordinal-ignore-case), mirroring the other bridges; add a parse-level unit test with a forged foreign source (the `TryParse(json, source, ...)` seam pattern already exists for this).

### M-2 (Medium) — Unbounded `ExecuteScriptAsync` awaits can wedge lifecycle flags

`src/PiPlay/Services/YouTubeDomBridge.cs:956-975` (`ExecuteRawAsync`)

No timeout wraps `webView.ExecuteScriptAsync`. A renderer crash faults the task (handled), but a *stalled* renderer (pathological page JS, GC storm) never completes it. Reachable stuck states:

- `BringVideoBackAsync` sets `_popoutInProgress = true` then awaits `CaptureReturnStateNowAsync()` (`MainWindow.xaml.cs:1590-1613` → `PlayerWindow.xaml.cs:743-757`). A hang leaves every Source command and the popout action disabled until the user manually X-closes the popout.
- `StartVideoPopoutAsync` awaits `ReadPlayerStateAsync` under the same flag (`MainWindow.xaml.cs:1447,1458`); a hang blocks all future popout attempts for the session.
- `SyncTimer_Tick` latches `_syncTickInProgress` (`PlayerWindow.xaml.cs:768-779`): the return timestamp silently freezes — the app keeps running, and the failure is invisible until a return seeks to a stale position.

Suggestion: `WaitAsync` (a few seconds) around the script task, routed into the existing `ConsecutiveFailureGate` so the timeout logs like any other DOM failure. The injected-executor seams (e.g. `SuppressPlaybackAsync(Func<string,Task<string>>)`) make the timeout unit-testable with a never-completing task.

### L-1 (Low) — Single-instance pipe: no per-connection deadline

`src/PiPlay/App.xaml.cs:144-155`. One server instance (`maxNumberOfServerInstances: 1`); `ReadToEndAsync(token)` completes only when the client closes. A local client that connects and never writes/closes occupies the slot indefinitely, silently disabling second-instance URL handoff for the session. Suggest a ~5 s linked cancellation around the whole serve; `SingleInstancePipePolicy.RunAsync` then recreates the server on the normal path.

### L-2 (Low) — Global unhandled-exception swallow

`src/PiPlay/App.xaml.cs:102-110`. `e.Handled = true` for everything is the deliberate Q-6 stance, but two sharpening points: (a) a repeating fault (e.g. a per-tick throw) spams a modal `MessageBox` each time — no rate limit; (b) exceptions thrown mid-mutation (theme resource replacement, settings apply) leave partial state that the app then keeps running with. Suggest a one-shot/coalesced dialog and, at minimum, a comment recording which exception classes are considered recoverable.

### L-3 (Low) — `WebViewEnvironmentService.EnsureCreatedAsync` is not single-flighted

`src/PiPlay/Services/WebViewEnvironmentService.cs:50-70`. Two concurrent creators would race (both target the same user-data folder; the loser throws or leaks). Unreachable today — the popout requires `_browserReady`, so the env exists before any second caller — and the deep audit rejected prioritizing it (R-002). A `SemaphoreSlim` + double-check makes it structurally safe for future callers; cheap insurance.

### L-4 (Low) — Navigation allowlist permits `data:`

`src/PiPlay/Services/NavigationPolicy.cs:45`. `about:`/`blob:` serve WebView2 internals; top-level `data:` is rarely needed and marginally widens the injection surface. Verify a real need exists; drop it otherwise.

### L-5 (Low) — A transient shell read can zero a good return timestamp

`src/PiPlay/PlayerShell/player-shell.js:40-43` returns `0` when `getCurrentTime()` throws; `PlayerWindow.xaml.cs:494` writes it unconditionally. A transient IFrame-API fault inside the last 250 ms before close turns the return into seek-to-0 instead of the last good position. The 4 Hz cadence almost always heals it first; a `> 0` guard (as the fallback path already applies at `PlayerWindow.xaml.cs:660`) closes the edge.

### L-6 (Low) — Command-line URL sniffing accepts any `youtu*` arg

`src/PiPlay/App.xaml.cs:112-124`. Harmless downstream (`YouTubeUrlHelper.TryParse` re-validates), but the prefix match is broader than intended (`youtube.com/...`, `youtu.be/...`).

## Process / structural observations (not defects)

- `MainWindow.xaml.cs` (~2026 lines) now carries eight concerns (browser init, navigation, profiles, privacy, settings-dialog lifecycle, popout lifecycle, Auto, accent preview). The extracted pure policies keep it testable, but the next growth pass should carve out a popout-lifecycle coordinator and the accent-preview pipeline before the file becomes the merge-conflict hotspot.
- The `internal *ForTests` seam surface is large but consistent and deliberate; it is what lets the WPF lane test behavior instead of implementation trivia. Keep the convention.

## Strengths (verified in code, not inferred)

- **Race hygiene**: generation tokens at every async boundary — `_navigationGeneration`, `_playerInitializationGeneration`, `_documentGeneration`, sync-poll generations — with re-validation after each `await` (e.g. the return-replay loop re-checks currency every iteration, `MainWindow.xaml.cs:319-341`).
- **Q-1 duplicate-audio chain**: acknowledged suppression before popout construction (`PopoutLaunchPolicy`), 1 Hz suppression guard, mute fallback resolution that can never return the Source silent (`ReturnPolicy.ResolveReturnSettings`), and a full rollback path on launch failure (`MainWindow.xaml.cs:1547-1574`).
- **Return correctness**: navigate-vs-seek identity compare, pending replay bounded at 12×250 ms with staleness re-validation, Auto de-dup armed *before* the first await so return-resume cannot re-pop (`MainWindow.xaml.cs:1883-1890`).
- **Failure bounding**: `ConsecutiveFailureGate` coalesces every recurring failure log; the logger is a bounded queue with drop accounting, coalesced writes, rotation, and drain-on-exit; the pipe loop backs off 250 ms → 30 s.
- **Persistence**: temp + durable flush + `File.Replace` atomic writes, corrupt-file quarantine with 30-day cleanup, schema ≤2→3 migration backfill, sanitize-on-load (`SettingsService`).
- **Win32**: subclasses self-remove on `WM_NCDESTROY`; delegate roots held by state objects; managed exceptions contained inside native procs; HWND-keyed dictionaries pruned; HRGN ownership follows the `SetWindowRgn` contract; placement math in pixel space under PerMonitor V2.
- **Trust boundaries**: navigation allowlist with TLD-suffix shape validation that rejects look-alike auth hosts; protocol parsers validate shape (including video-id form) at the wire; both page bridges use nonce + per-document tokens + trusted-source checks.
- **Tests**: 985 green in ~4 s; behavior-driven via internal seams; the C#↔JS contract pinned from both sides (`PlayerShellAssetTests` reads the JS and asserts against the C# constants); executable JS tests under Node; one deterministic plan shared by local and GitHub CI; pinned action SHAs and SDK via `global.json`.

## Coverage boundary

Read in full: `App.xaml.cs`, `MainWindow.xaml.cs`, `PlayerWindow.xaml.cs`, `YouTubeDomBridge.cs`, `PlayerShellBridge/Protocol`, `PlayerSurfaceDrag*`, `PlayerFirstSurface*`, `SettingsService`, `ProfileService`, `Log`, `WebViewEnvironmentService`, `BrowserDataClearCoordinator`, `SingleInstancePipePolicy`, `PopoutLaunchPolicy`, `ConsecutiveFailureGate`, `NavigationPolicy`, `YouTubeUrlHelper`, `PopoutTargetResolver`, `ReturnPolicy`, `AutoPopoutPolicy`, `WindowPlacementService`, `BorderlessWindowHelper`, `WindowOpacityApplier`, `RoundedWindowRegionApplier`, `player.html`, `player-shell.js`, theme core (`ThemeResourceApplier`, `ColorMath`, `AccentReadabilityPolicy`, `ContrastBrushConverter`, `ToggleAccent`), `AppPaths`/`AppChannel`, csproj/CI/Test-LocalCI, sampled tests.

Not line-read (lower risk or covered by pinned invariants): `SettingsWindow.xaml(.cs)`, `AccentColorPicker.xaml(.cs)`, `ThemeCatalog.cs`, `ThemeColors.cs`, `ThemeSettingsWriter`, `ThemePreferenceResolver`, XAML markup files, `Prompt.cs` (first ~120 lines only), most test files (~6 of 50 sampled), publish/verify scripts, `Models/*`. Findings above should therefore be read as "verified where stated", not as a claim of exhaustive coverage.
