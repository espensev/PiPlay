# Design — compact overlay controls + whole-window opacity (2026-06-10)

**Status:** designed, not implemented. Staged plan in
`docs/superpowers/plans/2026-06-10-popout-overlay-and-opacity.md`.
**Builds on:** the compact shell (Stages 2-3) and the Stage 4 error/fallback path
(`docs/superpowers/specs/2026-06-10-compact-stage4-fallback-design.md`).

## Goal

Make the Popout Player able to look like a sleek floating mini-player (user-supplied reference
screenshots, 2026-06-10): a chromeless rounded window where the video fills the frame, transport
controls (prev / play-pause / next, progress bar, volume, captions, close, fullscreen) are drawn
*on* the video and auto-hide, and — optionally, selectable — the whole window is translucent so the
desktop shows through.

Two user decisions (2026-06-10, recorded verbatim from the option dialog):

1. **Controls: "Custom overlay controls"** — PiPlay-drawn HTML controls inside the compact shell
   over a chromeless IFrame player, matching the screenshots. Compact mode only. Chosen over
   "YouTube's own controls" with the compliance caveat visible in the option text.
2. **Transparency: "Both, separate settings"** — an idle opacity *and* a constant opacity, as
   independent settings. The user's second screenshot is the fully opaque variant ("or not
   transparent"), so 100% must remain a first-class look.

## Requirements (existing spec anchors)

- Spec **§7 (Fade, opacity, and transparency policy)** — the controlling section. §7.3
  whole-window opacity: the entire Popout Player may fade to a configured opacity when idle; hover
  restores; "must preserve normal mouse interaction". §7.5 hard non-goal: **no click-through** —
  `WS_EX_TRANSPARENT`, transparent hit-testing, and pointer pass-through are forbidden (also
  ADR-0006).
