# PiPlay — Manual QA Checklist

Re-runnable manual test pass for each shareable build. Derived from spec section 22 (Quality gates). Copy this list per release and fill it in.

> **Test the deployed copy, not the repo.** Run this checklist against the deployed Stable exe at
> `E:\Dev_test_implemenations\PiPlay\PiPlay.exe` (deployed via `scripts\Publish-Stable.ps1`) — never
> against repo build output (`src\...\bin\...`, `bin\publish\...`); stale binaries are the classic
> false pass. Before starting, run `.\scripts\Verify-StableDeploy.ps1` and copy its identity block
> into the fields below. The verifier must print `VERDICT: RELEASE VERIFIED`; diagnostic or dirty
> deploys marked "not release evidence" are not valid for this checklist.

- **Build / version:** ____________________
- **Deployed exe (must be `E:\Dev_test_implemenations\PiPlay\PiPlay.exe`):** ____________________
- **Source commit (from `Verify-StableDeploy.ps1`):** ____________________
- **Date / tester:** ____________________
- **OS / DPI / monitors:** ____________________
- **WebView2 runtime version:** ____________________

Mark each: pass / issue (link) / skipped.

## 1. Functional (Q-1, Q-2)
- [ ] youtube.com loads without crashing.
- [ ] A `watch?v=` URL loads and plays.
- [ ] Warm WebView: after the Popout Player has played for about 3 s, its timestamp is within 2 s of (expected source timestamp + elapsed playback time); target ≤1 s.
- [ ] Source audio stops on popout — no duplicate audio through launch, ads, autoplay-next, and YouTube SPA re-render. **(Q-1)**
- [ ] Source Placeholder is visible with no WebView bleed-through.
- [ ] Closing the player after popping out from a playing source returns at the timestamp and follows the popout's live play/pause state. **(Q-2, REQ-RETURN-01)**
- [ ] Popping out from a paused source does not auto-start playback; closing without pressing play returns at the timestamp and stays paused. **(REQ-RETURN-01)**
- [ ] Popping out from a paused source, pressing play in the popout, then closing returns at the timestamp and resumes. **(REQ-RETURN-01)**
- [ ] Seek player to 0, then close — source returns to 0, not a stale timestamp.
- [ ] Rapid double-click on Pop out opens only one player.
- [ ] Launching PiPlay while it is already running focuses the existing instance — no second process or WebView2 user-data contention. **(REQ-APP-01)**
- [ ] `watch?v=X&list=PL...` preserves video `X` plus playlist context.
- [ ] `playlist?list=PL...` starts the first playable playlist item.
- [ ] Unsupported/mix/radio/restricted list, e.g. `list=RD...`, falls back to single current video with a non-blocking note; popout does not fail.
- [ ] Source Window external non-YouTube link opens in the system browser without hijacking the WebView.
- [ ] Popout Player blocks or externally redirects off-YouTube navigation; the player never wanders.
- [ ] Sign-in / new-window popup is handled intentionally on the allowed Google auth surface.
- [ ] With Auto on, navigating to a `/watch` video that plays auto-starts the popout once for the
  currently armed identity. If the page's canonical URL is stale/different, the visible Source URL wins
  and the Popout opens that same video (one Source-first identity from detection through launch).
- [ ] Every return path re-arms Auto with the video restored to the Source Window before playback
  resumes: the returned video does **not** immediately re-pop; a *different* playing video does.
- [ ] A Short (`/shorts/`) does **not** auto-pop; enabling Auto on an already-playing video pops it **immediately**.
- [ ] Toggling Auto off stops auto-popping; the setting persists across restarts; manual Pop out is unaffected.

## 2. Window quality (Q-7)
- [ ] Player drags smoothly from the guaranteed 44 DIP top handle and from passive picture pixels or
  unused YouTube chrome space after a deliberate drag threshold; a normal click still
  plays/pauses and does not move the window. The handle shows a move cursor, while its adjacent
  Settings/Fade/Pin/Expand/Close controls remain ordinary buttons.
