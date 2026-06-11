# UI overhaul stabilization and theme readiness - design

Source outline: `piplay-ui-overhaul-address-outline.md`

Discovery basis:

- `docs/ui-overhaul-discovery/ui-state-notes.md`
- `docs/ui-overhaul-discovery/theme-system-overhaul-evaluation.md`

## Goals

Turn the UI discovery pass into implementation-ready work for the larger settings, appearance, and
theme overhaul. The first priority is to stabilize core Popout Player behavior before visual polish:
resize, scroll, compact navigation, compact expand/fullview behavior, Settings reachability, action
copy, and accessibility names.

The theme work should then proceed through a compatible foundation: add a theme model and presets,
keep legacy appearance fields readable, restructure Settings so it can grow safely, and move toward
one theme accent instead of separate Pin and Fade color choices.

The existing happy path stays intact: PiPlay keeps a single Popout Player, the Source Window pauses
behind Video Popout, Compact remains an opt-in playback mode, and YouTube controls/branding remain
available.

## Requirements served

- `Q-1` - no duplicate playback after Video Popout.
- `Q-2` and `REQ-RETURN-01` - return preserves video, timestamp, playback intent, and window context.
- `Q-3` - DOM and shell integration remains isolated and best-effort.
- `Q-5` - no invasive YouTube behavior; compact mode must not bypass or remove required controls.
- `Q-6` - confusing states and failed target resolution produce understandable behavior.
- `Q-7`, `REQ-WINDOW-01`, and `REQ-WINDOW-02` - native-quality move, resize, DPI, and window policy.
- `Q-8` - visible Popout Player remains interactable; no click-through or mouse pass-through.
- `REQ-UI-01` - dark themed secondary surfaces and Settings remain coherent.
- `REQ-UI-02` - icon controls render and expose intended meanings.
- `REQ-PRIVACY-01` and `REQ-PRIVACY-02` - Settings restructure preserves distinct reset and browser-data actions.
- `REQ-PROFILE-01` and compact placement policy - profile mode overrides remain `profile.Mode ?? global PlayerSettings.CompactMode`.

## Comparison to the outline

The outline's priority order is accepted:

- Batch A behavior fixes come before Batch D/E theme work.
- The working state names are kept for implementation and QA: Source Home, Source Watch, Source
  Expanded Player, Popout Standard, Popout Fullview Faded, Compact Popout, and Settings.
- Popout Standard and Popout Fullview Faded stay separate in evidence but are not separate product
  logic unless a distinct invariant appears.
- Compact player remains a playback behavior, not an Appearance theme.
- Theme settings are additive and migration-safe; legacy fields stay readable through at least one
  schema version.

The implementation split is adjusted in six places:

1. The return-context risk is made explicit and WIDENED: the same gap exists in the normal-page
   popout today (in-popout SPA navigation and autoplay-advance), so close/return must use the
   popout's current video state in BOTH modes, not only after compact recommendation clicks.
2. Settings clipping is pulled forward so Settings can safely host both bug-fix copy and later theme
   controls.
3. Theme foundation is split from theme polish. Presets, tokens, migration, and accent chips can
   ship before a color wheel, Media Glow, or live theme switching.
4. Source Home popout behavior starts with clearer copy/affordance polish; the current baseline is
   already a clear pre-pause modal, and DOM mini-player target resolution is intentionally a
   follow-up unless the first pass needs it. The popout button's enabled state is NOT gated on URL
   shape (popout works from shorts/live/embed/youtu.be pages).
5. Outline item 5.6 (fade/top-edge reveal discoverability: more forgiving band, first-run hint,
   non-collapsed default) is explicitly DEFERRED from this pass — previously this was silently
   dropped; it is now a recorded deferral, not lost scope.
6. "Explicit external intent" for popout new-window handling is reframed as a URL-shape proxy:
   WebView2's `NewWindowRequested` exposes no window-open disposition, so left-click and
   "open in new window" are indistinguishable to the host.

## Acceptance criteria

- Popout Standard and Popout Fullview Faded resize from all four edges and all four corners in normal
  window state, without breaking fade, pin, close, drag, or YouTube controls.
- Normal-page Popout Player scroll works over the YouTube page area by mouse wheel, touchpad, and the
  page scrollbar where YouTube exposes one.
- Compact Popout left-click navigation to an allowed YouTube watch target stays inside PiPlay and
  continues as compact playback when possible.
