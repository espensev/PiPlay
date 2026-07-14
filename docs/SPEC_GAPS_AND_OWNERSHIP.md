# PiPlay - Spec gaps and ownership boundaries

**Status:** Working notes after the 2026-06-25 P1/spec-sync review. Resolved items have been folded into `PiPlay_Product_Engineering_Spec.md`; keep this file for the remaining open decisions, ready implementation items, and ownership boundaries.

## ⚠️ Open bugs (owner-reported — must not drop)

| Bug | Reported | Status | Detail |
|---|---|---|---|
| **Double-audio: source "base" keeps playing after popout** | 2026-06-25 (pre-existing, not a v0.6.0 regression) | NEEDS STABLE SMOKE — **fix released in v0.8.0 (b29), so the smoke is now runnable** | The source is muted+paused at popout launch and guarded by a ~1 Hz reassertion timer while the popout owns playback (`MainWindow.xaml.cs`, `YouTubeDomBridge.SuppressPlaybackAsync`). **Until b29 this fix existed only on an unmerged branch, so every deployed Stable the owner tested still had the bug and the smoke was impossible to run.** It is now in the deployed copy and still unconfirmed. Reassertion is periodic, so watch for a *brief* leak at the moment of an ad / autoplay-next / SPA re-render — not merely "audio stops on popout." Worst historical case: mix/radio sources (`start_radio=1`). |

## Current-ways review note (2026-06-26, test count refreshed 2026-07-14)

The current local stack is deterministic-test green (`dotnet test PiPlay.sln -c Debug` = 769/769 as of
2026-07-14 on the accent-reach follow-up working tree — re-run for the current count rather than trusting
this number), but
that is a headless contract gate, not runtime signoff. Do not RC-tag from the green suite alone. The next
release evidence must come from the deployed Stable WebView2/YouTube smoke rows: duplicate-audio
suppression, immediate popout close/unmute, paused-source launch, different-video replay, ads/autoplay-next,
and SPA re-render transitions.

The current visual path is also an intentional compromise, not the final owner target: DWM fixed corner
modes, a 4 DIP resize band, whole-popout opacity (with the active value also painting only the Source
title-bar backdrop), and dormant compact-player plumbing. All three presets now default top-bar auto-hide
on; active/idle opacity is Sharp Dark `1.00/1.00`, Minimal `0.94/0.86`, and Soft Glass `0.82/0.72`.
A large rounded floating-card silhouette, chrome-only transparency, or Browse/Cinema/Compact main-window
modes need separate design/ADR work instead of being folded into the current release cleanup.

## ✅ CLOSED: does a profile's color become the app accent? (P2)

**Resolved 2026-07-14 — YES.** The owner confirmed: a profile's color drives the app accent. The v0.6.0
identity-only split is reversed, and the contradiction between the spec and roadmap P2 is gone (both
updated).

The decision hinged on *why* P2 had looked pointless: the accent reached almost nothing. In the normal
window it painted the **Pop-out button** and a title-bar wash tuned to a 1.20:1 contrast target — close to
imperceptible. Everything else was conditional (Pin/Auto when on), transient (caret, focus ring), or hidden
(Settings, error states). Owner: *"that would be ok, BUT it just changed one button."* So the accent was
given reach **first** — the functional toolbar row now carries it and the wash was raised to 1.45:1 — and
only then was it wired to the profile. Shipped together.

See `docs/superpowers/specs/2026-07-14-profile-accent-reach-design.md` for the shipped decision and
`docs/superpowers/specs/2026-07-14-accent-reach-default-and-routing-fixes-design.md` for the follow-up
preference. **The accent's reach is owner-tunable by eye**: default 50 reproduces v0.9.0, lower values
fade glyphs and wash, and higher values deepen only the wash. QA rows **UI-CHK-9–11** govern it.

## Remaining open product decisions

