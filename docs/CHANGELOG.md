# Changelog

All notable changes to PiPlay are recorded here. Format loosely follows [Keep a Changelog](https://keepachangelog.com/); draft numbering is used until 1.0.

## [Unreleased]

### Changed
- **Visible dark accents without nested profile controls:** dark custom app accents are lifted only
  for presentation so action/chrome cues stay visible while the stored hex remains exact. The global
  accent now washes into the Source Window title bar, while profile identity uses one contrast-safe
  leading rail in the closed selector and dropdown rows—no filled inner chip or colored outer frame.

## [0.7.2] - 2026-06-25

Patch release (from 0.7.1, build 25): continues **P1 — borderless** by trimming the largest remaining
"framed tray" cue the v0.7.1 (b24) comparison review flagged — the inset around the hosted video — and
dropping the prompt dialog's inner border. Published exact-source to Stable. P1 reads cleaner but is not
finished: a persistent top chrome band, internal separators/hairlines, the letterboxed video tray, and
`soft`/`round` both mapping to DWM `Round` still preserve a framed feel.

### Changed
- **Thinner WebView inset (P1):** the left/right/bottom margin around the hosted WebView2 in both the
  main window and the popout player is reduced from 10 DIP to 4 DIP, and the borderless resize band is
  matched at the same 4 DIP (`WindowChrome.ResizeBorderThickness` and
  `BorderlessResizeHitTestPolicy.ResizeBorderDip`), so the video sits nearer the window edge and reads
  less as a black tray. The 32-DIP corner grab is preserved, so resize ergonomics are unchanged, and the
  maximized state still goes full-bleed (inset 0).
- **Borderless prompt dialog (P1):** the shared prompt shell (`Prompt.BuildShell`) no longer draws a 1px
  inner border, matching the borderless Settings dialog, so prompts read as a clean dark surface — the
  title-bar strip and body background already separate the dialog from what's behind it.

## [0.7.1] - 2026-06-25

Patch release (from 0.7.0, build 24): completes **P1 — borderless** by suppressing the Windows 11
DWM system frame border that the 0.7.0 control-border pass left untouched. Published exact-source to
Stable.

### Fixed
- **Borderless window frame (P1 completion):** the Windows 11 system frame border — the faint grey
  hairline tracing each window's rounded outer edge — is now suppressed via `DWMWA_BORDER_COLOR =
  DWMWA_COLOR_NONE` on all four borderless windows (main, popout player, Settings, prompts). The
  v0.7.0 P1 pass made the WPF *control* borders transparent but never touched the OS frame, so the
  window still read as boxed; this removes the last visible border. Applied unconditionally (the
  default Sharp Dark theme uses the pristine DWM corner mode, so the prior corner-only path never
  ran), High Contrast keeps the system border (an accessibility boundary cue), and no
  window-hosting / transparency change is involved.

## [0.7.0] - 2026-06-25

Minor release (from 0.6.0, build 23): owner UI roadmap **P1 — a borderless surface**, plus a
documentation prune. Published exact-source to Stable.

### Changed
- **Borderless controls (P1):** resting control outlines (toolbar buttons, the URL box, the profile
  combo, the primary accent button) and the Settings dialog frame are now transparent — the UI reads
  as a clean floating surface instead of a grid of grey boxes. Keyboard-focus rings are preserved
  (the URL box still shows the accent ring on focus), and per-theme accent/identity behavior is
  unchanged. Delivered without any window-hosting change; the WebView resize-band reduction and the
  larger-card/transparency work remain deferred.

### Maintenance
- Pruned stale documentation artifacts and repaired the surviving doc references.

## [0.6.0] - 2026-06-25

Minor release (from 0.5.0, build 22): an appearance follow-up to the owner UI review. The global app
accent is separated from per-profile identity color, accent actions are filled, color acceptance is
widened, the control borders and Soft Glass translucency are quieted, and the embed Compact player is
removed. Published exact-source to Stable.

### Changed
- **Profile color is identity, not app accent:** saved profile colors now fill the profile selector
  chip and no longer override the global app accent when a profile is active; Settings Appearance
  always edits the global accent.
- **Filled accent actions:** accent buttons now use the selected app accent as their fill with
  generated dark/white foreground text instead of reading as a heavy accent outline.
- **Wider custom colors:** Settings and profile colors now accept any valid `#RRGGBB` value; invalid
  hex is still blocked/defaulted, but mid-tone colors are no longer repaired away.
- **Quieter chrome:** the control borders across all themes (sharp-dark, minimal, soft-glass plus the
  `Colors.xaml` fallback) are now a faint hairline instead of hard grey, so the UI no longer reads as
  a boxed-in browser window.
- **Soft Glass is near-opaque by default:** its default translucency drops from a heavy active 0.92 /
  idle 0.78 to a slight, controlled active 0.97 / idle 0.90; the per-window opacity control still
  overrides it. Sharp Dark and Minimal stay fully opaque.

### Removed
- **Embed "Compact player":** the embedded-player popout mode is gone; new Video Popouts always use
  the full YouTube watch page (the embed broke on embed-disabled videos for near-zero visible gain).
  The compact code path is kept dormant behind `PlaybackModePolicy.CompactPlayerEnabled = false`.

### Fixed
- **Detached-video placeholder action:** the Source Window placeholder now includes a direct
  **Show popout** action that brings the existing Video Popout to the front.
- **Popout action rendering:** the Source Window `Pop out video` button now applies pixel-aligned
  text rendering to its nested icon and label and has enough toolbar height budget for the largest
  theme density, preventing malformed or clipped accent-button text.

## [0.5.0] - 2026-06-20

Minor release (from 0.4.3, build 21): the accent color wheel + per-profile accents land on top of
the UI overhaul / Theme V2 work, published exact-source to Stable.

### Added
- **Popout expand/restore:** a native expand button on the popout top bar toggles a
  full-monitor view and back, in both playback modes; the glyph and tooltip flip together.
  The YouTube fullscreen button inside the compact player now expands the popout window too,
  and exiting restores it (without un-expanding a window you expanded yourself). Esc restores
  while the popout chrome has focus. Closing an expanded popout never relaunches the next one
  expanded.
- **Compact recommendations stay in PiPlay:** clicking a recommendation or end-screen video in
  the compact player moves this same popout to that video in compact mode instead of leaving
  the app; channels, search and non-YouTube links still open in the system browser. After such
  a move, the error bar's "Open normal page" reopens the video you are actually on.
- **Theme presets, corners, and a single accent:** Settings → Appearance offers a theme preset
  (Sharp Dark, Minimal, Soft Glass), a corner profile, and one accent color (cyan, steel blue,
  steel, violet, green, amber). Theme, accent, and corners apply to every open window when Settings
  closes, with no restart needed; opacity sliders still live-preview on the open Popout Player.
  Theme settings are stored additively and migration-safe — an older build reading a newer
  settings file no longer drops the theme block.
- **Accent color wheel and profile accents:** the fixed accent swatches are replaced with a
  reusable color-wheel picker with RGB/hex fields, readable-color gating, nearest-readable repair,
  and live preview. Profiles can optionally carry their own accent override; selecting an accented
  profile re-themes the app and the active profile is restored on restart.

### Changed
- **Same-semver release-candidate rebuild:** `BUILD_NUMBER` advances to `20` so the accepted Phase 0
  provenance stack can publish from a new exact-source commit without moving the existing
  `stable-v0.4.3-b19` tag.
- **Release provenance is fail-closed:** Stable release-candidate publishes now use committed
  `VERSION`/`BUILD_NUMBER` by default, refuse dirty trees unless explicitly marked diagnostic,
  create the matching `stable-vX.Y.Z-bN` tag locally, record source cleanliness in the manifest,
  support pre-manifest signing hooks, and verify `FileVersion` plus `ProductVersion` before QA.
- **One accent replaces separate Pin/Fade colors:** the Source Pin, Popout Pin, and Popout Fade
  glyphs share a single chosen accent instead of two independent color pickers.
- **Video-aware return:** closing a popout that moved to a different video (recommendation
  click, playlist auto-advance, or in-page navigation) returns the main window to that video
  at the popout's timestamp, instead of seeking the original video to a foreign timestamp.
  With Auto on, the returned video is not instantly re-popped.
- **Settings layout:** Settings is sectioned (Privacy, Appearance, Playback, Advanced) and
  scrolls inside a height bounded by the screen work area, so it fits shorter displays. Fade
  delay, window opacity, and top-bar auto-hide moved under Advanced; the compact-player copy
  states it applies to new popouts only.
- **Resize over the video:** edge and corner resize now work with the pointer over the
  video/page surface on both windows (a 10 DIP inset band owned by the window); maximized
  stays full-bleed.
- **Popout action clarity:** while a popout is open, the main-window action reads
  "Show popout" (and restores a minimized popout); the YouTube mix/radio fallback reason is
  shown on the source placeholder instead of being log-only.

### Fixed
- **Accent wheel input cleanup:** releasing the hue wheel now releases mouse capture on mouse-up,
  on cleanup, and when dragging stops, so later clicks are not swallowed by the wheel.
- **Profile accent validation:** invalid RGB/hex input in the profile editor disables Save and is
  also guarded in the click path, preventing stale previously-selected accents from being saved.
- **Profile quick-save overwrite:** saving over an existing profile now preserves profile-specific
  playback mode, accent color, fade, and bounds while refreshing the URL/current pin state.
- **Workflow preflight resilience:** build/publish/verify/spec-preflight Git helpers no longer abort
  on benign native Git stderr such as LF-to-CRLF normalization warnings; real Git failures still flow
  through exit codes.
- **Accent workflow cleanup:** Settings copy now matches the actual live-preview behavior; profile
  accent storage normalization is centralized; release scripts share one native-command stderr
  wrapper; and the color-wheel WPF tests are split into focused files.

### Accessibility
- Explicit accessible names for every icon-only control: main chrome, navigation, URL box,
  profiles controls, Pin, Auto, the popout action (name tracks its state), popout
  Fade/Pin/Expand/Close, Settings close, and the Prompt dialog close button.

### Known behavior
- Wheel scroll over the normal-page popout needs one click into the page first (wheel
  focus-routing; documented owner decision 2026-06-10). Scroll then works at any opacity.

## [0.4.0-beta] - 2026-06-10

### Added
- Initial WPF application (`src/PiPlay`, `net10.0-windows`): Source Window + borderless
  Popout Player, dark visual identity from the spec color tokens, app icon and
  PerMonitor V2 DPI manifest wired in.
- **Video Popout** end-to-end: capture source timestamp + was-playing **before** pausing,
  pause source, show the Tier-1 Source Placeholder (hide the source WebView, no
  bleed-through), open one Popout Player on the shared WebView2 environment at the
  handed-off timestamp; popout-in-progress + single-player guards against double-clicks.
- **Return** lifecycle: closing the Popout Player restores the source, seeks to the last
  known timestamp (nullable `LastKnownSeconds`; `0` is valid), and resumes **only if the
  source was playing when popout started** (REQ-RETURN-01).
- Shared `CoreWebView2Environment` / user-data folder so login/session is shared; friendly
  "WebView2 runtime missing" recovery panel.
- Navigation/new-window allowlist for both windows (REQ-NAV-01/02): YouTube everywhere,
  Google sign-in/auth redirects on allowed Google account domains, everything else opens in
  the system browser.
- `settings.json` with atomic save (temp + flush + rename) and corruption recovery; basic
  profile save/load; Pin/topmost on both surfaces; window placement save/restore with
  monitor clamping; local file logging with URL redaction.
- Single-instance behaviour (REQ-APP-01): a second launch focuses the running instance and
  hands off its URL instead of starting a new process.
- Unit tests for URL parsing, settings recovery, and the navigation allowlist.

### Fixed
- Navigation allowlist no longer blocks legitimate Google sign-in (REQ-NAV-01/02). Google's
  regional sign-in/account domains (e.g. `accounts.google.no`) were being bounced to the system
  browser mid-login; the allowlist now treats those sign-in domains across any TLD as allowed on
  both the Source Window and the Popout Player. It remains a guardrail against drifting onto
  unrelated sites (stray links, ads, general Google browsing), not a hard blocker.
- Chrome visual identity (REQ-UI-01 / REQ-UI-02): chrome icons now render reliably instead of
  empty `.notdef` boxes (glyphs drawn through an in-template element with the icon font, so the
  app-wide text style can't reset it); the profiles dropdown is fully dark (control, popup, and
  items) with an intentional "No saved profiles yet" empty state instead of a blank light popup;
  an overflowing dropdown now uses a dark scrollbar rather than the light system one; tooltips use
  a dark style placed below their control so they don't occlude the caption buttons.
- URL/address-bar text was being clipped to a thin band at fractional display scales — caused by
  the window-level `UseLayoutRounding="True"` rounding the text line off the device grid. Layout
  rounding is now off on both windows; the URL text renders fully and legibly (UI-CHK-5).
- `Build-PiPlay.ps1` Release stage no longer exits non-zero on success when no old publish
  folders are pruned.
- **Clear browser data** now reports outcomes truthfully (REQ-PRIVACY-02, Q-6): result and
  not-ready notices read as statements rather than the "Clear browser data?" question; a clear
  that exceeds its ~30 s safety timeout says it will finish in the background instead of claiming
  it failed; and any unexpected error is surfaced instead of being silently swallowed.
- The Settings **Clear browser data** button now explains via a tooltip why it is disabled while
  the browser is still loading.
- Themed dialogs treat the title-bar close as Cancel (consistent dismissal).
- `Build-PiPlay.ps1` now forces runtime-specific restore assets when a Runtime is configured, so
  a prior no-RID restore such as `dotnet test` cannot leave the Release build missing its `win-x64`
  asset target.
- Single-instance activation (REQ-APP-01) no longer drops a maximized Source Window: when the running
  instance was minimized and a second launch handed it a URL, it used to come back at the *Normal* size,
  silently discarding the maximized layout. It now un-minimizes to the prior state (spec 16.4 / REQ-WINDOW-01).
- A pasted/typed YouTube link carrying an out-of-range timestamp (e.g. `t=99999999999h`) no longer pops the
  generic "unexpected problem" dialog or silently jumps to a wrong time. `YouTubeUrlHelper.ParseTime` now
  parses each h/m/s component safely and rejects out-of-range values, so a broken timestamp degrades to
  "no offset" and the link still plays (spec 17: broken URLs fail gracefully).
- `Build-PiPlay.ps1` prunes by recency (`LastWriteTimeUtc`) and never deletes the current label, so a
  custom `-PublishLabel` that sorts lexically below the default timestamp labels can no longer delete the
  just-built publish folder (data-loss guard).
- `Build-PiPlay.ps1` no longer rolls back `VERSION`/`BUILD_NUMBER` when a *post-publish* step fails after
  the artifact was already produced (which broke the monotonic build counter and orphaned the stamped
  folder); a pre-publish failure still rolls back and now also removes the partial publish folder.
- Borderless Source Window and Popout Player resize targets are easier to acquire (REQ-WINDOW-02):
  the invisible resize border is now 10 DIP instead of 6 DIP, with native hit testing that gives
  each corner a 32 DIP diagonal resize length without adding a visible size grip or click-through
  behavior.

### Removed
- Deleted the outdated `Main app.txt` pre-spec brainstorm (superseded by the product spec).
- Deleted the duplicate reference icon at `docs/piplay.ico`; the app/taskbar icon reference copy
  remains under `docs/assets/app-icon/piplay.ico`, and the shipped app uses `src/PiPlay/Assets/piplay.ico`.
- Deleted the unlinked generated brand-lockup HTML snippet; the product spec owns the canonical
  brand asset roles.

- **Popout Player controls fade** (spec 11): the chrome strip (Fade, Pin, Close) fades
  out after ~2.5 s idle and reappears on mouse movement, satisfying the §22.1 fade test
  row. A new in-popout **Fade toggle** turns the behavior on/off live; the choice is
  persisted (`PlayerSettings.FadeEnabled`, on by default). Only the WPF chrome fades —
  the WebView2 video surface is never made transparent, so the player stays fully
  interactable (Q-8, no click-through). Decision logic lives in `Services/FadePolicy.cs`
  with unit-test coverage.
- **Pin/Fade appearance customization.** Settings now includes an Appearance section with fixed
  swatches for active Pin and Fade colors plus Short / Normal / Long controls-fade delay presets.
  Defaults preserve the existing cyan active color and 2.5 s fade delay; values persist in
  `PlayerSettings` and are sanitized on load. This does not add whole-window opacity,
  click-through, profile overrides, or transparent WebView2 behavior.

- **Reset app state** (REQ-PRIVACY-01) and **Clear browser data** (REQ-PRIVACY-02) as separate,
  confirmed actions in a new themed **Settings** window (gear in the Source Window title bar).
  Reset atomically rewrites `settings.json` to defaults (settings, profiles, placement) and
  **keeps the YouTube session** — you stay signed in. Clear browser data is a separate, red-confirmed
  action that clears the shared WebView2 profile (`ClearBrowsingDataAsync(AllProfile)`) and signs you
  out. The only code path that logs you out is this explicit action — enforced by a regression test.
  Wording lives in `Services/PrivacyService.cs` and the UI binds to it so the visible text and the
  tested copy cannot drift. The flow is hardened against double-clicks, stale browser readiness,
  failed clears, and modal-owner issues (result-based, work runs after the modal closes).

- **Edit and delete saved profiles** from the Source Window (spec 17). Two new toolbar buttons next
  to the profiles dropdown act on the selected profile: **Edit** opens a themed Name + URL editor
  with **inline ("proactive") URL validation** — a broken URL or empty name is flagged in place and
  nothing is saved until it's fixed — and **Delete** removes it behind a red confirmation. Editing
  keeps the profile's position in the list and, if a rename collides with another profile, prompts
  to overwrite (the same prompt the Save action uses) instead of silently creating clutter. The
  buttons are disabled until a profile is selected.

- **Stable channel + differentiable stable publish.** A release channel is baked into the binary
  (`PiPlayChannel`, default `Default`, read at runtime by `AppChannel`). `scripts\Publish-Stable.ps1`
  builds the **Stable** channel, validates the publish metadata, and deploys a runnable copy to
  a configurable deploy root (`-DeployRoot`), replacing binaries but preserving
  the runtime data folder across redeploys. A Stable copy keeps its **data beside the exe** (`PiPlayData`,
  isolated from the dev profile), gets its **own single-instance identity** (so dev + stable run together,
  each single-instance), and shows **"PiPlay — Stable vX.Y.Z (bN)"** in the title bar/taskbar. The Default
  channel is behaviorally unchanged (same data location, single-instance identity, and plain "PiPlay"
  title). See `docs/adr/0007-stable-channel-and-portable-data.md`.
- **Auto (opt-in auto-popout).** A new toolbar toggle (off by default) that automatically starts a
  Video Popout when a `/watch` video is playing, reusing the manual popout's single-player lifecycle.
  It fires **once per video** (so returning from a popout doesn't re-pop it, and an in-source
  pause/resume won't either) and **excludes Shorts/embeds**. Resolves the open "Auto trigger timing"
  decision in favour of playback-start.

### Added — Phase 3 (compact player)
- **Compact player mode (spec 10.2–10.3).** A new global **Settings → Playback → Compact player**
  preference (off by default) makes new popouts open in a clean compact player instead of the full
  YouTube watch page; **Normal page mode stays the default and the fallback**. Saved profiles gain a
  per-profile **playback mode** override in the profile editor — *Use global default*, *Normal page*,
  or *Compact player* — that wins over the global default (REQ-PROFILE-01); the override is
  additionally scoped to that profile's own video so a stale combo selection can't apply compact to
  an unrelated video. Mode resolution, the durable `null`/`normal`/`compact` vocabulary (legacy
  internal `embed` folded to `compact`), and the separate **480×270 compact minimum** (vs. 320×180
  normal) live in a pure `PlaybackModePolicy`; the compact Popout Player clamps both its launch size
  and a restored sub-minimum placement up to that floor.
- Compact mode hosts a **local `player.html` shell** served from a WebView2 virtual host
  (`https://piplay.local/`) that drives the official **YouTube IFrame Player API**, with a small,
  versioned **host↔shell message bridge** (`PlayerShellBridge` / `PlayerShellProtocol`): the shell
  reports ready / state / error and the host commands play / pause / seek / requestState. The shell
  URL carries only non-sensitive target data (video id, playlist id, start) — no credentials — and
  the bridge (not DOM scraping) is the source of truth for the compact return timestamp. The shell
  keeps YouTube's controls and branding; no click-through, transparent WebView2, ad-blocking, or
  media download is introduced (Q-5/Q-8). The local virtual host is allowlisted on the Popout Player
  only.
- **Compact error states + normal-page fallback (Stage 4, Q-6).** A compact popout that can't
  play — embed-disabled (IFrame API codes 101/150), unavailable (100), an invalid reference (2), a
  playback error (5), a failed shell load, or an IFrame API that never responds (watchdog
  timeout) — now shows a native error bar with a code-specific message and an **Open normal page**
  action that reopens the same video in normal page mode at the best-known timestamp, in the same
  window. The bar dismisses itself if playback recovers (e.g. a playlist auto-advances past a dead
  entry). The error→message map, the auto-dismiss rule, and the watchdog timeout live in a pure
  `PlayerShellErrorPolicy`; logs carry redacted targets only.
- **Verified locally (deterministic):** the mode/precedence/min-size policy, the mode→URL and
  profile-override seams, the shell URL builder + host single-source-of-truth, the navigation
  allowlist for the shell host, the host↔shell protocol, the shell-asset invariants (structure, no
  third-party origins, no credential strings, build-copy), the error/fallback policy and error-bar
  lifecycle, and that every window constructs. **Live-verified (2026-06-07 smoke, no account):**
  the core compact path — the shell loading from the virtual host, the IFrame API playing a public
  video, and the bridge-sourced timestamp surviving return/resume. **Live-verified (2026-06-10
  smoke):** the Stage-4 error→fallback path — a valid-shape nonexistent video id surfaced IFrame
  API error 150, the error bar rendered with the embed-disabled message, and the **Open normal
  page** action reopened the same window on the real watch page with the bar hidden and only
  redacted URLs logged. **Release-candidate QA (live, not yet run):** playlists,
  restricted/embed-disabled handling on real videos, signed-in/account-backed playback, the tuned
  shell CSP (deferred until the IFrame API's real requests are enumerated live), and the Stage-4
  paths not yet seen live (watchdog timeout, auto-dismiss on playlist recovery, timestamp-carrying
  fallback after real playback).

### Added — Phase 4 (window quality + floating look)
- **Expanded borderless resize zones (REQ-WINDOW-02, Q-7).** Both borderless windows answer
  `WM_NCHITTEST` through a pure `BorderlessResizeHitTestPolicy`: a 10-DIP edge band plus 32-DIP
  corner runs make diagonal resize easy to acquire on the thin chrome. Resize bands yield to
  enabled chrome buttons (the popout's Close/Pin/Fade sit flush with the edge), and the hit test
  guards window teardown.
- **Whole-window opacity (spec 7.3).** Two Settings → Appearance sliders — *Active* and *When
  idle* — apply layered-window alpha to the whole popout (45% UI floor, live preview on the open
  popout, DWM-rounded corners while the translucent look is on). Idle shares the controls-fade
  idle timer (one idleness definition); an activity probe covers the WebView2 area WPF can't see,
  is occlusion-aware, and restores opacity on movement over the video. With both sliders at 100%
  the window stays byte-identical to the previous popout. Clicks never pass through at any
  opacity (Q-8 / ADR-0006); `WS_EX_TRANSPARENT` is never set, and tests assert it.
- **Auto-hiding top bar (spec 7.2, selectable, default off).** With controls fade on, the chrome
  strip height-collapses once fully faded so the video fills the window; hovering the top edge
  reveals it. Turning the behavior off (live or from Settings) restores the strip immediately.
- **Shell request channel (protocol v2).** The compact shell can request exactly the window
  actions the chrome strip already offers — `close` / `pinToggle` / `fullscreenToggle` — through
  a closed allowlist enforced on both sides (off-allowlist actions degrade to `Unknown`). This is
  the substrate for the upcoming in-shell overlay controls; nothing in the shell calls it yet.

### Validation
- Stable Phase 2 evidence captured for `v0.3.0` build `10`: deterministic tests,
  non-mutating build gate, Stable publish/deploy, metadata validation, and deployed Stable UI
  smoke. Build 10 replaces the earlier build 9 Stable deploy and is built from the final Phase 2
  landing commit.
- Account-backed/live YouTube rows in `docs/QA_Checklist.md` remain the release-candidate manual
  gate. Compact-mode placement is resolved for Phase 3 as global default plus optional profile
  override, with implementation planned in the compact-player sweep.

### Tests & quality
- **Layered regression suite**, 221 tests in
  `dotnet test` across three lanes plus a manual smoke:
  - **Layer 1 — XAML markup invariants** (`tests/.../Ui/XamlInvariantTests.cs`): parses the
    `.xaml` as XML and asserts the burned-in properties that break the app if they silently
    flip — `UseLayoutRounding="False"` (re-catches the "rounding = 0" URL-text clipping),
    `AllowsTransparency="False"`, `WindowChrome CornerRadius=0`, the required `x:Name` controls,
    glyph icon-font fallback, tooltips, that every `{StaticResource}` resolves, WCAG contrast,
    and the PerMonitorV2 manifest.
  - **Layer 2 — expanded logic** filling spec-coverage gaps: `Log.RedactUrl` (URL/token
    redaction), `ProfileService`, `YouTubeUrlHelper` path/start/embed/playlist edges, nav in-app
    schemes, plus the new pure `PlacementMath`/`ReturnPolicy`.
  - **Layer 3 — live WPF on a shared STA thread** (`Ui/WpfRuntimeTests.cs`): constructs the real
    windows (never shown, so WebView2/network are untouched) to verify runtime resource
    resolution, the layout/airspace DependencyProperty invariants, dark-theme styles, and a
    `RenderTargetBitmap` proving the URL text is not clipped at 150% DPI.
  - **Layer 4 — manual UIA + screenshot smoke** (`scripts/Test-UiSmoke.ps1`) for the true-render
    chrome gates at fractional DPI.
- **Spec-conformance review:** 92 findings, no current bugs; historical details were folded into the
  retained regression-suite plan.
- **Test-enabling seams** (behavior-preserving): `AppPaths` honors `PIPLAY_DATA_ROOT` and `AppChannel`
  honors `PIPLAY_CHANNEL` (both resolved per access; production channel identity is baked into the build); the
  placement clamp extracted to a pure `PlacementMath`; the return-resume decision extracted to a
  pure `ReturnPolicy`; `MainWindow`'s icon pack URI made assembly-qualified
  (`/PiPlay;component/...`) so it resolves independent of `Application.ResourceAssembly`.

## [0.3] - 2026-05-30
- Documentation only (pre-MVP). Established the Video Popout terminology, visual-identity tokens, and the fade/opacity/transparency policy split. Spec deduplicated and cleaned; requirement IDs and an atomic settings-save fix added.

---

_Shareable beta builds ship from `bin\publish` via `Build-PiPlay.ps1 -Stage Release`._
