# UI overhaul stabilization and theme readiness - implementation plan

**Spec:** `docs/superpowers/specs/2026-06-10-ui-overhaul-stabilization-design.md`

**Goal:** Stabilize Popout Player behavior and Settings reachability first, then add the theme-ready
settings/model foundation without changing the single-player lifecycle, YouTube compliance posture, or
Compact-as-playback semantics.

**Review status:** Plan revised 2026-06-10 after an adversarial plan-vs-code review (multi-agent +
manual code verification). Diagnoses for Tasks 1-4 are now settled against source; dead investigation
routes are recorded as rule-outs so they are not re-litigated during implementation. See the spec's
"Review addendum" for the evidence trail.

**Result:** In progress (2026-06-10, branch `claude/condescending-ptolemy-dee3cf`). Tasks 1, 6, and
7 landed; `dotnet test PiPlay.sln --configuration Debug` = 425/425 and
`.\Build-PiPlay.ps1 -Stage Build -NoVersionBump -NoBuildNumberBump` clean (0 warnings). Task 1's
inset band was live-verified by an HWND-rect probe on a real popout (left/right/bottom delta exactly
10 px at 100% DPI; top delta 32 = chrome strip row) and Task 6's "Show popout" state + placeholder
by screenshot; manual drag-resize from each edge/corner and the Task 2 scroll diagnostics still need
a live interactive session. Tasks 2-4 carry settled diagnoses, rule-outs, and protocols below.

## Tasks

- [x] **Task 1 - Repair edge and corner resize over WebView2 (Popout Player AND Source Window).**
  *(Landed `fix(popout): restore edge resize over player surface`; live HWND probe confirmed the
  band geometry; manual drag QA from each edge/corner remains for the Task 12 pass.)*
  - Diagnosis (settled): both resize mechanisms — the `BorderlessWindowHelper` subclass and
    `WindowChrome.ResizeBorderThickness` — act on top-level `WM_NCHITTEST`, which Windows never sends
    for points over the WebView2 child HWND chain (owned cross-process by msedgewebview2.exe). The
    working zones map 1:1 to the 32 DIP `ChromeStrip` WPF pixels; left/right/bottom/lower corners die
    over the `Player`/`Browser` surface. `MainWindow` has the identical defect below its toolbar.
  - Route (settled): layout inset — give the WebView2 element a
    `BorderlessResizeHitTestPolicy.ResizeBorderDip` (10) margin on left/right/bottom so the top-level
    HWND owns those band pixels; the existing subclass + policy then work unchanged, corners included.
    Trade-off accepted: a visible ~10 DIP window-background band (vs REQ-WINDOW-02's "0-2 px outline"
    wording) — recorded as a settled decision in the spec. Fallback if QA rejects the frame:
    in-process layered band child HWNDs that synthesize `WM_NCLBUTTONDOWN` resize on the parent.
  - Ruled out (do not re-investigate): subclassing `Chrome_WidgetWin_1` / `Chrome_RenderWidgetHostHWND`
    (cross-process — `SetWindowSubclass` cannot attach); JS/DOM pointer forwarding (fails structurally
    in compact mode: the YouTube iframe is cross-origin); CompositionController rehosting (out of
    scope for stabilization); WebView2 non-client-region support (drag-only, no resize).
  - Known residual: with strip auto-hide collapsed, the WebView reaches y=0 and the TOP band dies until
    the top-edge reveal poll restores the strip (hover reveals, then resize works). Accepted for this
    pass; add a QA row documenting the reveal-then-resize beat.
  - Verification: the existing `BorderlessResizeHitTestPolicyTests` and the WpfRuntimeTests NCHITTEST
    row pass TODAY with the bug live (no test surface hosts a real WebView2 child), so they cannot
    gate this fix. Add `XamlInvariantTests` margin invariants for `Player`/`Browser` (band ==
    `ResizeBorderDip` on left/right/bottom), keep policy tests unchanged, and gate on manual
    edge/corner resize on a live popout (Popout Standard and Popout Fullview Faded are one path —
    verify both captures, one fix).
  - Commit: `fix(popout): restore edge resize over player surface`

