# PiPlay Product & Engineering Specification

**Status:** Draft 0.13 (beta candidate)
**Project:** PiPlay
**Purpose:** Quality-first Windows desktop app for playing YouTube videos in a movable, resizable Video Popout window.
**Primary platform:** Windows desktop
**Primary user:** A power user who wants a reliable always-available YouTube video surface while working in other apps.
**Last updated:** 2026-07-15

---

## Contents

- [Conventions](#conventions)
- [1. Product vision](#1-product-vision)
- [2. Terminology and product language](#2-terminology-and-product-language)
- [3. Quality-first principles](#3-quality-first-principles)
- [4. Target experience](#4-target-experience)
- [5. Visual and interaction identity](#5-visual-and-interaction-identity)
- [6. Feature toggles: Auto, Fade, Pin](#6-feature-toggles-auto-fade-pin)
- [7. Fade, opacity, and transparency policy](#7-fade-opacity-and-transparency-policy)
- [8. Non-goals](#8-non-goals)
- [9. Recommended technical direction](#9-recommended-technical-direction)
- [10. Playback modes](#10-playback-modes)
- [11. High-level architecture](#11-high-level-architecture)
- [12. Component responsibilities](#12-component-responsibilities)
- [13. Video Popout lifecycle](#13-video-popout-lifecycle)
- [14. Return/close lifecycle](#14-returnclose-lifecycle)
- [15. WebView requirements](#15-webview-requirements)
- [16. Window quality requirements](#16-window-quality-requirements)
- [17. Profiles](#17-profiles)
- [18. Logging and diagnostics](#18-logging-and-diagnostics)
- [19. Security and privacy](#19-security-and-privacy)
- [20. Accessibility and usability](#20-accessibility-and-usability)
- [21. Packaging and release strategy](#21-packaging-and-release-strategy)
- [22. Quality gates](#22-quality-gates)
- [23. MVP scope](#23-mvp-scope)
- [24. Phase plan](#24-phase-plan)
- [25. Resolved defaults and open decisions](#25-resolved-defaults-and-open-decisions)
- [26. Implementation notes](#26-implementation-notes)
- [27. Reference notes](#27-reference-notes)
- [28. Document history](#28-document-history)

---

## Conventions

The key words **must**, **must not**, **should**, **should not**, and **may** are used in the sense of RFC 2119: *must* marks a hard requirement, *should* a strong recommendation that needs a reason to skip, and *may* a genuine option.

Normative requirements carry stable IDs so tests, issues, and pull requests can cite them:

- **Q-1 … Q-8** — the quality-first principles in section 3. These are the project's non-negotiable bar.
- **REQ-<AREA>-<n>** — other normative requirements, named by area (for example `REQ-POPOUT-03`, `REQ-WINDOW-02`). Assign IDs as requirements become tracked work; until then a section number plus this scheme is enough to reference any requirement unambiguously.

---

## 1. Product vision

PiPlay is a small Windows utility for watching YouTube videos in a movable, resizable **Video Popout** window. It should feel similar to browser picture-in-picture or Opera-style video popout, but the experience is controlled by PiPlay's own native Windows window rather than by browser-native PiP internals.

The app should feel boringly reliable: popping out should work, the video should continue from the expected timestamp, the original browser view should not produce duplicate audio, and the Popout Player should remember where it belongs.

The product goal is not to replace YouTube or build a custom media platform. The goal is to make YouTube playback behave like a high-quality desktop tool.

---

## 2. Terminology and product language

Use these terms consistently in the UI, code, docs, issues, and tests.

| Term | Meaning | Preferred usage |
|---|---|---|
| **PiPlay** | The app/product. | App title, installer, settings. |
| **Video Popout** | The user-facing feature that moves the current YouTube video into a floating player. | Button/tooltip: `Pop out video`. |
| **Popout Player** | The floating borderless playback window. | Window/state names, user docs. |
| **Source Window** | The main PiPlay browser window that launched the popout. | Engineering docs and diagnostics. |
| **Source Placeholder** | The black placeholder shown while the video is popped out. | UI copy: `Playing in Video Popout`. |
| **Pin** | Keep the active PiPlay surface above other windows. | UI toggle: `Pin`. |
| **Fade** | Hover/idle fading of Popout controls, chrome, or optional whole-popout opacity. | UI toggle: `Fade`. |
| **Auto** | Automatically pop out supported YouTube watch videos when enabled. | UI toggle: `Auto`; off by default. |

Prefer **Pop out video**, **Video Popout**, and **Popout Player** in user-facing UI.

`Detach`, `fake PiP`, and `PlayerWindow` are acceptable internal engineering terms, but should not be the main user-facing vocabulary.

---

## 3. Quality-first principles

Quality outranks scope.

A smaller app that behaves correctly is preferred over a larger app with clever but fragile features. Each release should preserve the following standards:

1. **[Q-1] No duplicate playback.** Popping out must not leave the source WebView playing audio behind the Popout Player.
2. **[Q-2] No lost user context.** Return from Video Popout should restore the expected video, timestamp, playback state, and window state where practical.
3. **[Q-3] No brittle hidden magic.** YouTube DOM injection may be used for pragmatic control, but it must be isolated, tested, and treated as best-effort.
4. **[Q-4] No unnecessary browser-runtime ownership.** Use the platform-provided WebView2 Evergreen runtime unless a future requirement proves that a fixed runtime is necessary.
5. **[Q-5] No invasive YouTube behavior.** Do not intercept credentials, bypass ads, download videos, remove required controls, or fake platform behavior in ways that create compliance or account risk.
6. **[Q-6] Recover cleanly.** Missing WebView2 runtime, broken settings, failed navigation, bad URLs, offline state, and popup/login edge cases should produce understandable behavior rather than crashes.
7. **[Q-7] Prefer native window quality.** Moving, resizing, topmost, monitor restore, close/return behavior, and DPI handling should be implemented natively and intentionally.
8. **[Q-8] Visible means interactable.** A visible Popout Player should remain directly controllable by the user; click-through/mouse pass-through transparency is not part of the current product direction.

---

## 4. Target experience

### 4.1 Core user story

As a user, I can open a YouTube video or playlist, press **Pop out video**, and get a floating Popout Player that I can move, resize, pin above other windows, and close when finished. Fade is a Phase 2 convenience feature, not required for the MVP.

### 4.2 Expected behavior

When Video Popout starts:

- The original YouTube view is paused.
- The original YouTube area shows a black Source Placeholder such as `Playing in Video Popout`.
- The Popout Player starts within 2 s of the expected source timestamp under the warm-WebView test condition, with a target of ≤1 s.
- The Popout Player can be resized, moved, pinned, and closed.
- A deliberate drag on passive video pixels moves the Popout Player after the system drag threshold;
  ordinary clicks and interactive player controls keep their YouTube behavior.
- Closing the Popout Player returns the Source Window without duplicate playback.

### 4.3 Quality target

PiPlay should feel like a native Windows media utility, not a hacked browser wrapper. The Source Window may contain a normal YouTube browsing surface, but the Popout Player should feel like a focused media object rather than a mini browser.

---

## 5. Visual and interaction identity

PiPlay should feel quiet, dark, polished, and utility-first.

### 5.1 Visual keywords

- Matte black.
- Soft rounded rectangles.
- Low-glow cyan/purple accents.
- Minimal chrome.
- Controls visible in MVP; hover/idle controls fade in Phase 2.
- High-contrast controls over video.
- Native Windows utility feel.
- Not flashy, not browser-clone-first.

### 5.2 Color tokens

Suggested tokens:

| Token | Suggested value | Use |
|---|---:|---|
| `AppBackground` | `#0B0D0E` | Main app/window background. |
| `SurfaceBase` | `#111316` | Popout shell and icon tile base. |
| `SurfaceRaised` | `#1C2025` | Raised controls, tiles, panels. |
| `SurfaceHover` | `#2C323A` | Hover backgrounds. |
| `BorderSubtle` | `#30363D` | Thin outlines and window borders. |
| `TextPrimary` | `#F3F5F7` | Primary text/icons. |
| `TextSecondary` | `#A7ADB4` | Labels, inactive text. |
| `AccentCyan` | `#2BAED0` | PiPlay brand/action accent. |
| `AccentCyanLight` | `#51BDD8` | Derived hover/light accent. |
| `AccentPurple` | `#6D3BFF` | Secondary popout/action accent. |
| `AccentViolet` | `#9E84F0` | Customization palette accent for active Pin/Fade controls. |
| `AccentGreen` | `#2DB57F` | Customization palette accent for active Pin/Fade controls. |
| `AccentAmber` | `#D69A2E` | Customization palette accent for active Pin/Fade controls. |
| `DangerPin` | `#FF4B55` | Destructive/danger states such as close/delete. Not used for Pin/Fade customization. |

YouTube red should remain YouTube-owned. PiPlay action accents should use cyan/purple rather than copying YouTube red.

### 5.3 Shape tokens

| Element | Radius | Notes |
|---|---:|---|
| Main app window | 0-8 px | Keep simple; respect Windows shell expectations. |
| Popout Player | 14-18 px | Rounded, compact, media-like. |
| Icon button | 10-14 px | 32x32 or 36x36 hit target. |
| App icon tile | About 22% of size | Matches the rounded-square icon direction. |

> **Outer-window corner ownership is split.** Source, Settings, prompts, and non-`Round` Popouts use
> the OS `DWMWA_WINDOW_CORNER_PREFERENCE` radii. A `Round` Popout additionally applies the resolved
> `PopoutFrame` radius (22 DIP for Soft Glass / explicit Round) as a DPI-scaled top-level Win32 window
> region. The region clips the child-HWND WebView2 without `AllowsTransparency=True`; it clears while
> maximized or snap-like and reapplies when floating. This reaches the large media-card silhouette,
> but it does not provide a guaranteed curve-following DWM border/shadow. See ADR-0008.

### 5.4 Icon style

- Use a simple line/glyph style with rounded caps and joins.
- Default menu/action icons should use an 18-20 px glyph inside a 32-36 px hit target.
- Stroke weight should feel consistent, roughly 1.75-2.25 px at 20 px size.
- Use `TextPrimary` for inactive icons, `AccentCyan`/`AccentPurple` for active or brand actions.
  Customized Pin/Fade active icons may use `AccentCyan`, `AccentViolet`, `AccentGreen`, or
  `AccentAmber`.
- App/taskbar icon family: the cyan play/out-arrow assets under `docs/assets/app-icon/` (`piplay.ico`, `piplay.svg`, `piplay-glyph.svg`, `piplay-small.svg`, `piplay-256.png`). These are the Windows app icon and Video Popout action identity.
- Logo/favicon family: the `P` monogram assets under `docs/assets/monogram-logo/` (`piplay-monogram.svg`, variants, and `piplay-favicon.ico`). These are the product logo, docs/marketing mark, and favicon family.
- The shipped app references `src/PiPlay/Assets/piplay.ico`; `docs/assets/app-icon/piplay.ico` is the reference copy for the app/taskbar icon family.
- Menu icons, title-bar icons, and placeholder icons should feel compatible with the app/taskbar family.

#### Icon rendering

- **[REQ-UI-02]** All glyph/line icons come from one consistent icon set so they share weight and style. Every icon **must** always render as its intended symbol; a missing or unresolved icon that shows as an empty box is a defect, not an acceptable fallback.
- Icons must render consistently wherever they appear — buttons, title bar, and placeholders alike — not only in some places.
- If the chosen icon set is unavailable at runtime, fall back to a readable text label and record it; never present an empty/placeholder glyph to the user.
- Acceptance (see section 22.2): every icon in the chrome renders correctly at common display scales, with no empty boxes.

### 5.5 Window taxonomy

PiPlay has two primary surfaces.

#### Main Window / Source Window

The Source Window is a dark native WPF host for YouTube browsing.

Required:

- Thin custom title bar.
- App name/logo on the left.
- Window controls on the right.
- Navigation controls: the MVP minimal set is **Back**, **Reload**, and **Home** (YouTube home). Forward is optional. A URL/search field is required. Any additional nav control is an intentional spec change, not implementer discretion.
- MVP utility controls: `Pin` and profile save/load. Profile Save/Edit/Delete share one native dark
  actions menu so the URL/search field remains useful at the minimum width. Phase 2 adds `Auto` on the
  Source Window and `Fade` in the Popout Player.
- The transfer action is **Pop out video** while docked and **Bring video back** while a player exists.
  A separate **Show Popout** action restores/focuses that player without closing or transferring it.
- The Source Window remains usable at its declared 760 x 480 DIP minimum. At compact widths, only the
  transfer action's visible label may collapse; its icon, tooltip, and accessible name remain.
- WebView2 content below the title bar.
- If pinned, show a clear but small active state.

#### Popout Player

The Popout Player is a borderless media window.

Required:

- No address bar.
- No browser tabs.
- No host-app browser chrome.
- Resizable from edges and corners.
- Draggable from the top chrome/empty shell area and, after a real drag threshold, passive video pixels.
- Video controls, progress/seek areas, links, menus, ads with actions, and end cards are never drag regions.
- Pin/topmost toggle.
- Settings gear that opens the same single Settings dialog used by the Source Window.
- Close button.
- Controls visible in MVP; hover/idle controls fade in Phase 2.
- Optional **Focused overlay** presentation for an Opera-style media-first surface. Standard remains
  the default, and both presentations keep the real Normal YouTube watch page.
- Remembered bounds and monitor placement.
- Clamp to visible monitors on restore.

Recommended size behavior:

```text
Minimum:       320 x 180
Default:       640 x 360 or previous size
Preset sizes:  480p, 720p, 1080p, quarter-screen
Resize model:  free resize by default
Aspect model:  optional aspect-lock toggle later
Video fit:     never crop video by default; allow letterboxing
```

### 5.6 Dark-theme completeness

PiPlay's shell is dark and custom-styled (section 5.1). Any control surface left in a platform-default style will clash with that identity, so dark styling is a requirement, not per-control discretion.

- **[REQ-UI-01]** Every control and every secondary surface it opens — dropdowns, menus, popups, and tooltips — must match the dark visual identity, in both its closed and open states. No light or platform-default surface may appear over PiPlay's chrome.
- Tooltips must be legible against the dark chrome and positioned so they do not cover the control they describe.
- A legitimately empty state (for example, no saved profiles yet) must still look intentional, not like a broken or blank surface.
- Acceptance is verified in section 22.2: open every menu, dropdown, popup, and tooltip and confirm each matches the dark identity and is correctly placed.

---

## 6. Feature toggles: Auto, Fade, Pin

Scope by phase:

- **MVP:** `Pin` plus basic profile save/load. Popout controls are always visible in MVP.
- **Phase 2:** controls fade, `Auto`, profile edit/validation, and small Pin/Fade appearance
  customization.
- **Phase 3:** compact-mode polish and any deferred playback/profile refinements.
- **Phase 4:** chrome fade and whole-popout opacity.

### 6.1 Auto

`Auto` means automatic Video Popout for supported YouTube watch videos.

Rules:

- Off by default.
- Implemented in Phase 2; never required for MVP.
- Must not trigger on YouTube home, search, settings, history, or login pages.
- Must not open more than one Popout Player.
- Must respect the same single-player lifecycle as manual popout.
- Must be easy to disable.
- **Trigger = playback-start** (decided 2026-06-06): `Auto` pops out a `/watch` video when it is
  playing, once for the currently armed video identity. Because autoplay is enabled, a freshly
  navigated video is usually already playing, so this is detected as "is playing", not a literal
  pause→play transition.
- **One Source-first identity:** resolve the visible Source Window URL first. Use YouTube's canonical
  URL only when the Source URL does not identify a video, and carry that exact resolved target into
  the manual-popout lifecycle. Detection and launch must not independently choose different video ids.
- **`/watch` only:** Shorts and embeds never auto-pop (they would otherwise pop on every scroll).
  Playlist autoplay-next pops each new video.
- **Enabling Auto pops immediately:** turning `Auto` on while a `/watch` video is already playing pops
  *that* video right away — not only on the next video.
- **No re-pop loop:** on every return path, arm the de-dup latch with the video that will be visible in
  the Source Window *before* source navigation, seek, or resume can expose a playing edge. The returned
  video therefore does not immediately re-pop; a different `/watch` video playing still pops normally.
- Detection is best-effort (Q-6): a DOM hiccup means no auto-pop, never a crash.

### 6.2 Fade

`Fade` controls hover/idle fading for Popout Player controls.

Rules:

- MVP controls remain visible; no controls-fade requirement for MVP.
- Phase 2 controls fade applies to Popout Player controls unless explicitly configured otherwise.
- Conservative by default.
- Hover or mouse movement always restores full controls.
- Chrome fade is Phase 4 polish.
- Whole-popout opacity is a Phase 4 advanced sub-setting, not the default meaning of Fade.
- Fade never implies click-through behavior.
- Phase 2 customization may expose constrained controls-fade idle-delay presets. The default remains
  2500 ms; initial presets are Short = 1500 ms, Normal = 2500 ms, and Long = 4000 ms.

### 6.3 Pin

`Pin` controls topmost behavior.

Rules:

- MVP requirement.
- Pin state should be visually obvious without being loud.
- Persist separate pin values for the Source Window and Popout Player.
- The title-bar Pin toggle should control the active PiPlay surface.
- The Popout Player must expose a direct Pin toggle, because it may be used without focusing the Source Window.
- While a Popout Player owns playback, Source topmost is temporarily suspended so a pinned Source cannot
  cover an unpinned player. Restore the Source's actual pre-popout Pin state on return and persist that
  captured intent if the app closes mid-popout; do not merge the two windows' preferences.
- Pin tooltips and accessible names describe the next action (`Pin` or `Unpin`) on both native surfaces.
- Phase 2 customization may expose a fixed dark-theme-safe active-color palette for Pin. The Source
  Window Pin and Popout Player Pin use the same configured Pin accent.

---

## 7. Fade, opacity, and transparency policy

Do not treat `transparent` as one feature. Split it into explicit behaviors.

### 7.1 Controls fade

Phase 2 quality path. MVP controls remain visible at all times.

- Controls are visible on hover/mouse move.
- Controls fade after idle timeout.
- Video remains fully opaque.
- Window remains fully interactive.

Suggested defaults:

```text
Idle timeout:          2500 ms
Fade duration:         150-220 ms
Idle control opacity:  0-20%
Hover control opacity: 100%
```

### 7.2 Chrome fade

Phase 4 optional polish.

- Popout shell/title controls dim when idle.
- Border/shadow remain subtle enough to locate the window.
- Hover restores full chrome.
- All three shipped presets default the popout top bar to auto-hide. Auto-hide engages only while
  `Fade` is on; turning Fade off keeps the strip visible.

### 7.3 Whole popout opacity

Phase 4 Opera-like appearance setting.

- The **In use** value applies to the Source Window title-bar **backdrop only** and to the entire active
  Popout Player. It does not fade the Source WebView or the title-bar controls as a group.
- The **When idle** value applies only to the entire Popout Player after the shared fade idle delay.
- Hover/mouse movement restores the Popout Player to its configured In-use value, which may itself be
  translucent.
- Minimum normal setting should not go below 45% unless the user explicitly unlocks it.
- Whole popout opacity must preserve normal mouse interaction with the Popout Player.
- This is not video-safe chrome/background transparency: current layered-window alpha affects the
  hosted video surface too. A future transparency feature needs an explicit scope/target model.
- Preset, corner, active/idle opacity, and accent changes preview live on the shared resources and every
  open PiPlay surface. Done commits; title-bar close, Escape, or any other non-affirmative dismissal
  restores the complete appearance that was active before Settings opened.

Current preset defaults (active / idle; top-bar auto-hide):

| Preset | Active | Idle | Auto-hide |
|---|---:|---:|---|
| Sharp Dark (`Crisp · 100%`) | 1.00 | 1.00 | On |
| Minimal (`Quiet · 94%`) | 0.94 | 0.86 | On |
| Soft Glass (`Glass · 82%`) | 0.82 | 0.72 | On |

The normal Settings slider floor is 45% for both opacity values.

### 7.4 Click-through transparency

**Non-goal for now.**

Click-through, mouse pass-through, or making the Popout Player ignore pointer events is out of scope. Opacity and fade are visual states only. A visible Popout Player should remain directly draggable, resizable, and controllable.

Do not implement click-through via layered-window styles, transparent hit testing, `WS_EX_TRANSPARENT`, global hotkeys as the only recovery path, or any mode that lets clicks pass through to apps behind the player. This can be reconsidered only after the normal Popout Player is excellent and there is a clear escape/recovery design.

---

## 8. Non-goals

PiPlay will not initially:

- Download YouTube videos.
- Block ads or alter YouTube monetization behavior.
- Replace the full YouTube browsing experience.
- Provide cross-platform Linux/macOS builds.
- Build a custom video decoder.
- Depend on undocumented browser-native PiP internals.
- Require a YouTube API key for basic playback.
- Store or inspect YouTube credentials.
- Support multiple simultaneous Popout Players.
- Support click-through or mouse pass-through transparent windows.
- Make WebView2 itself transparent.
- Use global hotkeys as required functionality.

---

## 9. Recommended technical direction

### 9.1 App shell

**Use WPF.**

Reasons:

- The project is Windows-first.
- The hardest product details are native window behavior, not web UI complexity.
- WPF gives mature control over custom windows, topmost behavior, resize behavior, monitor placement, and small desktop-utility UX.
- The existing draft already uses WPF and is architecturally close to the desired shape.

### 9.2 .NET target

**Target .NET 10 for the durable branch.**

Development may continue on .NET 8 briefly if it avoids unnecessary churn, but the quality-first release target should be `net10.0-windows`.

Recommended project settings:

```xml
<PropertyGroup>
  <OutputType>WinExe</OutputType>
  <TargetFramework>net10.0-windows</TargetFramework>
  <UseWPF>true</UseWPF>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
  <PublishTrimmed>false</PublishTrimmed>
  <PublishSingleFile>false</PublishSingleFile>
</PropertyGroup>
```

Rationale:

- .NET 10 is the better long-term target for a new Windows utility.
- Avoid trimming and NativeAOT early; WPF, XAML, WebView2, native loader files, and reflection-heavy UI paths are not where we should chase size optimization first.
- Avoid single-file publish initially to keep WebView2 native-loader behavior and diagnostics straightforward.

### 9.3 Browser runtime

**Use Microsoft Edge WebView2 Evergreen Runtime.**

Recommended approach:

- Use `Microsoft.Web.WebView2` NuGet package.
- Use a shared WebView2 user data folder for PiPlay.
- Use Evergreen runtime by default.
- Detect missing WebView2 runtime and show a friendly install/recovery message.
- Do not package Fixed Version WebView2 unless offline/kiosk/exact-runtime needs become real requirements.

### 9.4 Playback strategy

Use a native “fake PiP” architecture rather than browser-native PiP.

```text
Source Window
  WebView2: normal YouTube browsing
  Source Placeholder: black placeholder while popped out

Popout Player
  Native WPF floating window
  WebView2: popped-out YouTube playback
  Optional Focused overlay: real watch player, full viewport, no crop
```

This gives PiPlay control over size, placement, topmost behavior, closing, returning, saved profiles, and monitor restore.

### 9.5 Single instance

**[REQ-APP-01]** PiPlay runs as a single instance. Because the Source Window and Popout Player share one WebView2 user-data folder (sections 9.3 and 12.3), and that folder is locked to a single process, launching PiPlay while it is already running must not start a second process that contends for it. A second launch must activate and focus the existing instance — handing off the requested URL or profile to it where applicable — rather than open a new process or window. Detect the running instance with a named mutex (or equivalent single-instance primitive). If true multi-instance is ever required, each instance must use an isolated user-data folder; that is out of scope for now.

---

## 10. Playback modes

PiPlay should support multiple playback modes, but only one should be the default quality path.

> **"Playback mode" vs "UX/layout mode" (terminology).** The modes in this section describe how the
> *Popout Player* plays video. They are a different axis from any main-window UX/layout mode (e.g. the
> Browse / Cinema / Compact / Popout modes proposed in the 2026-06-23 owner review). In particular, the
> **Compact player** plumbing describes the popout's playback surface (Mode B/C), but new popouts
> currently resolve to Normal because `PlaybackModePolicy.CompactPlayerEnabled = false`; Settings no
> longer exposes a Compact player toggle. This is *not* a main-window compact layout, which is not
> implemented. Do not call a main-window layout "Compact" unqualified. See
> `SPEC_GAPS_AND_OWNERSHIP.md` (2026-06-23 owner appearance / popout / compact review).

### 10.0 Popout presentation — Standard or Focused overlay

Presentation is independent from playback mode. It changes how the real Normal `/watch` player is
laid out and controlled; it does not select the dormant Compact embed/IFrame architecture.

- **Standard** is the default and preserves the ordinary YouTube watch-page layout.
- **Focused overlay** is optional. It gives the real watch-page player the Popout viewport and adds
  a minimal Opera-style overlay: Mute, Captions, PiPlay Settings, Pin, Expand/Restore, Close,
  Play/Pause, Next, progress, and time. Captions and Next are best-effort handoffs to YouTube's own
  controls; Settings routes to the same Source-owned single-dialog workflow as the native strip.
- Focused layout must use `object-fit: contain`; `cover` is forbidden by default. Free resize may
  letterbox, but it must not crop the picture.
- YouTube's native controls, branding, settings, quality, caption menu, and ad UI remain available.
- The overlay is pointer-transparent except for its real buttons and seek rail, is keyboard named,
  and follows the Popout Fade setting/delay.
- `PlayerSettings.FocusedPresentation` is the global default for new Popout Players. Optional
  `Profile.Presentation` tokens (`standard` / `focused`) override it only for that profile's target
  video under `REQ-PROFILE-01`. Invalid or missing values normalize to the global default; Standard
  itself remains the safe default.
- Changing presentation affects new Popout Players; it does not rewrite an already-open page posture.

Focused presentation is best-effort under Q-3/Q-6. Selector failure leaves the ordinary page and
native Popout strip as the recovery surface. Compact remains dormant while
`PlaybackModePolicy.CompactPlayerEnabled=false`.

### 10.1 Mode A — Normal YouTube page mode

**Default mode.**

URL shape:

```text
https://www.youtube.com/watch?v=VIDEO_ID&t=123s
```

Optional playlist:

```text
https://www.youtube.com/watch?v=VIDEO_ID&list=PLAYLIST_ID&t=123s
```

Pros:

- Highest compatibility.
- Preserves normal YouTube UI, login/session state, Premium behavior, playlists, quality menu, captions, comments if visible, and keyboard shortcuts.
- Simplest path to a reliable MVP.

Cons:

- More visual clutter.
- Requires pragmatic JavaScript injection for pause, current time, and resume.
- YouTube SPA and DOM changes can break selectors.

Quality requirement:

- All YouTube DOM access must be centralized in a small helper/service. No scattered ad-hoc JavaScript strings across the app.

### 10.2 Mode B — Compact embed mode

**Optional mode for a cleaner detached player.**

URL shape:

```text
https://www.youtube.com/embed/VIDEO_ID?autoplay=1&start=123
```

Playlist shape:

```text
https://www.youtube.com/embed/videoseries?list=PLAYLIST_ID&autoplay=1
```

Pros:

- Cleaner visual surface.
- Better fit for a floating mini-player.
- Less page chrome.

Cons:

- Some normal YouTube page features may be missing.
- Embed restrictions and autoplay behavior may vary.
- Requires more testing around login, playlists, captions, and restricted content.

Quality requirement:

- Compact mode must be opt-in until it matches normal mode reliability for the common path.
- Compact embed/IFrame mode must respect YouTube embedded-player viewport constraints. Do not use the normal-mode 320x180 minimum for embed mode; prefer at least 480x270 for a 16:9 player with controls, or define a separate compact-mode minimum before shipping.
- **Resolved (Phase 3, Stage 1):** compact mode uses a separate **480x270** minimum window size, distinct from the 320x180 normal minimum. The minimums and the global/profile mode precedence are owned by the pure `PlaybackModePolicy` (`Profile.Mode` ?? global `PlayerSettings.CompactMode`).
- **Current v0.7.2+ state:** compact embed/shell mode is dormant. `ResolveEffectivePopoutMode` returns
  Normal while `CompactPlayerEnabled=false`; `Profile.Mode` and `PlayerSettings.CompactMode` remain
  reserved/migration data and must not be described as user-facing until compact is deliberately
  re-enabled.

### 10.3 Mode C — PiPlay shell mode with YouTube IFrame API

**Future quality upgrade for compact mode.**

Architecture:

```text
AppHtml/
  player.html
  player.js
  player.css

WPF Popout Player
  WebView2 navigates to https://piplay.local/player.html?... via virtual-host mapping
```

Benefits:

- More official control surface than scraping the normal YouTube page DOM.
- Better event handling for player-ready, state change, current time, playlist state, and error reporting.
- Cleaner bridge between C# and JavaScript using WebView2 messages.
- Best long-term fit for the polished borderless Popout Player visual style.

This should not block the MVP. It should be the quality path for a polished compact player.

---

## 11. High-level architecture

```text
PiPlay
├─ MainWindow / Source Window
│  ├─ Browser WebView2
│  ├─ Source Placeholder overlay/fallback
│  ├─ URL/navigation controls
│  ├─ Pin/profile controls (MVP)
│  └─ Pop out video command
│
├─ PlayerWindow / Popout Player
│  ├─ Player WebView2
│  ├─ custom borderless chrome
│  ├─ pin/topmost toggle
│  ├─ Standard or optional Focused presentation
│  ├─ passive-picture threshold drag bridge
│  ├─ always-visible controls (MVP); fade/chrome controls later
│  ├─ close/return lifecycle
│  └─ timestamp sync timer
│
├─ Services
│  ├─ WebViewEnvironmentService
│  ├─ SettingsService
│  ├─ YouTubeUrlHelper
│  ├─ YouTubeDomBridge
│  ├─ WindowPlacementService
│  ├─ ProfileService
│  └─ LoggingService
│
└─ AppData
   ├─ settings.json
   ├─ logs/
   └─ WebView2UserData/
```

---

## 12. Component responsibilities

### 12.1 MainWindow / Source Window

Responsible for:

- Hosting the normal YouTube browsing WebView.
- Accepting URL/search/profile input.
- Starting the Video Popout lifecycle.
- Showing and hiding the Source Placeholder.
- Resuming source playback when the Popout Player closes.
- Saving app-level state on close.

Not responsible for:

- Low-level YouTube DOM scripts.
- Window-placement serialization internals.
- Parsing all YouTube URL variants directly.

### 12.2 PlayerWindow / Popout Player

Responsible for:

- Hosting popped-out playback.
- Providing high-quality native window behavior.
- Applying the resolved Standard/Focused presentation for the lifetime of that Popout Player.
- Tracking last known timestamp.
- Saving/restoring window bounds and topmost state.
- Exposing a simple close/return signal to MainWindow.
- Presenting polished borderless media chrome.

Required controls:

- Close.
- Pin/unpin topmost.
- Settings (gear), routed to MainWindow's shared single-dialog Settings workflow.
- Drag surface.
- Threshold drag from passive video pixels, excluding every interactive surface.
- Resize edges/corners or resize border.

Nice-to-have controls:

- Minimize.
- Return/attach button.
- Opacity slider.
- Size presets: 480p, 720p, 1080p, quarter-screen.
- Aspect lock toggle.

### 12.3 WebViewEnvironmentService

Responsible for:

- Creating a shared `CoreWebView2Environment`.
- Owning PiPlay’s WebView2 user-data folder.
- Providing consistent runtime options for all WebViews.
- Detecting WebView2 initialization failure and returning useful errors.

Quality requirement:

- The Source Window and Popout Player should share session/cookies unless the user explicitly chooses an isolated mode later.

### 12.4 YouTubeUrlHelper

Responsible for:

- Extracting video IDs from common YouTube URL formats.
- Extracting playlist IDs.
- Preserving useful query state.
- Building watch URLs.
- Building embed URLs.
- Handling timestamp conversion.

Must support:

```text
https://www.youtube.com/watch?v=VIDEO_ID
https://youtu.be/VIDEO_ID
https://www.youtube.com/shorts/VIDEO_ID
https://www.youtube.com/embed/VIDEO_ID
https://www.youtube.com/watch?v=VIDEO_ID&list=PLAYLIST_ID
https://www.youtube.com/playlist?list=PLAYLIST_ID
```

### 12.5 YouTubeDomBridge

Responsible for all JavaScript interaction with normal YouTube pages.

Minimum methods:

```csharp
Task<PlayerState?> ReadPlayerStateAsync(CoreWebView2 webView);
Task PauseAsync(CoreWebView2 webView);
Task PlayAsync(CoreWebView2 webView);
Task SeekAndPauseAsync(CoreWebView2 webView, int seconds);
Task SeekAndPlayAsync(CoreWebView2 webView, int seconds);
Task ApplyPlaybackSettingsAsync(CoreWebView2 webView, double? volume, bool? muted, double? playbackRate);
Task<string?> ReadCanonicalUrlAsync(CoreWebView2 webView);
```

Preferred selector strategy:

```js
const v =
  document.querySelector('#movie_player video.html5-main-video') ||
  document.querySelector('video.html5-main-video') ||
  document.querySelector('video');
```

Quality requirements:

- Failed script execution must not crash the app.
- If the timestamp cannot be read, Video Popout should still work with a sensible fallback.
- JavaScript strings should be centralized and tested.
- Focused layout/overlay and passive-surface drag selectors must remain centralized here, installed
  before navigation by dedicated lifecycle/message bridges, and covered by deterministic script tests.
- Page-to-host requests use versioned nonce-bearing closed protocols and accept only HTTPS YouTube
  message/current sources. A drag request carries no pointer coordinates; Focused may request only
  Close, Pin toggle, Expand/Restore, or the existing Source-owned Settings workflow from the host.

### 12.6 SettingsService

Responsible for:

- Loading settings.
- Sanitizing nulls and corrupted data.
- Saving settings atomically.
- Versioning the settings schema.

Settings should be stored under the user profile, for example:

```text
%LOCALAPPDATA%/PiPlay/settings.json
```

Save format should include:

```json
{
  "schemaVersion": 4,
  "lastUrl": "https://www.youtube.com/",
  "mainWindow": {
    "topmost": false,
    "placement": null
  },
  "player": {
    "placement": null,
    "topmost": true,
    "compactMode": false,
    "focusedPresentation": false,
    "fadeEnabled": false,
    "idleWindowOpacity": 1.0,
    "lastWidth": 960,
    "lastHeight": 540
  },
  "profiles": []
}
```

Quality requirements:

- Never lose settings due to partial write.
- If settings are corrupt, rename the bad file and start clean.
- Do not store YouTube cookies in settings; WebView2 profile owns browser session state.

### 12.7 WindowPlacementService

Responsible for:

- Saving player window placement.
- Restoring placement.
- Clamping windows to visible monitor work areas when monitor configuration changes.
- Handling DPI changes.

Quality requirement:

- PiPlay must not restore the Source Window or Popout Player fully off-screen.

---

## 13. Video Popout lifecycle

### 13.1 Preconditions

Video Popout is allowed only when:

- Main WebView is initialized.
- No popout operation is already in progress.
- No existing Popout Player is active.
- No return/replay transition is in progress.
- Current page contains a supported YouTube video or playlist URL.

If a player already exists, **Show Popout** activates it and **Bring video back** starts the return path;
neither command creates another player.

### 13.2 Sequence

```text
User clicks Pop out video
  ↓
Disable Popout button / set popoutInProgress
  ↓
Read current page URL and player state
  ↓
Capture source timestamp and sourceWasPlayingAtPopout before pausing
  ↓
Parse video ID, playlist ID, timestamp
  ↓
Pause source WebView video
  ↓
Show Source Placeholder
  ↓
Hide source WebView if using standard WPF WebView2 control
  ↓
Create Popout Player with selected mode URL
  ↓
Initialize Popout Player WebView2 using shared environment
  ↓
Navigate player to target URL
  ↓
Start timestamp sync timer
  ↓
Clear popoutInProgress / expose Bring video back + Show Popout
```

### 13.3 Source Placeholder behavior

Preferred text:

```text
Playing in Video Popout
```

Visual target:

- Black rectangle in the YouTube player region.
- Small PiPlay popout icon near the text or centered.
- Keep the surrounding YouTube page visible where practical.

> The Source Placeholder exposes two explicit recovery actions. **[Show popout]** restores/focuses the
> existing player without closing it. **[Bring video back]** captures fresh return state, closes the
> player, and drives the normal return path. These actions must not share a handler.

While Tier 1 hides the entire WebView, Back/Reload/Home, the URL field, profile selection, and profile
Save/Edit/Delete are disabled. Auto remains available to turn off, and the two recovery actions remain
available. Hidden navigation is prohibited because its result cannot be inspected and can invalidate
return assumptions.

Implementation tiers:

| Tier | Description | Use |
|---|---|---|
| Tier 1 | Hide entire source WebView and show PiPlay black placeholder. | MVP reliability fallback. |
| Tier 2 | Inject a black DOM overlay into the YouTube player rectangle. | More browser-like visual, but more fragile. |
| Tier 3 | WPF overlay aligned over player rectangle using `WebView2CompositionControl`. | Polished future implementation. |

Quality rule: Tier 1 must always remain available as fallback.

Because the standard WPF WebView2 control can render above WPF elements, the quality-first overlay implementation is:

```csharp
Browser.Visibility = Visibility.Hidden;
SourcePlaceholder.Visibility = Visibility.Visible;
```

On return:

```csharp
SourcePlaceholder.Visibility = Visibility.Collapsed;
Browser.Visibility = Visibility.Visible;
```

A later version may switch to `WebView2CompositionControl` if we need translucent overlays or WPF UI layered over the WebView.

### 13.4 Race prevention

Video Popout must be guarded:

```csharp
if (!_browserReady || _popoutInProgress || _returnInProgress ||
    _player is not null || _clearingBrowserData || _mainWindowClosing)
    return;
```

The action state is explicit: **Ready** (`Pop out video`), **Open** (`Bring video back` plus
`Show Popout`), or **Returning** (disabled until same-video scripting or different-video navigation and
state replay completes, fails, or times out). Manual and Auto launch use the same gate.

### 13.5 Failure behavior

If popout fails after source pause:

- Hide Source Placeholder.
- Show source WebView.
- Attempt to resume source playback if it was playing before.
- Show a concise error message.
- Log the exception details.

---

## 14. Return/close lifecycle

When the Popout Player closes:

```text
Popout Player closing
  ↓
Capture last known timestamp, paused state, volume, mute, playback speed, bounds, topmost state, fade state
  ↓
Stop sync timer
  ↓
Notify Source Window
  ↓
Source Window hides Source Placeholder and shows source WebView
  ↓
Source Window restores its pre-popout Pin state, restores from minimized if needed, and activates
  ↓
If the popout ended on a different video, Source Window navigates there and replays captured playback state after the source video element is ready
  ↓
Otherwise Source Window seeks source video to last known timestamp if available
  ↓
Source Window resumes playback only if REQ-RETURN-01 allows it
  ↓
Settings are saved before and after source-return scripting so popout placement survives return-script failure
```

Important details:

- `LastKnownSeconds` must be nullable.
- `0` is a valid known timestamp.
- Unknown timestamp and known-zero timestamp must not be conflated.
- **[REQ-RETURN-01]** Source playback follows the Popout Player's live paused/playing state when that
  state is known at return. If the popout paused state is unknown, fall back to whether the source was
  playing when Video Popout started.
- `sourceWasPlayingAtPopout` is captured before PiPlay suppresses the source and is a fallback only.
- User return must restore and activate a minimized Source Window without forcing a previously maximized
  Source to Normal. App shutdown captures state but must not restore or steal focus.
- Keep the Source action in **Returning** and block Auto/manual re-entry until return scripting and any
  pending post-navigation replay reaches a terminal state.
- Source and Popout Pin preferences stay independent. Return restores the actual Source Pin state that
  was active before launch, including a profile-derived state.
- **[REQ-RETURN-07]** If the source was paused at popout launch, PiPlay must not auto-nudge the Popout
  Player into playing; a return to playing state from that path must come from user action inside the
  popout. Launch intent is passed into the Popout Player for the whole session, not just suppressed as
  a one-shot nudge. (PiPlay does not force-pause a watch page that autoplays on its own — that residual
  is a runtime-QA concern, not a guarantee.)

Recommended model:

```csharp
public int? LastKnownSeconds { get; private set; }
public bool? PopoutPausedAtReturn { get; private set; }
public double? PopoutVolumeAtReturn { get; private set; }
public bool? PopoutMutedAtReturn { get; private set; }
public double? PopoutPlaybackRateAtReturn { get; private set; }
public bool SourceWasPlayingAtPopoutFallback { get; private set; }
```

---

## 15. WebView requirements

### 15.1 Shared environment

All WebViews should use the same `CoreWebView2Environment` and user data folder so that YouTube login/session behavior is consistent.

### 15.2 Navigation and new-window handling

Handle both top-level navigation and `CoreWebView2.NewWindowRequested` in the Source Window and Popout Player.

The allowlist is a **guardrail against accidental drift** — stray links, ad click-throughs — **not a hard security boundary**. Its job is to keep PiPlay on YouTube without ever getting in the way of a legitimate Google sign-in. When in doubt, do not block a real login.

**[REQ-NAV-01] Source Window policy:**

- Allow YouTube navigation in the Source Window: `youtube.com` subdomains, `youtu.be`, and `youtube-nocookie.com` subdomains.
- Allow the Google sign-in/account domains used by YouTube login, **including regional/country-specific variants** (any Google sign-in domain, not only the `.com` ones). A real login must not be bounced out to the system browser.
- Cancel only genuinely unrelated top-level/new-window navigations and open them in the system browser without a per-link prompt. General Google browsing (search, maps, and the like) is not part of the allowed surface.

**[REQ-NAV-02] Popout Player policy:**

- Keep the Popout Player on YouTube playback **and the same Google sign-in surface**, so a sign-in/consent redirect never dead-ends the player.
- The Popout Player must not wander onto unrelated sites.
- Cancel unrelated navigation in the player. For user-initiated HTTP(S) new-window requests, open externally or show a non-blocking note; never replace the player with the external page.
- Log blocked/redirected domains without logging full credential-bearing URLs.

### 15.3 Navigation failure

On navigation failure:

- Show a compact in-app error state.
- Provide retry.
- Preserve the URL for debugging.
- Do not crash.

### 15.4 Runtime failure

If WebView2 runtime is missing or broken:

- Show a clear message.
- Explain that PiPlay requires Microsoft Edge WebView2 Runtime.
- Provide install/retry instructions in the installer or release notes.

---

## 16. Window quality requirements

### 16.1 Popout Player behavior

The Popout Player must:

- Be borderless/chromeless from the host-app perspective.
- Move smoothly by dragging the top shell/empty chrome area or deliberately dragging passive video
  pixels past the system drag threshold.
- Resize predictably from edges and corners.
- Remain usable at 320x180 in normal page mode; compact embed/IFrame mode may require a larger minimum.
- Remember size and position.
- Clamp to visible monitor work area at startup.
- Honor topmost state.
- Avoid stealing focus unnecessarily.
- Close cleanly.
- Preserve direct mouse interaction even when faded or partially transparent.

### 16.2 Dragging behavior

Default:

- The 44 DIP top shell/title strip remains an immediate, full-height native move region and recovery
  path. Its blank handle area shows a move cursor; the adjacent buttons retain ordinary input.
- A primary mouse/pen gesture may arm on passive pixels inside the real YouTube player. It becomes a
  window move only after horizontal or vertical movement crosses the system drag threshold; below
  threshold the click is untouched.
- Once the threshold is crossed, suppress that gesture's click, release DOM pointer capture, and
  post native `WM_SYSCOMMAND / (SC_MOVE | HTCAPTION)` movement only while the left button is still
  physically down and the window is in its Normal state. The host listens only to top-document web
  messages; recursive child-frame event wiring is prohibited after a deployed WebView2 crash repro.
- Unused YouTube top/bottom chrome may arm a drag and still receives an ordinary click below
  threshold. Never arm over buttons, links, inputs, menus, rendered caption containers,
  timeline/progress/volume areas, settings/captions/fullscreen controls, PiPlay overlay controls, end
  cards, or ads with actions.
- The same behavior applies to Standard and Focused presentation. It introduces no click-through,
  coordinate-bearing host message, global hook, or transparent WebView.

Future optional behavior:

- `Alt + drag anywhere`.
- A temporary Move mode button.

### 16.3 Resize behavior

Required:

- Native-feeling edge and corner resize.
- Free resize by default.
- Letterbox rather than crop video when aspect ratio differs.
- Focused presentation explicitly enforces `object-fit: contain`; it must never silently switch to
  `cover` to fill a mismatched aspect ratio.
- **[REQ-WINDOW-02]** Phase 3 window-quality target: borderless windows use an edge resize hit area
  owned by the top-level window because the WebView2 child consumes hit testing over the video. The
  current owner-tuned implementation target is a 12 DIP black edge band for mouse/pen use and a 96 DIP corner
  length for diagonal resize. The corner length is measured along the edge band; it is not a full
  96 x 96 DIP square that steals clicks from content. A visible outline, if drawn, should stay subtle
  (0-2 px).

Future optional behavior:

- Aspect lock.
- Size presets.
- Snap to common video ratios.

### 16.4 Multi-monitor behavior

Required cases:

- Restore to the same monitor when available.
- If the previous monitor is gone, restore to the nearest visible work area.
- Respect DPI scaling changes between sessions.
- **[REQ-WINDOW-01]** Declare per-monitor DPI awareness (PerMonitorV2) in `app.manifest`. WPF + WebView2 across mixed-DPI monitors must stay crisp (tested in section 22.2).
- A saved Source placement is raised to the declared 760 x 480 DIP minimum before native restore, using
  the saved DPI scale. Interactive borderless resize also preserves each window's DPI-scaled WPF
  `MinWidth` / `MinHeight` in `WM_GETMINMAXINFO` while retaining the monitor work-area maximize bounds.

### 16.5 Keyboard behavior

Minimum:

- `Esc` should not accidentally close the player while video is focused unless deliberately implemented.
- Standard YouTube shortcuts should work when the WebView is focused.
- `Ctrl+L` and `F6` focus and select the Source URL/search field while Source navigation is available.

Future:

- Global hotkeys for popout/return/pin should be opt-in and conflict-aware.

---

## 17. Profiles

Profiles are named saved launch targets.

MVP support:

- Save a basic named profile.
- Load a basic named profile.
- Keep Pin/topmost and placement when already available.

Phase 2 support:

- Edit and delete saved profiles from the Source Window.
- Validate names and URLs before saving in the edit path.
- Apply per-field precedence for optional profile fields that the profile explicitly carries.

Phase 2 profile model:

```json
{
  "name": "Compiler videos",
  "url": "https://www.youtube.com/playlist?list=...",
  "mode": null,
  "presentation": null,
  "accentColor": null,
  "topmost": true,
  "fadeEnabled": null,
  "bounds": {
    "x": 2200,
    "y": 140,
    "width": 960,
    "height": 540,
    "monitorDeviceName": "\\\\.\\DISPLAY2",
    "monitorWorkArea": { "x": 1920, "y": 0, "width": 2560, "height": 1400 },
    "dpiScale": 1.25
  }
}
```

Quality requirements:

- Duplicate profile names should prompt overwrite/rename instead of silently creating clutter.
- Profiles validate URLs before saving in the Phase 2 edit path.
- Broken profile URLs must fail gracefully even in MVP.
- **[REQ-PROFILE-01]** For fields that a profile is allowed to carry, a launched profile overrides the global default per field. Unset/null fields fall back to the global value.
- `accentColor` is an optional per-profile **identity color**, and it **drives the app accent** (roadmap **P2**, owner-confirmed 2026-07-14). The effective app accent = the active profile's `accentColor` when it has a valid one, else the global `theme.accentColor`. A profile with no color of its own **inherits** the global accent rather than blanking the app out. Selecting a profile therefore re-tints PiPlay: the toolbar glyphs, the primary action, and the Source Window title-bar wash.
  - This **reverses the v0.6.0 identity-only split**, which had held the profile color to a selector rail. The reason: the app accent reached almost nothing — in the normal window it painted the Pop-out button and a title-bar wash tuned so low it was near-imperceptible — so a per-profile accent changed one button and was not worth having. P2 was made worth having by giving the accent reach first. Design: `docs/superpowers/specs/2026-07-14-profile-accent-reach-design.md`.
  - **Accent reach.** The functional toolbar row (Back / Reload / Home / Save / Edit / Delete) carries the accent. The window-management caption row (Settings / Minimize / Maximize / Close) stays neutral: the accent wash already sits behind it, so accenting those glyphs too would place accent on accent-tint. The accent adds **no** border, line, or fill anywhere — it re-colors chrome that already exists. The 0–100 reach preference defaults to 50. Its wash scales linearly over the full range (none at 0, the v0.9.0 1.45 target at 50, restrained 1.90 ceiling at 100), while toolbar glyphs reach the full presentation accent by 50 and remain full above it. Therefore 50 reproduces v0.9.0 exactly; lower values fade the reach and higher values deepen only the wash.
  - **Editing.** Settings' accent picker edits whatever is currently painting the app — the active profile's color when that profile is overriding, the global default otherwise — and names its target in the hint above the picker, so a live preview is always truthful and Done always sticks. Done writes only that named target: applying an unrelated Appearance change with a colored profile active must not overwrite `Theme.AccentColor`. A preset switch may adopt a new preset default only for the global target; a profile-owned color remains exact even when it happens to equal the old preset default. A profile's color is also editable in the profile editor.
  - An optional active-profile popout border remains a future enhancement.
  - **Contrast contract.** Stored global/profile colors remain **exact**; only non-text *presentation*
    colors are minimally adjusted, and only enough to stay visible. The rail is lifted to at least a
    **3:1** ratio (`ThemeColors.EnsureContrast`, default `minimumRatio = 3.0`) against **`SurfaceHover`**,
    which is deliberately the reference: it is the lightest surface the rail can sit on, so a color that
    clears 3:1 there stays discernible on `SurfaceRaised`, `SurfaceBase`, and `AppBackground` too. The
    Source Window title-bar accent wash is held to a lower floor against `SurfaceBase` — it is a tint, not
    a cue, and must stay restrained. A profile with **no** color yields a fully transparent rail while the
    gutter is retained, so rows do not shift when a color is added or removed.
- **[REQ-PROFILE-02]** Profiles store both bounds and monitor identity. Restore to the saved monitor when present; otherwise clamp to the nearest visible work area using `WindowPlacementService`.
- Compact-mode placement exists as reserved data (`PlayerSettings.CompactMode` plus optional
  `Profile.Mode` override), but the user-facing Compact player is dormant in v0.7.2+. New popouts
  force Normal while `PlaybackModePolicy.CompactPlayerEnabled=false`.
- Popout presentation is an independent, live option: optional `Profile.Presentation` values
  (`standard` / `focused`) override `PlayerSettings.FocusedPresentation` per target video. It must
  not reuse or reactivate `Profile.Mode`.

---

## 18. Logging and diagnostics

Add lightweight file logging.

Log location:

```text
%LOCALAPPDATA%/PiPlay/logs/piplay.log
```

Log these events:

- App startup/shutdown.
- WebView2 environment initialization.
- Navigation failures.
- Video Popout start/success/failure.
- Player close/return.
- Settings load/save failure.
- Runtime missing/broken state.

Do not log:

- Cookies.
- Authorization headers.
- Full credential URLs.
- User-entered search text unless needed and sanitized.

---

## 19. Security and privacy

PiPlay should be a respectful WebView host.

Requirements:

- Do not inspect or store YouTube credentials.
- Do not implement video downloading.
- Do not bypass ads, DRM, region restrictions, age gates, or playback restrictions.
- Do not inject scripts that alter YouTube monetization or required controls.
- Keep JavaScript injection limited to local playback/presentation interaction: read current time,
  pause, play, seek, read the canonical URL, threshold-detect a passive-picture drag, and apply the
  optional Focused no-crop layout/controls. Do not hide required YouTube controls or alter ads.
- Treat WebView2 user data as private browser data.
- **[REQ-PRIVACY-01]** (**Phase 2** — deferred from MVP per section 23.) `Reset app state` clears PiPlay app settings, profiles, and placement from `settings.json` while keeping WebView2 browser data intact so the user stays logged into YouTube.
- **[REQ-PRIVACY-02]** (**Phase 2** — deferred from MVP per section 23.) `Clear browser data` is a separate confirmed action that clears PiPlay's WebView2 user-data folder/profile and logs the user out of YouTube. It must be worded separately from app reset.

> Scope note: `Reset app state` and `Clear browser data` are not part of the MVP (section 23 lists them under "MVP should defer"); they arrive in Phase 2. Until then, the only way to reset is to remove PiPlay's stored data directly (the settings file and WebView2 user-data folder described in sections 12.6 and 9.3). The QA checklist tests these actions only in its Phase 2 section.
- Do not implement click-through windows as a hidden privacy/security risk.

---

## 20. Accessibility and usability

Minimum quality bar:

- Buttons have text labels or tooltips.
- Pin state is visually obvious.
- Fade state is understandable.
- Settings is directly reachable from both the Source Window and Popout Player, and both entry points
  activate the same single dialog rather than opening competing copies.
- Close button is easy to find.
- Error messages are readable.
- Main controls are keyboard reachable where practical.
- Focused overlay buttons and progress have accessible names and visible keyboard focus.
- Dark UI has sufficient contrast.
- Popout remains recoverable and interactable at all opacity settings.

Future:

- High contrast mode handling.
- Screen reader names for controls.
- Configurable title bar size.
- Larger hit targets setting.

---

## 21. Packaging and release strategy

### 21.1 Development builds

Recommended:

```bash
dotnet publish -c Release -r win-x64 --self-contained false
```

Use for personal machines where the matching .NET runtime is already installed.

### 21.2 Shareable builds

Recommended:

```bash
dotnet publish -c Release -r win-x64 --self-contained true
```

Use for sharing with other machines.

Initial release style:

- Normal publish folder or simple installer.
- No trimming.
- No single-file publish.
- Include WebView2 runtime check or installer guidance.
- **[REQ-RELEASE-01]** Sign the published executable and any installer with the SevIQ code-signing certificate before distribution, to establish provenance and avoid SmartScreen warnings. **Status: deferred / non-gating** — signing is optional today and is not part of release evidence (provenance comes from the exact-source commit, the stable tag, and `Verify-StableDeploy.ps1`). This requirement revives if PiPlay is ever distributed publicly under a real, non-self-signed certificate. See `SPEC_GAPS_AND_OWNERSHIP.md`.

Future:

- MSIX or installer if distribution becomes formal.
- Auto-update only after the app is stable.

---

## 22. Quality gates

A build should not be called “release candidate” until the following pass.

### 22.1 Functional tests

| Area | Test | Expected result |
|---|---|---|
| Basic navigation | Open youtube.com | Page loads without crash |
| Watch URL | Open video URL | Video page loads |
| Video Popout timestamp | Press Pop out during playback | Warm WebView: after the Popout Player has been playing for about 3 s, player timestamp is within 2 s of expected source timestamp plus elapsed time; target ≤1 s |
| Source pause | Pop out during playback | Source audio stops |
| Placeholder | Pop out | Source Placeholder visible, no WebView bleed-through |
| Close player | Close Popout Player after source was playing | Source returns at timestamp and follows the popout's live play/pause state |
| Close paused source | Pop out while source is paused, then close without pressing play | Source returns at timestamp and stays paused (`REQ-RETURN-01`) |
| Paused source, user plays in popout | Pop out while source is paused, press play in the popout, then close | Source returns at timestamp and resumes (`REQ-RETURN-01`) |
| Timestamp zero | Seek player to 0 and close | Source returns to 0, not stale timestamp |
| Double-click popout | Rapidly click Pop out | Only one player opens |
| Playlist watch URL | Pop out `watch?v=X&list=PL...` | Preserves video `X` and playlist context |
| Playlist page | Pop out `playlist?list=PL...` | Starts the first playable item in the playlist |
| Mix/radio fallback | Pop out unsupported list such as `list=RD...` or restricted/radio list | Pops out the current single video with a non-blocking note; popout does not fail |
| Source external link | Open a non-YouTube link from Source Window | Link opens in the system browser; PiPlay WebView remains on the allowed surface |
| Popout external link | Trigger off-YouTube navigation in Popout Player | Player does not navigate away from YouTube; request is blocked or opened externally |
| Login popup | Trigger sign-in/new window | App handles allowed Google auth surface intentionally |
| Missing runtime | Simulate missing WebView2 | Friendly message, no crash |
| Corrupt settings | Break settings.json | App recovers with defaults |
| Monitor removed | Restore after monitor change | Player appears on visible monitor |
| Network loss | Disconnect network | Error state, no crash |
| Phase 2 controls fade | Let player idle, then hover | Controls fade and restore reliably |
| Passive-picture drag | Click video, then deliberately drag passive picture pixels | Click still plays/pauses below threshold; deliberate drag moves the window |
| Drag exclusions | Try controls, timeline, links, menus, captions/settings, end cards, ads with actions, and Focused controls | Each remains interactive and never starts a move |
| Focused presentation | Enable Focused overlay and pop out a `/watch` video | Real watch player fills the viewport with no crop; overlay controls work where YouTube permits |
| Presentation precedence | Exercise global Focused plus profile Standard/Focused/inherit | Profile target override wins per field; inherit follows global; Compact remains dormant |
| Popout Settings | Open Settings from the Popout gear, then invoke the Source gear | The same Settings dialog activates; no second dialog opens |
| Phase 4 opacity | Select each preset and exercise active/idle | Source title-bar backdrop follows active only; whole Popout follows active/idle and remains interactable |
| Appearance preview rollback | Preview a preset and corner, then dismiss without Done | All open surfaces revert to the complete pre-dialog appearance |

### 22.2 UX tests

- Main window looks intentional, not like an accidental browser wrapper.
- Popout Player has no address bar, no tabs, and no OS border except custom shell.
- Player drag feels immediate.
- Passive-picture click remains a click until the drag threshold; passive-picture drag then feels native.
- Resize does not produce jarring artifacts.
- Topmost toggle is obvious.
- Fade toggle is understandable when Phase 2 fade ships; MVP controls remain visible.
- Close behavior is predictable.
- App does not steal focus unexpectedly.
- Source Placeholder copy is understandable.
- Icons share stroke weight, corner style, and active color behavior.
- Popout remains usable at 320x180 in normal page mode; compact mode has its own minimum before release.
- Focused presentation uses `contain` at wide, tall, and free-resized ratios: letterboxing is allowed,
  cropping is not. Its overlay is pointer-transparent outside actual controls and seek rail.
- Borderless resize zones are easy to acquire: left/right/top/bottom edges expose the configured
  edge resize border, corners expose diagonal resize over the configured corner length, and caption
  or player controls remain clickable outside the outer resize band.
- App remains crisp at 100%, 125%, 150%, and mixed-monitor DPI.

#### Chrome acceptance (binary)

These are pass/fail, not subjective. A build is not a release candidate until every row passes. Capture current screenshots as proof and keep them with the release evidence (the project's screenshot-evidence procedure).

| ID | Check | Pass condition |
|---|---|---|
| UI-CHK-1 | All chrome icons render (window controls, navigation, save, Pin, Pop out, Popout Settings, placeholder). | Every icon shows its intended symbol; **zero** empty boxes. (REQ-UI-02) |
| UI-CHK-2 | Profiles dropdown, closed. | Renders dark, matching the chrome — not a light platform control. (REQ-UI-01) |
| UI-CHK-3 | Profiles dropdown, open. | The list and its items render dark; an empty list still looks intentional, not a blank light box. (REQ-UI-01) |
| UI-CHK-4 | Tooltips. | Render dark and legible; none covers the control it describes. (REQ-UI-01) |
| UI-CHK-5 | Address/URL field text. | Legible at common display scales; no clipping or faint/unreadable text. |
| UI-CHK-6 | Icon coherence. | Icons share weight, corner style, and active-color behavior across the chrome. |

### 22.3 Reliability tests

- Run for two hours with repeated popout/return cycles.
- Navigate between videos repeatedly.
- Open and close the app 20 times and verify settings persist.
- Test with YouTube logged in and logged out.
- Test with autoplay allowed and blocked.
- Test with Pin on/off in MVP; add Fade on/off to the Phase 2 test pass.

### 22.4 Performance tests

- App startup feels fast enough for utility use.
- Warm WebView target: Popout Player shows video within about 1.5 s of pressing **Pop out video** on broadband with a 1080p watch URL. Cold first-run WebView2 environment initialization is exempt.
- CPU/GPU usage is similar to a normal browser WebView playing the same video.
- No unbounded log or settings growth.

### 22.5 Definition of Done (MVP)

The MVP is "done" only when **both** the functional gate (22.1) **and** the visual/chrome gate (22.2, including the Chrome acceptance table) pass. A solid Video Popout loop with failing chrome is **not** a shippable MVP — the two are equal gates, not a primary plus a nice-to-have.

Each MVP scope bullet (section 23) maps to an acceptance check here:

| MVP scope item (section 23) | Acceptance check |
|---|---|
| Manual Video Popout, source pause, placeholder, timestamp sync, return | 22.1 functional rows; REQ-RETURN-01 |
| Single Popout Player / single instance | 22.1 double-click + REQ-APP-01 rows |
| Navigation/new-window policy | 22.1 external-link rows; REQ-NAV-01/02 |
| Window placement + monitor clamp + DPI | 22.1 monitor row; 22.2 DPI bullet; REQ-WINDOW-01 |
| Settings JSON with recovery | 22.1 corrupt-settings row |
| URL parsing + playlist fallback | 22.1 playlist/mix rows |
| Friendly WebView2 runtime error | 22.1 missing-runtime row |
| **Basic visual identity: dark shell, coherent icons, no browser chrome** | **22.2 Chrome acceptance (binary): UI-CHK-1…6; REQ-UI-01/02** |

Treating the visual-identity bullet as ID-backed and check-mapped (rather than prose) is deliberate: it is the item most prone to silent drift, because WPF defaults are light-themed.

---

## 23. MVP scope

MVP should include:

- WPF shell.
- Main WebView2 browser.
- Shared WebView2 environment.
- Manual Video Popout to one Popout Player.
- Source pause.
- Reliable Source Placeholder by hiding source WebView.
- Timestamp sync with the section 22 tolerance.
- Topmost/Pin toggle.
- Controls always visible in the Popout Player.
- Window placement save/restore with monitor clamping.
- Settings JSON with recovery.
- URL parsing for common YouTube video URLs and the required playlist fallback cases.
- Basic profile save/load.
- Navigation/new-window policy from section 15.2.
- Basic logging.
- Friendly WebView2 runtime error.
- Basic visual identity: dark shell, coherent icons, no browser chrome in Popout Player — meeting the binary Chrome acceptance checks in section 22.2 (REQ-UI-01, REQ-UI-02).
- Single-instance behavior — a second launch focuses the existing instance instead of starting a new process (REQ-APP-01).

MVP should defer:

- Controls fade.
- `Auto`.
- Profile edit/validation beyond graceful failure and basic overwrite handling.
- Chrome fade.
- Whole-popout opacity.
- Full custom compact player UI.
- Local `player.html` IFrame API shell.
- Global hotkeys.
- Tray mode.
- Auto-update.
- Advanced playlist management.
- Cross-platform support.
- Browser extension integration.
- Click-through/mouse pass-through transparency.
- `Reset app state` and `Clear browser data` actions (REQ-PRIVACY-01 / REQ-PRIVACY-02) — Phase 2.

---

## 24. Phase plan

### Phase 1 — MVP ship candidate

- Rename user-facing `Detach` to `Pop out video` / `Video Popout`.
- Hide source WebView when Source Placeholder is visible.
- Fix app-close save order.
- Add popout-in-progress guard.
- Add single-instance guard (named mutex) and focus the existing instance (REQ-APP-01).
- Make `LastKnownSeconds` nullable and accept zero.
- Implement `REQ-RETURN-01`.
- Implement section 15.2 navigation/new-window policy.
- Sanitize settings load.
- Add WebView2 initialization error handling.
- Add basic logging.
- Add monitor clamping.
- Improve URL parsing and required playlist fallback.
- Apply base visual identity to Source Window and Popout Player.
- Keep Popout Player controls always visible.
- Keep `Pin` and basic profile save/load.
- Strip `bin/` and `obj/` from repo/ZIP.

### Phase 2 — Convenience and profile polish

Delivered Phase 2 scope:

- Controls fade.
- `Auto`, off by default, using the manual-popout lifecycle.
- Profile edit/validation and overwrite/rename path.
- Stable release publish profile.
- Manual test checklist coverage for Phase 2 features.
- `Reset app state` and `Clear browser data` actions (REQ-PRIVACY-01 / REQ-PRIVACY-02), worded as separate confirmed actions.
- Fixed-swatch Pin/Fade active-color customization and controls-fade idle-delay presets.

Phase 2 landing status:

- Stable build `v0.3.0` build `10` completed the deterministic test lane, build gate, Stable
  publish/deploy, metadata validation, deployed UI smoke, and UI Automation title check. Build 10
  replaces the earlier build 9 Stable deploy and is built from the final Phase 2 landing commit.
- Account-backed/live YouTube rows in `docs/QA_Checklist.md` remain the release-candidate
  manual gate for Auto playback, controls fade/customization in live playback, profile edit/delete
  through the running Source Window, and privacy sign-in/sign-out invariants.
- Compact-mode placement is resolved for Phase 3 as global default plus optional profile override.
  Implementation remains part of the compact-player sweep, not Phase 2 landing.

### Phase 3 — Compact player upgrade

- Maintain borderless resize zones per `REQ-WINDOW-02` (current owner-tuned target: 12 DIP edge band plus
  96 DIP corner length).
- Keep compact-mode placement data dormant unless the Compact player is explicitly re-enabled.
- Add embed mode improvements.
- Add local `player.html` wrapper.
- Use YouTube IFrame API for compact mode.
- Add WebView2 virtual host mapping for local app HTML.
- Bridge C# and JavaScript with WebView2 messaging.
- Move polished Popout shell controls into the shell mode.

### Phase 4 — Utility polish

- Tray/minimize behavior.
- Optional global hotkeys.
- Chrome fade.
- Whole-popout opacity and size presets.
- Import/export profiles.
- Better accessibility and high-contrast support.
- Optional Focused overlay presentation on the real Normal watch page, independent from dormant
  Compact playback mode; Standard remains the default.

Click-through remains out of scope for Phase 4 unless explicitly re-approved with a recovery design.

---

## 25. Resolved defaults and open decisions

### 25.1 Resolved defaults

The following are normative defaults unless superseded by a later ADR or requirement update.

| ID / source | Decision |
|---|---|
| ADR-0005 | PiPlay is single-player for now. A popout request while a player exists activates the existing player rather than opening another. |
| REQ-RETURN-01 | Return follows the Popout Player's live paused/playing state when known; if unknown, return falls back to whether the source was playing when Video Popout started. |
| REQ-RETURN-07 | If the source was paused at popout launch, PiPlay must not auto-nudge the Popout Player into playing; launch-from-paused intent is passed into the popout for the session, and a return to playing must come from user action inside the popout. The principled companion to REQ-RETURN-01 (Option A). |
| REQ-NAV-01 | The allowlist is a guardrail, not a blocker: the Source Window allows YouTube plus Google sign-in (including regional domains); other links open in the system browser without per-link prompts. |
| REQ-NAV-02 | The Popout Player stays on YouTube plus the Google sign-in surface and never wanders onto unrelated sites; unrelated navigation is blocked or opened externally. |
| REQ-PRIVACY-01 / REQ-PRIVACY-02 | `Reset app state` and `Clear browser data` are separate actions. Reset keeps the YouTube session; clear browser data logs the user out. |
| Section 16.3 | Popout resize is free by default; aspect lock is optional later. |
| Section 16.2 | A deliberate primary mouse/pen drag on passive player pixels or unused YouTube chrome space moves the Popout after the system threshold. Ordinary clicks and every actual interactive YouTube/PiPlay control are preserved; the guaranteed native top handle is 44 DIP high. |
| Section 10.0 | Standard is the default Popout presentation. Optional Focused overlay keeps the real Normal `/watch` page, fills the viewport with `object-fit: contain`, never crops by default, and is independent from dormant Compact mode. |
| REQ-PROFILE-02 | Profiles store both bounds and monitor identity; restore same monitor when present, otherwise clamp to visible work area. |
| Section 6.1 Auto | Trigger is playback-start, `/watch`-only, and off by default. Detection and launch share one Source-first target identity; every return arms the latch with the returned identity before navigation/resume, so it cannot immediately loop. Shorts and embeds are excluded. |
| Sections 5.3 / 7.2 / 7.3 appearance | All presets default top-bar auto-hide on. Active/idle opacity defaults are Sharp Dark `1.00/1.00`, Minimal `0.94/0.86`, and Soft Glass `0.82/0.72`. Active also paints only the Source title-bar backdrop; active/idle paint the whole Popout. Preset/corner previews are live and non-affirmative dismissal reverts them completely. Soft Glass / explicit Round shapes the floating Popout with the DPI-scaled `PopoutFrame` region and clears it for maximized/snap-like layouts. |
| Section 6.2 / 6.3 initial customization (historical Phase 2 boundary) | The first slice was fixed Pin/Fade swatches plus delay presets. Later releases superseded its no-opacity/no-profile-override boundary with the shared accent/profile model and §§7.2–7.3 appearance controls. Click-through and transparent WebView2 remain out of scope. |
| REQ-WINDOW-02 | Borderless resize targets use a 12 DIP black edge resize band and a 96 DIP corner length for diagonal resize. Direct owner testing superseded the earlier 4/32, 8/48, and 12/72 targets as too difficult to acquire. No visible size grip is required. |
| Compact placement | Compact mode has reserved data (`PlayerSettings.CompactMode`, off by default, and optional `Profile.Mode`: `null` = global, `normal` = force normal, `compact` = force compact; legacy/internal `embed` normalizes to `compact`), but new popouts currently ignore it and force Normal while `PlaybackModePolicy.CompactPlayerEnabled=false`. |

### 25.2 Open decisions

1. Should the Source Window be optional after launching a profile directly?
2. Appearance / popout / compact directions from the 2026-06-23 owner review — the remaining outer-shadow/border decision after ADR-0008's large Popout silhouette, the transparency band, a main-window mode model (Browse / Cinema / Compact), optional active-profile popout border, and a "Restore video here" action — are tracked in `SPEC_GAPS_AND_OWNERSHIP.md` (2026-06-23 owner appearance / popout / compact review). The old global-accent/profile-identity split was explicitly superseded by the 2026-07-14 P2 decision in §17: an active profile color now drives the app and the global accent remains its fallback. The relaxed valid-hex gate remains implemented.

---

## 26. Implementation notes

### 26.1 Source Placeholder helper

```csharp
private void ShowSourcePlaceholder(bool visible)
{
    Browser.Visibility = visible ? Visibility.Hidden : Visibility.Visible;
    SourcePlaceholder.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
}
```

### 26.2 Nullable timestamp

```csharp
public int? LastKnownSeconds { get; private set; }
```

### 26.3 Safer video selector

```js
(() => {
  const v =
    document.querySelector('#movie_player video.html5-main-video') ||
    document.querySelector('video.html5-main-video') ||
    document.querySelector('video');

  if (!v) return null;

  return {
    currentTime: Math.floor(v.currentTime || 0),
    paused: !!v.paused,
    duration: Number.isFinite(v.duration) ? Math.floor(v.duration) : null
  };
})()
```

### 26.4 Atomic settings save

`File.Copy(..., overwrite: true)` is **not** atomic: it rewrites the destination in place, so a crash mid-copy can leave `settings.json` half-written (the failure mode Q-2 / section 12.6 forbid). Write to a temp file, flush it to disk, then swap it in with an atomic same-volume rename:

```csharp
var tempPath = FilePath + ".tmp";

using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
using (var writer = new StreamWriter(stream))
{
    writer.Write(JsonSerializer.Serialize(settings, Options));
    writer.Flush();
    stream.Flush(flushToDisk: true); // durable before the swap
}

// Atomic on the same volume; settings.json is never left partial.
File.Move(tempPath, FilePath, overwrite: true);
// Alternative: File.Replace(tempPath, FilePath, destinationBackupFileName: null)
// also swaps atomically and preserves the destination's ACLs.
```

### 26.5 Borderless resize contract

Prefer native hit testing for edge and corner resize in the Popout Player and Source Window.
`WindowChrome.ResizeBorderThickness` is acceptable for a uniform edge border, but it does not expose
a separate corner length. If corner acquisition is still too small, use `WM_NCHITTEST` with these
names and return values:

| Area | Preferred implementation term | Win32 result |
|---|---|---|
| Left/right edge | left/right resize border, west/east resize zone | `HTLEFT`, `HTRIGHT` |
| Top/bottom edge | top/bottom resize border, north/south resize zone | `HTTOP`, `HTBOTTOM` |
| Corners | corner resize zones, diagonal resize zones, NW/NE/SW/SE resize corners | `HTTOPLEFT`, `HTTOPRIGHT`, `HTBOTTOMLEFT`, `HTBOTTOMRIGHT` |
| Visible lower-right gripper, if ever added | size grip, size box, grow box | `HTSIZE` / `HTGROWBOX` |

Do not implement resizing with only visible resize handles. Invisible edges/corners should work like a normal native window.

Implementation target for `REQ-WINDOW-02`:

```text
Previous baseline:       6 DIP WindowChrome resize border on both primary windows
Interim expanded target: 10 DIP invisible resize zone
Current edge target:     12 DIP black resize band tied to BorderlessResizeHitTestPolicy.ResizeBorderDip
Mouse/pen corner target: 96 DIP corner length along the edge band
Visual outline:          0-2 px, optional/subtle
Touch-first future:      use explicit 40 x 40 effective-pixel affordances only in a touch/posture pass
```

### 26.6 Transparency implementation caution

For MVP, keep the WebView/video surface opaque. Implement controls fade first. Whole-popout opacity may use layered window alpha if it works reliably with WebView2, but it must be tested for input, rendering, and performance.

Do not make the WebView itself transparent and do not implement click-through behavior.

---

## 27. Reference notes

These references support the current technical direction and should be rechecked before a formal release:

- Microsoft .NET support policy: https://dotnet.microsoft.com/en-us/platform/support/policy
- WebView2 Evergreen vs Fixed Version runtime: https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/evergreen-vs-fixed-version
- WebView2 in WPF apps and `WebView2CompositionControl`: https://learn.microsoft.com/en-us/microsoft-edge/webview2/platforms/wpf
- WebView2 `CoreWebView2.NewWindowRequested`: https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2.newwindowrequested
- WebView2 local content and virtual host mapping: https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/working-with-local-content
- YouTube embedded player parameters: https://developers.google.com/youtube/player_parameters
- YouTube IFrame Player API reference: https://developers.google.com/youtube/iframe_api_reference
- WPF `WindowChrome.ResizeBorderThickness`: https://learn.microsoft.com/en-us/dotnet/api/system.windows.shell.windowchrome.resizeborderthickness
- Win32 `WM_NCHITTEST` resize hit-test results: https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-nchittest
- Windows touch target guidance: https://learn.microsoft.com/en-us/windows/apps/develop/input/guidelines-for-targeting
- Windows touch interactions and 40 x 40 epx target: https://learn.microsoft.com/en-us/windows/apps/develop/input/touch-interactions
- Windows 11 rounded-corner/non-client border guidance: https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/ui/apply-rounded-corners

---

## 28. Document history

| Version | Date | Notes |
|---|---|---|
| 0.1 | 2026-05-29 | Initial draft: product vision, quality-first principles, WPF direction. |
| 0.3 | 2026-05-30 | Established the Video Popout terminology and visual-identity tokens; split the fade / opacity / transparency policy into distinct behaviors; deduplicated and cleaned the document (three stacked copies collapsed to one); added a Conventions section, requirement IDs (Q-1…Q-8), and an atomic settings-save fix (section 26.4). |
| 0.4 | 2026-05-30 | Folded resolved defaults from spec-gap cleanup into normative requirements: MVP scope, return behavior, navigation policy, timestamp/performance tolerances, playlist fallback, reset/browser-data split, profile precedence/placement, and brand asset roles. |
| 0.5 | 2026-05-30 | Made visual identity verifiable after the chrome UI review: added REQ-UI-01 (dark-theme completeness for all popup-bearing controls and tooltips) and REQ-UI-02 (icon-font contract, no `.notdef` glyphs); added the binary Chrome acceptance table and the section 22.5 Definition of Done; specified the minimal Source Window nav control set; and resolved the privacy-actions scope contradiction by marking REQ-PRIVACY-01/02 as Phase 2 consistently across sections 19, 23, and 24. |
| 0.6 | 2026-06-06 | Folded current Phase 2 decisions into the spec: Auto playback-start behavior, profile edit/validation, privacy actions, Stable publish, and fixed-swatch Pin/Fade customization. Clarified that remaining Phase 2 work is manual release evidence plus the compact-mode placement decision only if compact mode is exposed before Phase 3. |
| 0.7 | 2026-06-06 | Recorded Phase 2 landing evidence for Stable build `v0.3.0` build `9`; clarified that live/account-backed YouTube checks remain the release-candidate manual gate while compact-mode placement is deferred unless compact mode is exposed before Phase 3. |
| 0.8 | 2026-06-07 | Recorded Stable build `v0.3.0` build `10` as the replacement deployed build from the final Phase 2 landing commit. |
| 0.9 | 2026-06-07 | Added and implemented `REQ-WINDOW-02` for larger borderless resize zones: previous 6 DIP baseline, 10 DIP edge target, 32 DIP corner length, and Win32 hit-test naming. |
| 0.10 | 2026-06-07 | Planned the Phase 3 compact-player sweep and resolved compact-mode placement as global default plus optional profile override. |
| 0.11 | 2026-06-10 | Beta candidate cut (v0.4.0-beta): release-facing copy cleaned for beta publication without changing requirements. Phase 4 §7.2/§7.3 resolution notes and the overlay compliance record remain tracked on the 2026-06-10 overlay/opacity plan (Task 6). |
| 0.12 | 2026-06-25 | Aligned the living spec with the v0.7.2 P1 surface: the current resize band is 4 DIP with 32 DIP corner acquisition, and the Compact player is dormant behind `PlaybackModePolicy.CompactPlayerEnabled=false` while its settings/profile data remains reserved. |
| 0.13 | 2026-07-15 | Added the optional Focused Popout presentation over the real Normal watch page, no-crop full-viewport layout and accessible overlay-control contract, global/profile precedence, and threshold drag from passive video pixels while preserving all interactive controls. Compact remains dormant and Standard remains the default. |
| 0.14 | 2026-07-15 | Made Source return visible and single-flight: minimum-size restore, minimized activation, pre-popout Pin restoration, hidden-navigation suspension, separate Show Popout/Bring actions, compact toolbar disclosure, and a dark profile actions menu. |