- [ ] **Phase 3 — resize zones:** edge resize feels native on the Source Window and Popout Player **with the pointer over the video/page surface itself** (the WebView2 area — the originally reported dead zone), not only over the chrome strip/toolbar: left/right/bottom resize cursors appear without pixel-perfect aiming on the owner-tuned 12 DIP inset band (`REQ-WINDOW-02`; the visible band is the accepted trade-off).
- [ ] **Phase 3 — resize zones:** corner resize feels native on the Source Window and Popout Player **including corners reached over the player surface**: each corner gives diagonal resize over the first/last ~96 DIP along the edge band, and normal page-mode player remains usable at 320x180.
- [ ] **Phase 3 — resize zones:** expanded resize zones do not swallow controls: Source caption buttons and Popout Fade/Pin/Expand/Close remain clickable outside the outer resize band.
- [ ] Pin/topmost is obvious and independent for the player and the Source Window.
- [ ] Player remembers size and position across restarts.
- [ ] Player restores onto a visible monitor after a monitor is removed.
- [ ] Crisp at 100%, 125%, 150%, and mixed-monitor DPI. **(Q-7)**
- [ ] **Round Popout silhouette (ADR-0008):** with Soft Glass or Corners → Round, the floating
  Popout has a clean large-radius outline and the video is clipped at all four corners. Resize at
  100%, 125%, and 150%; move between mixed-DPI monitors; maximize/restore; and exercise Windows Snap
  halves/quarters. Floating restores the curve, while maximized and snap-like layouts are full-bleed
  with no stale crop, seams, or lost diagonal-resize acquisition. **(Q-7, REQ-WINDOW-01/02)**
- [ ] App does not steal focus unexpectedly.

### 2.1 Popout behaviors, two captures (overhaul stabilization 2026-06-10)

Popout Standard and Popout Fullview Faded are ONE normal-page `PlayerWindow` path captured twice
(spec settled decision 3): each row below is one behavior and one procedure — run it in both
captures and record the evidence per column, never write per-state procedures.

| Behavior (one procedure) | Popout Standard | Fullview Faded |
|---|---|---|
| Wheel scroll needs one click into the page first (wheel focus-routing; documented owner decision 2026-06-10), then wheel/touchpad/page-scrollbar scroll works — including at reduced opacity. | | |
| Wheel over the 12 DIP resize band is inert by design (the band belongs to the window, not the page); in-page scroll just inside the band is unaffected. | | |
| Expand button on the strip toggles full-monitor expand and back; glyph and tooltip flip Expand/Restore together. | | |
| Restore stays reachable while expanded: the strip (or its top-edge reveal under auto-hide) exposes the restore button; Esc restores after clicking the strip once (WPF focus). | | |
| Close while expanded, pop out again: the next popout launches at the prior normal bounds, never expanded. | | |
| Auto-hide reveal-then-resize beat (Task 1 residual): with the strip collapsed, the TOP edge band is dead until the top-edge hover reveals the strip; after the reveal, top-edge resize works. | | |
| The Popout Settings gear opens the same single Settings dialog as the Source Window gear; invoking the other entry point activates that dialog instead of opening a second copy. | | |

### 2.2 Focused presentation and passive-picture drag (2026-07-15)

Run these rows on the deployed Stable copy with both **Standard** and **Focused overlay**. A dirty
`-AllowDirty` deployment may be used for exploratory diagnostics, but its results are not release
evidence and do not complete this checklist.

- [ ] **Standard remains the default:** reset app state or start with no presentation field, pop out a
  `/watch` video, and confirm the ordinary Normal YouTube page opens. Compact remains dormant.
- [ ] **Threshold drag:** click once on passive video and confirm YouTube receives the click; then drag
  from passive picture pixels and confirm the Popout moves only after real pointer movement. Repeat
  with mouse and pen where available. **(Q-7, Q-8)**
- [ ] **Child-frame crash regression:** auto-open a Standard Popout, leave it open through the first
  YouTube child-frame load, then drag from passive picture pixels. The Popout remains alive and the
  Windows Application log records no PiPlay `Application Error` / `STATUS_BREAKPOINT` event.