| Item | Phase pressure | Current issue | Needed decision |
|---|---|---|---|
| Source Window after direct profile launch | Future/profile polish | Profiles can be launch targets, but the app has not decided whether direct launch can run without a visible Source Window. | Decide whether Source Window remains required, can start minimized/hidden, or becomes optional. |
| Fresh DOM capture on popout X-close | Return fidelity | Closing the popout with **X** returns the last *timer-sampled* state, not a fresh read: `CaptureCurrentPlaybackStateAsync` runs only on the Bring-video-back path (`CaptureReturnStateNowAsync`). So an X-close can return a timestamp up to one poll interval stale. | Decide whether to add an async close-deferral that takes a fresh DOM read before the popout tears down, or accept the sampled value. |
| Return target loses playlist context | Return fidelity | `PlayerReturnState` carries only `VideoId`. A different-video return rebuilds a plain watch URL, so playlist/mix position (list + index) can be dropped on the way back to the Source Window. | Decide whether the return state should carry list/index context. |
| Pure-function seam for playback scripting | Test coverage | The playback-settings script build and player-state parse are still inline, so there is no locale-regression test pinning `ToString("R", InvariantCulture)` for the volume/rate values crossing into JS. | Decide whether to extract `BuildPlaybackSettingsScript` / `ParsePlayerState` as pure functions and pin them. |

## Deferred requirements

| Requirement | Status | Why | What would revive it |
|---|---|---|---|
| **REQ-RELEASE-01** (code signing) | **Deferred — not a release gate** | The owner signs locally with a **self-signed** certificate, which proves nothing that the commit hash does not already prove. Release provenance is the exact-source commit, the `stable-vX.Y.Z-bN` tag, and `Verify-StableDeploy.ps1`. v0.8.0-b29 released unsigned. | Public distribution under a real (non-self-signed) certificate, where SmartScreen reputation and third-party provenance actually matter. **Until then, do not add an Authenticode check to the release-evidence path.** |

## 2026-06-23 owner appearance / popout / compact review (historical input)

Source: owner review captured and folded into this section before the raw review artifact was pruned.
Ground-truthed against the code on
2026-06-23 with four read-only investigations (compact mode; popout button + placeholder; corner
silhouette; accent + transparency). **Runtime QA was not performed this pass** — the findings below are
verified-vs-code / observed-behavior, not a sign-off that anything renders or behaves correctly when
run. Items the owner reported as observed behavior stay flagged for a deployed-Stable runtime check.

> **Supersession note (2026-07-14):** this section preserves what the owner said on June 23, but its
> identity-only profile-color direction was explicitly reversed by the later P2 decision recorded above.
> Current implementation work must follow the closed P2 section and product spec §17. The remaining
> corner/transparency/layout directions below are not superseded by that accent decision.

### Organizing frame at the time (owner, review §9)

The June 23 split put an optional identity-only color under **Profiles** and the global accent under
**Appearance**. That accent boundary is historical: the July 14 P2 decision now makes a profile color
drive the app while retaining `theme.accentColor` as fallback. The separate **Popout state** boundary
(whether playback is detached, where it is, and return/focus actions) remains useful.

### Owner-directed intent as captured then

These were directives at capture time. Later explicit decisions supersede individual rows where noted;
the record remains here so the change in direction is auditable.

