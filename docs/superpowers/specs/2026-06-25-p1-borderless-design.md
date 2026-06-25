# P1 — Borderless window surface — design

Date: 2026-06-25
Status: approved direction (owner UI roadmap P1, cheap-first / no-lift tier)
Roadmap: `docs/PiPlay_UI_Priority_Improvements.md` (P1). Grounding: `docs/reviews/2026-06-25-ui-review-crossvalidation.md`.

## Context

The owner's #1 priority is a clean, floating, **borderless** surface — "border visibility effectively
zero," no double-frame, no WebView edge lines, no harsh boxed control outlines. The v0.6.0 pass only
*quieted* the borders (faint hairline); P1 removes them.

Cross-validation established this is achievable on the **current opaque-HWND + windowed-WebView2
model with no `AllowsTransparency`/composition lift** — with one corrected constraint (below).

## The one hard constraint (corrected from the cross-val synthesis)

The windowed **WebView2 child swallows `WM_NCHITTEST`** (documented at `MainWindow.xaml:156-160`,
`PlayerWindow.xaml:95-99`). The top-level window only receives edge mouse where the WebView is *not*,
so the WebView **resize inset is structurally required** for edge/corner resize over the video.
**Therefore the band cannot go to literal zero** — it is shrunk and blackened, not removed. Literal
pixel-zero edges are the only thing that would need the WebView2 windowless/composition lift, and
that is an explicit non-goal here (deferred escalation).

## Goal

Make PiPlay read borderless on the current architecture:
1. Remove resting control borders (use fill/hover/focus instead; keep focus rings).
2. Remove the Settings dialog outer border.
3. Shrink + blacken the WebView resize inset so it merges with the video letterbox and reads as ~no
   frame, while still owning the edge pixels for `WM_NCHITTEST` resize.
4. Keep DWM rounded corners (already render seamless — verified by the `corner-topleft.png` capture).
5. Rewrite the tests that *pin* the old gutter/hairline so they guard the new invariant (resize
   works + borderless surface) instead of the removed treatment.

## Non-goals (deferred escalation — only if the cheap tier still reads as a frame on the deployed build)

- No `AllowsTransparency=true`, no WebView2 windowless/composition hosting.
- No literal pixel-zero window edges (would lose resize-over-video or need the lift).
- No larger-than-DWM custom rounded card (that is P7's custom route + the lift).
- No new border-mode setting (`Off/Hairline/Accent`) — borderless becomes the default; a setting can
  be added later if wanted.

## Design

### A. Resting control borders → off (keep affordances)
In `src/PiPlay/Theme/ControlStyles.xaml`, drop the resting `BorderBrush`/`BorderThickness` (the
`BorderSubtle` + `BorderThicknessDefault` defaults) from the resting state of: `DarkButton`,
`PinToggle`, `DarkComboBox`, and the URL `TextBox` style. Replace the affordance with the existing
fill/hover/pressed/selected visuals. **Keep**: the keyboard **focus ring** (REQ-UI-02 accessibility),
hover/pressed/checked/selected states, and `AccentButton`'s filled accent look. `AccentBorder` (the
accent focus/checked outline) stays — it is a focus/selected affordance, not a resting border.

### B. Settings dialog border → off
`src/PiPlay/SettingsWindow.xaml:83`: set the root `Border` `BorderThickness` to `0` (or remove the
stroke), relying on the window background/elevation. Internal section separators (`Border Height="1"`)
are a separate, lower-priority call — leave for now unless they read as harsh.

### C. WebView resize inset → shrink + blacken
- Reduce the normal-state WebView margin from `10,0,10,10` to **`4,0,4,4`** in both
  `MainWindow.xaml` (Grid.Row=2 WebView style) and `PlayerWindow.xaml` (Player WebView style).
  Maximized stays `0` (full-bleed).
- Reduce `BorderlessResizeHitTestPolicy.ResizeBorderDip` from `10` to **`4`** so the resize hit-test
  zone stays equal to the visible band (the existing invariant: band == `ResizeBorderDip`).
  `CornerLengthDip` (32) stays so corner grab remains easy despite the thinner edge.
- The inset grids are already `Background="Black"`; the thinner black band merges with the video
  letterbox. (Exact value is tunable; `4` is the first cheap attempt, validated by deploy + look.)

### D. Corners
No change. DWM `DwmCornerMode.Round` already renders seamless over the WebView at the native radius.

### E. Tests (rewrite the pins, don't delete the guarantees)
`tests/PiPlay.Tests/Ui/XamlInvariantTests.cs`:
- `WindowChrome_invariants_hold` (~48-58): keep `CornerRadius=0`; assert
  `ResizeBorderThickness == BorderlessResizeHitTestPolicy.ResizeBorderDip` (now `4`) rather than the
  literal `10`.
- `WebView_margin_gives_the_window_the_resize_band` (~65-86): assert the normal margin equals the new
  `ResizeBorderDip` (`4,0,4,4`) and maximized `0` — i.e. the band tracks the policy constant, not a
  hard-coded `10`.
- Add/adjust a **borderless invariant**: resting `DarkButton`/`DarkComboBox`/`PinToggle`/URL `TextBox`
  carry no resting `BorderSubtle` stroke (assert the resting `BorderThickness` is `0` / the
  resting setter no longer binds `BorderSubtle`), while the focus-ring trigger remains.
- `BorderlessResizeHitTestPolicyTests` (logic): a `ResizeBorderDip = 4` case still returns the right
  HT codes at the edges/corners (resize behavior preserved).

## Testing

- Layer 1 (Markup): the rewritten `XamlInvariantTests` (band tracks policy, no resting control border,
  Settings border 0).
- Logic: `BorderlessResizeHitTestPolicyTests` proves edge/corner hit-test at the new `4` DIP band.
- Layer 3 (Wpf): `WpfRuntimeTests` — resting controls realize with no border brush; focus ring still
  realizes.
- Full Lane A green → deploy to Stable → **look**: confirm no grey control outlines, no Settings box,
  the WebView band reads as letterbox not frame, corners clean. Escalate to the windowless lift only
  if it still reads as a frame.

## Resolved choices
- Band first attempt: **4 DIP**, pure black, `ResizeBorderDip` tracks it. Tunable on visual review.
- No new border-mode setting this pass (borderless = default).
- Build base: `main` @ the v0.6.0 release lineage; code on branch `feat/p1-borderless`.
