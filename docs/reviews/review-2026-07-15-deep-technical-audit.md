# Deep technical audit — Popout UI working tree

**Date:** 2026-07-15
**Surface reviewed:** current `main` working tree at audit start — 48 paths (30 tracked modifications, 18 untracked additions), no staged changes
**Final remediated surface:** 50 paths (31 tracked modifications, 19 untracked additions), no staged changes
**Verdict:** **FAIL for release / PASS for a controlled diagnostics-only deployment**

The implementation is substantially better than an unaudited UI spike: settings normalization, source gating, native handle ownership, timer shutdown, and the ordinary close path are deliberate and well tested. No confirmed unbounded managed, HWND-subclass, WebView bridge, GDI-region, or timer leak was found.

Release sign-off should nevertheless wait. The audit found one high policy/compliance defect, six medium lifecycle/performance/correctness risks, and meaningful maintainability debt. The current dirty Stable deployment is useful for controlled testing, but its results are not release evidence under `docs/QA_Checklist.md`.

**Remediation result (2026-07-15):** the audited working tree was hardened and deployed as controlled
diagnostics build `20260715-080621-v0.10.1-b33-stable`. It is still not release evidence: the manifest
records `-AllowDirty`, H1/M6 require a live deployed Focused/ad/SPA exercise, the longer
resource-settling soak below is outstanding, and a clean exact-source candidate has not yet been
published. The original severities are retained below so the reason for each change stays auditable.

| Finding | Working-tree disposition | Verification |
|---|---|---|
| H1 | Enforced in code; release proof pending | Active `ad-showing`/`ad-interrupting` states hide and disable custom seek/Next surfaces, and both handlers fail closed before a media write/native Next click. Contract regressions pass; live ad QA remains mandatory. |
| M1 | Closed | Bridge creation stays local across every await, initialization generations invalidate late continuations, stale bridges dispose immediately, and final navigation is guarded. |
| M2 | Closed | Focused appearance IPC is single-flight/latest-wins, deduplicated, navigation-aware, disposal-safe, and lifetime log-once. |
| M3 | Closed for the audited hot path | Cached controls and conditional writes replace unconditional churn; media events drive state; fallback polling is 1 second and active-only; observers are narrowed. Long Focused soak remains a release gate. |
| M4 | Closed | Passive drag exits in child frames and requires trusted primary mouse/pen input. |
| M5 | Closed | Both bridges revoke on accepted navigation and authorize only the matching successful trusted document with an independent random per-navigation token. Exact schemas require window nonce, document token, and current source; different-video and exact-same-URL reload replay tests pass. |
| M6 | Enforced and executable-DOM verified; deployed proof pending | Synthetic Focused actions and drag gestures fail the `isTrusted` gate, protocols use exact schemas and nonces, and native actions remain allowlisted. The dependency-free Node VM harness executes the real generated scripts across eight passive/Focused scenarios; a deployed WebView2 exercise remains mandatory. |

The low-risk close/init and native move findings are also addressed: both WebViews now have auditable
teardown gates, `WM_ENTERSIZEMOVE`/`WM_EXITSIZEMOVE` owns drag-idle state, native subclass cleanup is
exercised against a real HWND, ineligible corner modes skip snap calls, and size/DPI events no longer
rewrite unchanged DWM attributes. Compliance/design/plan drift is reconciled.

Combined verification after remediation: **937/937 Debug tests passed**, including eight executable
fake-DOM scenarios driven through the real generated JavaScript; the RID-specific Release build
completed with **0 warnings / 0 errors**, all six generated JavaScript programs passed `node --check`,
the spec gate passed, `git diff --check` passed, and changed code contained no debug-marker or
literal-secret pattern matches.

The large-file ownership debt remains deliberately deferred rather than mixed into this stabilization
deploy. The next bounded refactor should extract Popout WebView initialization/lifecycle coordination and
move generated Focused/passive scripts into syntax-checked assets. That work must preserve the bridge and
policy seams established here and receive its own behavior baseline; a broad rewrite is not a prerequisite
for this diagnostic build.

## High findings

### H1 — The Focused seek rail does not enforce the no-ad-alteration invariant

**Axis:** standards, spec, regression risk
**Evidence:**