- Spec **Phase 4 gate** ("Enable whole-window opacity → Player remains interactable; clicks do not
  pass through") and **Q-8**.
- QA checklist §3: opacity cannot drop below the **45% normal floor without an explicit unlock**.
- Spec §26-ish implementation note: "Whole-window opacity may use normal window opacity if it works
  reliably with WebView2, **but it must be tested for input, rendering, and performance**" — this
  mandates the spike stage below.
- Spec WebView note: "A later version may switch to `WebView2CompositionControl` if we need
  translucent overlays or WPF UI layered over the WebView" — kept as the fallback tier if the
  layered-window spike fails.
- Existing plumbing: `PlayerSettings.IdleWindowOpacity` (default 1.0) + its `SettingsService`
  sanitization (reset outside 0.1–1.0) already exist and are consumed by nothing — reserved for
  exactly this feature.

## The YouTube-compliance decision (settled)

YouTube's API Services "Required Minimum Functionality" guidance prohibits displaying overlays in
front of any part of an embedded player. PiPlay's own bar so far (Q-5, QA §3.5) has been "compact
mode keeps YouTube controls/branding visible". Custom overlay controls deviate from the strictest
reading of both.

**Settled (user decision, option text carried the caveat):** implement overlay controls, with these
mitigations, recorded as a deliberate, personal-use deviation:

- Overlay controls are **hover/paused-revealed and auto-hide during playback** — the playing video
  is unobscured in steady state (same reveal model as YouTube's own controls).
- The chromeless player (`controls=0`) is an **officially supported IFrame API parameter**; the
  YouTube watermark/branding the chromeless player itself renders is never covered by pinned UI.
- The overlay look is a **selectable appearance** (default **off**): the shipped default remains
  the current compliant look with YouTube's own controls. Q-5's QA row applies to the default look;
  a new QA row covers the overlay look.
- No ad-blocking, no download, no background-audio circumvention — unchanged hard non-goals.

## Settled design decisions

1. **Overlay controls live inside the shell (`player.html`), not in WPF.** WPF cannot render over
   the WebView2 surface (airspace — same constraint that shaped the Stage 4 error bar). Inside the
   shell it's all DOM: absolutely-positioned HTML over the IFrame player, no airspace, themeable
   with the existing accent vocabulary. The shell already owns the `YT.Player` object, so
   prev/next (`previousVideo`/`nextVideo`), play/pause, seek/progress (`getCurrentTime` /
   `getDuration` / `seekTo`), volume (`setVolume`/`mute`), and captions are **shell-local** — no
   host round-trip, no protocol change for transport.
2. **Window-level actions go shell→host over the existing versioned protocol.** New outbound kind
   `request` with an allowlisted `action` field: `close`, `pinToggle`, `fullscreenToggle`. The host
   validates the action against the allowlist and maps to the existing native handlers (Close,
   Pin, maximize/restore). Protocol version bumps; the JS↔C# field-name drift guard extends to the
   new kind.
3. **The native chrome strip stays, as the hover escape hatch.** In overlay look the strip
   auto-hides fully (height-collapses on idle, reveals on hover near the top edge). It remains the
   native recovery path — close/pin must never depend on a JavaScript layer that can be broken
   (the entire Stage 4 rationale). The Stage 4 **error bar is unchanged**: it is native, appears on
   shell failure regardless of look, and its fallback action still works when the shell is dead.
4. **Whole-window opacity = layered window alpha on the top-level HWND.**
   `WS_EX_LAYERED` + `SetLayeredWindowAttributes(LWA_ALPHA)` applies uniform alpha to the entire
   window including the WebView2 child HWND (Win8+). `WS_EX_TRANSPARENT` is **never** set; input
   behaves normally at every opacity (§7.5 / ADR-0006 / Q-8). WPF `AllowsTransparency` is **not**
   used (it does not affect HwndHost children and degrades rendering).
   **This is the highest-risk unknown** — WebView2 rendering under a layered parent must be
   live-verified before any UI is built (spike S-1). If it fails: fallback is the
   `WebView2CompositionControl` tier (bigger, separate design) and the opacity feature gates on it.
5. **Two opacities, one pure policy.** `WindowOpacityPolicy` (house pure-seam pattern):
   `Effective(bool isIdle, double constant, double idle)` → idle ? idle : constant; clamping; the
   45% UI floor vs the 10% file floor; and the animation rule (reuse `FadePolicy.FadeDurationMs`;
   idle-detection comes from the same idle source as the controls fade so there is **one idleness
   definition**). Settings: `player.idleWindowOpacity` (existing) + new
   `player.constantWindowOpacity`, both default 1.0, both sanitized to 0.1–1.0.
   **Explicit unlock (settled):** Settings UI sliders stop at 45%; values 10–45% are honored only
   when hand-edited into `settings.json` — the manual edit *is* the explicit unlock for now. A UI
   unlock can come later without a model change.
6. **Rounded corners via DWM, not WPF clipping.** `DwmSetWindowAttribute(DWMWA_WINDOW_CORNER_PREFERENCE
   = DWMWCP_ROUND)` on the Popout Player — composited by DWM so it costs nothing, clips the
   WebView2 child correctly, and no-ops gracefully on Windows 10. WPF `CornerRadius` clipping
   cannot clip an HwndHost (airspace again).
7. **Dragging in the overlay look: CSS `app-region: drag` (spike-gated).** WebView2's non-client
   region support (`CoreWebView2Settings.IsNonClientRegionSupportEnabled` + CSS `app-region: drag`)
   lets a shell-defined region initiate native window drag. Runtime/SDK support must be verified
   live (spike S-2). Fallback if unsupported: the auto-hiding native strip remains the drag handle
   (current behavior) — the look survives, only "drag anywhere on the video" degrades.
8. **Scope split: opacity is mode-agnostic; the overlay look is compact-only.** Translucency,
   rounded corners, and strip auto-hide are window-level and apply to both popout modes. Overlay
   controls require the shell, which only exists in compact mode (user decision). Normal page mode
   keeps the full YouTube page UI.

## Non-goals

- Click-through / pointer pass-through at any opacity (hard non-goal, §7.5, ADR-0006).
- Making the WebView2 surface itself transparent or per-pixel alpha blending of the video
  (`WebView2CompositionControl` rearchitecture) — fallback tier only, separate design if needed.
- Replacing or restyling YouTube's controls in **normal page mode**.
- Overlay controls for playlists beyond prev/next (no in-overlay playlist browser).
- Source Window opacity (popout only — §7.3 scopes this to the Popout Player).
- The tuned shell CSP (still deferred to live QA, unchanged from Stage 4).

## Acceptance criteria

1. With overlay look **off** (default) nothing changes: current chrome strip, YouTube controls,
   opaque window — byte-for-byte the Stage 4 behavior.
2. Overlay look **on** (compact): video fills the rounded window; controls appear on hover/pause
   (transport, progress, volume, captions, close, fullscreen) and auto-hide on idle during
   playback; the native strip reveals on top-edge hover; close/pin/fullscreen round-trip
   shell→host; drag works (app-region or strip fallback).
3. Constant opacity < 100%: the whole window (chrome + video + error bar) is uniformly translucent
   while remaining fully interactable — clicks land on the player, never through it.
4. Idle opacity < constant: window eases to idle level after the fade idle delay, restores on
   hover/interaction, using the same idleness as the controls fade.
5. Sliders floor at 45%; hand-edited values down to 10% are honored; anything outside 0.1–1.0
   sanitizes to 1.0.
6. Stage 4 error bar + fallback still work in every look/opacity combination, including with a dead
   shell.
7. Pin, return/resume, single-player guard, placement persistence, and the 480×270 compact minimum
   are unaffected.
8. All deterministic lanes green; spikes have recorded live evidence before dependent stages start.

## Testing approach

- **Logic lane:** `WindowOpacityPolicy` (effective-opacity table, clamps, floor vs unlock,
  idle/constant interplay), protocol `request`-kind parse/validate/allowlist tests, appearance
  vocabulary normalization.
- **Markup lane:** XAML invariants — strip auto-hide bindings, error bar untouched, no
  `AllowsTransparency`, names/automation properties for any new toggles.
- **Wpf lane:** window constructs in every look/opacity combination; `*ForTests` seams for
  opacity application (assert the policy output is what the window would apply, without live HWND
  alpha); strip collapse/reveal state machine.
- **Shell asset tests:** extend the existing static asset/drift guards to the overlay markup —
  structure, no third-party origins, no credential strings, protocol field-name SSOT incl. the new
  `request` kind.
- **Live (spikes + smoke):** S-1 layered alpha over playing video (input + rendering + perf);
  S-2 app-region drag; S-3 DWM rounding (incl. combined with S-1); then a scripted smoke driving
  the overlay controls via the shell and the opacity sliders via settings, with screenshots.

## Changes by file (planned)

- `src/PiPlay/Services/WindowOpacityPolicy.cs` (new) + tests — pure seam.
- `src/PiPlay/Services/PlayerShellProtocol.cs` — `request` kind + version bump; tests.
- `src/PiPlay/Services/PlayerShellBridge.cs` — surface `RequestReceived`; tests.
- `src/PiPlay/PlayerShell/player.html` / `player-shell.js` (+ new `player-shell.css` if split) —
  chromeless param, overlay controls, reveal/auto-hide, app-region region; asset tests.
- `src/PiPlay/PlayerWindow.xaml(.cs)` — strip auto-hide, DWM corner preference, layered alpha
  application, request-action handlers; Wpf/markup tests.
- `src/PiPlay/Models/AppSettings.cs` + `SettingsService` — `constantWindowOpacity`, overlay-look
  flag; sanitization tests.
- `src/PiPlay/SettingsWindow.xaml(.cs)` + `PlayerAppearancePolicy` — sliders (45% floor) + overlay
  toggle; tests.
- `docs/PiPlay_Product_Engineering_Spec.md` — §7.3 resolution notes + the compliance-deviation
  record for the overlay look; `docs/QA_Checklist.md` — new rows; `docs/CHANGELOG.md`.