- Non-watch, unsafe, and non-YouTube new-window targets still route externally or are blocked by the
  shared navigation policy (URL-shape proxy; no disposition signal exists).
- Closing a popout that played a DIFFERENT video than the one popped out returns the source to that
  current video and timestamp (navigate, not blind seek) in both playback modes, and Auto mode does
  not instantly re-pop the returned video.
- The Popout Player has one reliable expand/fullview path owned by PiPlay (native chrome strip
  affordance); restore stays reachable while expanded, and an expanded state does not persist as the
  next popout's launch state. A visible broken affordance is not left as the primary path.
- Settings content is scrollable, fits shorter displays, and keeps Privacy, Appearance, Playback, and
  Advanced controls reachable.
- Settings copy states that Compact player applies to new popouts only.
- When a Popout Player is active, the Source Window primary action reflects the actual behavior
  (`Show popout` or equivalent) instead of implying a second popout will open.
- YouTube mix/radio fallback is visible to the user when PiPlay pops only the current video.
- Source Expanded Player terminology is used in docs, tests, and user-facing text where this state is
  referenced; true fullscreen is reserved for a PiPlay-owned chrome/window state.
- Icon-only and templated controls have explicit accessible names, including main chrome buttons,
  navigation buttons, profile controls, Pin, Auto, Popout, Fade, popout Pin, popout Close, Settings
  Close, and the profiles combo.
- Theme model defaults load safely, invalid theme IDs and invalid accent colors fall back safely, and
  existing settings files with legacy appearance fields continue to load.
- First theme presets exist as data (`sharp-dark`, `minimal`, `soft-glass`) with a single normalized
  accent hex value and compatibility aliases for existing brush names.
- Separate Pin/Fade color controls are replaced only after the theme accent path is live and covered
  by tests.
- The final implementation passes `dotnet test PiPlay.sln --configuration Debug` and
  `.\Build-PiPlay.ps1 -Stage Build -NoVersionBump -NoBuildNumberBump`.

## Settled decisions

1. Fix behavior before theme polish. Resize, scroll, navigation, and Settings clipping affect basic
   usability and have higher release risk than visual presets.
2. Keep the single Popout Player invariant. ADR-0005 remains the simplest way to protect audio,
   return state, focus, and window ownership.
3. Treat Popout Fullview Faded as a captured variant of Popout Standard for now. The current evidence
   shows the same `PlayerWindow`, normal page path, normal window state, and shared fade/resize
   behavior.
4. Keep Compact as Playback. It changes the YouTube host path and navigation behavior, not the app
   theme.
5. Settings restructure happens before adding many theme controls. A scrollable section layout avoids
   worsening the existing clipping problem.
6. Add `ThemeSettings` additively and migrate conservatively. The app must keep reading `PinAccent`,
   `FadeAccent`, `FadeIdleDelayMs`, `ConstantWindowOpacity`, `IdleWindowOpacity`, `StripAutoHide`,
   and `CompactMode` while the new model stabilizes.
7. Store accent as normalized hex from the first theme pass. Preset chips can be simple now while
   keeping a future color wheel compatible.
8. Ship Sharp Dark, Minimal, and Soft Glass first. Media Glow needs additional shadow/glow tokens and
   visual QA and should not block foundation work.
9. Keep opacity and fade as visual states only. ADR-0006 still forbids click-through and requires a
   visible Popout Player to remain controllable.
10. Prefer policy seams and markup tests before runtime-only checks. Navigation, playback mode,
    theme migration, and XAML invariants should be deterministic before manual QA.
11. Resize fix route: layout inset. The WebView2 element gets a
    `BorderlessResizeHitTestPolicy.ResizeBorderDip` (10 DIP) margin on left/right/bottom in BOTH
    windows so the top-level HWND owns the band pixels and the existing subclass + policy work
    unchanged. Trade-off accepted: a visible ~10 DIP window-background band against REQ-WINDOW-02's
    "0-2 px outline" wording. Fallback if QA rejects the frame: in-process layered band child HWNDs
    synthesizing `WM_NCLBUTTONDOWN` on the parent. Cross-process child-HWND subclassing is
    infeasible and struck from the option space.
12. Scroll fix is diagnosis-gated. RESOLVED 2026-06-10 by live owner A/B: the owner is wheel
    focus-routing — after clicking into the page once, scroll works even at 86% opacity with the
    layered window engaged, so WS_EX_LAYERED is ruled out. Fix deferred by owner decision;
    click-into-page-then-scroll is the documented current behavior (QA row in Task 11). Any future
    fix (e.g. focusing the WebView after NavigationCompleted) must be fade/opacity-state-agnostic.
