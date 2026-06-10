# PiPlay - Spec gaps and ownership boundaries

**Status:** Working notes after Draft 0.10 compact-player sweep planning. Resolved items have been folded into `PiPlay_Product_Engineering_Spec.md`; keep this file for the remaining open decisions, ready implementation items, and ownership boundaries.

## Remaining open product decisions

| Item | Phase pressure | Current issue | Needed decision |
|---|---|---|---|
| Source Window after direct profile launch | Future/profile polish | Profiles can be launch targets, but the app has not decided whether direct launch can run without a visible Source Window. | Decide whether Source Window remains required, can start minimized/hidden, or becomes optional. |

## Recent implementation items

| Item | Ready status | Notes |
|---|---|---|
| Borderless resize zones | Implemented in working tree | Previous implementation was `WindowChrome.ResizeBorderThickness="6"` on both primary windows. `REQ-WINDOW-02` now uses 10 DIP edge resize zones plus 32 DIP corner length via native hit testing, without adding a visible size grip or touch-first 40 x 40 target. Design: `docs/superpowers/specs/2026-06-07-borderless-resize-zones-design.md`; plan: `docs/superpowers/plans/2026-06-07-borderless-resize-zones.md`. Manual DPI resize QA remains a release-candidate check. |
| Compact player sweep | Stages 1–3 implemented in working tree | Compact mode is implemented as **shell mode**: mode policy (`PlaybackModePolicy` — durable `null`/`normal`/`compact` vocabulary, global/profile precedence, 480×270 minimum, mode→URL join, profile-override video-id gate), global/profile placement UI, the local `player.html` shell served from the `https://piplay.local/` WebView2 virtual host (allowlisted on the Popout Player only), and the YouTube IFrame-API host↔shell bridge (`PlayerShellBridge`/`PlayerShellProtocol`). Verified deterministically (logic/markup/WPF + protocol/asset tests); **live** compact playback, return/resume, playlists, and restricted/embed-disabled handling remain release-candidate QA. **Stage 4 deferred:** the in-app embed-disabled error→normal fallback (Task 6) — Normal page mode remains the fallback meanwhile. The earlier Stage-1 direct embed is superseded; `BuildEmbedUrl` is kept as a reserved fallback tier. Design: `docs/superpowers/specs/2026-06-07-compact-player-sweep-design.md`; plan: `docs/superpowers/plans/2026-06-07-compact-player-sweep.md`. |

## Phase 2 landing status

| Item | Scope | CI impact |
|---|---|---|
| Release evidence | Phase 2 Stable build `v0.3.0` build `10` has automated release evidence in `docs/evidence/phase2-release-v0.3.0-b10.md`: deterministic tests, build gate, Stable publish/deploy, metadata validation, deployed UI smoke, and a UI Automation title check. Build 10 replaces the earlier build 9 Stable deploy and is built from the final Phase 2 landing commit. Account-backed/live YouTube rows in `docs/QA_Checklist.md` remain the release-candidate manual gate, not an implementation blocker. | No new CI gate needed; current deterministic tests cover schema, policy, XAML resources, and WPF construction seams. |
| Compact-mode placement | Resolved for Phase 3: global default plus optional profile override. | CI change starts with compact implementation. |

## Resolved in the spec cleanup

