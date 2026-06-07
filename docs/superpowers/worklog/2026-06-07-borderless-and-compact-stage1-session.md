# Session worklog — borderless resize zones landing + compact player Stages 1-3 (2026-06-07)

Saved record of the working session that produced branch `feat/compact-player-stage1`
(commits: `dceb0cf` borderless, `0433095` compact Stage 1, `c0982f1` worklog, compact Stages 2-3).

## Request

> "review the next set of plans add more into the scope as u see fit then drive to completions,
> yet following the documentation flow"

Interpreted against the two dated 2026-06-07 plans already present in the tree: borderless resize
zones (implemented but uncommitted) and the compact player sweep (planned, not implemented).
"Following the documentation flow" was read as the binding constraint — including the compact
design's own staged risk plan.

## What was reviewed

- `docs/Feature_Workflow.md` + `docs/AGENTS.md` — the design→plan→implement→gate→PR flow, ownership
  seams, the terminology table, the Q-1..Q-8 quality bar, and the hard non-goals.
- The two dated plans and design specs (borderless resize zones; compact player sweep), plus spec
  sections 10 (playback modes) and 16 (window quality), and `SPEC_GAPS_AND_OWNERSHIP.md`.
- Code seams the work touches: `MainWindow`/`PlayerWindow` (popout lifecycle), `YouTubeUrlHelper`
  (`BuildEmbedUrl`/`BuildWatchUrl`), `YouTubeDomBridge`, `SettingsService`/`AppSettings`/`Profile`,
  `WindowPlacementService`/`PlacementMath`, `Prompt` (profile editor), `SettingsWindow`, the theme
  control styles, and the three test lanes (Markup/Logic/Wpf).
- The already-implemented borderless code in the working tree (`BorderlessResizeHitTestPolicy`,
  `BorderlessWindowHelper`) — confirmed complete and green before committing it.

## Decisions

- **Land borderless as its own commit first.** It was complete in the tree; committing it separately
  keeps it shippable independently of the compact work.
- **Drive the compact sweep to Stage 1 only, deterministically.** The design stages precisely to
  avoid stacking an unverified JavaScript messaging layer (Stages 2–3: local shell + IFrame-API
  bridge) on an unverified embed path. Stage 1 is the largest self-contained shippable unit — a
  working compact mode reusing the existing Popout Player lifecycle and the `YouTubeDomBridge`
  against the embed page's `<video>`. Stages 2–4 (shell, bridge, embed-disabled fallback, live QA)
  are deferred and gated on live Stage-1 verification this environment can't perform. Reframed from
  an early "can't test it" rationale (wrong — the shell/bridge are unit-testable) to the risk-staging
  rationale (right).
- **Profile-mode call-site = video-id match.** The selected profile's `Mode` applies only when the
  popout target IS that profile's own video, so a stale combo selection plus manual navigation can't
  apply a profile's compact preference to an unrelated video. The decision lives in the pure
  `PlaybackModePolicy.ResolveProfileOverride`.
- **Hold push/PR for the user.** Committing per the flow is in-scope; opening a PR is outward-facing.
- **(Mid-session user decision)** After Stage 1 landed, asked the user: stop at Stage 1, or continue
  into Stages 2-3 (shell + IFrame bridge are unit-testable here; only live behavior needs their QA)?
  User chose **continue to Stages 2-3** and **hold the PR**. So compact evolved from direct embed to
  **shell mode**: the local `player.html` + IFrame API + a host↔shell bridge. Task 6 (in-app
  embed-disabled→normal fallback) and a tuned shell CSP stay deferred to Stage 4 / live QA — the CSP
  must be enumerated against the real IFrame-API requests live before a restrictive policy can ship
  without breaking playback.

## Implementation

- New:
  - `src/PiPlay/Services/PlaybackModePolicy.cs` — durable null/normal/compact vocabulary (legacy
    `embed`→`compact`), `profile.Mode ?? global` precedence, 480×270 compact minimum, the mode→URL
    join (`BuildPopoutUrl`), and the profile-override video-id gate (`ResolveProfileOverride`).
  - `tests/PiPlay.Tests/PlaybackModePolicyTests.cs`.
  - (borderless commit) `BorderlessResizeHitTestPolicy.cs` + tests.
- Edited:
  - `MainWindow.xaml.cs` — resolve effective mode + launch via `BuildPopoutUrl`; `SettingsWindow`
    compact param; `ApplyPlayerPreferences` persists the global compact default.
  - `PlayerWindow.xaml.cs` — mode param, mode-specific minimum, launch-size clamp, and
    `PlacementMath.EnsureMinSize` raising a restored sub-minimum placement up to the floor.
  - `SettingsWindow.xaml(.cs)` — Settings → Playback "Compact player" toggle.
  - `Prompt.cs` — profile-editor playback-mode override (`BuildModePicker`).
  - `Models/Profile.cs`, `Models/AppSettings.cs`, `Services/SettingsService.cs` (mode sanitization),
    `Services/PlacementMath.cs` (`EnsureMinSize`).
  - Tests: `PlaybackModePolicyTests`, `PlacementMathTests`, `SettingsServiceTests`,
    `Ui/WpfRuntimeTests`, `Ui/XamlInvariantTests`.
  - Docs: `CHANGELOG.md`, `PiPlay_Product_Engineering_Spec.md`, `SPEC_GAPS_AND_OWNERSHIP.md`, the
    compact plan, the QA checklist (compact rows were already present from planning).