- `YouTubeDomBridge.BuildPlayerFirstSurfaceScript` selects the current video broadly at lines 452–453.
- The custom progress control is enabled solely from `media.duration > 0` at lines 560–567.
- `handleSeek` writes `media.currentTime` directly at lines 603–608.
- There is no ad-state guard such as an active-ad check, and no test covers one.
- `docs/YouTube_Compliance.md` lines 12–16 prohibits skipping or altering ads.
- Product spec lines 1248–1254 requires the Focused controls not to alter ads or monetization.
- `PlayerSurfaceScriptTests` lines 97–192 only inspect generated text; they do not execute the DOM behavior.

**Impact:** while an ad is active, PiPlay can attempt a direct seek even when YouTube's native seek surface is unavailable. YouTube may reject that write in some states; that does not close the defect. PiPlay currently does not enforce its own explicit invariant.

**Required before release:** disable or hide custom seeking while an ad state is active, never write `currentTime` in that state, keep required native ad UI reachable, add an executable behavior test, and complete the deployed ad/manual row. Review the new Focused surface against `YouTube_Compliance.md` before public distribution.

## Medium findings

### M1 — Close can overtake asynchronous bridge installation

**Axis:** lifecycle, regression
`PlayerWindow.InitializePlayerAsync` awaits bridge creation and then publishes it directly to fields at lines 261–280. Both bridge constructors subscribe to `CoreWebView2.WebMessageReceived` before awaiting document-script registration. `PlayerWindow_Closed` at lines 1290–1295 can dispose only fields already assigned.

A close that lands during registration can therefore let a continuation assign a subscribed bridge after the one cleanup pass has already run. WebView teardown will probably reclaim the graph, so this is an unverified race rather than a confirmed persistent leak, but cleanup depends on COM/WebView teardown and GC.

Use a local bridge, recheck `_closing` or an initialization generation after each await, dispose stale results immediately, then publish the field. Add a controlled close-during-registration regression test. Apply the same closing gate to the final navigation.

### M2 — Focused appearance refresh has no backpressure or latest-wins behavior

**Axis:** performance, lifecycle, spec
`PlayerFirstSurfaceBridge.ApplyAppearance` starts an untracked `ApplyAppearanceAsync` task for every call. Each call crosses WebView2 IPC through `ExecuteScriptAsync`; there is no single-flight guard, cancellation, generation, equality check, or pending-value coalescing. Every failure is also logged independently, contrary to the Focused design's “at most once per setup path” requirement.

The Settings preview timer can feed this path about 30 times per second. Accent-intensity-only changes repaint WPF with the same accent but still resend an identical Focused configuration. If WebView execution falls behind input, retained tasks/configuration snapshots can accumulate and teardown can produce duplicate error bursts.

Implement one latest-wins pump: at most one execution in flight, one pending snapshot, skip identical configurations, reject stale navigation/disposal generations, and log once per failure state. Intensity-only repaint must not call the overlay when its configuration did not change.

### M3 — Focused mode combines permanent polling with broad DOM observation

**Axis:** performance, power
The Focused script installs a 250 ms `setInterval`, a whole-document subtree `MutationObserver`, and global pointer/keyboard listeners at `YouTubeDomBridge.cs` lines 611–658. `updateControls` repeatedly queries nodes and replaces the play SVG `innerHTML`, labels, attributes, time, and progress even when most state is unchanged. Pointer movement also clears/recreates a timeout and queries the video on each event. Leaving `/watch` hides the root but does not stop the interval or observer.

This is bounded, not a proven memory leak, but it duplicates the host's 250 ms return-state poll and can turn unrelated YouTube DOM churn into repeated install/query work. Two hours of Focused playback creates at least 28,800 interval cycles before pointer and mutation work.

Cache overlay nodes; update only changed fields; use media events for play/mute/duration state; update progress only while active; throttle pointer activity; and stop or narrow polling/observation while the Focused surface is inactive.

### M4 — Drag handlers are injected into frames whose messages are intentionally ignored

**Axis:** correctness, regression
The Focused script correctly exits when `window.top !== window`; the passive-drag script at lines 156–160 does not. Document-created scripts run for child-frame documents, while `PlayerSurfaceDragBridge` intentionally listens only to the top-level `CoreWebView2.WebMessageReceived` after the earlier recursive frame wiring crashed WebView2.

A matching YouTube iframe can therefore arm and suppress a gesture, post a frame message that the host does not consume, and never move the window. It also installs unnecessary listeners in every child frame. Add the same top-frame guard before installing drag handlers and retain the top-document-only native bridge.