- [ ] **Task 2 - Restore normal-page Popout Player scroll (diagnose first, then fix one owner).**
  - Candidate owners, ranked (settled by code review): (1) **WS_EX_LAYERED whole-window opacity** —
    the only popout-vs-source input-path delta in code; engaged at the user's 85%/78% in every
    observed failure; the Stage 0 spike never wheel-tested over the WebView child. (2) **Wheel
    routing / WebView focus** — `WM_MOUSEWHEEL` is focus-routed unless Windows' "scroll inactive
    windows" redirects by position; `PlayerWindow` never focuses its WebView, the source Browser
    gets focus from normal clicking. (3) Page state (weakest — same watch-page family as source).
  - Ruled out (recorded): the resize hit-test subclass (never executes over the child HWNDs — it
    cannot eat wheel there) and any WPF overlay (`Player` sits alone in its grid row; no overlay
    element exists; "overlay IsHitTestVisible" was a phantom owner).
  - Diagnostic protocol (run in order, record results in the worklog):
    1. Opacity A/B: set active/idle to 100%/100% (applier disengages to a byte-identical non-layered
       window) and retest wheel over the page.
    2. Click into the page first, then wheel (focus routing).
    3. Check the system "Scroll inactive windows when hovering" setting on the test machine.
    4. `ExecuteScriptAsync` wheel-listener probe: does the renderer see wheel events at all?
    5. YouTube scrollbar drag + keyboard PgDn/arrows (scroll path vs wheel path).
    6. Touchpad vs physical wheel.
  - Fix only the confirmed owner; the fix must be fade/opacity-state-agnostic so Popout Standard and
    Popout Fullview Faded stay one path. If the owner is layered opacity itself, the fix decision
    (e.g. force 100% while pointer is over the window, vs document the limitation) goes back to the
    spec as a settled decision before code.
  - Re-verify scroll AFTER Task 1 lands: the inset band claims the outer 10 DIP for WPF, so wheel over
    the band is inert by design — confirm in-page scroll is unaffected and record the band contract.
  - Verification: scripted wheel smoke on a live popout (reuse the spike's real-input driver pattern)
    plus manual wheel/touchpad/scrollbar checks in both captures.
  - Commit: `fix(popout): allow scrolling in normal page player`

- [ ] **Task 3 - Keep compact navigation inside PiPlay and make return state video-aware (both modes).**
  - New-window policy (reframed): WebView2's `NewWindowRequested` carries no window-open disposition,
    so "explicit external intent" is NOT distinguishable from a left-click. Use the URL-shape proxy,
    mirroring `MainWindow.Core_NewWindowRequested`: a parsed YouTube watch target with a `VideoId`
    (`YouTubeUrlHelper.TryParse`) retargets in place; everything else stays on `OpenExternal`. The
    in-app gate must be TryParse-with-VideoId, NOT `NavigationPolicy.IsAllowed` (which would admit
    channel/shorts/search pages into the shell). Extract the decision as a pure policy seam.
  - Compact retarget: rebuild the shell URL via `PlaybackModePolicy`/`YouTubeUrlHelper.BuildShellUrl`
    and `Navigate` the same `CoreWebView2`. Retargeting invalidates launch-time state: make the
    fallback target/current-URL mutable (the readonly `_fallbackTarget`/`_url` would make the error
    bar reopen the WRONG video), re-arm the shell-ready watchdog, and reset `LastKnownSeconds`.
  - Shell protocol: add `videoId` to shell state messages via `player.getVideoData()` — playlist
    auto-advance and end-screen clicks navigate INSIDE the iframe with no host event, so host-side
    tracking alone is insufficient. Additive and parse-compatible; update the `PlayerShellAssetTests`
    pinned field list.
  - Return state (both modes — fixes an existing REQ-RETURN-01 corruption): `PlayerReturnState` gains
    video identity; `ReturnPolicy` gains a navigate-vs-seek decision; `Player_OnClosed` navigates the
    source to `BuildWatchUrl(newTarget, t=seconds)` when the returned video differs from the source's
    video (URL-borne timestamp avoids scripting after navigation) and updates
    `_autoLastHandledVideoId` so Auto mode does not instantly re-pop the returned video. Normal mode
    reports its current video too (canonical-URL capture on the sync cadence / at close) — today a
    popout that SPA-navigated or autoplay-advanced seeks the ORIGINAL source video to the new video's
    timestamp.
  - Keep unsafe URLs and non-YouTube URLs on the existing external/block path.
  - Verify with new logic tests: new-window policy decisions (watch/shorts/channel/external/unsafe),
    shell-URL rebuild with list/start, protocol `videoId` parse + asset pins, `ReturnPolicy` navigate
    decision, Auto de-dup update, retargeted-fallback correctness; run affected `PlayerShell*`,
    `NavigationPolicyTests`, `ReturnPolicyTests`, and compact WPF tests.
  - Commit: `fix(compact): keep allowed recommendations in piplay` (+ follow-up commit
    `fix(player): return current video and timestamp on close` if landed separately)

- [ ] **Task 4 - Provide one reliable expand/fullview path (native strip affordance + gated event).**
  - Primary route (settled): a native WPF expand/restore button on the existing `ChromeStrip`,
    calling the same host handler `fullscreenToggle` already reaches. No protocol change is needed —
    the `fullscreenToggle` channel (protocol consts, dual allowlists, bridge event, host handler,
    tests) is ALREADY complete end-to-end; it merely has no caller. A native button serves BOTH
    playback modes, avoids overlaying the YouTube iframe (Q-5), and supersedes the still-open
    popout-overlay plan's in-shell button (reconcile that plan's Task 5 with a note).
  - Secondary route (optional, makes the YouTube button honest): handle
    `CoreWebView2.ContainsFullScreenElementChanged` → toggle the same window state, gated on the LIVE
    `_mode == Compact` (the compact→normal fallback flips `_mode` in place). Un-gated, this would
    silently give the normal-page popout a new fullscreen invariant — exactly the
    Standard/Fullview-Faded divergence that is out of bounds. (Today the YT button "does nothing"
    because the unhandled fullscreen element fills only the WebView bounds, which the player already
    fills, and `fs` defaults to 1 in the IFrame playerVars.)
  - Decide and record the maximize semantics: `PlayerWindow` lacks `EnableProperMaximize`, so
    Maximized covers the FULL monitor including the taskbar — currently an accident. Settle it as the
    deliberate fullview behavior for a video window (or add the work-area hook; pick one in the spec).
  - Reversibility requirements: the restore affordance must stay reachable in maximized state (verify
    the top-edge reveal geometry under strip auto-hide while maximized; consider Esc as exit); and
    closing while maximized must NOT persist Maximized as the next popout's launch state (normalize
    the placement capture or store the prior normal bounds).
  - Verify with shell-request WPF tests (exist), new WindowState toggle/restore tests, placement
    persistence normalization tests, and manual compact + normal expand/restore.
  - Commit: `feat(popout): add reliable expand path`

