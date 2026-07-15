# PiPlay UI Specification — Priority UX Improvements

> **Source:** owner, 2026-06-25 (delivered after the v0.6.0 cheap-cleanup review). This is the
> forward roadmap toward the "floating media player" vision. P1/P3/P7/P8 require the deferred
> transparency/WebView2 architecture lift; scope notes are in `SPEC_GAPS_AND_OWNERSHIP.md`.
>
> **Status as of v0.9.0 — parts of this roadmap have SHIPPED; read the sections below as the
> original owner brief, not as a list of current defects:**
> - **P1** is delivered and owner-retuned on 2026-07-15 (DWM frame, 12 DIP resize band/inset,
>   96 DIP diagonal corner reach).
>   Its remaining items are the chrome band / separators / letterbox tray.
> - **P2 SHIPPED in v0.9.0.** The profile color now drives the app accent, and the accent was given
>   real reach first (the toolbar row carries it; the title-bar wash is no longer near-imperceptible) —
>   without it, P2 would only have re-tinted a single button. The **CONFLICT flag below is resolved**:
>   the owner confirmed the reversal of the v0.6.0 identity-only split, and the product spec now agrees.
> - **P4's core dock/undock shipped in v0.8.0** — the toolbar and placeholder actions now flip to
>   "Bring video back" and return playback to the Source Window (see `docs/CHANGELOG.md` [0.8.0]).
>   Its "Current Problems" text below is therefore **stale**. Not all of its acceptance bullets are met
>   (no placeholder at all, seamless transfer, playlist position), so P4 is **not** signed off.
> - **P3, P5, P6, P7, P8** remain open as written.

---

# Priority 1 — Eliminate Visible Borders

## Goal
PiPlay should present a clean, floating, modern appearance with no distracting borders or outlines.
The application should feel like a native media player rather than a standard framed desktop window.

## Problems
* Visible outer window borders
* Double-frame appearance
* WebView edge lines
* Title bar edge artifacts
* Inactive window outline
* Rounded corners exposing border seams
* Border colors that remain visible regardless of theme

## Requirements
Remove visible borders from: Main window, Popout window, WebView container, Video container,
Titlebar, Rounded corner edges, Inactive window outline (where technically possible).
Prevent: Double borders, Double shadows, Layered frame appearance.

## Acceptance Criteria
* No visible border around the application
* Rounded corners appear seamless
* Windows look like floating panels
* Only a subtle shadow remains (optional)
* Border visibility should be effectively zero

---

# Priority 2 — Make Profile Color the Global Accent

> **RESOLVED + SHIPPED (v0.9.0).** This did reverse the v0.6.0 decision (profile color = identity chip,
> global app accent separate); the owner confirmed the reversal on 2026-07-14 and the product spec now
> agrees. It shipped together with the accent-reach work, because on its own it would have re-tinted a
> single button — the app accent barely painted anything. Design:
> `docs/superpowers/specs/2026-07-14-profile-accent-reach-design.md`.

## Goal
The selected profile color should become the application's primary accent color and consistently
replace the default blue throughout the interface. Changing profile color should feel like changing
the application's identity, not merely recoloring a single control.

## Current Problems
Profile colors currently affect too few UI elements. The default blue still appears throughout the
interface even after selecting another profile color.

## Requirements
The profile accent should apply to:
- **Navigation:** Profile selector, Dropdowns, Active tabs, Active navigation icons
- **Controls:** Play, Popout, Pin, Save, Edit, Delete buttons
- **States:** Hover, Pressed, Selected, Checked, Active, Keyboard focus
- **Window Chrome (optional):** Titlebar highlights, Accent strips, Window controls
- **Popout Window:** must inherit the exact same accent color automatically

## Acceptance Criteria
Changing the profile color updates: Entire application, Popout window, Active states, Interactive
controls. No default blue remains unless blue is the selected profile.

---

# Priority 3 — Transparency Scope

## Goal
Users should be able to independently control transparency for the main window and the popout window.

## Required Modes
* Off
* Main window only
* Popout only
* Both windows

## Transparency Targets
Window chrome, Background surfaces, Panels, Header areas, Overlay surfaces.
Transparency should **not** reduce video quality. Video content must remain fully opaque unless a
future option explicitly enables video transparency.

