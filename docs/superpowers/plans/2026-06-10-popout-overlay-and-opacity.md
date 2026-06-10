# Plan — compact overlay controls + whole-window opacity (2026-06-10)

**Goal:** Deliver the floating-mini-player look (user reference screenshots, 2026-06-10): in-shell
overlay controls in compact mode (selectable, default off), DWM-rounded corners, an auto-hiding
chrome strip, and whole-window opacity with separate idle + constant settings. Design and settled
decisions in `docs/superpowers/specs/2026-06-10-popout-overlay-and-opacity-design.md`.

**Implementation status (2026-06-10):** planned only. Nothing implemented. Stacked after the
compact Stage 4 branch (`feat/compact-player-stage4`), which is itself stacked on PR #13.

**Risk staging (same discipline as the compact sweep):** the three live unknowns are isolated in
Stage 0 spikes *before* any dependent code is written. Everything after Stage 0 is deterministic
until the final smoke. If S-1 fails, Stages 2's opacity work gates on a
`WebView2CompositionControl` re-design and the rest of the plan still ships.

## Tasks

- [ ] **Stage 0 / Task 1 — Spikes (live, throwaway code, evidence in worklog).**
  - **S-1 (gates Stage 2):** `WS_EX_LAYERED` + `SetLayeredWindowAttributes(LWA_ALPHA)` on the live
    Popout Player while a video plays — verify rendering (not black/glitched), input at 60%/45%
    alpha, drag/resize, CPU/GPU sanity. The product spec explicitly mandates this test before
    whole-window opacity ships.
  - **S-2 (gates Task 5 drag):** `CoreWebView2Settings.IsNonClientRegionSupportEnabled` + CSS
    `app-region: drag` in the shell — verify on the installed Evergreen runtime; record
    runtime/SDK versions. Fallback if unsupported: native strip stays the drag handle.
  - **S-3:** `DWMWA_WINDOW_CORNER_PREFERENCE = ROUND` on the borderless popout, alone and combined
    with S-1 alpha; confirm Win10 no-op path.
  - Verification: screenshots + worklog record per spike; no production code merged from spikes.
  - Commit: `chore(spike): record layered-alpha / app-region / DWM-rounding spike results`

- [ ] **Task 2 — WindowOpacityPolicy + settings model.**
  - Pure `WindowOpacityPolicy`: effective opacity (idle vs constant), 0.45 UI floor vs 0.10 file
    floor, clamp rules, animation duration (reuse `FadePolicy.FadeDurationMs`), one-idleness-source
    rule documented in XML docs.
  - `AppSettings`: add `player.constantWindowOpacity` (default 1.0) beside the existing
    `idleWindowOpacity`; `SettingsService` sanitization mirrors the 0.1–1.0 reset.
  - Verification: logic tests (policy table, clamps, floor/unlock) + settings sanitization tests.
  - Commit: `feat(player): window opacity policy + settings model`

- [ ] **Task 3 — Apply opacity in PlayerWindow + Settings UI.**
  - Layered-alpha application behind a `*ForTests`-seamed applier (Wpf lane asserts policy output,
    not live HWND alpha); idle hook shares the controls-fade idle timer; `WS_EX_TRANSPARENT` never
    set (assert in tests via the style mask seam).
  - Settings → Appearance: two sliders (Active / When idle), 45% floor, live preview on the open
    popout; `PlayerAppearancePolicy` owns the vocabulary.
  - DWM rounded corners on the popout (from S-3 result).
  - Verification: logic + markup + Wpf lanes; manual smoke at 100/85/60/45%.
  - Commit: `feat(player): whole-window opacity (idle + constant) with 45 percent floor`

- [ ] **Task 4 — Protocol `request` kind + native strip auto-hide.**
  - `PlayerShellProtocol`: outbound `request` kind, allowlisted actions `close` / `pinToggle` /
    `fullscreenToggle`; version bump; drift-guard extension. `PlayerShellBridge.RequestReceived`.
  - Chrome strip auto-hide (height-collapse on idle, top-edge hover reveal) as a selectable
    appearance behavior — native escape hatch per the design; Stage 4 error bar untouched.
  - Verification: protocol/bridge logic tests, markup invariants, Wpf strip state-machine tests.
  - Commit: `feat(player): shell request channel + auto-hiding chrome strip`

- [ ] **Task 5 — Overlay controls in the shell (compact, selectable, default off).**
  - `player.html`/`player-shell.js`: `controls=0` chromeless player when overlay look is on;
    overlay DOM (prev/play-pause/next, progress + seek, volume/mute, captions, close, fullscreen),
    hover/pause reveal + idle auto-hide; transport is shell-local via the IFrame API;
    close/pin/fullscreen via the `request` channel; drag region per S-2 outcome.
  - Settings toggle ("Overlay controls (compact player)"), default off; accent follows the existing
    appearance vocabulary.
  - Verification: shell asset/drift tests extended to overlay markup; protocol round-trip tests;
    live smoke (scripted: drive overlay buttons via the shell, screenshot evidence).
  - Commit: `feat(player): compact overlay controls (selectable look)`

- [ ] **Task 6 — Docs, QA, compliance record, release smoke.**
  - Product spec: §7.3 resolution notes + the overlay compliance-deviation record (user decision
    2026-06-10); QA checklist rows (overlay look on/off, opacity floors, clicks-never-pass-through
    gate, error bar under every combination); CHANGELOG.
  - Full gate + a combined live smoke (overlay on + idle opacity 60%): screenshots, log evidence.
  - Commit: `docs(appearance): record overlay + opacity verification and QA rows`

## Decision trail

- 2026-06-10 user: overlay controls = **custom in-shell** (chose with the compliance caveat in the
  option text); transparency = **both idle and constant, separate settings**; the opaque look
  ("or not transparent") stays first-class (100% default).
- Branch: `feat/popout-overlay-opacity`, stacked on `feat/compact-player-stage4` → PR #13. Rebase
  forward as those land; PRs held for the user per standing practice.

## Quality gates

Every task: `dotnet test PiPlay.sln --configuration Debug` all green, 0 skipped;
`.\Build-PiPlay.ps1 -Stage Build -NoVersionBump -NoBuildNumberBump` 0W/0E. Stage 0 evidence must
exist in the worklog before Tasks 3/5 start. Q-8 gate at the end: player interactable at every
opacity; clicks never pass through (ADR-0006).
