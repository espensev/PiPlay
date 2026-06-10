# Borderless resize zones - design

## Goals

Make PiPlay's borderless windows easier to resize without adding loud visual chrome. The current
resize target is too small for a desktop utility whose main value is native-quality move and resize.
This pass makes the next implementation ready by fixing the vocabulary, the target dimensions, the
implementation seam, and the test plan.

Baseline before implementation: both `src/PiPlay/MainWindow.xaml` and `src/PiPlay/PlayerWindow.xaml` used
`WindowChrome ResizeBorderThickness="6"`. `tests/PiPlay.Tests/Ui/XamlInvariantTests.cs` also asserts
that value. That meant the invisible resize border was 6 device-independent pixels (DIP) on all edges
and corners, with no separate larger corner length and no visible size grip.

The happy path stays unchanged: Source Window and Popout Player remain opaque `WindowStyle=None`
WPF windows; WebView2 stays non-transparent; the user still resizes by grabbing the outer edges and
corners. This change is only about the non-client hit-test area, not playback, placement, aspect
lock, compact mode, opacity, or click-through.

## Requirements served

- `Q-7` - native window quality: resizing should be easy to acquire and predictable.
- `Q-8` - visible means interactable: expanded hit testing must not introduce click-through or
  pointer pass-through behavior.
- Spec sections 16.1, 16.3, and 26.5 - Popout Player must resize predictably from edges and corners
  through native hit testing, not only visible handles.
- `REQ-WINDOW-02` - added by this ready-state docs pass: borderless resize targets use larger
  invisible resize zones than the visual outline.

## Naming

Use the Win32 hit-test vocabulary in code, tests, and docs. User-facing text should still say
"resize" rather than exposing `HT*` constants.

| Area | Preferred name | Win32 hit-test result |
|---|---|---|
| Left edge | left resize border, west resize zone | `HTLEFT` |
| Right edge | right resize border, east resize zone | `HTRIGHT` |
| Top edge | top resize border, north resize zone | `HTTOP` |
| Bottom edge | bottom resize border, south resize zone | `HTBOTTOM` |
| Corners | corner resize zones, diagonal resize zones, NW/NE/SW/SE resize corners | `HTTOPLEFT`, `HTTOPRIGHT`, `HTBOTTOMLEFT`, `HTBOTTOMRIGHT` |
| Visible lower-right gripper, if ever added | size grip, size box, grow box | `HTSIZE` / `HTGROWBOX` |

## Acceptance criteria

- The previous documented baseline is preserved for reviewers: PiPlay was at 6 DIP via
  `WindowChrome.ResizeBorderThickness` before this pass.
- Both primary borderless windows expose a mouse/pen edge resize zone of 10 DIP while in the normal
  window state.
- Corners use a 32 DIP corner length for diagonal resizing. This is the length along the edge where
  an edge resize zone returns the diagonal corner result; it is not a full 32 x 32 DIP square that
  steals clicks from content.
- Hit-test precedence is corners first, then left/right/top/bottom edges, then normal client/chrome
  handling.
- A maximized window does not report resize borders.
- Interactive controls remain clickable outside the outer resize band. The implementation must not
  make a full top-right square consume the close/minimize/maximize buttons.
- The visual border remains subtle: a 0-2 px outline is acceptable, and no visible size grip ships in
  this pass.
- The Popout Player remains usable at its current 320 x 180 minimum.
- No click-through, transparent hit testing, `WS_EX_TRANSPARENT`, transparent WebView2, or
  whole-window opacity behavior is introduced.
- Tests prove the hit-test classifier returns the expected Win32 constants for all eight resize
  results and for near-miss client points.
- Manual QA verifies the resize cursor and drag behavior at 100%, 125%, and 150% display scale.

## Settled decisions

1. **Use 10 DIP for mouse/pen edge resize zones.** It sits in the 8-12 DIP usability range while
   avoiding an oversized invisible border around a compact media window.

2. **Use a 32 DIP corner length.** It sits in the 24-40 DIP corner range and makes diagonal resize
   easier without turning each corner into a large square hit target.

3. **Treat corner length as edge length, not filled area.** A 10 DIP-thick top edge for the first
   32 DIP from a corner returns `HTTOPLEFT`/`HTTOPRIGHT`; a 10 DIP-thick side edge for the first
   32 DIP returns the same diagonal result. This protects caption and toolbar controls.

4. **Prefer a small `WM_NCHITTEST` helper over only increasing `WindowChrome`.** WPF
   `WindowChrome.ResizeBorderThickness` can make the uniform border larger, but it cannot express a
   separate corner length. A pure classifier plus HWND hook makes the behavior explicit and testable.