13. Return state becomes video-aware in both modes: the shell reports `videoId` in state messages
    (additive protocol field), normal mode captures the canonical/current URL, `PlayerReturnState`
    carries video identity, `ReturnPolicy` decides navigate-vs-seek, and the Auto de-dup key updates
    on return.
14. Expand/fullview affordance is a native ChromeStrip button driving the existing host
    `fullscreenToggle` handler (the protocol channel is already complete end-to-end; only a caller
    is missing). `ContainsFullScreenElementChanged` handling, if added, is gated on the live compact
    mode. Maximize semantics DECIDED 2026-06-10: keep the current full-monitor maximize (no
    work-area hook on PlayerWindow) — now deliberate. Restore reachability and no-persistence of
    the expanded state remain explicit decisions with tests.
15. Settings schema is round-trip-protected: `[JsonExtensionData]` on `AppSettings`/`PlayerSettings`
    so an older binary cannot silently delete the theme block on save; `ThemeSettings.AccentColor`
    seeds from the legacy `PinAccent` at migration.

## Non-goals / out of scope

- Multiple simultaneous Popout Players.
- Click-through, mouse pass-through, transparent WebView, or hiding YouTube controls/branding.
- Downloaded themes, imported theme packs, or a full user theme editor.
- Media Glow in the first theme foundation pass.
- Color wheel in the first theme foundation pass unless all derived-token and contrast checks are
  already reliable.
- DOM mini-player target resolution on YouTube Home as part of the first stabilization pass.
- Outline item 5.6: fade/top-edge reveal discoverability improvements (more forgiving band,
  first-run hint, non-collapsed default) — explicitly deferred, revisit after stabilization.
- Replacing YouTube's player UI with custom media controls.
- A production release cut; this spec prepares implementation work and verification gates.

## Testing approach

Logic/unit:

- `BorderlessResizeHitTestPolicy` is unchanged by the inset route; keep its tests as-is. (The
  previously suggested "WebView child-HWND resize forwarding" tests are struck — that route is
  cross-process-infeasible.) Note: the existing policy tests and the WpfRuntimeTests NCHITTEST row
  pass with the bug live, so the resize gate is layout invariants + manual QA, not policy tests.
- Add popout new-window policy tests (URL-shape proxy): allowed YouTube watch URLs retarget in-app,
  shorts/channel/search/non-YouTube/unsafe URLs stay external/blocked, and compact-shell rebuild
  preserves v/list/start.
- Add return-state tests for BOTH modes: shell `videoId` state parse, `ReturnPolicy` navigate-vs-seek
  decisions, Auto de-dup key update on return, and retarget-aware fallback-target correctness.
- Add `ThemeCatalog` and `ThemeSettings` tests for default theme, catalog uniqueness, invalid theme
  fallback, invalid accent fallback, migration from legacy appearance fields (including the
  `PinAccent` accent seed), `[JsonExtensionData]` round-trip preservation, and override semantics.

Markup:

- Extend `XamlInvariantTests` for the WebView2 inset margins (`Player`/`Browser` band ==
  `ResizeBorderDip`), the Settings `ScrollViewer`, section names, accessible names, required theme
  resources, and compatibility brush aliases. The Task 10 swatch replacement is a test REWRITE: the
  current suite pins the eight `PinAccent*`/`FadeAccent*` names (presence + tooltips/names) and the
  `SettingsWindow(pinAccent:, fadeAccent:, ...)` constructor surface — invert/migrate those rows,
  then add the obsolete-controls-gone checks.

WPF/runtime:

- Extend `WpfRuntimeTests` for Settings construction at constrained height, compact-copy visibility,
  primary popout action state, Settings theme controls, and popout shell request handling.
- Keep existing runtime checks for `PlayerWindow` minimum sizes, chrome hit-test visibility, fade, pin,
  opacity floors, and compact-mode shell requests.

Manual QA:

- Capture before/after screenshots for Source Home, Source Watch, Source Expanded Player, Popout
  Standard controls visible, Popout Standard faded/idle, Popout Fullview Faded while still kept,
  Compact Popout, and Settings.