### M5 — Trusted messages are not bound to the current document/navigation

**Axis:** protocol correctness
Both protocols require a window-lifetime nonce and require the message source and current source to be trusted HTTPS YouTube URLs. They do not require the message to belong to the current document or navigation generation. A queued message from a prior YouTube watch document can still pass after a YouTube-to-YouTube navigation because both sources remain trusted and the nonce remains valid.

The capability set is narrow, which limits impact, but the current checks do not enforce stale-message rejection. Rotate or bind a document/navigation token, reject callbacks from older generations, and add a same-origin navigation/replay test.

### M6 — Synthetic page events can invoke native Focused window actions

**Axis:** capability boundary, security
The Focused overlay click handler accepts any event that reaches a `[data-action]` control. It does not require `event.isTrusted` before calling the nonce-bearing native bridge. YouTube page code does not need to learn the closure's nonce: it can call `.click()` on PiPlay's injected Close, Pin, Expand, or Settings button, and PiPlay's own handler supplies the nonce. The drag handlers also lack a trusted-event gate, although their host-side physical-button check limits that path.

Require trusted user input before emitting native window requests and before arming a drag. Add executable DOM tests proving synthetic clicks/pointer events cannot produce host messages while real input still can. Source/origin and nonce validation remain necessary, but they do not replace user-gesture validation when the page can invoke the injected handler.

## Low findings and maintainability debt

- **Native refresh churn:** `ApplyCornerModeToHwnd` calls snap classification before checking whether the selected corner mode can use a custom region. Move/size/DPI events therefore perform avoidable native calls; resize also replaces the HRGN synchronously on every `SizeChanged`. Ownership is correct. Short-circuit non-Round modes, cache native state/geometry, and coalesce refreshes to one render frame if profiling shows resize cost.
- **Large ownership surfaces:** `MainWindow.xaml.cs` is 1,598 lines with roughly 96 methods; `PlayerWindow.xaml.cs` is 1,309 lines with roughly 95 methods; `YouTubeDomBridge.cs` is 725 lines and now contains an approximately 415-line JS/CSS/SVG builder. These are not line-count defects by themselves, and the new bridge/policy services are good seams. They are now costly enough that the next focused refactor should extract the Popout initialization/lifecycle coordinator and move the generated surface into dedicated, syntax-checked script assets while retaining one centralized YouTube-DOM owner. Avoid a broad rewrite.
- **Surface-drag state ends before the native move does:** the surface path posts `WM_SYSCOMMAND` and clears `_isDragging` as soon as `PostMessage` returns, before Windows enters/exits its move loop. A sufficiently long drag can therefore satisfy the fade/idle logic while movement is still active. Track `WM_ENTERSIZEMOVE` / `WM_EXITSIZEMOVE`, or otherwise hold the activity state for the real native loop.
- **Main-window startup cancellation:** the Source WebView initialization has no post-await `_mainWindowClosing` gate and the main WebView is not explicitly disposed in `MainWindow_Closing`. App shutdown normally tears the process down, so this is lower risk than M1, but explicit cancellation/disposal would make ownership auditable and support future window recreation.
- **Compliance-document drift:** `YouTube_Compliance.md` still describes injection as playback-control-only and does not describe Focused styling, pointer observation, or the native window-action allowlist. After H1/M6 are fixed, update the policy and perform its required pre-release review.
- **Dated-spec contradiction:** the Focused design acceptance line says invalid/missing profile presentation falls back to Standard, while its settled policy, the living product spec, tests, and implementation correctly inherit the global default. Correct the dated acceptance sentence so future tests do not encode the wrong rule.
- **Plan drift:** the rounded-card implementation plan still reads as incomplete although its implementation and tests are present. Update it when the implementation lane closes.

## Confirmed-sound paths

- Normal close stops the Popout sync, idle, shell-ready, and cursor-probe timers.
- Both new bridges unsubscribe `WebMessageReceived` and remove their registered document-created scripts on ordinary disposal.
- `Player.Dispose()` runs after bridge cleanup.
- The resize subclass removes its static `Window`/delegate entry on `WM_NCDESTROY`.
- Rounded-region ownership follows `SetWindowRgn`: failed regions are deleted locally; successful regions transfer to Windows; test probe regions are always deleted.
- Close and Settings actions are deferred out of WebView2 callbacks, avoiding the known reentrancy hazard.
- No recursive `CoreWebView2Frame` event wiring or temporary debug tracing remains.
- Settings/profile presentation values normalize to a closed vocabulary, preserve global precedence, and keep Standard as the absent/global default.

