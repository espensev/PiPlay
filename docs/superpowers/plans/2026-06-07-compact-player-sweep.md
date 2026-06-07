# Compact player sweep - implementation plan

**Spec:** `docs/superpowers/specs/2026-06-07-compact-player-sweep-design.md`

**Goal:** Deliver Phase 3 compact-player support as a staged sweep: resolve compact placement,
prove direct embed behavior, add the local `player.html` shell, bridge the YouTube IFrame API, then
finish release-grade QA. Normal page mode remains the default and fallback.

**Result:** Stage 1 (Tasks 1-3) is implemented in the working tree and the deterministic gate is
green (`dotnet test` = 280/280; non-mutating build gate clean). Stages 2-4 (Tasks 4-6, plus the live
release proof in Task 7) are intentionally deferred and gated on live Stage-1 verification.

## Implementation status

This pass landed **Stage 1 — Policy and direct embed** end to end, deterministically:

- `PlaybackModePolicy` owns the durable `null`/`normal`/`compact` vocabulary (legacy `embed` ->
  `compact`), the `profile.Mode ?? global CompactMode` precedence, and the separate 480x270 compact
  minimum. `SettingsService.Sanitize` normalizes stored profile modes on load.
- Settings exposes a global **Compact player** preference (off by default); the profile editor adds a
  **playback mode** override (Use global / Normal page / Compact player).