- [ ] **Task 5 - Make Settings scrollable and sectioned.**
  - Update `SettingsWindow.xaml` from a tall fixed dialog (`SizeToContent="Height"`, no scrolling) to
    a bounded layout: `MaxHeight` (work-area-derived) + `ScrollViewer` so it fits shorter displays.
  - Organize sections as Privacy, Appearance, Playback, and Advanced.
  - Keep `Compact player` under Playback and add visible copy that it applies to new Popout Players.
  - Move fade delay, active/idle opacity, auto-hide top bar, and future reset-theme defaults toward
    Advanced. The opacity live-preview path (`OpacityPreviewChanged` → `_player.ApplyWindowOpacity`)
    must survive the re-section.
  - Constraint: preserve every existing control `x:Name` through the restructure — the current
    `XamlInvariantTests`/`WpfRuntimeTests` pin the Pin/Fade swatch names and click them by name, and
    the swatches survive until Task 10 replaces them.
  - Preserve distinct Reset app state and Clear browser data wording/actions.
  - Outline item 5.6 (fade/top-edge reveal discoverability) is explicitly deferred from this pass
    (recorded in the spec); do not fold ad-hoc discoverability changes in here.
  - Verify with `XamlInvariantTests` for a Settings `ScrollViewer` and section/control existence, plus
    `WpfRuntimeTests` for construction at constrained height and existing settings behavior.
  - Commit: `refactor(settings): make settings scrollable and sectioned`

