# Popout rounded-card region — design

## Goals

Make the Popout Player's existing Soft Glass / `Round` appearance use its 22 DIP
`PopoutFrame` token for the real outer silhouette, including clipping the HWND-hosted WebView2.
Keep the standard WebView2 control, playback path, borderless resize behavior, and all non-round
themes unchanged.

## Requirements served

- Product spec §5.3 Popout Player shape target.
- `Q-7` native-quality move, resize, maximize, and per-monitor-DPI behavior.
- `REQ-WINDOW-01` per-monitor DPI correctness.
- `REQ-WINDOW-02` native edge and corner resize behavior.
- Owner appearance direction 2.4 / P1 in `docs/SPEC_GAPS_AND_OWNERSHIP.md`.

## Acceptance criteria

- Soft Glass and the explicit `Round` corner override clip the complete Popout Player, including
  WebView2, to the resolved `RadiusPopoutFrame` silhouette.
- Sharp Dark/default, `Square`, and `Small` retain their existing DWM-only behavior and never gain a
  custom window region.
- The custom radius is converted from DIPs to physical pixels for the Popout's current monitor DPI
  and is refreshed after resize and DPI changes.
- Maximized Popout Players are rectangular/full-bleed; restoring reapplies the rounded silhouette.
- Snap classification uses DWM visible frame bounds (not `GetWindowRect`'s invisible resize border)
  and clears the region only for edge-aligned standard layout fractions.
- The design initially retained the existing 4 DIP edge / 32 DIP corner zones. Direct owner testing
  later that day found them too difficult, so the implementation now uses a 12 DIP edge / 96 DIP
  corner reach; the rounded
  region must preserve those enlarged zones.
- The implementation keeps `AllowsTransparency=False`, the standard WebView2 HWND host, and the
  existing opacity path.
- If the real deployed result shows unacceptable aliasing, snap seams, or resize regressions, the
  custom region is rejected and removed rather than shipped as polish.

## Settled decisions

1. Use `SetWindowRgn` with a `CreateRoundRectRgn` on the top-level Popout HWND for `Round` only.
   A top-level window region clips child HWNDs, so this reaches the video without WPF airspace tricks.
2. Keep `WebView2`, not `WebView2CompositionControl`. Microsoft's composition control is a
   GraphicsCaptureSession-based fallback with potentially lower frame rate and no DRM playback; that
   trade is inappropriate for the default video path.
3. Keep `AllowsTransparency=False`. Per-pixel transparent WPF windows would replace the proven native
   opacity, resize, and WebView2 behavior with a new layered-window architecture.
4. Wire the already-defined semantic `ThemeRadii.PopoutFrame` token. Soft Glass and explicit `Round`
   resolve to 22 DIP; no new user setting or duplicate radius scale is introduced.
5. Clear the custom region while maximized and for every non-round mode. DWM remains the owner of
   ordinary/small rounding; the custom region is only the large-card exception.
6. Accept that a custom region has no guaranteed curve-following DWM shadow. This pass targets the
   silhouette and clean clipping; an outer shadow remains deferred.

## Non-goals / out of scope

- A transparent WebView, click-through, or pass-through window.
- Switching the playback surface to composition/windowless WebView hosting.
- A custom outer shadow or glow.
- Applying a custom region to the Source Window, Settings, or prompts.
- Per-profile radius values; profiles continue to inherit the active global theme/corner setting.

## Testing approach

- Pure policy tests pin eligibility, DPI conversion, and radius clamping.
- WPF runtime tests create a real Popout HWND, assert a region for `Round`, assert clipped corner
  points, and verify live switch/maximize clearing and restore reapplication.
- Existing layout, WebView2 airspace, opacity, resize-hit-test, settings, and theme tests remain green.
- The deterministic repo gate runs before a diagnostics-only Stable publish.
- Manual visual acceptance uses only the deployed Stable copy at 100%, 125%, and 150% DPI.

## Changes by file

| File | Change |
|---|---|
| `src/PiPlay/Services/RoundedWindowRegionPolicy.cs` | Pure eligibility and DIP-to-pixel geometry policy. |
| `src/PiPlay/Services/RoundedWindowRegionApplier.cs` | Own the Win32 region creation, apply, clear, and test probes. |
| `src/PiPlay/PlayerWindow.xaml.cs` | Apply and refresh the resolved Popout radius across lifecycle changes. |
| `src/PiPlay/MainWindow.xaml.cs` | Pass the resolved `PopoutFrame` radius during launch and live preview/apply. |
| `tests/PiPlay.Tests/RoundedWindowRegionPolicyTests.cs` | Pin geometry policy. |
| `tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs` | Pin the real HWND silhouette lifecycle. |
| `docs/adr/0008-popout-rounded-window-region.md` | Record the architecture choice and tradeoffs. |
| Current product/theme/QA docs | Replace the obsolete DWM-only statement and add deployed checks. |

## Docs & changelog impact

- Add ADR-0008 and index it.
- Update product spec §5.3, `Theme_Preset_Differences.md`, `SPEC_GAPS_AND_OWNERSHIP.md`, and
  `QA_Checklist.md` after the visual spike is accepted.
- Add an Unreleased entry to `docs/CHANGELOG.md` if the region passes deployed visual QA.

## Unresolved decisions

- Whether a future composition-hosted player is worth its frame-rate and DRM limitations for richer
  overlays and shadows. This change deliberately does not decide that broader migration.
