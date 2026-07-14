# Popout interaction cohesion — design

## Goals

Close the owner-observed interaction gaps without redesigning an app that already looks sound and
basically works: returning a video must never immediately trigger Auto again; Popout appearance
settings must be reachable from the Popout Player; Fade should reclaim the empty top-bar row by
default in the shipped Normal mode; the existing active-opacity control should also affect the
Source Window title-bar background; and selecting a theme preset should make its identity visible
immediately.

The accent color/intensity curve is explicitly unchanged. In particular, full accent glyphs remain
reached at intensity 50 and the title wash continues to use the full 0-100 range.

## Requirements served

- Spec section 6.1 / section 25.1 Auto: once per video id; return-resume must not re-pop.
- Q-2 / REQ-RETURN-01: a return preserves the playback/window context instead of starting a new
  popout lifecycle.
- Q-6: recover predictably from YouTube SPA/canonical drift.
- Q-8 and spec sections 7.2/7.3: the Popout Player stays interactable while Fade and opacity are in use.
- REQ-UI-01 / REQ-UI-02: the added Popout control uses the dark shared chrome and has truthful UIA copy.
- Owner UI priorities P3, P6, and P8: surface opacity on the Source title bar, reclaim faded chrome,
  and make the Source/Popout appearance workflow feel like one application.

## Acceptance criteria

- Auto passes the already-resolved current Source video target into popout launch; a stale DOM
  canonical cannot silently substitute a different id.
- Every return arms Auto's handled-id latch before any awaited playback scripting. Same-video return,
  different-video return, and enabling Auto while a Popout is open all leave the returned/current
  Source video protected from immediate re-pop.
- The Popout Player has a Settings gear using the same glyph, tooltip, accessible name, and dialog as
  the Source Window. `PlayerWindow` raises a request; `MainWindow` continues to own persistence.
- Source and Popout requests cannot open two Settings windows. The first requester is the modal owner;
  a repeated request activates that existing dialog and raises it above either pinned PiPlay window.
- With Fade enabled and no explicit override, all three presets collapse the Popout top bar after the
  idle fade. A Normal-mode regression proves collapse, non-hit-testability, and activity restore.
- Moving the existing Active opacity slider changes both the whole active Popout and only the Source
  title-bar background. Source controls, browser content, and text remain fully opaque.
- Theme preset clicks preview palette, density, radii, accent derivation, behavior defaults, Popout
  opacity, and Source title-bar opacity immediately; cancel restores every persisted value.
- Sharp Dark, Minimal, and Soft Glass have clearly stepped opaque-to-glass behavior defaults and
  communicate those roles in the preset chooser. Defaults stay within the owner's 0-30% transparency
  direction.

## Settled decisions

1. Pass Auto's parsed `YouTubeTarget` into `StartVideoPopoutAsync` and make manual resolution prefer
   `CoreWebView2.Source` over DOM canonical. One current-window identity beats a second canonical-first
   read that can be stale during a YouTube SPA transition.
2. Re-arm Auto at the start of every return, not only `ReturnAction.Navigate`. The return boundary is
   the one place that can guarantee the video visible after transfer will not be interpreted as new.
3. Add one Settings gear to the Popout strip instead of inventing or rearranging overflow menus. It
   fixes reachability while preserving the direct Source/Popout controls that already work.
4. Keep Settings ownership and writes in `MainWindow`; `PlayerWindow` raises an event only. This holds
   the documented ownership boundary and avoids two persistence implementations.
5. Make auto-hide the preset default while retaining the existing explicit override. Fade then does
   what the owner expects by default, while a user can still reserve the strip row deliberately.
6. Extend the existing Active opacity value to the Source title-bar *background only*. This gives the
   main bar the requested opacity response without making WebView2, text, or input translucent and
   without reopening the deferred composition-host architecture.
7. Preview the real theme resources on preset click and strengthen only preset-owned behavior defaults.
   Immediate truthful feedback plus clearer opaque-to-glass steps is smaller and safer than a new
   fourth preset or a broad palette redesign.

## Non-goals / out of scope

- No true desktop transparency, transparent WebView2, click-through, or `AllowsTransparency=True`.
- No independent full-main-window opacity mode; the deferred P3 composition architecture remains open.
- No new ContextMenu/overflow system and no merging of Source/Popout Pin or window controls.
- No re-enable of Compact playback and no Normal-mode special-case in the fade state machine.
- No change to the approved accent gradient or intensity mapping.

## Testing approach

- Logic: source-vs-canonical target selection, preset defaults, resolver/writer/settings persistence.
- WPF: same-video return latch, next Auto decision, Popout Settings request event, guarded Settings
  launcher seams, Source title-bar opacity, full theme preview/revert, and Normal-mode strip collapse.
- Markup: Popout gear, tooltip/UIA/style, non-fading title backdrop, and preset chooser copy.
- Full deterministic gate, non-mutating build, spec preflight, and diff checks.
- Owner testing uses only a diagnostics-only Stable deploy at
  `E:\Dev_test_implemenations\PiPlay\PiPlay.exe`.

## Changes by file

| File | Change |
|---|---|
| `src/PiPlay/MainWindow.xaml(.cs)` | One target through Auto/launch, return latch, guarded shared Settings launcher, live theme preview/revert, Source title-bar opacity. |
| `src/PiPlay/PlayerWindow.xaml(.cs)` | Settings gear/request event and clearer Fade copy. |
| `src/PiPlay/Services/PopoutTargetResolver.cs` | Pure source-first target selection. |
| `src/PiPlay/SettingsWindow.xaml(.cs)` | Clearer preset cards and full-theme preview event. |
| `src/PiPlay/Theme/ThemeCatalog.cs` | Default auto-hide and stepped opacity defaults. |
| `tests/PiPlay.Tests/**` | Red-capable logic, markup, and WPF regressions. |
| `docs/CHANGELOG.md`, `docs/QA_Checklist.md`, current spec/ownership/theme docs | Record the owner-approved behavior and verification rows. |

## Docs & changelog impact

Update the user-facing changelog, Auto/appearance normative text, QA rows, ownership tracker, and the
generated-by-hand preset difference inventory. No ADR is needed because window/composition ownership
does not change.

## Unresolved decisions

- None for this pass. Full independent main-window transparency remains a separately tracked
  architecture decision and is not implied by Source title-bar background opacity.