- [ ] **Drag exclusions:** seek/timeline, volume, captions, settings, fullscreen, links, menus, end
  cards, ads with actions, and every Focused overlay control remain clickable and never begin a
  window move. Edge/corner resize remains acquirable outside those controls. **(Q-5, Q-7, Q-8)**
- [ ] **Maximized/stale guard:** passive-picture drag does nothing while expanded/maximized; releasing
  the button before native handoff does not move the window. Restore and confirm drag works again.
- [ ] **Focused full-frame contain:** enable Settings → Appearance → Focused overlay, pop out wide, 16:9,
  4:3, and portrait videos, then freely resize wide and tall. The real watch-page player fills the
  available viewport, letterboxes when needed, and never crops (`contain`, never `cover`).
- [ ] **Focused controls:** Mute, Play/Pause, seek rail, PiPlay Settings, Pin, Expand/Restore, and Close work. Captions
  and Next work when YouTube exposes their native controls and degrade harmlessly when unavailable.
  YouTube branding, settings/quality, captions menu, and ad UI remain reachable. **(Q-3, Q-5, Q-6)**
- [ ] **Active-ad fail-closed behavior:** exercise real `ad-showing` and `ad-interrupting` states in
  both Standard and Focused presentation. Focused seek and Next are hidden/disabled, direct media
  writes/native Next handoff do not run, and YouTube's required ad UI, disclosures, links, and Skip
  control remain reachable. **(Q-5)**
- [ ] **Trusted current-document actions:** synthetic `.click()` / dispatched pointer events do not
  close, pin, expand, open Settings, move the Popout, seek, or advance. Repeat video A→B,
  watch→non-watch→watch, and an exact-URL reload; requests from the previous document are rejected
  while real input in the current document still works. **(Q-3, Q-6, Q-8)**
- [ ] **Overlay input/accessibility:** empty overlay pixels pass input to the video; only actual buttons
  and the progress rail intercept input. Tab focus has readable names and a visible focus indicator.
- [ ] **Fade:** with Fade on, Focused controls hide after the configured delay while playing and reveal
  on pointer/keyboard activity, focus, or pause. Fade off keeps them visible; no state is click-through.
- [ ] **Presentation precedence (`REQ-PROFILE-01`):** global Standard/Focused controls new popouts;
  a matching profile's `Use global`, `Standard`, or `Focused overlay` value inherits or overrides per
  target video. A different video does not inherit an unrelated profile override.
- [ ] **Recovery:** navigate away from `/watch` or exercise a page where selectors are unavailable.
  The Focused layer withdraws or fails harmlessly; native strip drag, Settings, Pin, Expand/Restore,
  Close/return, timestamp capture, and Standard presentation remain available.

## 3. Fade and appearance (Q-8)
- [ ] Popout controls fade after idle and restore on hover / mouse-move.
- [ ] Settings → Appearance Accent color: picking a chip recolors the Source Pin, Popout Pin, AND Popout Fade glyphs to the SAME accent (one accent, not separate Pin/Fade colors), live on the open popout, and persists across restart.
- [ ] Settings → Appearance fade delay Short / Normal / Long changes the controls-fade idle timing; Normal is the default 2.5 s behavior.
- [ ] The player stays clickable at all times — clicks do **not** pass through. **(Q-8)**
- [ ] Whole popout opacity: Active and When-idle sliders apply live to the open popout; idle dims after the fade delay and movement over the player restores it.
- [ ] Whole popout opacity cannot drop below the 45% floor from the UI; the player stays fully interactable at every opacity. **(Q-8)** This is not video-safe chrome-only transparency; video also fades.
- [ ] Opacity scope is exact: **In use** changes only the Source title-bar backdrop (not the Source
  WebView or title-bar controls as a group) and the whole active Popout; **When idle** changes only the
  whole Popout. Movement restores the configured In-use level, not necessarily 100%.
- [ ] Select each preset with no behavior overrides and verify active/idle opacity: Sharp Dark
  `1.00 / 1.00`, Minimal `0.94 / 0.86`, Soft Glass `0.82 / 0.72`.