- [x] **Task 6 - Clarify source actions and fallback messages.**
  *(Landed `fix(source): clarify popout action states`; "Show popout" state live-verified by
  screenshot with an open popout.)*
  - Add an `UpdatePopoutActionState()` seam in `MainWindow` keyed off `_player`, called from
    `StartVideoPopoutAsync` (after create) and `Player_OnClosed`, with an internal hook for
    `WpfRuntimeTests`. While `_player != null` the primary action reads as show/focus
    ("Show popout"), and the click path must RESTORE a minimized popout (normalize `WindowState`)
    before `Activate()` — bare `Activate()` does not un-minimize.
  - Content-swap mechanics: name the button's inner glyph/label `TextBlock`s and toggle `.Text`
    (assigning a plain string to `Content` would drop the icon); keep `x:Name="PopOutButton"` stable
    (external automation — `Test-UiSmoke.ps1`, run-piplay — locates it by AutomationId); add a
    ToolTip per state.
  - Baseline recorded: pressing popout on YouTube home already shows a clear pre-pause modal — the
    no-target work is copy/affordance polish, not crash recovery. Do NOT gate the button's enabled
    state on `IsWatchUrl`-style checks: popout legitimately works from shorts/live/embed/youtu.be
    pages that are not `/watch` URLs.
  - Surface the existing YouTube mix/radio `FallbackReason` on the SOURCE side as secondary text on
    `SourcePlaceholder` (new TextBlock; visible exactly while the popout owns playback). Do not use
    the popout's `ErrorBar` — that is the compact-shell error surface and would diverge the popout
    captures.
  - Use `Source Expanded Player` terminology in any new docs/tests/user copy for the source-side
    expanded YouTube state.
  - Verify with `MainWindow` WPF runtime tests for action state/copy via the new seam, logic tests
    for target/fallback policy, and manual Source Home/Source Watch checks.
  - Commit: `fix(source): clarify popout action states`

- [x] **Task 7 - Add missing accessible names.**
  *(Landed `fix(a11y): name icon controls`; XamlInvariantTests sweep + Prompt runtime assert.)*
  - Add explicit `AutomationProperties.Name` values for icon-only or templated controls in
    `MainWindow.xaml`, `PlayerWindow.xaml`, and `SettingsWindow.xaml`.
  - Inventory (verified): MainWindow — Settings/Minimize/Maximize/Close, Back/Reload/Home, UrlBox
    (unnamed primary input), ProfilesCombo (empty name), Save/Edit/Delete profile, Pin, Auto,
    PopOutButton (empty name). PlayerWindow — FadeToggle, PinToggle, CloseButton. SettingsWindow —
    CloseButton only (the appearance controls are already named).
  - Dynamic names: `MaximizeButton` flips Maximize/Restore content in `StateChanged` — either set the
    name in the same handler or use a state-neutral name; `PopOutButton`'s name tracks the Task 6
    action state (static `XamlInvariantTests` row for the initial name + `WpfRuntimeTests` row that
    label and name flip together).
  - Out-of-XAML target: the code-built `Prompt.BuildShell` close button (all Prompt dialogs) needs
    `AutomationProperties.SetName` in code and a `WpfRuntimeTests` assertion (XamlInvariantTests
    cannot see code-built UI).
  - Verify by extending `XamlInvariantTests` required-name rows and running existing WPF runtime tests.
  - Commit: `fix(a11y): name icon controls`

- [ ] **Task 8 - Add theme settings model and migration.**
  - Add `ThemeSettings` to `src/PiPlay/Models/AppSettings.cs` with default `ThemeId`,
    `AccentColor`, `FadeDelayPreset`, and nullable overrides for strip auto-hide and active/idle
    opacity. Pin the precedence rules (ThemeSettings override vs legacy field) in tests.
  - Round-trip protection: add `[JsonExtensionData]` to `AppSettings` (and `PlayerSettings`) so an
    OLDER binary that reads a newer settings file does not silently DELETE the theme block on its
    next atomic save — System.Text.Json drops unknown members by default.
  - Migration seed: initialize `ThemeSettings.AccentColor` from the normalized hex of the legacy
    `PinAccent` on first load, so the single-accent transition starts from the user's choice.
  - Keep old fields readable: `PinAccent`, `FadeAccent`, `FadeIdleDelayMs`,
    `ConstantWindowOpacity`, `IdleWindowOpacity`, `StripAutoHide`, and `CompactMode`.
  - Define invalid theme ID and invalid accent hex fallback behavior in `SettingsService.Sanitize`.
  - Add a theme catalog/model seam under `src/PiPlay/Theme/` with `sharp-dark`, `minimal`, and
    `soft-glass` presets.
  - Verify with `SettingsServiceTests` and new theme catalog tests for defaults, round trip (both
    directions), migration, invalid values, extension-data preservation, and catalog uniqueness.
  - Commit: `feat(theme): add compatible theme settings model`

