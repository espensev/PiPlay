# Auto (playback-start auto-popout) — implementation plan

**Spec:** `docs/superpowers/specs/2026-06-06-auto-popout-design.md`

**Goal:** Add opt-in `Auto` that starts a Video Popout when a watch video begins playing, off by default,
reusing `StartVideoPopoutAsync` and the single-player lifecycle, with the false-positive/anti-loop logic
in a pure, unit-tested seam. Manual popout and the return policy stay unchanged.

**Result:** _pending — fill with `dotnet test PiPlay.sln -c Debug` counts and the manual-smoke outcome._

## Tasks

- [ ] **Task 1 — Pure decision seam + setting (no UI, fully unit-tested).**
  - Add `AppSettings.AutoPopout` (bool, default false). Add `src/PiPlay/Services/AutoPopoutPolicy.cs`:
    static `Decide(autoEnabled, isPlaying, isWatchVideo, currentVideoId, lastHandledVideoId,
    popoutActive) → AutoPopDecision { Skip, Pop }` (Pop only when enabled + isPlaying + `/watch` + id ≠
    lastHandled + no active player; `isWatchVideo` means `/watch` specifically, so Shorts/embeds Skip).
  - Verify: `dotnet test --filter Category=Logic`. New `AutoPopoutPolicyTests` truth-table green;
    `SettingsServiceTests` Auto default-false + round-trip green.
  - Commit: `feat(auto): AutoPopoutPolicy decision seam + off-by-default AutoPopout setting (spec §6.1)`

- [ ] **Task 2 — Toolbar toggle (persisted, no trigger yet).**
  - `MainWindow.xaml`: new `ColumnDefinition` + `AutoToggle` mirroring `PinToggle`. `MainWindow.xaml.cs`:
    `AutoToggle_Click`/`ApplyAuto` (Pin-style → `Save`); apply in ctor; re-sync in `ApplyResetState`.
  - Verify: `dotnet test --filter "Category=Markup|Category=Wpf"`. `XamlInvariantTests` asserts
    `AutoToggle` + tooltip; `WpfRuntimeTests` constructs MainWindow and reflects the loaded flag.
  - Commit: `feat(auto): off-by-default Auto toolbar toggle wired to settings`

- [ ] **Task 3 — Live playback detector (manual/Lane-B verified).**
  - `MainWindow.xaml.cs`: source-side `DispatcherTimer` (~250 ms, `_autoTickInProgress` reentrancy guard)
    that best-effort reads "is playing + current `/watch` id", consults `AutoPopoutPolicy.Decide`, and
    calls `StartVideoPopoutAsync` on `Pop`. Record the handled id **inside `StartVideoPopoutAsync`** (covers
    manual + auto in one place). **Seed** `lastHandledVideoId = currentId` when Auto is enabled (toggle-on
    and at startup) so the already-playing video isn't yanked. Start/stop the timer with `_browserReady`,
    the Auto flag, popout state, `_clearingBrowserData`, and `MainWindow_Closing`. All best-effort.
  - Verify: full `dotnet test PiPlay.sln -c Debug` green; **manual smoke** — auto-pops once while a watch
    video plays, no re-pop after return, a second video re-pops, a Short does not pop, Auto-off stops it,
    one player under rapid trigger.
  - Commit: `feat(auto): source-side playback-start detector that auto-starts the popout (spec §6.1)`

- [ ] **Task 4 — Docs: resolve the open decision + record the feature.**
  - `CHANGELOG.md` `[Unreleased]` Phase-2 Auto entry; `SPEC_GAPS_AND_OWNERSHIP.md` move "Auto trigger
    timing" to Resolved (playback-start); spec §6.1 decided-behavior; `QA_Checklist.md` Phase-2 Auto row.
  - Verify: `docs-sync` clean; no contradictions; `Planned — Phase 2 (remaining)` updated.
  - Commit: `docs(auto): resolve Auto trigger-timing decision and document the feature`

## Self-review

- **Requirements → tasks:** §6.1 off-by-default + easy-disable → T1/T2; playback-start trigger +
  single-player + no-loop → T3 (logic in T1's seam); SPEC_GAPS resolution → T4. Acceptance criteria's
  unit assertions → T1/T2; the live/loop criteria → T3 manual smoke.
- **Ownership:** trigger + lifecycle stay in `MainWindow`; DOM reads stay in `YouTubeDomBridge`
  (poll-only, unchanged); decision logic isolated in `AutoPopoutPolicy`. `ReturnPolicy`,
  `YouTubeUrlHelper`, settings persistence untouched in contract.
- **Risk:** concentrated in the return-resume re-trigger loop (closed by id-based arm/disarm, unit-tested
  in T1) and the WebView2-bound timer (not unit-testable → manual smoke in T3). The pure seam carries the
  branch logic so the risky part is covered by `dotnet test`.
- **Verified:** _final test count + manual-smoke result to be recorded on completion._