- [ ] Auto-hide top bar defaults **on for Sharp Dark, Minimal, and Soft Glass**. With Fade on, each strip
  collapses after its preset delay and top-edge hover reveals it; turning Fade off keeps it visible.
- [ ] The preset cards communicate the intended look: Sharp Dark `Crisp · 100%`, Minimal
  `Quiet · 94%`, Soft Glass `Glass · 82%`; their tooltips/accessible names describe crisp/opaque,
  quiet/warm, and airy/translucent respectively.
- [ ] Full appearance preview transaction: with Source and Popout open, click every preset and corner
  option. Shared theme resources, native corners, Source title-bar backdrop, and the whole Popout update
  live. Dismiss with title-bar close and again with Escape: every surface returns to its exact pre-dialog
  appearance. Repeat and press Done: the previewed values persist after restart.
- [ ] **Overhaul Task 5 — Settings fits short displays:** on (or simulating) a ~768 px work area, the Settings window caps at the work area, the sections scroll, the title-bar close button never scrolls away, and all four sections are reachable in order: Privacy, Appearance, Playback, Advanced.
- [ ] Settings exposes no Compact player toggle/copy; new popouts use Normal while `PlaybackModePolicy.CompactPlayerEnabled=false`.
- [ ] Settings exposes **Focused overlay** as a Popout presentation option, not a Compact playback-mode
  toggle; changing it affects new Popout Players and does not rewrite an already-open player.
- [ ] **Overhaul Tasks 9-10 — Theme preset + accent smoke:** Settings → Appearance shows a Theme row (Sharp Dark / Minimal / Soft Glass) and a single Accent color chip row. Selecting a preset checks it and adopts that preset's default accent. The chosen accent recolors the primary "Pop out video" button, the URL caret/focus, and the Pin/Fade glyphs LIVE on the open main window (DynamicResource); a newly launched Popout Player also uses it. Restart and confirm the accent persists and is applied at startup. A hand-edited invalid `theme.themeId`/`accentColor` in settings.json falls back to Sharp Dark / cyan without crashing.
- [ ] **Video-aware return (Normal mode):** let the popout move to a different video (playlist auto-advance or normal-page in-page navigation), then close it — the source NAVIGATES to that video at the popout's timestamp instead of seeking the original video, then replays play/pause, volume/mute, and speed where YouTube permits; with Auto on, the returned video does not instantly re-pop.
- [ ] **Bring video back (P4):** pop out a video, pause/change volume/mute/speed in the popout, then click **Bring video back** in the Source Window — playback returns to the Source Window with timestamp and play/pause preserved, and volume/mute/speed preserved where YouTube permits.
- [ ] **Plain X-close/Alt-F4 return:** pop out a video, pause/change volume/mute/speed in the popout, then close the popout from its window chrome — playback returns to the Source Window with timestamp and play/pause preserved, and volume/mute/speed preserved where YouTube permits. Repeat once by closing immediately after popout launch to confirm the source does not return silent.
- [ ] **Visible Source recovery:** minimize the Source while a Popout is open, then return once with
  **Bring video back**, once with native X, and once with Focused Close. Source restores and activates;
  a previously maximized Source returns maximized, and app shutdown does not reopen it.
- [ ] **Single-flight return:** during same-video return scripting and different-video navigation/replay,
  the primary action says **Returning video...** and remains disabled. Repeated clicks and Auto do not
  create a new Popout until the return settles, fails, or times out.
- [ ] **Show versus Bring:** minimize the Popout, then use **Show Popout** from the Source toolbar and
  placeholder. It restores/focuses the existing player without closing it. **Bring video back** still
  transfers playback and closes the player.
- [ ] **Hidden Source commands:** while the Tier-1 placeholder hides YouTube, Back/Reload/Home, URL,
  profile selection, and profile Save/Edit/Delete are disabled. Auto can still be turned off and both
  recovery actions remain reachable.
- [ ] **Pin transition matrix:** test Source pinned/unpinned x Popout pinned/unpinned. Source never covers
  the active player; on every return it restores the actual pre-popout Source Pin state, including a
  pinned profile, and the two persisted preferences remain separate after restart.