- [ ] **Task 9 - Introduce theme resources and compatibility aliases.**
  - Add theme-owned resource tokens for base colors, accent derivations, opacity/fade defaults, and
    staged radius values while keeping existing brush keys as aliases during migration.
  - Startup ordering: settings are currently loaded inside the `MainWindow` constructor — applying
    theme resources "before any window is created" requires a separate read-only settings load in
    `App.OnStartup` (acceptable; the applier only reads). Record this in the spec.
  - Resource mechanics: window XAML uses `StaticResource` throughout, which freezes values at parse
    time — replacing dictionary values in `App.OnStartup` BEFORE window construction works; LIVE
    switching of open windows would require a `StaticResource`→`DynamicResource` migration. Default
    this pass to startup/next-window application and say so in Settings copy.
  - Move key styles in `Theme/Colors.xaml` and `Theme/ControlStyles.xaml` from hardcoded values to
    tokens where risk is low.
  - Verify with XAML resource invariant tests, contrast tests, WPF construction tests for all windows,
    and a manual startup/theme smoke.
  - Commit: `feat(theme): apply preset resource tokens`

- [ ] **Task 10 - Add theme selector and accent chips.**
  - Add Settings controls for theme preset selection and fixed accent chips: muted cyan, steel blue,
    violet, green, and amber. Store normalized hex from the start.
  - Replace separate Pin/Fade color controls only after the single accent path drives Source Window
    Pin, Popout Pin, and Popout Fade consistently.
  - Test-rewrite is part of this task (not just "removal checks"): invert/remove the
    `XamlInvariantTests` rows that PIN the eight `PinAccent*`/`FadeAccent*` swatch names and their
    tooltip/name coverage, and migrate the `SettingsWindow(pinAccent:, fadeAccent:, ...)` constructor
    surface that multiple `WpfRuntimeTests` construct and click by name.
  - Keep advanced overrides for opacity/fade/top-bar behavior distinct from Compact player.
  - Verify with Settings WPF runtime tests, XAML invariant tests that obsolete color controls are gone
    after replacement, and manual captures for Pin/Fade/accent consistency.
  - Commit: `feat(settings): add theme and accent controls`

- [ ] **Task 11 - Refresh QA docs and discovery evidence.**
  - `docs/QA_Checklist.md`: AMEND the existing Phase-3 resize rows to require resize with the pointer
    OVER the player surface (the actual reported bug) rather than adding duplicate rows; add rows for
    scroll, compact recommendation navigation, compact expand/fullview (including
    reveal-then-restore while maximized), Settings scroll, accessibility names, theme preset smoke,
    and the auto-hide reveal-then-resize beat from Task 1. Retire/rewrite the Pin/Fade color rows
    after Task 10. Write new rows per-BEHAVIOR with state-evidence columns (Popout Standard /
    Fullview Faded), never per-state procedures.
  - `docs/CHANGELOG.md`: open a new `[Unreleased]` section at the top — the current file starts at
    the already-shipped 0.4.0-beta block.
  - `docs/SPEC_GAPS_AND_OWNERSHIP.md`: fix the stale "Stage 4 deferred" claim (the compact error
    bar/fallback shipped) while touching the docs surface.
  - Update `docs/ui-overhaul-discovery/ui-state-notes.md` with post-fix state notes and screenshot
    paths; rename the State 03 heading wording to `Source Expanded Player`.
  - Update `docs/ui-overhaul-discovery/theme-system-overhaul-evaluation.md` if theme decisions changed.
  - Verify with `rg` for stale doc/file references and stale "fullscreen" wording where it should be
    Source Expanded Player — scoped to user-facing/doc wording, explicitly EXCLUDING PlayerShell
    protocol identifiers (`ActionFullscreenToggle`, `fullscreenToggle`) and compact-mode QA rows.
  - Commit: `docs(ui): refresh overhaul qa evidence`