- `MainWindow.StartVideoPopoutAsync` resolves the effective mode (profile override scoped to the
  popout's own video, else global) and launches the Popout Player with `BuildEmbedUrl` in compact
  mode; `PlayerWindow` applies the mode-specific minimum and clamps the launch size up.

**Why stop at Stage 1:** the design stages precisely to avoid stacking an unverified JavaScript
messaging layer (Stages 2-3: local `player.html` shell + YouTube IFrame-API bridge) on top of an
unverified embed path. Stage 1 is the largest self-contained shippable unit — a working compact mode
that reuses the existing Popout Player lifecycle and the `YouTubeDomBridge` against the embed page's
`<video>`. Stages 2-3 are half a feature each (a shell without a bridge is incomplete), and their
core contract is live YouTube IFrame-API behavior that this environment cannot verify. They, the
embed-disabled error->normal fallback (Task 6), and the live release QA (Task 7) are the next gated
pass. Live compact playback, return/resume, and playlist behavior remain release-candidate QA.

## Tasks

- [x] **Task 1 - Mode policy and settings sanitization.** *(Done: `PlaybackModePolicy` + `SettingsService` sanitization; `PlaybackModePolicyTests` and `SettingsServiceTests` cover defaults, global/profile resolution, the legacy `embed` alias, invalid-value recovery, and the 480x270 min.)*
  - Add `PlaybackModePolicy` with durable values `normal` and `compact`.
  - Resolve effective mode from `Profile.Mode` and `PlayerSettings.CompactMode`.
  - Accept legacy/internal `embed` as a compact alias; sanitize unknown profile modes to `null`.
  - Define compact minimum size as 480 x 270, separate from normal 320 x 180.
  - Verification: logic/settings tests for defaults, global true/false, profile override,
    invalid-value recovery, and min-size choice.
  - Commit: `feat(compact): add playback mode policy`

- [x] **Task 2 - Settings and profile UI for compact placement.** *(Done: global "Compact player" toggle in Settings → Playback, off by default; "Use global / Normal page / Compact player" override in the profile editor. Markup test asserts the named control + tooltip + accessible name; WPF tests round-trip the global toggle and the `Prompt.BuildModePicker` selection. User-facing wording avoids `embed`/`PlayerWindow`.)*
  - Add global compact-player preference in Settings, off by default.
  - Add profile mode override in the profile editor: `Use global`, `Normal`, `Compact`.
  - Keep user-facing wording "Compact player"; do not surface `embed` or `PlayerWindow`.
  - Verification: markup tests for named controls/tooltips/accessibility names; WPF tests for
    selected value round-trip.
  - Commit: `feat(settings): expose compact player mode`

- [x] **Task 3 - Direct embed compact launch.** *(Done: `StartVideoPopoutAsync` threads the resolved mode into `PlayerWindow` and builds the URL via the pure `PlaybackModePolicy.BuildPopoutUrl` (compact -> `BuildEmbedUrl`); `PlayerWindow` applies the mode-specific minimum, clamps the launch size up, and raises a restored sub-minimum saved placement up to the floor via `PlacementMath.EnsureMinSize`. Source pause/placeholder, single-player guard, Pin/Fade, placement, and the return lifecycle are preserved. Logic tests cover the mode->URL join, the profile-override video-id gate, and the placement clamp; WPF tests assert mode-specific minimums. Live video smoke remains a release-candidate check before proceeding to shell work.)*
  - Thread effective mode from `MainWindow.StartVideoPopoutAsync` into `PlayerWindow`.
  - In compact mode, use `YouTubeUrlHelper.BuildEmbedUrl` for the initial stage.
  - Clamp compact player launch size to at least 480 x 270; normal mode keeps 320 x 180.
  - Preserve source pause/placeholder, single-player guard, Pin/Fade settings, placement capture,
    and return lifecycle.
  - Verification: logic tests for URL/mode choice; WPF tests for mode-specific minimums; manual
    video smoke before proceeding to shell work.
  - Commit: `feat(player): launch compact embed mode`

- [ ] **Task 4 - Local shell assets and virtual-host mapping.** *(Deferred — Stage 2, gated on live Stage-1 verification.)*
  - Add `src/PiPlay/PlayerShell/player.html` and `player-shell.js`.
  - Map local shell assets through `WebViewEnvironmentService` with a stable HTTPS host such as
    `https://piplay.local/`.
  - Build shell URLs carrying only video id, playlist id, start seconds, and non-sensitive mode data.
  - Include `enablejsapi=1` and an `origin` matching the shell origin for the YouTube iframe.
  - Verification: shell asset static tests; WebView environment construction tests; no credential
    or token-bearing strings in shell URLs/messages.
  - Commit: `feat(player): add local compact shell`

- [ ] **Task 5 - Host/shell messaging bridge.** *(Deferred — Stage 3; its core contract is live YouTube IFrame-API behavior.)*
  - Add a host-side `PlayerShellBridge` and shell message protocol.
  - Shell sends `ready`, periodic/current state, playback state changes, and errors.
  - Host sends `play`, `pause`, `seek`, and `requestState` commands.
  - Compact mode return-state capture uses IFrame API state, while normal mode keeps
    `YouTubeDomBridge`.
  - Verification: pure protocol tests, WPF construction tests, and live compact return/resume smoke.
  - Commit: `feat(player): bridge compact shell playback state`

- [ ] **Task 6 - Error/fallback behavior.** *(Deferred — Stage 4 fallback/error states; robust embed-disabled detection needs the Stage 3 IFrame API. Stage 1 leaves Normal page mode as the default and fallback.)*
  - Add compact in-app error state for embed-disabled, unavailable, failed shell load, or IFrame API
    timeout.
  - Provide a clear fallback action to reopen the same target in normal player mode.
  - Log redacted target information only; do not log full query strings with sensitive data.
  - Verification: logic/protocol tests for error messages and fallback URL; manual tests with an
    unavailable/restricted video.
  - Commit: `fix(player): add compact fallback path`

- [ ] **Task 7 - Docs, QA, and release proof.** *(Partial: Stage 1 docs done — CHANGELOG Phase 3 entry, spec resolved 480x270 minimum, SPEC_GAPS status, QA compact rows, and compact placement moved out of open decisions. The full live YouTube release QA is pending and stays a release-candidate gate.)*
  - Update product spec resolved defaults: compact placement = global default plus profile override.
  - Move compact placement out of open decisions in `SPEC_GAPS_AND_OWNERSHIP.md`.
  - Update `docs/CHANGELOG.md` and `docs/QA_Checklist.md` with compact-mode rows.
  - Run:

    ```powershell
    dotnet test PiPlay.sln --configuration Debug
    .\Build-PiPlay.ps1 -Stage Build -NoVersionBump -NoBuildNumberBump
    ```

  - For release candidate, run live YouTube QA for normal/compact, playlist, restricted/unavailable
    fallback, signed-in/signed-out sessions, return/resume, Pin/Fade, resize/DPI, and Stable publish.
  - Commit: `docs(compact): record phase 3 compact player sweep`

## Self-review

- Requirements -> tasks: mode placement in Tasks 1-2; compact playback in Tasks 3-5; fallback in
  Task 6; docs/release proof in Task 7.
- Ownership: URL building stays in `YouTubeUrlHelper`; settings persistence stays in
  `SettingsService`; WebView environment mapping stays in `WebViewEnvironmentService`; shell
  messaging gets its own bridge; `MainWindow` only resolves effective mode and starts Video Popout;
  `PlayerWindow` owns playback surface and return state.
- Risk: highest risk is YouTube embed compliance, return timestamp drift, API-message race
  conditions, and profile/global precedence confusion. The plan isolates these into policy tests,
  protocol tests, WPF construction tests, and live YouTube QA before release.
- Verified: Stage 1 (Tasks 1-3) deterministic gate green — `dotnet test PiPlay.sln --configuration
  Debug` = 280/280 (Logic/Markup/Wpf lanes, 0 skipped) and `.\Build-PiPlay.ps1 -Stage Build
  -NoVersionBump -NoBuildNumberBump` clean. Stages 2-4 (Tasks 4-6) and the live release proof
  (Task 7) are deferred and gated on live Stage-1 verification (see Implementation status). Live
  compact playback/return/resume, playlist behavior, and the embed-page DOM read are
  release-candidate QA, not yet proven.
