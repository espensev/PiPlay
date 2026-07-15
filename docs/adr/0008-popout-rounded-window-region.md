# ADR-0008: Use a custom window region for large round Popout corners

- **Status:** Accepted
- **Date:** 2026-07-15

## Context

PiPlay's Popout Player uses the standard WPF WebView2 control, which is an `HwndHost` containing a
child HWND. WPF clips, masks, rounded Borders, and overlay effects do not cross that airspace boundary.
The existing Windows 11 `DWMWA_WINDOW_CORNER_PREFERENCE` integration is safe and native, but exposes
only the system's fixed square/small/standard radii. It cannot realize the 14–18+ DIP floating-media
card requested by the product shape target; Soft Glass already defines a 22 DIP `PopoutFrame` token
that is therefore unused at the top-level window.

Changing to `WebView2CompositionControl` would make WPF clipping possible, but Microsoft documents
that it captures browser output through `GraphicsCaptureSession`, may render at a lower frame rate,
and cannot display DRM-protected content. A transparent WPF top-level window would also replace the
proven opacity and native-window path. Those are poor default tradeoffs for a video player.

Win32 window regions are applied to a top-level HWND in device pixels and bound the portion of the
window that Windows displays. That boundary includes child HWND content, so it can clip WebView2
without changing the browser host.

## Decision

For the Popout Player only, use a rounded top-level window region when the effective corner mode is
`Round`. The ellipse radius comes from the resolved `ThemeRadii.PopoutFrame` token and is converted
from DIPs with the current window DPI. Refresh it after resize and DPI changes, clear it while
maximized, and clear it for edge-aligned Snap Layout fractions using DWM visible frame bounds. Restore
it when the Popout floats again. All other corner modes remain on the existing DWM path.

Keep `AllowsTransparency=False`, the standard `WebView2` control, the current native opacity system,
and the existing non-client resize subclass. Treat deployed visual acceptance as part of adopting the
decision: if the region edge or snap behavior is not clean, reject this ADR and remove the spike.

## Consequences

- Soft Glass / explicit `Round` can use the existing 22 DIP Popout radius and clip the actual video.
- The normal WebView2 frame-rate and DRM behavior stay intact.
- Sharp Dark, Minimal, Square, Source Window, Settings, and prompts retain their current native path.
- The region must be maintained in physical pixels across size, DPI, and maximize/restore changes.
- A custom region is not a DWM custom-radius API: a curve-following native shadow or border is not
  guaranteed. This decision delivers the silhouette, not an outer glow.
- Composition hosting remains a separate future ADR if rich overlays/shadows ever outweigh its video
  playback limitations.