- Manually verify resize from every edge/corner WITH THE POINTER OVER THE PLAYER SURFACE (the
  reported bug), normal-page scroll per the Task 2 diagnostic protocol, compact recommendation
  clicks, expand/restore (including restore reachability while maximized with strip auto-hide on,
  and that the expanded state does not persist to the next popout), return-after-navigation in both
  modes (Auto on and off), top-edge reveal with Fade enabled (including the reveal-then-resize beat),
  Settings on a shorter display, theme preset application, reset app state, and clear browser data.

Gates:

```powershell
dotnet test PiPlay.sln --configuration Debug
.\Build-PiPlay.ps1 -Stage Build -NoVersionBump -NoBuildNumberBump
```

## Changes by file

| File | Change |
|---|---|
| `docs/superpowers/specs/2026-06-10-ui-overhaul-stabilization-design.md` | This design record. |
| `docs/superpowers/plans/2026-06-10-ui-overhaul-stabilization.md` | Multi-step implementation plan. |
| `docs/ui-overhaul-discovery/ui-state-notes.md` | Keep state evidence current; add after screenshots and findings as tasks land. |
| `docs/ui-overhaul-discovery/theme-system-overhaul-evaluation.md` | Update if theme decisions change during implementation. |
| `docs/CHANGELOG.md` | Add user-visible Settings, Popout Player, Compact, accessibility, and theme changes when they land. |
| `docs/QA_Checklist.md` | Amend the existing Phase-3 resize rows (pointer-over-player), add rows for scroll, compact navigation, expand/restore, return-after-navigation, Settings scroll, accessibility names, and theme presets; retire Pin/Fade color rows after Task 10. |
| `docs/SPEC_GAPS_AND_OWNERSHIP.md` | Fix the stale "Stage 4 deferred" compact-sweep claim while touching the docs surface. |
| `src/PiPlay/MainWindow.xaml` / `.xaml.cs` | Source action state, accessible names, mix/radio fallback message, Settings launch/apply path, and any source expanded-player copy. |
| `src/PiPlay/PlayerWindow.xaml` / `.xaml.cs` | Resize/scroll fix integration, compact navigation handling, compact expand/fullview path, accessible names, theme application. |
| `src/PiPlay/SettingsWindow.xaml` / `.xaml.cs` | Scrollable section layout, Compact copy, theme/accent controls, advanced overrides, close/accessibility cleanup. |
| `src/PiPlay/Models/AppSettings.cs` | Add `ThemeSettings` while preserving existing `PlayerSettings` fields. |
| `src/PiPlay/Services/SettingsService.cs` | Theme defaults, migration, invalid-value recovery, atomic save behavior unchanged. |
| `src/PiPlay/Services/PlaybackModePolicy.cs` | Keep compact/normal URL and mode policy stable; extend only if compact navigation requires a policy seam. |
| `src/PiPlay/Services/NavigationPolicy.cs` | Shared allow/block/external decisions for source and popout navigation. |
| `src/PiPlay/Services/YouTubeUrlHelper.cs` | Target normalization and mix/radio fallback semantics if needed. |
| `src/PiPlay/Services/BorderlessWindowHelper.cs` | Unchanged by the inset route (subclass already correct); touch only if the band-HWND fallback is needed. |
| `src/PiPlay/Services/BorderlessResizeHitTestPolicy.cs` | Unchanged; its `ResizeBorderDip` constant becomes the inset-band source of truth. |
| `src/PiPlay/Services/PopoutNavigationPolicy.cs` | NEW: pure new-window decision for the Popout Player (URL-shape proxy: playable target retargets in place, else external). |
| `src/PiPlay/Services/ReturnPolicy.cs` | Navigate-vs-seek decision when the returned video differs from the source video. |
| `src/PiPlay/Models/PlayerReturnState.cs` | Carry the popout's current video identity alongside the timestamp. |
| `src/PiPlay/Services/WindowPlacementService.cs` | Normalize maximized capture so an expanded popout does not relaunch maximized (Task 4 decision). |
| `src/PiPlay/App.xaml.cs` | Read-only settings load + theme resource application before `MainWindow` construction (Task 9). |
| `src/PiPlay/Services/PlayerShellProtocol.cs` / `PlayerShellBridge.cs` | Compact shell expand/navigation/return messages if the shell owns the path. |
| `src/PiPlay/PlayerShell/player.html` / `player-shell.js` | Compact shell navigation and expand/fullview behavior if implemented in the shell. |
| `src/PiPlay/Theme/Colors.xaml` / `ControlStyles.xaml` | Theme token aliases, preset resources, control styling, radius tokens. |
| `src/PiPlay/Theme/*` | New theme catalog/model/applier helpers as needed. |
| `tests/PiPlay.Tests/*` | Logic, markup, and WPF runtime coverage for the tasks above. |

