# Session worklog — borderless resize zones landing + compact player Stage 1 (2026-06-07)

Saved record of the working session that produced branch `feat/compact-player-stage1`
(commits `dceb0cf` borderless, `0433095` compact Stage 1).

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

## Verification

- **Deterministic gate (local, matches CI):** `dotnet test PiPlay.sln --configuration Debug` =
  **293/293, 0 skipped** (Logic/Markup/Wpf lanes); `.\Build-PiPlay.ps1 -Stage Build -NoVersionBump
  -NoBuildNumberBump` = **0 warnings / 0 errors**. Baseline before this session was 254/254.
- **Adversarial review (workflow, 12 agents):** five dimensions (correctness, YouTube
  compliance/privacy, ownership/terminology, coverage, docs accuracy), each finding independently
  verified. 7 findings → 5 confirmed real+material, 2 dismissed. All 5 fixed: extracted the pure
  `BuildPopoutUrl` and `ResolveProfileOverride` seams with tests (closed two untested-behavior
  gaps); made the restored-placement clamp true-by-construction via `EnsureMinSize` + unit tests
  (removed a comment/acceptance-criterion over-claim); re-scoped a `REQ-PROFILE-01` citation.
- **Not run / deferred to release-candidate QA:** live compact playback, return/resume, playlist
  behavior, the embed-page DOM read, manual DPI resize smoke. These remain the manual gate.

## Disposition

- Branch `feat/compact-player-stage1` (off `main`), two commits, working tree clean, 31 files vs
  `main`. **Not pushed; no PR opened** — held for the user (outward-facing). PR scope (combined vs
  split borderless) and whether to continue into Stage 2–3 are open user decisions.

## Commits

- `0433095` feat(player): compact player mode — Stage 1 (policy + direct embed)
- `dceb0cf` feat(window): land borderless resize zones (REQ-WINDOW-02)
