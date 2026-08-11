# Manual release checklist

Run every release-candidate pass against `E:\Dev_test_implemenations\PiPlay\PiPlay.exe`, deployed only by `scripts\Publish-Stable.ps1`. Repo binaries and diagnostic/dirty deploys are not release evidence.

```powershell
.\scripts\Verify-StableDeploy.ps1
pwsh -File .\scripts\Test-UiSmoke.ps1 -ExePath E:\Dev_test_implemenations\PiPlay\PiPlay.exe
```

The verifier must print `VERDICT: RELEASE VERIFIED`.

- Build/version: __________
- Source commit/tag: __________
- Date/tester: __________
- OS/DPI/monitors: __________
- WebView2 runtime: __________

Mark each item pass, issue (link), blocked, or skipped.

## Playback, launch, and return

- [ ] YouTube home and `/watch` load; Popout starts warm playback within 2 s of expected timestamp plus elapsed time (target ≤1 s); Source Placeholder has no WebView bleed-through.
- [ ] Q-1: Source stays muted/paused through launch, ads, autoplay-next, SPA rerender, and `start_radio=1`. Listen for brief transition leaks between the approximately 1 Hz suppression checks.
- [ ] Playing-source return follows Popout timestamp/play state; paused-source launch does not auto-play; user play in Popout returns playing; timestamp `0` returns to zero. Volume/mute/rate replay where YouTube permits.
- [ ] Native X, Alt+F4, Focused Close, and **Bring video back** restore/activate a minimized Source without demoting a maximized Source. Immediate close never returns silent.
- [ ] **Show Popout** restores/focuses without transfer. **Bring video back** transfers/closes. Repeated clicks/Auto remain blocked during **Returning video...**.
- [ ] While Source WebView is hidden, Back/Reload/Home, URL, profile selection, and Save/Edit/Delete are disabled; Auto-off and both recovery actions remain usable.
- [ ] Rapid Pop out creates one player. A second PiPlay launch focuses the existing channel instance without WebView2-root contention.
- [ ] `watch?v=X&list=PL...` preserves list; playlist-only launch starts a playable item; return preserves current video/list. `watch?v=X&list=RD...` carries/advances the mix in Normal and returns with it. Malformed list degrades to one video with a non-blocking note.
- [ ] Source external links open in the system browser; Popout never wanders off YouTube; regional Google sign-in/new-window flow works intentionally.
- [ ] Auto is off by default, `/watch`-only, uses visible Source identity, pops immediately when enabled on playing video, skips Shorts/embeds, and does not re-pop the returned identity. A different next video remains eligible.

## Window, input, and presentation

- [ ] Source never restores/resizes below 760 x 480 DIP. Normal Popout remains usable at 320 x 180; dormant Compact remains forced off and would require 480 x 270.
- [ ] Source/Popout edge resize acquires across the 12 DIP outer band over WebView content; 96 DIP corner lengths give diagonal resize without stealing controls. The band reads as black canvas/letterbox, not a second frame.
- [ ] The 44 DIP top handle moves natively. Passive-picture mouse/pen click stays a click below system threshold; deliberate drag moves only after threshold. Timeline, volume, captions, settings, fullscreen, links, menus, end cards, ads, and overlay controls never drag.
- [ ] Page wheel/touchpad scrolling requires one click into the page for focus, then works—including at reduced opacity. Wheel input over the 12 DIP resize band is inert; scrolling just inside the band is unaffected.
- [ ] Passive drag is inert while maximized or after button release. Standard first child-frame load does not crash (`STATUS_BREAKPOINT` regression); no child-frame message wiring exists.
- [ ] Expand toggles full-monitor and back; glyph/tooltip change to Restore. Restore remains reachable after collapsed-strip top-edge reveal, and Escape restores after the strip receives focus. Close while expanded, then relaunch: prior normal bounds return, never expanded bounds.
- [ ] With the strip collapsed, top-edge hover reveals it before top-edge resize becomes active; after reveal, top resize works.
- [ ] Pin preferences remain independent. Pinned Source never covers active Popout; return restores the exact pre-popout Source Pin, including profile-derived state.
- [ ] Placement restores to the same/visible monitor and remains crisp at 100%, 125%, 150%, and mixed DPI.
- [ ] Soft Glass/explicit Round clips the video with a clean 22 DIP floating region. Resize/move across DPI; maximize/restore and snap halves/quarters clear/reapply the region without stale crops, seams, or lost corner resize.
- [ ] Standard is default; Focused uses the real `/watch` page and `contain` at wide/16:9/4:3/portrait ratios. Letterboxing is allowed; crop is not. Compact stays dormant.
- [ ] Focused Mute, Play/Pause, seek, Settings, Pin, Expand/Restore, Close work. Captions/Next hand off when available. Required YouTube branding, quality/settings, captions, fullscreen, ad UI, links, and Skip controls remain reachable.
- [ ] During `ad-showing`/`ad-interrupting`, custom seek/Next fail closed. Synthetic/stale/foreign-document actions cannot close, pin, expand, open Settings, move, seek, or advance; real current-document input still works.
- [ ] Empty Focused overlay pixels pass input; real controls/rail intercept it and expose readable names/focus. Selector/navigation failure withdraws Focused harmlessly while the native strip/return path works.
- [ ] Source and Popout Settings gears share one dialog; invoking the other entry point activates the existing dialog instead of opening a second copy.

