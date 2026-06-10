# PiPlay UI Overhaul Address Outline

Status: draft outline for converting the discovery findings into implementation tickets  
Source material: `ui-overhaul-discovery/ui-state-notes.md` and `ui-overhaul-discovery/theme-system-overhaul-evaluation.md`  
Recommended timing: after the current functional bug pass, before the theme/settings refactor begins

## 1. Guiding rules

1. Fix behavior before polish. Resize, scroll, compact navigation, and settings clipping should be handled before the theme system is expanded.
2. Keep the current state evidence intact. The discovery states are useful even where two states appear nearly identical.
3. Do not collapse the “double” popout state yet. Treat it as an evaluation item until the runtime model is clearer.
4. Keep compact player as a playback behavior, not a visual theme.
5. Theme work should move toward presets plus one accent color, but should not remove old settings without migration.

## 2. Working state model

Use these names while implementing and testing:

| State | Meaning | Keep as separate? | Notes |
|---|---|---:|---|
| Source Home | YouTube home/feed in the main PiPlay WebView | Yes | Includes the case where YouTube’s own mini-player is visible inside the page. |
| Source Watch | Normal YouTube watch page in the main PiPlay WebView | Yes | Main source for manual popout target resolution. |
| Source Expanded Player | YouTube’s expanded/fullscreen-like player inside the source window | Yes | Avoid calling this true PiPlay fullscreen unless PiPlay hides its own chrome. |
| Popout Standard | Normal-page `PlayerWindow` with YouTube watch UI | Yes | Current standard popout. |
| Popout Fullview Faded | Normal-page `PlayerWindow` in a cleaner/faded presentation | Keep for now | This is the “double” state. It may later become a presentation variant of Popout Standard. |
| Compact Popout | `PlayerWindow` using the compact/embed shell | Yes | Different navigation and fullscreen behavior from standard popout. |
| Settings | Settings dialog with privacy, appearance, and playback controls | Yes | Needs scroll/restructure before adding theme controls. |

## 3. “Double” state policy

The current Popout Standard and Popout Fullview Faded states appear near-identical at the implementation level:

- both are `PlayerWindow`
- both use the normal-page WebView path
- both report `WindowVisualState = Normal`
- both share the same resize/scroll issue
- both use the same fade/top-strip behavior

Keep both states in the notes for now, but do not build separate application logic around them yet.

Recommended temporary model:

```text
PopoutStandard
  ├── ControlsVisible
  ├── ControlsHidden
  ├── FadedIdle
  └── FullviewLikeCapture
```

Decision rule for later:

Only keep “Fullview” as a real product state if it has at least one distinct invariant:

- a different WebView URL or playback mode
- a different PiPlay chrome policy
- a different window state or placement rule
- a different input/hit-test policy
- a different user command to enter/exit it
- a meaningfully different restore/return path

If none of those become true, merge it into `Popout Standard` as a presentation variant: `PopoutStandard.Faded` or `PopoutStandard.IdleClean`.

## 4. Priority 0: behavior bugs before UI refactor

These should be handled before the theme system, because they affect core usability.

### 4.1 PlayerWindow resize only works from the top edge

Observed:

- upper-left, upper-right, and top edge resize work
- left, right, bottom, and lower corners do not appear to resize
- this affects both Popout Standard and Popout Fullview Faded

Likely cause to investigate:

- top edge is over WPF chrome
- side/bottom edges are over the WebView2 child HWND / HwndHost area
- parent `WM_NCHITTEST` may not be receiving side/bottom hit tests through WebView2 airspace

Acceptance criteria:

- all four edges resize in normal window state
- all four corners resize in normal window state
- resize is disabled when maximized/fullscreen-like
- WPF controls such as fade, pin, and close still receive clicks correctly
- YouTube in-page controls remain usable near the edges

### 4.2 Scroll does not work in standard/fullview popout

Observed:

- scroll behavior fails or is unreliable in the normal-page popout
- PiPlay does not own the YouTube HTML scrollbar

Acceptance criteria:

- mouse wheel scroll works over the YouTube page area when expected
- touchpad scroll works when expected
- dragging YouTube’s own scrollbar works when expected
- resize hit zones do not steal ordinary scroll interaction

### 4.3 Compact popout opens suggestions externally

Observed:

- clicking compact YouTube recommendations can open another app/system browser
- current `NewWindowRequested` handling is too broad

Desired behavior:

- normal left-clicks to allowed YouTube watch URLs should stay inside PiPlay
- compact shell should rebuild/navigate the compact player target when possible
- explicit external intent should still open externally
- non-YouTube and unsafe URLs should still route externally or be blocked

Acceptance criteria:

- left-clicking an allowed YouTube video from compact mode keeps playback in PiPlay
- explicit external/new-window actions still go to the system browser
- tests cover `Core_NewWindowRequested` policy for compact mode