5. **Keep the visual treatment separate from the hit area.** The product can draw no border or a
   1-2 px visual outline while keeping a larger invisible resize target.

6. **Do not ship a touch-first resize mode in this pass.** Microsoft touch guidance treats 40 x 40
   effective pixels as a normal touchable target, but PiPlay's default window resize affordance is
   mouse/pen first. Touch-optimized resize can be considered later with a visible affordance or
   posture-aware mode.

7. **Apply the default target to both Source Window and Popout Player.** The Popout Player is the
   most important surface, but the Source Window is also borderless and should not keep the old
   hard-to-grab border.

## Non-goals / out of scope

- Aspect-lock resizing.
- Size presets.
- Compact-player minimum-size changes.
- A visible lower-right size grip.
- Touch-first resize controls or a 40 x 40 resize handle.
- DWM rounded-corner policy changes.
- Global hotkeys or "grab anywhere to resize" behavior.
- Click-through or transparent hit testing.

## Testing approach

- **Logic tests:** add a pure resize-hit-test classifier test matrix for all eight `HT*` resize
  values, non-edge client points, boundary inclusivity, maximized-window suppression, and negative
  screen-coordinate safety.
- **Markup tests:** update `XamlInvariantTests.WindowChrome_invariants_hold` from 6 DIP to the new
  edge target, or replace the assertion with a named constant check if the helper owns the target.
- **WPF runtime tests:** construct both windows and verify the helper can be attached without
  showing a WebView2-backed window.
- **Manual UI QA:** run the built app and verify edge and diagonal resize cursor behavior at 100%,
  125%, and 150% display scale. Confirm close/minimize/maximize, Pin, Fade, and Source toolbar
  controls are still clickable.
- **Release smoke:** keep `scripts\Test-UiSmoke.ps1` focused on launch/title/chrome screenshots; add
  resize evidence only if a shareable build is promoted.

## Changes by file

| File | Change |
|---|---|
| `src/PiPlay/Services/BorderlessResizeHitTestPolicy.cs` | New pure classifier/constants for 10 DIP edge zones and 32 DIP corner length. |
| `src/PiPlay/Services/BorderlessWindowHelper.cs` | Add or call a `WM_NCHITTEST` hook that returns the classifier's Win32 hit-test result for borderless windows. Keep existing `WM_GETMINMAXINFO` behavior. |
| `src/PiPlay/MainWindow.xaml.cs` | Enable expanded resize zones for the Source Window. |
| `src/PiPlay/PlayerWindow.xaml.cs` | Enable expanded resize zones for the Popout Player. |
| `src/PiPlay/MainWindow.xaml` | Increase `WindowChrome.ResizeBorderThickness` to the edge target if WindowChrome remains part of the edge path. |
| `src/PiPlay/PlayerWindow.xaml` | Increase `WindowChrome.ResizeBorderThickness` to the edge target if WindowChrome remains part of the edge path. |
| `tests/PiPlay.Tests/BorderlessResizeHitTestPolicyTests.cs` | Add focused logic coverage for all resize zones and boundaries. |
| `tests/PiPlay.Tests/Ui/XamlInvariantTests.cs` | Update burned-in chrome invariant to the new target or helper-owned constant. |
| `tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs` | Add construction/attachment coverage if needed. |
| `docs/PiPlay_Product_Engineering_Spec.md` | Record `REQ-WINDOW-02` and the resize-zone contract. |
| `docs/QA_Checklist.md` | Add measurable manual resize checks. |
| `docs/CHANGELOG.md` | Add a user-visible fix entry when implementation lands. |

## Docs & changelog impact

This ready-state pass updates the product spec, QA checklist, and spec-gaps/status docs. The
implementation pass should update `docs/CHANGELOG.md` because easier window resizing is user-visible.
No ADR is needed unless the implementation changes the platform-window strategy or transparency
policy.

## Reference notes

- WPF `WindowChrome.ResizeBorderThickness` defines the width of the click-and-drag resize area; it is
  not itself a visual border.
- Win32 `WM_NCHITTEST` defines the edge and corner return values used for native resize behavior.
- Windows touch guidance uses 40 x 40 effective pixels as the normal touchable target, but that is a
  touch-target guideline, not a requirement to make every mouse resize border 40 DIP thick.
- Windows 11 rounded-corner guidance mentions a 1-pixel non-client area border as enough frame
  information for DWM corner rounding; that is separate from the resize hit area.

## Unresolved decisions

- None for the default mouse/pen resize-zone pass.