## Docs & changelog impact

- Keep this spec linked from the implementation plan and future PR.
- Update `docs/CHANGELOG.md` for each user-visible batch.
- Update `docs/QA_Checklist.md` before manual QA or release-candidate work.
- Update `docs/SPEC_GAPS_AND_OWNERSHIP.md` if Popout Fullview Faded becomes a real state, if theme
  ownership moves out of `PlayerSettings`, or if compact fullscreen behavior settles a product gap.
- Add an ADR only if the resize fix changes native-window architecture, if theme resources become a
  new architecture boundary, or if live theme switching changes open-window ownership.

## Unresolved decisions

- Popout Fullview Faded promotion is no longer an open-ended question; it follows a decision RULE:
  keep it as captured evidence of the same normal-page `PlayerWindow` path (no separate logic, no
  deletion yet), and promote it to a real state ONLY if it gains a distinct invariant — a different
  playback URL, a different chrome policy, a different window state, different input behavior, or a
  separate entry/exit command. Absent such an invariant after stabilization, fold the captures into
  Popout Standard as idle/faded evidence rows.
- Should selecting a theme overwrite current active/idle opacity, or keep existing values as user
  overrides until the user chooses theme defaults?
- Should fade delay be theme-owned by default, or remain an independent user setting with optional
  theme defaults?
- Should Soft Glass default to auto-hide top bar, or keep auto-hide off for discoverability?
- Should theme changes update already-open Popout Players live, or apply only to the next Popout
  Player like Compact mode?
- How should PiPlay save profiles created from YouTube mix/radio URLs: original URL, normalized current
  video, or a warning with user choice?
- Should Source Expanded Player ever hide PiPlay chrome, or remain a YouTube-in-WebView expanded state?
- When the deferred scroll fix is picked up: focus the popout WebView automatically (after
  `NavigationCompleted` / on activity), or keep click-to-focus? (Owner ruled out the layered-opacity
  theory live on 2026-06-10; current click-then-scroll behavior is accepted and documented.)

## Review addendum - plan-vs-code findings (2026-06-10)

An adversarial review (7 parallel review dimensions over the actual source, cross-checked manually)
validated the plan's direction and settled the open diagnoses. Key evidence, recorded so
implementation does not re-derive it:

**Resize (Task 1).** Confirmed: `EnableExpandedResizeZones` subclasses only the top-level HWND
(`BorderlessWindowHelper.cs:74-83`); `WM_NCHITTEST` goes to the deepest HWND under the cursor, and
the WebView2 surface is a cross-process child HWND chain — the hook never fires there. The working
zones map exactly to the 32 DIP `ChromeStrip` WPF pixels (policy corner length is also 32 DIP).
`MainWindow` has the identical defect. The codebase already records the airspace fact three times
(PlayerWindow comments, opacity spike worklog). Existing resize tests pass with the bug live — they
exercise the policy and a synthetic NCHITTEST, never a real WebView2 child.

**Scroll (Task 2).** No overlay exists over `Player`; no wheel handler/hook/capture exists anywhere
in src; the resize subclass cannot eat wheel where it never executes. The one popout-vs-source input
delta is `WindowOpacityApplier`'s WS_EX_LAYERED engagement (the user runs 85%/78%, so every observed
failure was under layering; the Stage 0 spike's "input preserved at every alpha" claim never tested
wheel over the WebView child). Secondary candidate: wheel focus-routing (`PlayerWindow` never focuses
its WebView). Resize and scroll are mechanically independent failures sharing one architectural fact.

**Compact navigation / return (Task 3).** `Core_NewWindowRequested` treats every request as external;
`NewWindowRequestedEventArgs` has no window-open disposition (URL-shape proxy required; gate on
TryParse-with-VideoId, not `IsAllowed`). The shell state channel carries currentTime/playerState/
duration but no videoId; playlist auto-advance and end-screen clicks navigate inside the iframe with
no host event. `PlayerReturnState` carries no video identity and `Player_OnClosed` only seeks/plays
the never-navigated source page — so the NORMAL popout already corrupts return state today after
in-popout navigation (REQ-RETURN-01 gap, both modes). Additional traps found: the Auto de-dup key
(`_autoLastHandledVideoId`) would instantly re-pop a return-navigated video; the readonly
`_fallbackTarget`/`_url` make the compact error-bar fallback reopen the wrong video after an in-place
retarget; the shell-ready watchdog needs re-arming on retarget.