### 4.4 Compact fullscreen/expand affordance is unclear

Observed:

- the YouTube iframe expand/fullscreen icon is visible in compact mode
- clicking it currently does nothing useful
- PiPlay already has a host-side fullscreen/maximize action, but the iframe button does not trigger it

Options:

1. Add a PiPlay-owned compact expand/fullview button and do not rely on the YouTube iframe button.
2. Investigate WebView2 fullscreen permissions/events and wire the native iframe behavior if possible.
3. Suppress or de-emphasize unsupported expectations if the iframe button cannot be made reliable.

Acceptance criteria:

- compact mode has one reliable expand/fullview path
- user does not see a prominent control that appears broken
- host-side fullscreen/maximize behavior remains reversible

### 4.5 Settings window can clip on shorter displays

Observed:

- settings dialog is tall
- current layout uses `SizeToContent="Height"`, `ResizeMode="NoResize"`, and no `ScrollViewer`

Acceptance criteria:

- settings content is scrollable
- settings fits on shorter displays
- privacy actions, appearance settings, and playback settings remain reachable
- future theme controls can be added without growing the dialog indefinitely

## 5. Priority 1: UX clarity and product vocabulary

### 5.1 Popout button availability on YouTube home

Problem:

- `PopOutButton` can be enabled on YouTube home
- YouTube’s own mini-player may be visible inside the WebView
- URL-based popout target resolution may still fail because the page URL is home

Options:

- disable `PopOutButton` until a valid target is detected
- add DOM-based mini-player target resolution
- keep enabled but show a clear “No popout target found” message

Recommended first pass:

- add clearer disabled/empty/error behavior first
- treat DOM mini-player resolution as follow-up

### 5.2 YouTube mix/radio fallback is silent

Problem:

- `RD...` mix/radio playlists are unsupported for popout playlist preservation
- current behavior pops out only the current video and logs the fallback reason

Acceptance criteria:

- user-visible message explains that the current video was popped out, not the whole YouTube mix
- profile-saving behavior for mix URLs is explicitly decided

Open decision:

- saving a profile from an `RD...` mix URL should either preserve the original URL, normalize to the current video, or warn about popout limitations

### 5.3 Source “fullscreen” naming

Problem:

- YouTube’s expanded player fills the WebView area
- PiPlay title bar and toolbar remain visible
- `MainWindow.WindowVisualState` remains normal

Recommendation:

- call this `Source Expanded Player`, not true fullscreen
- only use “fullscreen” if PiPlay hides its own chrome or enters a real fullscreen/maximized mode

### 5.4 Compact mode applies to new popouts only

Problem:

- changing `Compact player` while a popout is open does not switch the open player
- this is intentional in current code, but easy to miss

Acceptance criteria:

- settings copy clearly says “applies to new popouts”
- optional follow-up: offer “reopen current popout in compact mode”

### 5.5 Existing popout button while popout is open

Problem:

- source window still shows `Pop out video`
- pressing it activates the existing popout instead of creating a new one

Recommendation:

- change label/state while `_player != null`
- possible copy: `Show popout`, `Focus popout`, or `Return to popout`

### 5.6 Top-edge reveal discoverability

Problem:

- faded/auto-hidden controls are visually good but hard to discover/capture

Options:

- make top-edge reveal band more forgiving
- add a brief first-run hint
- offer a non-collapsed mode as default
- keep auto-hide off by default for discoverability

## 6. Priority 1: accessibility cleanup

Add explicit `AutomationProperties.Name` for icon-only and templated controls.

Targets:

- main title bar: settings, minimize, maximize, close
- navigation: back, reload, home
- profile buttons: save, edit, delete
- state toggles: pin, auto-popout
- primary action: pop out video
- popout controls: fade, pin, close
- settings dialog close button
- profiles combo

Acceptance criteria:

- UI Automation does not expose glyphs as control names
- `PopOutButton` and `ProfilesCombo` no longer have empty accessible names
- names match the current visible/action state where applicable

## 7. Priority 2: settings and theme preparation

This should begin only after P0 behavior issues are handled or clearly deferred.

### 7.1 Restructure Settings

Recommended order:

1. Privacy
2. Appearance
3. Playback
4. Advanced

Move these into Advanced if they remain separate:

- fade delay
- active/idle opacity
- auto-hide top bar
- reset theme defaults

Keep `Compact player` under Playback.

### 7.2 Add theme model without removing legacy settings immediately

Add:

```csharp
public sealed class ThemeSettings
{
    public string ThemeId { get; set; } = "sharp-dark";
    public string AccentColor { get; set; } = "#2D6F8F";
    public string FadeDelayPreset { get; set; } = "normal";
    public bool? StripAutoHideOverride { get; set; }
    public double? ActiveOpacityOverride { get; set; }
    public double? IdleOpacityOverride { get; set; }
}
```

