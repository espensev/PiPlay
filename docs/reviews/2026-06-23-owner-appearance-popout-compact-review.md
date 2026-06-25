iPlay Appearance / Popout / Compact Mode Spec
1. Summary

PiPlay should feel like a floating media card, not a browser window. The current implementation has good base functionality, but the appearance controls do not yet create a strong enough visible difference between themes, corner modes, and profile accents.

The intended direction is:

Opaque or near-opaque floating video surface
Clean rounded clipping
Subtle border and shadow
Minimal chrome
Very little transparency by default
Strong visual difference between appearance modes

The rounded floating popout reference is much closer to the desired final result than the current theme/settings behavior.

2. Current Issues
2.1 Theme buttons have low visual impact

Current theme options:

Sharp Dark
Minimal
Soft Glass

exist in the UI, but they only make small changes. They do not meaningfully change the final window feel.

Expected behavior:

Sharp Dark
- opaque dark chrome
- crisp edges
- visible but subtle border
- strong contrast

Minimal
- reduced chrome
- no unnecessary visible frame
- controls should be hidden or quiet until needed
- video surface should dominate

Soft Glass
- only slight translucency
- soft border
- soft shadow
- rounded, floating card feeling

Transparency should not be heavy by default. It should be a controlled visual effect, not the core look.

2.2 Profile color and app accent are confused

The profile color currently feels like it is also acting as the app accent color. This is confusing because the profile color does not visibly affect enough of the first-state UI to justify being profile-specific.

Expected model:

Global app accent:
- primary buttons
- focus rings
- active outlines
- selected theme state
- popout/restore button
- compact mode button
- important UI highlights

Profile color:
- profile chip/dot
- optional profile identity marker
- optional popout border only when that profile is active

Profile color should only be profile-specific if it is actively and visibly used in the main UI. Otherwise, accent color should be global.

2.3 Accent color validation is too restrictive

The app currently warns:

This color is not readable as an app accent.

This is useful for text, but too restrictive for borders, glow, icons, and small identity accents.

Expected behavior:

User may choose any accent color.
The app automatically chooses readable foreground text when needed.
Border opacity and strength should be controlled separately.

Recommended logic:

Accent color = user-selected color
Text on accent = automatically black or white based on contrast
Border color = accent or neutral
Border opacity = independent setting

Do not reject useful colors just because they are poor text backgrounds.

2.4 Corner settings do not affect the final silhouette enough

Current corner options:

Theme
Square
Small
Soft
Round

are conceptually good, but the visual impact is too weak. The final popout should visibly change shape.

Expected behavior:

Square = 0 px radius
Small  = subtle radius
Soft   = medium rounded card
Round  = large rounded floating card

The corner radius must apply to the actual outer window and the video clipping area, not only to inner panels or settings UI.

Acceptance criteria:

When Round is selected, the popout visibly becomes a rounded floating card.
The video content is clipped to the same radius.
No square WebView/background layer should remain visible behind rounded corners.
The border follows the same rounded shape.
The shadow follows the same rounded shape.
3. Popout Behavior
3.1 Main-window placeholder state

When playback is moved to the video popout, the main window currently shows:

Playing in Video Popout
Close the popout to bring playback back here.

This state is clear, but it should be more functional.

Expected behavior:

When a video is playing in popout mode, the main window should show a clear placeholder state.
The placeholder should include a direct action to restore/focus the popout or bring playback back.

Suggested text:

Playing in Video Popout
Playback is currently detached.

[Bring popout back]

or:

Playing in Video Popout
The video is open in a floating window.

[Restore video here] [Show popout]
3.2 Toolbar button behavior while popout is active

The toolbar button should not behave like a normal “open popout” button when the popout already exists.

Current/observed issue:

The button does not clearly bring the popout window back.

Expected behavior:

If no popout exists:
  Button label = "Pop out video"
  Action = create popout and move playback there

If popout exists but is behind other windows/minimized:
  Button label = "Show popout"
  Action = restore, focus, and bring the popout window to front

If popout exists and user wants playback back in the main window:
  Provide a separate "Restore video here" action

Important requirement:

