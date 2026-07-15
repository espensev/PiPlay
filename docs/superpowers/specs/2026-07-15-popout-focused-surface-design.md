# Focused Popout surface, overlay controls, and threshold drag — design

Date: 2026-07-15
Status: implemented and diagnostics-verified; clean deployed acceptance remains pending

## Goals

1. Let the Popout Player move from the passive video surface after a real drag gesture, while
   preserving ordinary click/play and every interactive YouTube control.
2. Add an optional **Focused overlay** presentation that makes the real YouTube player fill the
   Popout viewport and supplies Opera-style, auto-hiding media/window controls.
3. Maximize the visible picture without cropping; non-matching video/window ratios still
   letterbox (`object-fit: contain`).
4. Keep Standard presentation as the default and preserve the current Normal watch-page playback,
   signed-in session, return-state capture, navigation policy, and dormant Compact plumbing.

Requirements served: **Q-2**, **Q-3**, **Q-5**, **Q-6**, **Q-7**, **Q-8**,
**REQ-PROFILE-01**, **REQ-UI-01**, **REQ-UI-02**, **REQ-WINDOW-02**.

## Owner reference and superseded default

The owner supplied Opera video-popout screenshots and asked for whole-window dragging, better
picture fill, and comparable controls as an option. This intentionally supersedes the old
§16.2 default that prohibited video-surface dragging. The replacement is narrower than literal
caption hit-testing: only passive player pixels arm, movement must cross a drag threshold, and
controls/timeline/links/end cards remain excluded.

## Settled decisions

### 1. Presentation is independent from playback mode

Add `PopoutPresentation` (`Standard`, `Focused`) and a pure
`PopoutPresentationPolicy`. The global default lives on `PlayerSettings`; a nullable profile token
(`standard` / `focused`) overrides it only when that profile's own video is popped out. Do not reuse
`Profile.Mode` or `PlayerSettings.CompactMode`: Compact is a separate, dormant embedded-player
architecture and remains disabled.

Standard stays the default. Focused applies to new Popout Players only; changing Settings does not
rewrite a live player's DOM posture.

### 2. Keep Normal watch-page playback

Focused presentation uses the same Normal YouTube `/watch` page and the existing
`YouTubeDomBridge` return-state path. This preserves cookies/sign-in, full volume/mute/rate capture,
ads/restrictions, and videos that reject IFrame embedding. The old local Compact shell is not
re-enabled.

### 3. Full-frame, no-crop layout is best-effort and reversible

A dedicated `PlayerFirstSurfaceBridge` installs an idempotent top-document script before
navigation. On supported `/watch` pages it:

- gives the YouTube player a fixed viewport-sized surface;
- clears page overflow/background around that surface;
- forces the main video to `width/height: 100%` with `object-fit: contain`;
- re-applies after YouTube SPA navigation/DOM replacement;
- removes its classes/overlay when the URL is no longer a watch page.

Selector failure leaves the ordinary page intact. `cover` is explicitly forbidden by default.

### 4. Overlay controls live inside WebView2

WPF cannot reliably layer controls over the standard WebView2 child HWND. Focused controls are
therefore an injected HTML layer above the YouTube player. The first slice mirrors the supplied
reference without replacing YouTube's complete control system:

- mute, captions (best-effort native-button handoff), PiPlay Settings, and Pin at top-left;
- Close and Expand/Restore at top-right;
- Play/Pause and Next (best-effort native-button handoff) in the center;
- a seek/progress rail and time readout at the bottom.

The overlay container is pointer-transparent; only actual buttons/rail accept input. YouTube's
native controls, branding, settings, quality, captions menu, and ad UI remain available. Controls
reveal on activity/pause/focus and follow the Popout Fade setting/delay. The overlay is selectable
and off by default, reducing the compliance risk of host UI over player content.

Window actions use a new nonce-bearing, versioned, closed protocol. Only `close`, `pinToggle`,
`fullscreenToggle`, and `settings` are accepted from an HTTPS YouTube source; Close and Settings
are deferred outside the WebView callback.

### 5. Whole-surface drag uses a threshold bridge

A mode-independent `PlayerSurfaceDragBridge` registers a capture-phase pointer script through
`CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync` before navigation. WebView2 applies that
script to top documents and child frames, but the host deliberately listens only to
`CoreWebView2.WebMessageReceived`: the real watch player is top-level, and deployed-runtime
reproduction showed recursive `CoreWebView2Frame` event wiring crashing WebView2 after frame creation.

The script arms only for a primary mouse/pen pointer over passive YouTube player content. It rejects
buttons, links, inputs, menus, progress/seek areas, settings/captions/fullscreen controls, end cards,
ads with actions, and PiPlay overlay controls. A normal click is untouched. Only after movement
crosses the horizontal/vertical drag threshold does it suppress that gesture's click and post one
nonce-bearing drag request.

After deployed owner testing, the guaranteed native top handle is 44 DIP high and its blank column
shows a move cursor. Unused YouTube top/bottom chrome containers may also arm threshold dragging;
rendered captions and all actual controls remain excluded. The accepted resize edge remains 12 DIP,
while diagonal corner reach extends from 72 to 96 DIP without making the visible band thicker.

The host source-gates HTTPS YouTube origins, exact-parses the message, then queues a native move.
Immediately before posting it rechecks that the left button is physically down and the window is in
Normal state. The page releases DOM pointer capture, and the host posts
`WM_SYSCOMMAND / (SC_MOVE | HTCAPTION)` to the Popout HWND. Posting keeps the WebView callback
non-blocking while preserving native move/Snap behavior. Maximized windows and stale/replayed
requests do nothing.

### 6. Failure posture

Every injection/message failure is logged at most once per setup path and leaves the native top
strip operational. The standard page, top-strip drag, edge resize, return path, and close path are
the recovery surface. No click-through, transparent WebView, global hook, or input pass-through is
introduced.

## Changes by file

- `Models/AppSettings.cs`, `Models/Profile.cs` — global and profile presentation values.
- `Services/PopoutPresentationPolicy.cs` — pure normalization/precedence/target scoping.
- `Services/PlayerSurfaceDragBridge.cs` — top-document message validation and native-move
  request seam.
- `Services/PlayerFirstSurfaceBridge.cs` — Focused layout/overlay script and closed action protocol.
- `Services/SettingsService.cs`, `Services/ProfileService.cs` — sanitize and preserve values.
- `PlayerWindow.xaml.cs` — install/dispose bridges and map allowlisted actions to existing handlers.
- `MainWindow.xaml.cs` — resolve presentation and pass it into the Popout Player.
- `SettingsWindow.xaml(.cs)`, `Prompt.cs` — global toggle and per-profile picker.
- focused logic/markup/WPF tests plus living spec, QA, gaps, changelog, and test-index updates.

## Acceptance

1. Standard is byte-for-behavior compatible except for threshold drag on passive video pixels.
2. Click, double-click, seek, captions, settings, fullscreen, links, and overlay buttons do not start
   a move; a deliberate drag on passive picture pixels does.
3. Focused fills the viewport with the real watch-page player, never uses `object-fit: cover`, and
   never crops by default across wide/tall/free resize.
4. Overlay controls match the supplied media-first reference, fade predictably, and remain keyboard
   named/focusable.
5. Global/profile precedence is deterministic: a missing or invalid profile override inherits the
   sanitized global default, while a missing or invalid global default resolves to Standard.
6. Return timestamp/play state/volume/mute/rate, single-player behavior, Pin, resize, rounded region,
   and Standard presentation remain intact.
7. Automated gates pass, then a diagnostics-only Stable deployment is used for real YouTube QA.