| Ref | Owner direction |
|---|---|
| 2.1 / 5 | Theme presets must be visibly distinct; add a 4th **Blackout** preset (0% transparency). |
| 2.2 / §9 | **SUPERSEDED 2026-07-14 by P2.** This row originally made the global accent exclusive and profile color identity-only. Current behavior: an active profile color drives the app accent; `theme.accentColor` is the fallback. |
| 2.3 | The user may pick **any** accent color; the app auto-picks readable foreground text (black/white by contrast); border opacity/strength is a separate, independent control. Do not reject useful colors for being poor text backgrounds. |
| 2.4 / 7 | Corner radius must change the **actual popout silhouette** (outer window + video clipping): Round = a large rounded floating card, with border and shadow following the rounded shape and no square backing layer. |
| 3.1 / 8 | The main-window placeholder should offer a direct action, not static text only. |
| P1 | Reduce default transparency; make it a controlled effect, not the core look (owner's suggested 0–30% band). |
| 4 / 6 | Provide explicit app modes — **Browse / Cinema / Compact / Popout** — where Compact = a small, player-first main-window layout with hover-reveal chrome. |

### Already present in code (clarify before re-litigating)

Code inspection found the mechanism for several P0 items. Code-presence is **not** a runtime sign-off;
where the owner reported observed behavior, that signal stands until a runtime check says otherwise.

| Owner claim | Code finding (file:line) | Status |
|---|---|---|
| 3.2 "Show popout" does not bring the popout back; duplicate risk | Toolbar button now flips label/tooltip/UIA name `Pop out video` ↔ `Bring video back`; the placeholder action uses the same `BringVideoBackAsync` path. The command captures fresh popout return state, closes the popout, and drives `ApplyReturnActionAsync`. Same-video return preserves paused/volume/mute/speed where the YouTube DOM allows it; different-video return navigates to the returned video/timestamp and replays paused/volume/mute/speed after the source video element is ready. | **Released in v0.8.0 (b29)** and deployed to Stable. Task 3b's post-navigation replay is covered headlessly, including no-sample fallback to launch playback settings. Manual QA still needs real-page WebView2/YouTube verification against the deployed copy. |
| 4 "Compact mode does not work" | "Compact" is no longer a user-facing popout toggle: the Settings control was removed, and `PlaybackModePolicy.CompactPlayerEnabled=false` forces new popouts to Normal while the compact plumbing remains dormant. It never changed the main-window layout. | **Terminology mismatch** — the owner's main-window "Compact Mode" is net-new and does not exist; the dormant popout compact plumbing is a different axis and should not be treated as the requested main-window mode. |
| P1 "reduce transparency by default" | The current preset defaults are Sharp Dark `1.00/1.00`, Minimal `0.94/0.86`, and Soft Glass `0.82/0.72` active/idle. Active opacity also paints the Source title-bar backdrop; the Popout remains whole-window alpha. | Superseded by the 2026-07-14 preset-cohesion values. The unresolved architectural question is still whether a future transparency system should separate chrome/background from video. |
| 2.3 "auto-pick readable foreground" | `ThemeColors.PickReadableForeground` picks the best dark/white foreground, and the 2026-06-25 follow-up accepts any valid `#RRGGBB` accent/profile color. | Text foreground is now separated from the old hard reject; border/glow strength remains a future control axis. |

### Terminology to disambiguate (settled fact)

"Mode" is overloaded. Spec §10 **playback modes** (Normal page / Compact embed / Shell) describe how the
*Popout Player* plays video. The review's **Browse / Cinema / Compact / Popout** are *UX/layout* modes
of the *main window* and do not exist in code. Keep the two axes separate in all future copy; do not
call a main-window layout "Compact" unqualified, since "Compact player" already means the popout
playback surface.

### Open sub-decisions (need owner / architecture sign-off)

| Item | The decision | Why it is open |
|---|---|---|
| Corner silhouette architecture | **Soft/round de-duplicated (2026-06):** the redundant **Soft** corner option was dropped (it shared `DWMWCP_ROUND` with **Round**); a stored `"soft"` now normalizes to `"round"`. Remaining open: accept the DWM limit (three fixed OS radii), or lift WebView2 airspace (windowless/composition hosting) to get a large card radius with an outer border + shadow following the curve. | Outer corners are DWM-only: three fixed OS radii, no large radius, no outer border/shadow, because the windows host WebView2 by HWND with `AllowsTransparency=False` (airspace). The `RadiusPopoutFrame`/`RadiusMainWindowFrame` tokens are **unconsumed** — wire or remove them. An ADR is likely warranted if airspace is lifted. |
| Transparency band | Current UI calls the Popout feature whole-popout opacity. The active setting also reaches the Source title-bar backdrop only; idle has no Source effect. A true transparency feature still needs scope/target decisions: Off / Main / Popout / Both, and chrome/background only by default with video opaque. | Architecture/UX decision remains open because the current Popout layered-window alpha fades the WebView/video too. The Source backdrop treatment does not solve that airspace limitation. |
| Main-window mode model | Whether to build Browse/Cinema/Compact main-window layouts (toolbar collapse, hover-reveal chrome), how they persist (global vs per-profile), and naming to avoid the "Compact" collision. | Net-new feature; no main-window mode state machine exists today. |
| "Restore video here" / "Bring video back" | The implemented command returns playback to the main window by capturing fresh popout state and closing the single popout through the existing return pipeline. | If the owner later wants a separate focus-only command or a non-closing duplicate surface, that is a new ADR-0005/single-player decision, not the P4 bring-back fix. |

Owner priorities, verbatim: **P0** — main-window compact mode; "Show popout" restore/focus; no
duplicate/unreachable popout. **P1** — corner silhouette; accent/profile split; reduce default
transparency; distinct theme presets. **P2** — border mode + strength; shadow strength; hover-reveal
chrome; separate "Restore video here". Re-read against the ground-truth above: the P0 "compact" is
net-new work, not a fix; `Bring video back` now covers the source-window return action by closing the
single popout through the existing return pipeline.

## Recent implementation items

| Item | Ready status | Notes |
|---|---|---|
| Profile-driven accent (P2) | Implemented on main | The 2026-07-14 owner decision reverses the v0.6.0 identity-only split: a valid active profile color drives the app, while `theme.accentColor` remains the preserved fallback. The functional toolbar, primary action, and title wash carry the resolved accent; the selector rail remains a contrast-safe identity cue. The reach preference defaults to the exact v0.9.0 look. Optional active-profile popout border remains future work. |
| Popout interaction + preset cohesion | Implemented on working tree | The Popout has a Settings gear routed to MainWindow's shared single-dialog workflow. All presets default top-bar auto-hide on; Sharp/Minimal/Soft use `1.00/1.00`, `0.94/0.86`, and `0.82/0.72`. Active opacity also paints the Source title-bar backdrop. Preset/corner choices preview across all open surfaces and non-affirmative dismissal fully reverts. Settings cards name the intended looks `Crisp · 100%`, `Quiet · 94%`, and `Glass · 82%`. |
| Auto target identity + return latch | Implemented on working tree | Auto and manual launch share one Source-first resolved target; canonical is fallback only. Every return arms the de-dup latch with the identity restored to Source before navigation/resume, preventing an immediate re-pop without blocking a later different playing video. |
| Accent gate relax + filled accent actions | Implemented on main | Any valid `#RRGGBB` accent/profile color is accepted; invalid hex is still blocked/defaulted. `AccentButton` now uses accent fill tokens with generated dark/white foreground instead of an outline-only treatment. |
| Borderless resize zones | Implemented on main | Previous implementations used `WindowChrome.ResizeBorderThickness="6"` and then a 10 DIP inset. The current v0.7.2 P1 implementation uses a 4 DIP black edge resize band (`BorderlessResizeHitTestPolicy.ResizeBorderDip`) plus 32 DIP corner length via native hit testing, without adding a visible size grip or touch-first 40 x 40 target. Manual DPI resize QA remains a release-candidate check. |
| Compact player plumbing | Dormant on main | Stages 1–4 shipped earlier, but the 2026-06-25 popout-look cleanup removed the Settings Compact toggle and set `PlaybackModePolicy.CompactPlayerEnabled=false`, so new popouts always resolve to Normal. `PlaybackMode.Compact`, `PlaybackModePolicy`, the player shell/IFrame assets, `PlayerSettings.CompactMode`, and `Profile.Mode` are retained as reserved/dormant plumbing, not a release-facing mode. **2026-06-26:** the per-profile Edit-profile playback-mode picker also hides the dead "Compact player" option while `CompactPlayerEnabled=false` (a stored `compact`/`embed` profile falls back to Use-global), closing the gap where Settings dropped the toggle but the profile editor still offered it. Compact manual QA is only a release gate if compact is deliberately re-enabled. |
| Return resume rule (REQ-RETURN-01) | Implemented on main | Option A is now the normative rule: the popout's live paused/playing state wins when known, and the source launch state is fallback only. A paused source no longer gets auto-nudged into playing unless it was playing at launch; if the user presses play in the popout, return follows that live state. |

## Phase 2 landing status

| Item | Scope | CI impact |
|---|---|---|
| Release evidence | Phase 2 Stable build `v0.3.0` build `10` completed deterministic tests, build gate, Stable publish/deploy, metadata validation, deployed UI smoke, and a UI Automation title check. Build 10 replaces the earlier build 9 Stable deploy and is built from the final Phase 2 landing commit. Account-backed/live YouTube rows in `docs/QA_Checklist.md` remain the release-candidate manual gate, not an implementation blocker. Historical screenshots were pruned after this summary was retained. | No new CI gate needed; current deterministic tests cover schema, policy, XAML resources, and WPF construction seams. |
| Compact-mode placement | Reserved/dormant data model: global default plus optional profile override exists for migration/future re-enable, but new popouts force Normal while `CompactPlayerEnabled=false`. | CI should keep the kill-switch invariant green; full compact QA only returns if the mode is re-enabled. |

## Resolved in the spec cleanup

| Item | Resolution |
|---|---|
| MVP scope for `Auto`, `Fade`, and profiles | MVP = Pin + basic profile save/load + always-visible controls. Phase 2 = controls fade + Auto + profile edit/validation. Phase 4 = chrome fade + whole-popout opacity. |
| Open questions vs accepted decisions | Section 25 is split into resolved defaults and open decisions. |
| Close/return behavior | Normative `REQ-RETURN-01`: return follows the Popout Player's live paused/playing state when known; source-was-playing-at-launch is fallback only. |
| External link handling | Normative Source Window allowlist + system-browser external policy; Popout Player never wanders off YouTube. |
| Timestamp tolerance | Functional gate now requires within 2 s, target ≤1 s, under warm-WebView test conditions. |
| Popout startup time | Warm-WebView performance target: video visible within about 1.5 s; cold first-run WebView2 init exempt. |
| Playlist fallback | Required cases: `watch?v=X&list=PL...`, `playlist?list=PL...`, and mix/restricted fallback to current video with non-blocking note. |
| Reset / browser data | `Reset app state` and `Clear browser data` are separate actions. |
| Profile state model | Profile > global per field; unset falls back to global; store both bounds and monitor identity. |
| Icon and brand asset source | `docs/assets/app-icon/` = app/taskbar/Video Popout icon family; `docs/assets/monogram-logo/` = monogram logo/favicon family. |
| Visual-identity verifiability (Draft 0.5) | Visual identity is now ID-backed: REQ-UI-01 (dark-theme completeness for all popup-bearing controls + tooltips) and REQ-UI-02 (icon-font contract, no `.notdef` glyphs), with a binary Chrome acceptance table (section 22.2) and a Definition of Done (section 22.5). Resolves the May 2026 chrome deviations now covered by the screenshot procedure and regression suite. |
| Privacy actions scope (Draft 0.5) | `Reset app state` / `Clear browser data` (REQ-PRIVACY-01/02) are **Phase 2**, consistent across sections 19, 23, and 24 and the QA checklist. Previously contradictory (normative + QA-tested but absent from MVP scope). |
| Source Window nav controls (Draft 0.5) | MVP minimal set fixed at Back, Reload, Home + URL/search field; Forward optional (section 5.5). |
| App icon canonical path | The shipped app references `src/PiPlay/Assets/piplay.ico` (self-contained source tree); `docs/assets/app-icon/piplay.ico` remains the reference copy. The duplicate root `docs/piplay.ico` has been removed. |
| Generated brand lockup snippet | The unlinked `docs/piplay_brand_lockup_and_usage.html` snippet was removed; canonical brand asset roles live in `PiPlay_Product_Engineering_Spec.md`. |
| `AGENTS.md` / `CHANGELOG.md` location | Decision: keep canonical copies under `docs/` intentionally; `AGENTS.md` already documents the path-prefix rule if moved to root. No duplicates to maintain. |
| Stable publish + channel/data isolation | A stable, runnable copy deploys to `E:\Dev_test_implemenations\PiPlay` via `scripts\Publish-Stable.ps1`. The release channel is baked into the binary (`PiPlayChannel`); a Stable copy uses portable data beside the exe, its own single-instance identity, and a `PiPlay — Stable …` title, while the Default channel is unchanged. Recorded in `adr/0007-stable-channel-and-portable-data.md`. |
| Auto trigger timing | **Playback-start**, `/watch`-only, with one Source-first target carried from detection into `StartVideoPopoutAsync`. Every return arms the id de-dup before navigation/resume; Shorts/embeds are excluded. Off by default. |
| Popout control customization first slice (historical Phase 2 boundary) | The initial slice was fixed swatches for Pin/Fade plus delay presets. Later releases superseded its "no opacity UI" boundary with whole-popout opacity, preset-owned auto-hide/opacity defaults, and the shared Settings path; click-through and transparent WebView2 behavior remain out of scope. |
| Compact-mode placement | Reserved global player default (`PlayerSettings.CompactMode`, off by default) plus optional profile override (`Profile.Mode`: null/global, `normal`, `compact`; legacy `embed` normalizes to `compact`). New popouts currently ignore these values and force Normal while `CompactPlayerEnabled=false`. |

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
| `PlayerWindow` / Popout Player | Floating playback window, chrome, pin/fade/settings UI, resize/drag behavior, last-known timestamp polling, close signal. It may raise `SettingsRequested`; MainWindow owns/activates the shared dialog. | Source Window navigation, profile persistence, the Settings transaction, or global app policy. |
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