| Item | Resolution |
|---|---|
| MVP scope for `Auto`, `Fade`, and profiles | MVP = Pin + basic profile save/load + always-visible controls. Phase 2 = controls fade + Auto + profile edit/validation. Phase 4 = chrome fade + whole-window opacity. |
| Open questions vs accepted decisions | Section 25 is split into resolved defaults and open decisions. |
| Close/return behavior | Normative `REQ-RETURN-01`: resume only if the source was playing when Video Popout started. |
| External link handling | Normative Source Window allowlist + system-browser external policy; Popout Player never wanders off YouTube. |
| Timestamp tolerance | Functional gate now requires within 2 s, target ≤1 s, under warm-WebView test conditions. |
| Popout startup time | Warm-WebView performance target: video visible within about 1.5 s; cold first-run WebView2 init exempt. |
| Playlist fallback | Required cases: `watch?v=X&list=PL...`, `playlist?list=PL...`, and mix/restricted fallback to current video with non-blocking note. |
| Reset / browser data | `Reset app state` and `Clear browser data` are separate actions. |
| Profile state model | Profile > global per field; unset falls back to global; store both bounds and monitor identity. |
| Icon and brand asset source | `docs/assets/app-icon/` = app/taskbar/Video Popout icon family; `docs/assets/monogram-logo/` = monogram logo/favicon family. |
| Visual-identity verifiability (Draft 0.5) | Visual identity is now ID-backed: REQ-UI-01 (dark-theme completeness for all popup-bearing controls + tooltips) and REQ-UI-02 (icon-font contract, no `.notdef` glyphs), with a binary Chrome acceptance table (section 22.2) and a Definition of Done (section 22.5). Resolves the chrome deviations found in `Chrome_UI_Spec_Review.md`. |
| Privacy actions scope (Draft 0.5) | `Reset app state` / `Clear browser data` (REQ-PRIVACY-01/02) are **Phase 2**, consistent across sections 19, 23, and 24 and the QA checklist. Previously contradictory (normative + QA-tested but absent from MVP scope). |
| Source Window nav controls (Draft 0.5) | MVP minimal set fixed at Back, Reload, Home + URL/search field; Forward optional (section 5.5). |
| App icon canonical path | The shipped app references `src/PiPlay/Assets/piplay.ico` (self-contained source tree); `docs/assets/app-icon/piplay.ico` remains the reference copy. The duplicate root `docs/piplay.ico` has been removed. |
| Generated brand lockup snippet | The unlinked `docs/piplay_brand_lockup_and_usage.html` snippet was removed; canonical brand asset roles live in `PiPlay_Product_Engineering_Spec.md`. |
| `AGENTS.md` / `CHANGELOG.md` location | Decision: keep canonical copies under `docs/` intentionally; `AGENTS.md` already documents the path-prefix rule if moved to root. No duplicates to maintain. |
| Stable publish + channel/data isolation | A stable, runnable copy deploys to `E:\Dev_test_implemenations\PiPlay` via `scripts\Publish-Stable.ps1`. The release channel is baked into the binary (`PiPlayChannel`); a Stable copy uses portable data beside the exe, its own single-instance identity, and a `PiPlay — Stable …` title, while the Default channel is unchanged. Recorded in `adr/0007-stable-channel-and-portable-data.md`. |
| Auto trigger timing | **Playback-start**, `/watch`-only, **once per video** (id-keyed). `Auto` detects a watch video playing on the Source Window and reuses `StartVideoPopoutAsync`; an id de-dup blocks the return-resume re-pop loop, and Shorts/embeds are excluded. Off by default. Recorded in `docs/superpowers/specs/2026-06-06-auto-popout-design.md`. |
| Popout control customization first slice | Fixed swatches for Pin and Fade active colors plus controls-fade idle-delay presets. No hex picker, profile override, whole-window opacity UI, click-through, or transparent WebView2 behavior. Recorded in `docs/superpowers/specs/2026-06-06-player-customization-design.md`. |
| Compact-mode placement | Global player default (`PlayerSettings.CompactMode`, off by default) plus optional profile override (`Profile.Mode`: null/global, `normal`, `compact`; legacy `embed` normalizes to `compact`). Recorded in `docs/superpowers/specs/2026-06-07-compact-player-sweep-design.md`. |

## Documentation ownership

| Surface | Owns | Must not own |
|---|---|---|
| `PiPlay_Product_Engineering_Spec.md` | Product behavior, vocabulary, MVP scope, phase plan, normative requirements. | Historical drafts, unresolved brainstorms, or one-off implementation experiments. |
| `docs/adr/` | Durable architecture decisions and reversals. | Feature backlog or general product requirements. |
| `QA_Checklist.md` | Manual release checks mapped to spec requirements. | New requirements that are not in the spec. |
| `YouTube_Compliance.md` | Contributor policy for YouTube behavior and release compliance checks. | Legal advice or product features that loosen Q-5. |
| `Data_and_Privacy_Map.md` | Files written by PiPlay, data sensitivity, reset/uninstall behavior. | General architecture or UI workflow details. |
| `AGENTS.md` | Contributor and agent working rules derived from the spec and ADRs. | New source-of-truth product decisions. |
| `CHANGELOG.md` | User-visible changes and release notes. | Planning queue or unresolved work list beyond the next release notes. |

## Code ownership boundaries

| Owner | Owns | Must not own |
|---|---|---|
| `MainWindow` / Source Window | Main browsing surface, navigation controls, profile commands, starting Video Popout, Source Placeholder visibility, return coordination. | Low-level URL parsing, raw JavaScript snippets, or direct settings-file writes. |
| `PlayerWindow` / Popout Player | Floating playback window, chrome, pin/fade UI, resize/drag behavior, last-known timestamp polling, close signal. | Source Window navigation, profile persistence, or global app policy. |
| `WebViewEnvironmentService` | Shared `CoreWebView2Environment`, user-data folder, WebView2 initialization errors. | Navigation policy or YouTube page scripting. |
| `YouTubeUrlHelper` | Supported YouTube URL parsing and target URL construction. | UI decisions, WebView calls, or JavaScript execution. |
| `YouTubeDomBridge` | Centralized JavaScript for read time, pause, play, seek, and canonical URL reads. | Scattered feature logic, ad/compliance changes, or credential inspection. |
| `SettingsService` | Atomic load/save, corruption recovery, schema defaults. | Product policy for what settings mean. |
| `WindowPlacementService` | Bounds, DPI-aware restore, visible monitor clamping. | Profile management or window chrome behavior. |
| `LoggingService` | Local diagnostics with redaction rules. | Telemetry, analytics, or credential-bearing data. |

## Remaining cleanup candidates

| Candidate | Reason | Recommendation |
|---|---|---|
| `docs/assets/app-icon/` and `docs/assets/monogram-logo/` | Active icon families now live under purpose-named asset folders. | Resolved as docs/asset housekeeping; keep both roles separate. |
