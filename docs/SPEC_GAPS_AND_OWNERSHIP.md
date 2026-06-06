# PiPlay - Spec gaps and ownership boundaries

**Status:** Working notes after Draft 0.5 cleanup. Resolved items have been folded into `PiPlay_Product_Engineering_Spec.md`; keep this file for the remaining open decisions and ownership boundaries.

## Remaining open product decisions

| Item | Current issue | Needed decision |
|---|---|---|
| Compact-mode placement | Profile precedence is defined, but compact mode itself is not approved for MVP and its setting surface remains undecided. | Decide whether compact mode is global only, profile override, or both before broad exposure. |
| Source Window after direct profile launch | Profiles can be launch targets, but the app has not decided whether direct launch can run without a visible Source Window. | Decide whether Source Window remains required, can start minimized/hidden, or becomes optional. |

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
| Icon and brand asset source | `docs/files (2)/` = app/taskbar/Video Popout icon family; `docs/files/` = monogram logo/favicon family. |
| Visual-identity verifiability (Draft 0.5) | Visual identity is now ID-backed: REQ-UI-01 (dark-theme completeness for all popup-bearing controls + tooltips) and REQ-UI-02 (icon-font contract, no `.notdef` glyphs), with a binary Chrome acceptance table (section 22.2) and a Definition of Done (section 22.5). Resolves the chrome deviations found in `Chrome_UI_Spec_Review.md`. |
| Privacy actions scope (Draft 0.5) | `Reset app state` / `Clear browser data` (REQ-PRIVACY-01/02) are **Phase 2**, consistent across sections 19, 23, and 24 and the QA checklist. Previously contradictory (normative + QA-tested but absent from MVP scope). |
| Source Window nav controls (Draft 0.5) | MVP minimal set fixed at Back, Reload, Home + URL/search field; Forward optional (section 5.5). |
| App icon canonical path | The shipped app references `src/PiPlay/Assets/piplay.ico` (self-contained source tree); `docs/files (2)/piplay.ico` remains the reference copy. The duplicate root `docs/piplay.ico` has been removed. |
| Generated brand lockup snippet | The unlinked `docs/piplay_brand_lockup_and_usage.html` snippet was removed; canonical brand asset roles live in `PiPlay_Product_Engineering_Spec.md`. |
| `AGENTS.md` / `CHANGELOG.md` location | Decision: keep canonical copies under `docs/` intentionally; `AGENTS.md` already documents the path-prefix rule if moved to root. No duplicates to maintain. |
| Stable publish + channel/data isolation | A stable, runnable copy deploys to `E:\Dev_test_implemenations\PiPlay` via `scripts\Publish-Stable.ps1`. The release channel is baked into the binary (`PiPlayChannel`); a Stable copy uses portable data beside the exe, its own single-instance identity, and a `PiPlay — Stable …` title, while the Default channel is unchanged. Recorded in `adr/0007-stable-channel-and-portable-data.md`. |
| Auto trigger timing | **Playback-start**, `/watch`-only, **once per video** (id-keyed). `Auto` detects a watch video playing on the Source Window and reuses `StartVideoPopoutAsync`; an id de-dup blocks the return-resume re-pop loop, and Shorts/embeds are excluded. Off by default. Recorded in `docs/superpowers/specs/2026-06-06-auto-popout-design.md`. |

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
| `docs/files/` and `docs/files (2)/` | Two active icon families with unclear folder names. | Cosmetic only. Keep both roles; optionally rename to canonical paths such as `assets/app-icon/` and `assets/logo-monogram/`. |