The button should bring the popout window back like it did earlier.

Acceptance criteria:

Clicking "Show popout" restores the popout if minimized.
Clicking "Show popout" brings the popout to the foreground if hidden behind other windows.
Clicking "Show popout" does not create a duplicate popout.
Clicking "Show popout" does not leave the user stuck on the placeholder screen.
4. Compact Mode Bug
4.1 Current issue

Compact mode currently does not work.

Observed behavior:

Compact mode button/action exists, but the window does not enter a useful compact state.

Expected behavior:

Compact mode should visibly transform the app into a smaller, cleaner player-first layout.

Compact mode should hide or reduce:

address bar
profile controls
save/edit/delete buttons
extra toolbar spacing
unnecessary window chrome
large empty black areas

Compact mode should keep:

video surface
minimal titlebar or hover titlebar
pin/always-on-top
close/restore controls
optional compact-mode exit button

Acceptance criteria:

Activating compact mode changes the layout immediately.
The video remains visible and usable.
The window can still be moved.
The user can exit compact mode.
Compact mode state is saved per profile or globally, depending on final design.
Compact mode does not break popout mode.

Recommended compact behavior:

Browse mode:
  Full toolbar, address bar, profile controls

Cinema mode:
  Minimal toolbar, video-first

Compact mode:
  Small floating player, no browser controls, hover-reveal controls only
5. Theme / Appearance Settings Redesign

Recommended structure:

Appearance

Theme
[ Sharp Dark ] [ Minimal ] [ Soft Glass ] [ Blackout ]

Window Shape
Corners:
[ Square ] [ Small ] [ Soft ] [ Round ]

Border:
[ Off ] [ Neutral ] [ Accent ] [ Glow ]

Border Strength:
0% ---- 100%

Shadow:
[ Off ] [ Soft ] [ Strong ]

Transparency:
0% ---- 30%

Transparency should default low:

Default transparency: 0% or near 0%
Soft Glass transparency: low, controlled, not heavy
Blackout transparency: 0%
6. Recommended Mode Definitions
Browse Mode
Purpose:
Finding videos, changing profiles, editing URL, managing playback target.

Visible:
- address bar
- back/forward/home
- profile selector
- save/edit/delete
- popout button
- settings
Cinema Mode
Purpose:
Watching video in a clean large player.

Visible:
- video
- minimal titlebar
- pin/close/restore controls
- optional hover controls
Compact Mode
Purpose:
Small always-on-top player.

Visible:
- video
- very small chrome
- controls only on hover
Popout Mode
Purpose:
Detach playback into a separate floating media card.

Visible in main window:
- placeholder state
- "Show popout" button
- "Restore video here" button, if supported

Visible in popout:
- floating rounded video card
- minimal hover chrome
- optional pin/always-on-top
7. Visual Target for Popout

The popout should match this direction:

Floating rounded media card
Opaque or near-opaque black background
Subtle 1 px border
Soft shadow
Clean rounded clipping
No browser toolbar
No visible square backing layer
No heavy transparency
Controls visible only when needed

The rounded reference is the correct target. The current settings/theme implementation should be treated as an early control surface, not the final visual model.

8. Priority Fixes
P0:
- Fix compact mode not working.
- Make "Show popout" restore/focus the existing popout window.
- Prevent duplicate or unreachable popout states.

P1:
- Make corner settings affect the actual popout/window silhouette.
- Separate global app accent from profile color.
- Reduce transparency by default.
- Make theme presets visually distinct.

P2:
- Add border mode and border strength.
- Add shadow strength.
- Add hover-reveal chrome for compact/cinema mode.
- Add separate "Restore video here" action from the main placeholder.
9. Short Implementation Intent
PiPlay should separate content profiles from visual appearance.

Profiles define:
- URL
- name
- saved placement
- saved launch mode
- optional identity color

Appearance defines:
- global accent
- theme
- corner radius
- border
- shadow
- transparency

Popout state defines:
- whether playback is detached
- where the popout window is
- whether the main window should show a placeholder
- whether "Show popout" should focus/restore the popout

The main UX rule:

When the video is detached, the user must always have an obvious way to either show the popout again or restore p