Keep compatibility with existing fields for at least one schema version:

- `PinAccent`
- `FadeAccent`
- `FadeIdleDelayMs`
- `ConstantWindowOpacity`
- `IdleWindowOpacity`
- `StripAutoHide`
- `CompactMode`

### 7.3 Start with accent chips, then add color wheel

First pass:

- Muted cyan
- Steel blue
- Violet
- Green
- Amber

The model should store a normalized hex value from day one, even before the wheel exists.

Later pass:

- add one accent color wheel
- derive hover/pressed/dim/glow/border tokens from that single accent

### 7.4 Theme presets

First-pass presets:

- `Sharp Dark`: default, compact, utility-style, muted blue-cyan accent
- `Minimal`: restrained daily-use shell
- `Soft Glass`: floating popout/overlay style, with opacity safety constraints

Defer:

- `Media Glow`, until glow/shadow tokens and visual QA are ready

### 7.5 Rounding tokens

Introduce radius as theme-owned tokens, not scattered hardcoded values:

```text
Radius.MainWindow
Radius.Popout
Radius.Button
Radius.Panel
Radius.Thumbnail
Radius.Settings
```

Stage the implementation because rounding touches:

- WPF border/template radii
- borderless window hit testing
- WebView2 airspace
- DWM rounded corners
- opacity/fade visuals

## 8. Priority 3: theme implementation sequence

Recommended order:

1. Add `PiPlayTheme`, `ThemeSettings`, `ThemeCatalog`, and migration tests.
2. Add generated theme resource tokens while keeping existing brush names as aliases.
3. Move key styles from hardcoded values to tokens.
4. Apply selected theme at app startup before windows are created.
5. Make Settings scrollable and restructured.
6. Add theme selector and accent chips.
7. Replace separate Pin/Fade color controls with the single accent path.
8. Thread theme opacity/fade defaults into popout behavior without changing compact/normal playback.
9. Add optional advanced overrides only after presets are stable.
10. Add color wheel after contrast-safe derived tokens are reliable.

## 9. Tests and QA captures

### Automated tests

- settings migration from old appearance fields
- invalid theme ID fallback
- invalid hex color fallback
- preset catalog uniqueness
- derived accent token generation
- basic contrast checks
- required XAML resources resolve
- settings contains a scroll container
- old pin/fade controls removed after theme replacement
- compact new-window policy
- popout return behavior still works after settings changes

### Manual QA states

Capture before/after screenshots for:

- Source Home
- Source Watch
- Source Expanded Player
- Popout Standard controls visible
- Popout Standard faded/idle
- Popout Fullview Faded, while still kept
- Compact Popout
- Settings

Manual checks:

- resize all edges/corners
- scroll inside normal-page popout
- compact recommendation click stays in PiPlay when expected
- compact expand/fullview path works
- top-edge reveal works with fade enabled
- theme switch applies consistently or clearly states next-launch/next-popout behavior
- reset app state restores default theme without clearing YouTube login
- clear browser data signs the user out as expected

## 10. Open decisions to keep visible

1. Should Popout Fullview Faded become a real state, or merge into Popout Standard as an idle/faded variant?
2. Should selecting a theme overwrite current active/idle opacity, or keep user opacity as overrides?
3. Should fade delay be theme-owned by default, or remain an independent user setting?
4. Should Soft Glass default to auto-hide top bar, or keep auto-hide off for discoverability?
5. Should theme changes update open popouts live, or apply to the next popout like compact mode?
6. How should PiPlay handle YouTube mix/radio profile saving?
7. Should source expanded player ever hide PiPlay chrome, or remain a WebView-only YouTube state?

## 11. Suggested ticket batches

### Batch A: stabilize popout behavior

- Fix PlayerWindow resize over WebView2
- Fix/verify scroll over normal-page popout
- Clarify source popout button state while popout exists
- Add compact new-window policy
- Add compact expand/fullview affordance

### Batch B: clarify UX and accessibility

- Add accessible names to icon-only controls
- Rename/source-document YouTube expanded player state
- Add mix/radio fallback message
- Add compact mode “new popouts only” copy
- Improve fade/top-edge reveal discoverability

### Batch C: unblock future settings work

- Add Settings scroll container
- Restructure Settings sections
- Move compact mode firmly under Playback
- Move fade/opacity/top-bar controls toward Advanced

### Batch D: theme foundation

- Add theme model/catalog/settings
- Add migration path
- Add theme resource tokens
- Add Sharp Dark, Minimal, Soft Glass
- Add accent chips

### Batch E: theme polish

- Replace old Pin/Fade color controls
- Add rounding tokens everywhere practical
- Decide opacity override behavior
- Add color wheel
- Add Media Glow only after visual QA