## Acceptance Criteria
Users can independently configure transparency for Main window, Popout, Both. Video playback quality
remains unaffected.

---

# Priority 4 — Make the Popout Button Useful

## Goal
The popout control should become a true dock/undock mechanism rather than simply focusing an already
open window.

## Current Problems
When the popout already exists, the button provides little practical value.

## Required Behaviour
- When docked: Move playback into the popout.
- When popped out: Bring playback back into the main window.
- Playback transfer must preserve: Current timestamp, Playing/paused state, Volume, Mute, Playback
  speed, Playlist position.

## UI Behaviour
Recommended labels: Docked → **Pop Out**; Popped → **Bring Back**. Alternative: single toggle button.

## Acceptance Criteria
No playback interruption. No placeholder screen. Video returns seamlessly.

---

# Priority 5 — Better Video Area Utilization

## Goal
The video should occupy as much of the available viewing area as possible while respecting aspect
ratio.

## Current Problems
Excessive black padding, Large unused margins, Small playback area inside a much larger container.

## Required Behaviour
Default mode: **Fit Window**. Additional viewing modes: Fit Window, Fill Window (crop), Cinema Padding.

## Layout Requirements
Maximize usable viewing area, Preserve aspect ratio, Minimize unused space, Never artificially shrink
the player.

## Acceptance Criteria
Video naturally fills the available content region. Large empty borders are eliminated.

---

# Priority 6 — Auto-Hide Window Chrome

## Goal
Provide an immersive playback experience by automatically hiding non-essential UI.

## Auto-Hide Targets
Navigation buttons, Address bar, Profile selector, Save/Edit/Delete controls, Playback controls,
Popout button, Window command area.

## Modes
Disabled, Main window, Popout, Both.

## Hide Conditions
Hide after: Video is playing, Pointer leaves top region, Controls lose focus, Configurable inactivity
timeout. Suggested default: **2–3 seconds**.

## Reveal Conditions
Show when: Pointer reaches top edge, Keyboard shortcut is pressed, Window gains focus, User
interaction requires controls.

## Acceptance Criteria
Controls disappear automatically and return instantly when needed without interrupting playback.

---

# Priority 7 — Cleaner Rounded Corner System

## Goal
Adopt a cleaner floating appearance inspired by the reference design. Rounded corners should enhance
the UI rather than expose border artifacts.

## Presets
None, Small, Medium, Large. Recommended default: **Medium**.

## Design Principles
Soft edges, Floating appearance, No visible outline, Optional soft shadow, Consistent radius across
windows.

## Acceptance Criteria
Windows appear polished and modern with no exposed corner seams.

---

# Priority 8 — Unified Main & Popout Experience

## Goal
The main window and popout should feel like different presentations of the same application rather
than separate products.

## Shared Visual System
Both windows share: Accent colors, Theme, Transparency, Corner radius, Borderless appearance, Shadows,
Chrome behaviour, Icon styling, Typography, Spacing.

## Shared Behaviour
Both windows support: Auto-hide, Theme updates, Profile switching, Live appearance changes.
Appearance changes should propagate immediately without reopening either window.

## Acceptance Criteria
Switching between the main window and popout should feel seamless, with no visual or behavioural
inconsistencies.

---

# Overall UX Objectives

**Visual Quality:** Borderless floating windows, Consistent theming, Minimal visual noise, Modern
Fluent-inspired appearance, Soft shadows instead of borders.

**Functional Quality:** Dock and undock without interruption, Maximum video area, Automatic chrome
hiding, Shared settings across windows, Live theme synchronization.

# Success Metrics

| Priority | Objective            | Success Metric                                                   |
| -------- | -------------------- | ---------------------------------------------------------------- |
| P1       | Borderless windows   | No visible outlines or double frames                             |
| P2       | Global accent color  | Profile color replaces default blue across the entire UI         |
| P3       | Transparency control | Independent transparency settings for main and popout            |
| P4       | Dock / Undock        | One-click transfer between windows with preserved playback state |
| P5       | Video utilization    | Video fills the available area with minimal wasted space         |
| P6       | Auto-hide chrome     | Controls hide unobtrusively and reappear instantly               |
| P7       | Rounded corners      | Clean floating appearance with no border artifacts               |
| P8       | Unified experience   | Main and popout remain visually and functionally consistent      |