- [ ] **Source minimum restore:** save/restart Source at its minimum on 100/125/150% DPI and after moving
  between mixed-DPI monitors. It never restores or interactively resizes below 760 x 480 DIP, and the
  YouTube browsing region remains usable.

## 3.5 Compact player plumbing (dormant)
- [ ] `PlaybackModePolicy.CompactPlayerEnabled` remains `false` for this release.
- [ ] New popouts resolve to Normal even if `PlayerSettings.CompactMode` or `Profile.Mode=compact` exists in settings data.
- [ ] Settings exposes no Compact player toggle.
- [ ] Focused presentation still uses a real Normal `/watch` page and does not navigate to an embed or
  `piplay.local` shell.
- [ ] If Compact is deliberately re-enabled later, restore the compact-player manual rows from history, including recommendation retarget/fallback and compact YouTube fullscreen, then re-run them before release.

## 4. Recovery / errors (Q-6)
- [ ] Missing WebView2 runtime: friendly message, no crash.
- [ ] Corrupt `settings.json`: app recovers with defaults (bad file renamed, not lost).
- [ ] Network loss: error state with retry, no crash.
- [ ] Navigation failure: compact in-app error with retry; URL preserved.

## 5. Reliability
- [ ] Two hours of repeated popout/return cycles, no leak or degradation.
- [ ] Open/close the app 20x: settings persist every time.
- [ ] Works logged in and logged out; autoplay allowed and blocked.

## 6. Performance
- [ ] Startup feels fast for a utility.
- [ ] Warm WebView: Popout Player shows video within about 1.5 s of pressing **Pop out video**; cold first-run WebView2 init is exempt.
- [ ] CPU/GPU comparable to a normal browser tab playing the same video.
- [ ] No unbounded log or settings growth.
- [ ] Open Settings (gear) → **Reset app state** → confirm. Settings/profiles/placement clear, but the YouTube tab is **still signed in** (no re-login, no 2FA). **(REQ-PRIVACY-01)**
- [ ] Settings → **Clear browser data** is a separate, red-confirmed action; after confirming, reload youtube.com and verify you are **signed out**. **(REQ-PRIVACY-02)**
- [ ] The two actions are clearly worded as distinct; the Clear confirm warns about signing out; Cancel (not the destructive button) has default focus.
- [ ] With the WebView2 runtime missing (recovery panel showing), the Clear browser data button is disabled or reports "browser isn't ready"; Reset still works.

## 7. Packaging
- [ ] `bin/` and `obj/` excluded from the repo / ZIP.
- [ ] Signing status recorded (locally self-signed / unsigned-internal). **Not a gate** — signing is
  optional via `Publish-Stable.ps1 -SignScript <path>`; release provenance is the exact-source commit,
  the stable tag, and `Verify-StableDeploy.ps1`. See `SPEC_GAPS_AND_OWNERSHIP.md` (REQ-RELEASE-01).
- [ ] Release notes mention the WebView2 Evergreen runtime requirement.

## 8. Chrome / visual identity (REQ-UI-01, REQ-UI-02)

Binary pass/fail (spec section 22.2 Chrome acceptance). Prefer ChatGPT-operated screenshot capture when local UI/screenshot access is available; otherwise capture manually. Save evidence into `docs/evidence/` per `Chrome_UI_Screenshot_Test_Procedure.md`, including the MUI-01 through MUI-12 manual UI cases where reachable, and attach as proof. A build is **not** a release candidate until every item passes — this is an equal gate to the functional checks (spec section 22.5 Definition of Done).

- [ ] **UI-CHK-1** All chrome icons render (window controls, navigation, save, Pin, Pop out, Popout Settings, placeholder) — **zero** empty boxes. **(REQ-UI-02)**
- [ ] **UI-CHK-2** Profiles dropdown (closed) renders dark, not a light platform control. **(REQ-UI-01)**
- [ ] **UI-CHK-3** Profiles dropdown (open) + its items render dark; empty list looks intentional, not a blank light box. **(REQ-UI-01)**
- [ ] **UI-CHK-4** Tooltips render dark and do not occlude the control they describe (esp. caption buttons). **(REQ-UI-01)**
- [ ] The Source profile actions menu opens dark, supports arrow keys/access keys/Escape, and keeps
  Save/Edit/Delete readable with correct disabled states. At compact Source width, only the transfer
  label hides; its icon, tooltip, and accessible name remain.