- [ ] **Task 12 - Run full gate and self-review.**
  - Run `dotnet test PiPlay.sln --configuration Debug`.
  - Run `.\Build-PiPlay.ps1 -Stage Build -NoVersionBump -NoBuildNumberBump`.
  - Landing note: prefer a single PR containing this spec; if the work splits across PRs, every
    code-touching PR must add/modify a dated `docs/superpowers/specs/*-design.md` or carry a
    `Spec-Exception:` line, or spec-check goes red (only "Build and test (Windows)" is required, but
    keep the gate green).
  - Perform a focused review over `PlayerWindow`, `MainWindow`, `SettingsWindow`, settings migration,
    navigation policy, return policy, and theme resource ownership.
  - Record final test counts, manual QA evidence paths, deferred decisions, and any follow-up tickets
    in this plan's Result and Self-review sections.
  - Commit: `chore(ui): record overhaul stabilization verification`

## Self-review

- Requirements -> tasks:
  - `REQ-WINDOW-02`, `Q-7`, `Q-8`: Tasks 1, 2, 4, and 12. (The "no resize while maximized" guard is a
    `BorderlessResizeHitTestPolicy` invariant, preserved by Tasks 1/4 — it is not REQ-WINDOW-02 text.)
  - `REQ-WINDOW-01` (PerMonitorV2 DPI): Tasks 1 and 12 via manual mixed-DPI resize QA.
  - `Q-1`, `Q-2`, `REQ-RETURN-01`: Tasks 3, 4, 6, and 12. Task 3 closes an EXISTING REQ-RETURN-01
    gap (return after in-popout navigation, both modes), not only the compact case.
  - `Q-3`, `Q-5`, `Q-6`: Tasks 3, 4, 6, and 12.
  - `REQ-UI-01`, `REQ-UI-02`: Tasks 5, 7, 9, 10, 11, and 12.
  - `REQ-PRIVACY-01`, `REQ-PRIVACY-02`: Task 5.
  - `REQ-PROFILE-01` and compact placement policy: Tasks 3, 8, and 10.
- Ownership:
  - Source Window policy stays in `MainWindow` and shared service seams.
  - Popout Player behavior stays in `PlayerWindow`, `PlayerShell*`, and window/navigation services.
  - Settings persistence stays in `SettingsService` / `AppSettings`.
  - Theme resources stay under `src/PiPlay/Theme`.
  - YouTube behavior remains allowlisted and compliance-bound; no custom media replacement.
- Dual-state policy (owner constraint): Popout Standard and Popout Fullview Faded are one normal-page
  `PlayerWindow` path captured twice. No task builds separate logic for them; fixes must be
  fade/opacity-state-agnostic; QA rows are behavior-keyed with state-evidence columns. Promotion to a
  real state happens ONLY if a distinct invariant appears (different playback URL, chrome policy,
  window state, input behavior, or separate entry/exit command) — see spec unresolved decision 1.
- Risk:
  - Highest risk remains native/WebView2 input behavior (Tasks 1-2) — both tasks now carry settled
    routes, recorded rule-outs, and a diagnostic protocol so live QA is confirmation, not search.
  - Task 3 grew: it now touches `PlayerShellProtocol` (additive `videoId`), `PlayerReturnState`,
    `ReturnPolicy`, `Player_OnClosed`, and the Auto de-dup key. The pieces are individually small and
    policy-seamed; the protocol change is parse-compatible.
  - Settings/theme migration risk is concentrated in schema compatibility (mitigated by
    `[JsonExtensionData]`) and resource drift; Task 10's swatch replacement is a known test-rewrite.
  - Manual QA remains required for real YouTube scroll, expand behavior, and mixed-display resize.
- Verified:
  - Pending. Fill with final `dotnet test PiPlay.sln --configuration Debug` count,
    `.\Build-PiPlay.ps1 -Stage Build -NoVersionBump -NoBuildNumberBump` result, and manual QA
    evidence paths.