**Compact expand (Task 4).** No handler for `ContainsFullScreenElementChanged` exists; `fs` defaults
to 1 in the IFrame playerVars, so YouTube renders a fullscreen button whose effect (fill the WebView
bounds) is invisible because the player already fills them. The `fullscreenToggle` protocol channel
is complete end-to-end (consts, dual allowlists, bridge, host handler, tests) but has NO caller —
`postRequest` in player-shell.js is dead code pending the deferred overlay-controls work. The host
toggle maps to `WindowState.Maximized`, which covers the full monitor because `PlayerWindow` never
installs `EnableProperMaximize` (accident, now a decision point). Closing while maximized persists
Maximized into placement — fullview would leak into the next popout. Expanded state currently has no
restore affordance beyond the same hidden toggle (one-way trap risk).

**Settings/theme (Tasks 5, 8-10).** `SettingsService` uses System.Text.Json with default
unknown-member handling: an older binary reading a newer settings file silently drops `ThemeSettings`
and DESTROYS it on its next atomic save — hence `[JsonExtensionData]`. Settings are loaded inside the
`MainWindow` constructor, so startup theme application needs an App-level read-only load. Window XAML
uses `StaticResource` throughout (live switching = DynamicResource migration; deferred). The existing
test suite PINS the Pin/Fade swatch names and the `SettingsWindow` constructor surface — Task 10 is a
test rewrite, and Task 5 must preserve `x:Name`s. Settings appearance controls already carry
accessible names; the gaps are MainWindow (16 controls incl. `UrlBox`), PlayerWindow strip buttons,
Settings close, and the code-built `Prompt.BuildShell` close button.

**Source actions / a11y (Tasks 6-7).** `_player.Activate()` alone does not restore a minimized
popout. No non-modal message surface exists in `MainWindow` for the mix/radio note — the
`SourcePlaceholder` is the designated surface (NOT the popout `ErrorBar`, which is compact-error
semantics and would diverge the popout captures). The Source-Home baseline is already a clear
pre-pause modal. `MaximizeButton`'s name must track its Maximize/Restore content flips.

**Docs/process (Tasks 11-12).** Reference integrity is clean: every test class, file, seam,
requirement ID, and gate command in this spec/plan exists under the exact name used; CI runs the
gates byte-identically ("Build and test (Windows)"). Outline item 5.6 was the only silently dropped
scope (now an explicit deferral). `SPEC_GAPS_AND_OWNERSHIP.md` carries a stale Stage-4-deferred
claim; `docs/CHANGELOG.md` lacks an Unreleased section; spec-check requires every code-touching PR to
include a dated design spec or a Spec-Exception line.

**Dual-state audit.** "Fullview" appears nowhere in src; nothing in the plan, spec, or QA forces
Popout Standard / Popout Fullview Faded divergence. Guardrails added where implementation could
accidentally create it: route-B mode gating (Task 4), fade-state-agnostic fixes (Tasks 1-2), and the
source-side fallback note placement (Task 6).

## Implementation reconciliation addendum (2026-06-11)

Two parallel implementations of Tasks 4-5 existed briefly: this branch's review-hardened pass and
a smaller direct landing on main (`b35c0dd`). The reconciliation merge settled the following as
design decisions:

1. **Settings frame model: fixed launch height, not SizeToContent.** The dialog declares
   `Width=520 Height=680 MinHeight=360`; the constructor clamps `MaxHeight` to the primary work
   area less a 48 DIP margin (floor 420 for misreported work areas) and clamps the launch Height
   under it. Rationale: the dialog must not grow with future sections — the scroll viewer absorbs
   content growth instead. Pinned by a XAML invariant (no `SizeToContent`, `Height`/`MinHeight`
   present, horizontal scrolling disabled) and runtime asserts on the exact clamp derivation.
2. **Expand affordance: `ExpandButton`, state-neutral UIA name** ("Expand or restore popout",
   MaximizeButton precedent); glyph and tooltip flip in code. One toggle path serves the native
   button, the shell `fullscreenToggle` request, and (gated on live compact mode) the WebView2
   fullscreen element, with a caused-by-element latch so an element exit never undoes a posture
   the user chose; the latch self-syncs on `StateChanged` for OS-driven exits.