## Verification and limits

- Full Debug suite after remediation: **937/937 passed**, including the eight-scenario executable DOM harness.
- Release build: **0 warnings, 0 errors**.
- Targeted lifecycle/UI slice: **210/210 passed**.
- Targeted surface/region/presentation slice: **104/104 passed**.
- All six generated JavaScript programs passed `node --check` from the current Release artifact.
- `git diff --check`: passed (line-ending warnings only).
- The current deployed Standard run held process-tree handles flat over a short 20-second sample; the main process had 62 GDI and 46 USER objects. This rules out only obvious immediate Standard/native growth. It is not a leak certification.
- No Focused soak, repeated open/close cycle, watch→non-watch→watch SPA soak, sustained resize/DPI soak, or real WebView2 overlay test has been completed.
- The repository itself says dirty `-AllowDirty` deployments are diagnostics only and cannot complete release QA (`QA_Checklist.md` lines 5–10 and 82–116).

Recommended profiling gate after fixes: compare Standard and Focused across at least 50 Popout open/close cycles plus a 30–60 minute watch/navigation/resize soak. Record descendant process count, private/working memory, handles, GDI/USER objects, and CPU, and require counts to settle after each close rather than merely remain finite during one run.

## Coverage map

All 48 original paths were assigned coverage. The audit report plus remediation/doc reconciliation
expanded the audited implementation surface to 50 paths. The later session worklog is packaging
history rather than part of that audited inventory; the final diff is the authoritative release scope.

**Deep runtime review (24):**

- `src/PiPlay/MainWindow.xaml`, `src/PiPlay/MainWindow.xaml.cs`
- `src/PiPlay/Models/AppSettings.cs`, `src/PiPlay/Models/Profile.cs`
- `src/PiPlay/PlayerWindow.xaml`, `src/PiPlay/PlayerWindow.xaml.cs`, `src/PiPlay/Prompt.cs`
- `src/PiPlay/SettingsWindow.xaml`, `src/PiPlay/SettingsWindow.xaml.cs`
- `src/PiPlay/Theme/Colors.xaml`, `src/PiPlay/Theme/ThemeCatalog.cs`
- `src/PiPlay/Services/BorderlessResizeHitTestPolicy.cs`, `BorderlessWindowHelper.cs`, `ProfileService.cs`, `SettingsService.cs`, `YouTubeDomBridge.cs`
- New services: `PlayerFirstSurfaceBridge.cs`, `PlayerFirstSurfaceProtocol.cs`, `PlayerSurfaceDragBridge.cs`, `PlayerSurfaceDragPolicy.cs`, `PlayerSurfaceDragProtocol.cs`, `PopoutPresentationPolicy.cs`, `RoundedWindowRegionApplier.cs`, `RoundedWindowRegionPolicy.cs`

**Deep test review (11 code paths) plus test index:**

- `tests/PiPlay.Tests/BorderlessResizeHitTestPolicyTests.cs`, `ProfileServiceTests.cs`, `SettingsServiceTests.cs`
- `tests/PiPlay.Tests/Ui/MainWindowProfileAccentTests.cs`, `WpfRuntimeTests.cs`, `XamlInvariantTests.cs`
- New tests: `BorderlessWindowHelperTests.cs`, `PlayerSurfaceProtocolTests.cs`, `PlayerSurfaceScriptTests.cs`, `PopoutPresentationPolicyTests.cs`, `RoundedWindowRegionPolicyTests.cs`
- `tests/README.md`

**Deep standards/spec review:**

- `CLAUDE.md`, `docs/AGENTS.md`, `docs/Feature_Workflow.md`, `docs/YouTube_Compliance.md`
- `docs/PiPlay_Product_Engineering_Spec.md`, `docs/QA_Checklist.md`, `docs/SPEC_GAPS_AND_OWNERSHIP.md`, `docs/CHANGELOG.md`
- `docs/adr/0005-single-player.md`, `0007-stable-channel-and-portable-data.md`, new `0008-popout-rounded-window-region.md`, and `docs/adr/README.md`
- Both 2026-07-15 Focused/rounded design specs and implementation plans

**Integration/consistency sampling:**

- `docs/PiPlay_UI_Priority_Improvements.md`
- `docs/Theme_Preset_Differences.md`
