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
- [ ] With Auto on, navigating to a `/watch` video that plays auto-starts the popout **once**.
- [ ] Returning from an Auto popout does **not** re-pop the same video; a *different* video does.
- [ ] A Short (`/shorts/`) does **not** auto-pop; enabling Auto on an already-playing video pops it **immediately**.
- [ ] Toggling Auto off stops auto-popping; the setting persists across restarts; manual Pop out is unaffected.

## 2. Window quality (Q-7)
- [ ] Player drags smoothly from the chrome area; video controls still work.
- [ ] **Phase 3 — resize zones:** edge resize feels native on the Source Window and Popout Player **with the pointer over the video/page surface itself** (the WebView2 area — the originally reported dead zone), not only over the chrome strip/toolbar: left/right/bottom resize cursors appear without pixel-perfect aiming on the 4 DIP inset band (`REQ-WINDOW-02`; the visible band is the accepted Task 1 trade-off).
- [ ] **Phase 3 — resize zones:** corner resize feels native on the Source Window and Popout Player **including corners reached over the player surface**: each corner gives diagonal resize over the first/last ~32 DIP along the edge band, and normal page-mode player remains usable at 320x180.
- [ ] **Phase 3 — resize zones:** expanded resize zones do not swallow controls: Source caption buttons and Popout Fade/Pin/Expand/Close remain clickable outside the outer resize band.
- [ ] Pin/topmost is obvious and independent for the player and the Source Window.
- [ ] Player remembers size and position across restarts.
- [ ] Player restores onto a visible monitor after a monitor is removed.
- [ ] Crisp at 100%, 125%, 150%, and mixed-monitor DPI. **(Q-7)**
- [ ] App does not steal focus unexpectedly.

### 2.1 Popout behaviors, two captures (overhaul stabilization 2026-06-10)

Popout Standard and Popout Fullview Faded are ONE normal-page `PlayerWindow` path captured twice
(spec settled decision 3): each row below is one behavior and one procedure — run it in both
captures and record the evidence per column, never write per-state procedures.

| Behavior (one procedure) | Popout Standard | Fullview Faded |
|---|---|---|
| Wheel scroll needs one click into the page first (wheel focus-routing; documented owner decision 2026-06-10), then wheel/touchpad/page-scrollbar scroll works — including at reduced opacity. | | |
| Wheel over the 4 DIP resize band is inert by design (the band belongs to the window, not the page); in-page scroll just inside the band is unaffected. | | |
| Expand button on the strip toggles full-monitor expand and back; glyph and tooltip flip Expand/Restore together. | | |
| Restore stays reachable while expanded: the strip (or its top-edge reveal under auto-hide) exposes the restore button; Esc restores after clicking the strip once (WPF focus). | | |
| Close while expanded, pop out again: the next popout launches at the prior normal bounds, never expanded. | | |
| Auto-hide reveal-then-resize beat (Task 1 residual): with the strip collapsed, the TOP edge band is dead until the top-edge hover reveals the strip; after the reveal, top-edge resize works. | | |

## 3. Fade and appearance (Q-8)
- [ ] Popout controls fade after idle and restore on hover / mouse-move.
- [ ] Settings → Appearance Accent color: picking a chip recolors the Source Pin, Popout Pin, AND Popout Fade glyphs to the SAME accent (one accent, not separate Pin/Fade colors), live on the open popout, and persists across restart.
- [ ] Settings → Appearance fade delay Short / Normal / Long changes the controls-fade idle timing; Normal is the default 2.5 s behavior.
- [ ] The player stays clickable at all times — clicks do **not** pass through. **(Q-8)**
- [ ] Whole popout opacity: Active and When-idle sliders apply live to the open popout; idle dims after the fade delay and movement over the player restores it.
- [ ] Whole popout opacity cannot drop below the 45% floor from the UI; the player stays fully interactable at every opacity. **(Q-8)** This is not video-safe chrome-only transparency; video also fades.
- [ ] Auto-hide top bar (with Fade on): the strip collapses after the fade and the video fills the window; hovering the top edge reveals it.
- [ ] **Overhaul Task 5 — Settings fits short displays:** on (or simulating) a ~768 px work area, the Settings window caps at the work area, the sections scroll, the title-bar close button never scrolls away, and all four sections are reachable in order: Privacy, Appearance, Playback, Advanced.
- [ ] Settings exposes no Compact player toggle/copy; new popouts use Normal while `PlaybackModePolicy.CompactPlayerEnabled=false`.
- [ ] **Overhaul Tasks 9-10 — Theme preset + accent smoke:** Settings → Appearance shows a Theme row (Sharp Dark / Minimal / Soft Glass) and a single Accent color chip row. Selecting a preset checks it and adopts that preset's default accent. The chosen accent recolors the primary "Pop out video" button, the URL caret/focus, and the Pin/Fade glyphs LIVE on the open main window (DynamicResource); a newly launched Popout Player also uses it. Restart and confirm the accent persists and is applied at startup. A hand-edited invalid `theme.themeId`/`accentColor` in settings.json falls back to Sharp Dark / cyan without crashing.
- [ ] **Video-aware return (Normal mode):** let the popout move to a different video (playlist auto-advance or normal-page in-page navigation), then close it — the source NAVIGATES to that video at the popout's timestamp instead of seeking the original video, then replays play/pause, volume/mute, and speed where YouTube permits; with Auto on, the returned video does not instantly re-pop.
- [ ] **Bring video back (P4):** pop out a video, pause/change volume/mute/speed in the popout, then click **Bring video back** in the Source Window — playback returns to the Source Window with timestamp and play/pause preserved, and volume/mute/speed preserved where YouTube permits.
- [ ] **Plain X-close/Alt-F4 return:** pop out a video, pause/change volume/mute/speed in the popout, then close the popout from its window chrome — playback returns to the Source Window with timestamp and play/pause preserved, and volume/mute/speed preserved where YouTube permits. Repeat once by closing immediately after popout launch to confirm the source does not return silent.

