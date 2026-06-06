# Changelog

All notable changes to PiPlay are recorded here. Format loosely follows [Keep a Changelog](https://keepachangelog.com/); draft numbering is used until 1.0.

## [Unreleased]

### Added — Phase 1 (MVP) implemented
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

### Removed
- Deleted the outdated `Main app.txt` pre-spec brainstorm (superseded by the Draft 0.4 spec).
- Deleted the duplicate reference icon at `docs/piplay.ico`; the app/taskbar icon reference copy
  remains under `docs/files (2)/piplay.ico`, and the shipped app uses `src/PiPlay/Assets/piplay.ico`.
- Deleted the unlinked generated brand-lockup HTML snippet; the product spec owns the canonical
  brand asset roles.

### Added — Phase 2 (convenience)
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

### Added — Phase 2 (privacy)
- **Reset app state** (REQ-PRIVACY-01) and **Clear browser data** (REQ-PRIVACY-02) as separate,
  confirmed actions in a new themed **Settings** window (gear in the Source Window title bar).
  Reset atomically rewrites `settings.json` to defaults (settings, profiles, placement) and
  **keeps the YouTube session** — you stay signed in. Clear browser data is a separate, red-confirmed
  action that clears the shared WebView2 profile (`ClearBrowsingDataAsync(AllProfile)`) and signs you
  out. The only code path that logs you out is this explicit action — enforced by a regression test.
  Wording lives in `Services/PrivacyService.cs` and the UI binds to it so the visible text and the
  tested copy cannot drift. The flow is hardened against double-clicks, stale browser readiness,
  failed clears, and modal-owner issues (result-based, work runs after the modal closes).

### Added — Phase 2 (profiles)
- **Edit and delete saved profiles** from the Source Window (spec 17). Two new toolbar buttons next
  to the profiles dropdown act on the selected profile: **Edit** opens a themed Name + URL editor
  with **inline ("proactive") URL validation** — a broken URL or empty name is flagged in place and
  nothing is saved until it's fixed — and **Delete** removes it behind a red confirmation. Editing
  keeps the profile's position in the list and, if a rename collides with another profile, prompts
  to overwrite (the same prompt the Save action uses) instead of silently creating clutter. The
  buttons are disabled until a profile is selected.

### Added — Phase 2 (release)
- **Stable channel + differentiable stable publish.** A release channel is baked into the binary
  (`PiPlayChannel`, default `Default`, read at runtime by `AppChannel`). `scripts\Publish-Stable.ps1`
  builds the **Stable** channel, validates the publish metadata, and deploys a runnable copy to
  `E:\Dev_test_implemenations\PiPlay` (configurable via `-DeployRoot`), replacing binaries but preserving
  the runtime data folder across redeploys. A Stable copy keeps its **data beside the exe** (`PiPlayData`,
  isolated from the dev profile), gets its **own single-instance identity** (so dev + stable run together,
  each single-instance), and shows **"PiPlay — Stable vX.Y.Z (bN)"** in the title bar/taskbar. The Default
  channel is behaviorally unchanged (same data location, single-instance identity, and plain "PiPlay"
  title). See `docs/adr/0007-stable-channel-and-portable-data.md`.
- **Auto (opt-in auto-popout).** A new toolbar toggle (off by default) that automatically starts a
  Video Popout when a `/watch` video is playing, reusing the manual popout's single-player lifecycle.
  It fires **once per video** (so returning from a popout doesn't re-pop it, and an in-source
  pause/resume won't either) and **excludes Shorts/embeds**. Resolves the open "Auto trigger timing"
  decision in favour of playback-start. See `docs/superpowers/specs/2026-06-06-auto-popout-design.md`.

### Planned — Phase 2 (remaining)
- Phase 2 QA coverage.

### Tests & quality
- **Layered regression suite** (`docs/Regression_Test_Suite_Design.md`), 173 tests in
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
- **Spec-conformance review** (`docs/Spec_Conformance_Review.md`): 92 findings, no current bugs.
- **Test-enabling seams** (behavior-preserving): `AppPaths` honors `PIPLAY_DATA_ROOT` and `AppChannel`
  honors `PIPLAY_CHANNEL` (both resolved per access; production channel identity is baked into the build); the
  placement clamp extracted to a pure `PlacementMath`; the return-resume decision extracted to a
  pure `ReturnPolicy`; `MainWindow`'s icon pack URI made assembly-qualified
  (`/PiPlay;component/...`) so it resolves independent of `Application.ResourceAssembly`.

## [0.3] - 2026-05-30
- Documentation only (pre-MVP). Established the Video Popout terminology, visual-identity tokens, and the fade/opacity/transparency policy split. Spec deduplicated and cleaned; requirement IDs and an atomic settings-save fix added.

---

_First shareable builds now ship from `bin\publish` via `Build-PiPlay.ps1 -Stage Release`. Promote `[Unreleased]` to a dated version section when cutting a tagged release._