3. **Every expand path counts as user activity** (adopted from `b35c0dd`): an auto-hidden strip
   un-collapses on expand/restore so the restore affordance is immediately reachable without the
   top-edge reveal.
4. **Placement normalization is a pure copy** (`PlacementMath.ForNextLaunch`): applied on BOTH
   capture and launch (a popout never launches expanded, including from pre-fix settings files),
   never mutates its input, and deep-copies `MonitorWorkArea` so snapshots share no mutable state.
5. **Shell-reported video ids are validated at protocol parse** (`PlayerShellProtocol.Parse`):
   a malformed id is wire-level malformed input and parses as absent, protecting every consumer —
   the host turns this string into a source navigation target on close.

## Theme pass addendum (Tasks 9-10, 2026-06-11)

Settled decisions for the theme-resource and theme-selector tasks:

1. **Single accent token + in-place startup recolor (Task 9).** `Theme/Colors.xaml` gains
   `AccentPrimary`/`AccentPrimaryLight` brushes (plus `AccentPrimaryColor`/`AccentPrimaryLightColor`
   and staged `ControlCornerRadius`/`ButtonCornerRadius`); `AccentButton`, the URL caret/focus, the
   `PinToggle` default, and the Settings preset chips repoint to it. `AccentCyan*` stays defined as a
   compatibility alias. `ThemeResourceApplier` (called from `App.OnStartup` after a read-only
   `SettingsService.Load`, before the first window parses) MUTATES the shared, unfrozen accent
   brushes' `Color` in place — `StaticResource` froze the lookup at parse time, but every consumer
   holds the SAME brush instance, so the in-place swap reaches the already-parsed app styles and the
   later-parsed windows alike, with no `StaticResource`→`DynamicResource` migration. Application this
   pass is startup/next-window only; live switching of already-open windows stays the deferred
   "Unresolved decision 3". Pure color math (`ThemeColors.ParseColor/Lighten/Brush`) is unit-tested;
   the applier is STA-tested against a synthetic `ResourceDictionary`.

2. **One accent drives Source Pin, Popout Pin, and Popout Fade (Task 10).** `Theme.AccentColor` is
   now the single color source: `MainWindow.ApplySourceAppearance`, `PlayerWindow.ApplyAppearance`,
   and the popout launch all resolve it via `ThemeColors.Brush` (one frozen brush shared by the two
   popout toggles). The `SettingsWindow`/`PlayerWindow` ctor + `ApplyAppearance` surfaces collapsed
   from `pinAccent`+`fadeAccent` to one `accentColor`. Legacy `Player.PinAccent`/`FadeAccent` (and
   `PlayerAppearancePolicy` accent mapping) stay readable for back-compat and migration seeding but
   drive no color.

3. **Palette realigned for readability and "sharp-dark = current shell" (deviation from the plan's
   literal chip wording).** The plan named the first two chips "muted cyan" (`#2D6F8F`) and "steel
   blue" (`#4D7EA8`); both measured BELOW the on-dark glyph contrast floor (2.33:1 and ~2.99:1 on the
   hover surface). To keep the default look identical to today and guarantee readability, the catalog
   default accent is the current shell cyan `#00D4FF` (so a fresh install and the migrated legacy
   "cyan" seed agree), and the chips are Cyan `#00D4FF`, Steel blue `#5AA9E6`, Violet `#A78BFA`,
   Green `#38D996`, Amber `#FFC857`. The `Minimal` preset's default accent moved to the brighter
   steel blue `#5AA9E6`. Every offered accent is gated by `Theme_accent_palette_is_readable`
   (≥3:1 as an on-dark glyph, ≥4.5:1 under the dark `AccentButton` text); a XAML↔catalog sync test
   keeps the hand-written chips aligned with `ThemeCatalog`. Task 8's only consequent test change was
   the two invalid-accent fallback rows (now `#00D4FF`).

4. **Fade delay stays `Player.FadeIdleDelayMs`-driven.** The theme model carries `FadeDelayPreset`
   (Task 8 migration artifact, resolver-backed) but the live Advanced "Fade delay" control and the
   popout idle timer keep using `Player.FadeIdleDelayMs` this pass — no behavior change, no dormant
   divergence wired into the runtime path.