- New (Stages 2-3): `Services/PlayerShellProtocol.cs` (pure host↔shell contract),
  `Services/PlayerShellBridge.cs` (host bridge), `PlayerShell/player.html` + `player-shell.js`
  (local shell driving the YouTube IFrame API), and tests `PlayerShellProtocolTests` +
  `PlayerShellAssetTests`.
- Edited (Stages 2-3): `NavigationPolicy` (SSOT shell host + Player-only allowlist),
  `WebViewEnvironmentService` (virtual-host mapping, DenyCors), `YouTubeUrlHelper` (`BuildShellUrl`),
  `PlaybackModePolicy` (compact→shell URL, `UsesDomSyncTimer`), `PlayerWindow` (compact-shell wiring:
  map-before-navigate, bridge→return-state, DOM timer normal-only, bridge dispose),
  `MainWindow` (pass shell base), `PiPlay.csproj` (copy shell assets), and the matching tests/docs.

## Verification

- **Deterministic gate (local, matches CI):** `dotnet test PiPlay.sln --configuration Debug` =
  **330/330, 0 skipped** (Logic/Markup/Wpf lanes) at session end; `.\Build-PiPlay.ps1 -Stage Build
  -NoVersionBump -NoBuildNumberBump` = **0 warnings / 0 errors**. Baseline before this session was
  254/254 (→ 293 after Stage 1 → 330 after Stages 2-3).
- **Two adversarial review passes (workflow, 12 agents each), each finding independently verified:**
  - Stage 1 review (5 dimensions): 7 findings → 5 confirmed, 2 dismissed. Fixed: extracted the pure
    `BuildPopoutUrl` + `ResolveProfileOverride` seams with tests; made the restored-placement clamp
    true-by-construction via `EnsureMinSize` + tests; re-scoped a `REQ-PROFILE-01` citation.
  - Stage 2-3 review (6 dimensions incl. security + YouTube compliance): 6 findings → 5 confirmed,
    1 dismissed. Fixed: `DenyCors` least-privilege virtual host; protocol field-name single source of
    truth + a JS↔C# drift guard; a pure `UsesDomSyncTimer` predicate pinning the one-timestamp-source
    invariant; malformed-field parse coverage; a stale plan note. The security surface was confirmed
    sound (exact host match, no credential leakage, the bridge unreachable from the cross-origin
    YouTube iframe). Proactively removed an untested CSP that would likely have broken the live
    IFrame API (deferred to live-QA tuning).
- **Not run / deferred to release-candidate QA:** live compact playback, timestamp/state over the
  bridge, return/resume, playlists, restricted/embed-disabled handling, signed-in/out sessions, the
  shell↔IFrame-API path (origin/enablejsapi/DenyCors), and manual DPI resize smoke.

## Live smoke (compact shell, 2026-06-07, no account)

Ran the `run-piplay` driver against the public 19-second video `jNQXAC9IVRw` ("Me at the zoo") with
the global `PlayerSettings.CompactMode` forced on, a fresh Debug build, and `-KillExisting`. This is
verification-only — nothing already-committed changed. The previously-deferred "shell↔IFrame-API
path" smoke now has **direct live evidence** for the core compact path:

- **Mode threaded end to end.** Log: `Video Popout started at t=0s, wasPlaying=True, mode=Compact.`
  then `Popout Player initialized (mode=Compact).`
- **Shell loaded from `https://piplay.local/` and the IFrame API played the video.** The captured
  Popout Player window rendered the live YouTube video playing (letterboxed) — not a blank frame and
  not an externally-opened page. So `player.html → player-shell.js → iframe_api → new YT.Player`
  initialized and autoplayed off the virtual host.
- **Host↔shell bridge round-trip proven by the return timestamp.** On close the log reported
  `Popout Player closed; lastKnownSeconds=19`. Compact mode disables the DOM sync timer
  (`UsesDomSyncTimer(Compact) == false`), so the only possible source of that value is
  `PlayerShellBridge.StateReceived` — i.e. the shell posted `state` messages that the host received,
  parsed (`PlayerShellProtocol`), and wired into return state. `19` is the full video length (it
  played to the end).
- **Return/resume carried the bridge timestamp.** After the close, auto-popout restarted in compact
  mode at `t=19s` — the bridge-sourced timestamp survived the return round-trip.

Driver fix (incidental): `launch-and-capture.ps1` had an em-dash in its final `Write-Output` that
broke parsing under the prescribed `powershell.exe -STA` (Windows PowerShell 5.1 reads the
BOM-less UTF-8 file as ANSI, and the em-dash's `0x94` byte decodes to a curly quote that terminated
the string). Replaced with an ASCII hyphen.

**Still release-candidate QA (not exercised live):** signed-in/account-backed playback (this run was
an anonymous/consent-passed session), playlists, restricted/embed-disabled handling plus the Task 6
in-app fallback (Stage 4), explicit host→shell commands (play/pause/seek) under real use, Pin/Fade in
compact, manual DPI resize, and CSP tuning.

## Disposition

- Branch `feat/compact-player-stage1` (off `main`), working tree clean. **Not pushed; no PR opened**
  — held for the user (outward-facing). Open: PR scope (combined vs split borderless). Compact is
  implemented through **Stage 3** (shell + IFrame bridge); **Stage 4** (in-app embed-disabled→normal
  fallback + tuned CSP) and the live release QA remain.

## Commits

- (Stages 2-3) feat(player): compact player Stage 2-3 — local shell + YouTube IFrame-API bridge
- `c0982f1` docs(worklog): record the 2026-06-07 session (this file; updated to cover Stages 1-3)
- `0433095` feat(player): compact player mode — Stage 1 (policy + direct embed)
- `dceb0cf` feat(window): land borderless resize zones (REQ-WINDOW-02)