> **Automated coverage (Lane A, `dotnet test`):** UI-CHK-5's clipping cause (`UseLayoutRounding`)
> and the contrast/resource/icon-font checks are now guarded by the markup (Layer 1) and live-WPF
> (Layer 3) tests; the rows below remain the **true-render** confirmation via the manual smoke
> (`scripts/Test-UiSmoke.ps1`, Lane B) at fractional DPI. See `tests/README.md`.

- [ ] **UI-CHK-5** Address/URL field text is legible at 100/125/150 % DPI — no clipping or faint text.
- [ ] **UI-CHK-6** Icons share weight, corner style, and active-color behavior across the chrome.
- [ ] **UI-CHK-7** Accessible names (overhaul Task 7, REQ-UI-02): a screen reader (Narrator / Accessibility Insights) announces real names for every icon-only control — main chrome (Settings/Minimize/Maximize/Close), navigation (Back/Reload/Home), URL box, profiles combo + profile actions menu, Pin, Auto, Show Popout, the transfer action (name flips across "Pop out video"/"Bring video back"/"Returning video..."), popout Fade/Pin/Settings/Expand/Close, Settings close, and the Prompt dialog close. Source and Popout Pin names flip between Pin and Unpin with actual state.
- [ ] **UI-CHK-8** *(P1 borderless, owner-retuned)* The 12 DIP black band around the hosted video reads as
  **letterbox/canvas** — not as a grey frame and not as a second app frame — on both windows at
  **100 / 125 / 150 % DPI**. This is a *visual read*, distinct from the resize-feel rows in §4.
- [ ] **UI-CHK-9** *(profile rail + accent wash, open)* Across **every theme preset** and at 100/125/150 %
  DPI: the Source Window title-bar accent wash stays a restrained **tint**, never a saturated banner; and
  the profile identity rail stays visible against the row it sits on — including a **very dark** profile
  color (which is contrast-lifted for presentation only) and a profile with **no** color (rail fully
  transparent, gutter retained, rows do not shift).
- [ ] **UI-CHK-10** *(accent reach + P2, v0.9.0 — OWNER JUDGEMENT, the real gate)* Selecting a profile
  with a color visibly **re-tints the app**: the toolbar glyphs (Back/Reload/Home/Save/Edit/Delete), the
  Pop-out button, and the title-bar wash all take that color. Selecting a profile with **no** color falls
  back to the global accent. At the reach dial's default **50**, compare directly with v0.9.0: toolbar
  glyphs must still be full accent and the wash must still be the 1.45 look. Then judge by eye:
  - **Is the wash right?** Below 50 it softens from the v0.9.0 1.45 target toward off; above 50 it
    deepens toward the restrained 1.90 ceiling. Too weak? Too loud?
  - **Are accented toolbar glyphs right,** or do they read as busy? The caption row (Settings/Minimize/
    Maximize/Close) is deliberately left neutral; confirm that split looks intentional rather than
    inconsistent.
  - Window controls unchanged, **Close still hovers red**. No new border, line, or fill anywhere.
- [ ] **UI-CHK-11** *(accent editing, v0.9.0)* With a **colored** profile active, open Settings: the hint
  above the accent picker names that profile, the preview is live, and **Done sticks** (it edits that
  profile's color, not the global). With **no** profile (or a colorless one) active, the hint says "app
  accent" and Done edits the global default. Regression check: note the global color, select a colored
  profile, change only another Appearance control, press Done, then deselect the profile; the original
  global color must return unchanged.
- [ ] **UI-CHK-12** *(preset cards + preview)* The three Settings cards visibly read `Crisp · 100%`,
  `Quiet · 94%`, and `Glass · 82%`; preset/corner clicks preview across Source and Popout immediately,
  and a non-Done dismissal restores the exact starting look without a flash of persisted state.