## Appearance and chrome

- [ ] Fade delays Short/Normal/Long behave as 1500/2500/4000 ms. Fade off keeps the strip visible; Fade on hides/reveals through pointer, keyboard, focus, or pause. No opacity is click-through.
- [ ] Preset defaults: Sharp `1.00/1.00`, Minimal `0.94/0.86`, Soft Glass `0.82/0.72`; all auto-hide the strip. Active affects Source title backdrop + whole Popout; idle affects whole Popout only. UI floor is 45%.
- [ ] Preset/corner/opacity/accent preview updates Source and open Popout. Escape/title close restores exact prior state; Done persists after restart. Settings fits and scrolls on an approximately 768 px work area.
- [ ] Profile color retints toolbar, primary action, title/background/letterbox, row wash, and 1 px Popout edge. Colorless profile inherits global. Test all presets at intensity 0/50/100; Settings itself keeps plain background. No added fill/line outside the specified surfaces; caption controls stay neutral and Close hover stays red.
- [ ] Accent editing names/writes the active profile or global target correctly; unrelated appearance edits never overwrite the hidden global fallback. Stored dark colors remain exact while presentation contrast stays readable.
- [ ] `UI-CHK-1`: every icon resolves; zero empty boxes. `UI-CHK-2/3`: profile control closed/open is dark. `UI-CHK-4`: dark tooltips do not occlude controls. `UI-CHK-5`: URL text is legible at fractional DPI. `UI-CHK-6`: icon weight/state is coherent.
- [ ] `UI-CHK-7`: every icon-only control has the correct changing accessible name. `UI-CHK-8`: resize band reads as canvas. `UI-CHK-9`: restrained wash/visible profile rail/no row shift. `UI-CHK-10`: profile retint is coherent. `UI-CHK-11`: accent target is correct. `UI-CHK-12`: cards show `Crisp · 100%`, `Quiet · 94%`, `Glass · 82%` and preview rollback is exact.
- [ ] The Source profile-actions menu is dark, supports arrow keys, access keys, and Escape, and keeps Save/Edit/Delete readable with correct disabled states. At compact Source width only the transfer label hides; its icon, tooltip, and accessible name remain.

## Recovery, privacy, reliability, performance

- [ ] Missing/broken WebView2 shows install/retry without crash; network/navigation failure shows retry and preserves safe URL; corrupt `settings.json` is quarantined and defaults load.
- [ ] **Reset app state** clears settings/profiles/placement but keeps YouTube login. **Clear browser data** is separate/red-confirmed, closes Popout, clears `AllProfile`, and signs out; Cancel has default focus. A timed-out clear remains unavailable until the underlying task finishes; Reset remains usable.
- [ ] Repeat Popout/return for two hours and app open/close 20 times; test logged in/out and autoplay allowed/blocked. No resource degradation or unbounded settings/log growth.
- [ ] Startup feels utility-fast; warm Popout video is visible in about 1.5 s; CPU/GPU resembles a normal browser WebView for the same playback.
- [ ] Package excludes `bin/`/`obj/`, release notes name WebView2 Evergreen, and signing status is recorded but not treated as a gate.

## Evidence

Keep `docs/evidence/` sparse: only current, build-specific release/review evidence belongs there. Fold durable facts from old evidence into active docs or change records, then delete stale screenshots and one-off notes. Never use generic `current` names.

Capture at fractional DPI, using names such as `v0.12.1-b36-main-chrome.png`. Capture Source chrome, tooltips, open profile menu, Settings Appearance, Standard/Focused Popout, and every questionable state. Account-specific unavailable states are blocked, never faked.

```text
Build / commit / tag:
Stable verification:
Date / tester / DPI:

| Area | Result | Evidence | Notes |
|---|---|---|---|
| Source chrome | pass/fail/blocked | docs/evidence/...png | |
| Menus/tooltips | pass/fail/blocked | docs/evidence/...png | |
| Settings | pass/fail/blocked | docs/evidence/...png | |
| Standard/Focused Popout | pass/fail/blocked | docs/evidence/...png | |
```