## 3.5 Compact player plumbing (dormant)
- [ ] `PlaybackModePolicy.CompactPlayerEnabled` remains `false` for this release.
- [ ] New popouts resolve to Normal even if `PlayerSettings.CompactMode` or `Profile.Mode=compact` exists in settings data.
- [ ] Settings exposes no Compact player toggle.
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

- [ ] **UI-CHK-1** All chrome icons render (window controls, navigation, save, Pin, Pop out, placeholder) — **zero** empty boxes. **(REQ-UI-02)**
- [ ] **UI-CHK-2** Profiles dropdown (closed) renders dark, not a light platform control. **(REQ-UI-01)**
- [ ] **UI-CHK-3** Profiles dropdown (open) + its items render dark; empty list looks intentional, not a blank light box. **(REQ-UI-01)**
- [ ] **UI-CHK-4** Tooltips render dark and do not occlude the control they describe (esp. caption buttons). **(REQ-UI-01)**
> **Automated coverage (Lane A, `dotnet test`):** UI-CHK-5's clipping cause (`UseLayoutRounding`)
> and the contrast/resource/icon-font checks are now guarded by the markup (Layer 1) and live-WPF
> (Layer 3) tests; the rows below remain the **true-render** confirmation via the manual smoke
> (`scripts/Test-UiSmoke.ps1`, Lane B) at fractional DPI. See `tests/README.md`.

- [ ] **UI-CHK-5** Address/URL field text is legible at 100/125/150 % DPI — no clipping or faint text.
- [ ] **UI-CHK-6** Icons share weight, corner style, and active-color behavior across the chrome.
- [ ] **UI-CHK-7** Accessible names (overhaul Task 7, REQ-UI-02): a screen reader (Narrator / Accessibility Insights) announces real names for every icon-only control — main chrome (Settings/Minimize/Maximize/Close), navigation (Back/Reload/Home), URL box, profiles combo + Save/Edit/Delete, Pin, Auto, the popout action (name flips with "Pop out video"/"Bring video back"), popout Fade/Pin/Expand/Close, Settings close, and the Prompt dialog close.
- [ ] **UI-CHK-8** *(P1 borderless, open)* The 4 DIP black band around the hosted video reads as
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
  back to the global accent. Then judge the two tunables by eye and say if they are wrong:
  - **Is the wash right?** It was raised from a near-imperceptible 1.20:1 to 1.45:1
    (`ThemeColors.ShellTintContrastTarget` — one constant, easy to change either way). Too weak? Too loud?
  - **Are accented toolbar glyphs right,** or do they read as busy? The caption row (Settings/Minimize/
    Maximize/Close) is deliberately left neutral; confirm that split looks intentional rather than
    inconsistent.
  - Window controls unchanged, **Close still hovers red**. No new border, line, or fill anywhere.
- [ ] **UI-CHK-11** *(accent editing, v0.9.0)* With a **colored** profile active, open Settings: the hint
  above the accent picker names that profile, the preview is live, and **Done sticks** (it edits that
  profile's color, not the global). With **no** profile (or a colorless one) active, the hint says "app
  accent" and Done edits the global default.
