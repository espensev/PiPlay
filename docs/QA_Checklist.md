# PiPlay — Manual QA Checklist

Re-runnable manual test pass for each shareable build. Derived from spec section 22 (Quality gates). Copy this list per release and fill it in.

- **Build / version:** ____________________
- **Date / tester:** ____________________
- **OS / DPI / monitors:** ____________________
- **WebView2 runtime version:** ____________________

Mark each: pass / issue (link) / skipped.

## 1. Functional (Q-1, Q-2)
- [ ] youtube.com loads without crashing.
- [ ] A `watch?v=` URL loads and plays.
- [ ] Warm WebView: after the Popout Player has played for about 3 s, its timestamp is within 2 s of (expected source timestamp + elapsed playback time); target ≤1 s.
- [ ] Source audio stops on popout — no duplicate audio. **(Q-1)**
- [ ] Source Placeholder is visible with no WebView bleed-through.
- [ ] Closing the player after popping out from a playing source returns and resumes. **(Q-2, REQ-RETURN-01)**
- [ ] Closing the player after popping out from a paused source returns at the timestamp and stays paused. **(REQ-RETURN-01)**
- [ ] Seek player to 0, then close — source returns to 0, not a stale timestamp.
- [ ] Rapid double-click on Pop out opens only one player.
- [ ] Launching PiPlay while it is already running focuses the existing instance — no second process or WebView2 user-data contention. **(REQ-APP-01)**
- [ ] `watch?v=X&list=PL...` preserves video `X` plus playlist context.
- [ ] `playlist?list=PL...` starts the first playable playlist item.
- [ ] Unsupported/mix/radio/restricted list, e.g. `list=RD...`, falls back to single current video with a non-blocking note; popout does not fail.
- [ ] Source Window external non-YouTube link opens in the system browser without hijacking the WebView.
- [ ] Popout Player blocks or externally redirects off-YouTube navigation; the player never wanders.
- [ ] Sign-in / new-window popup is handled intentionally on the allowed Google auth surface.
- [ ] **Phase 2 — Auto:** with Auto on, navigating to a `/watch` video that plays auto-starts the popout **once**.
- [ ] **Phase 2 — Auto:** returning from an Auto popout does **not** re-pop the same video; a *different* video does.
- [ ] **Phase 2 — Auto:** a Short (`/shorts/`) does **not** auto-pop; enabling Auto on an already-playing video pops it **immediately**.
- [ ] **Phase 2 — Auto:** toggling Auto off stops auto-popping; the setting persists across restarts; manual Pop out is unaffected.

## 2. Window quality (Q-7)
- [ ] Player drags smoothly from the chrome area; video controls still work.
- [ ] Edge/corner resize feels native; normal page-mode player is usable at 320x180.
- [ ] Pin/topmost is obvious and independent for the player and the Source Window.
- [ ] Player remembers size and position across restarts.
- [ ] Player restores onto a visible monitor after a monitor is removed.
- [ ] Crisp at 100%, 125%, 150%, and mixed-monitor DPI. **(Q-7)**
- [ ] App does not steal focus unexpectedly.

## 3. Fade / opacity (Q-8)
- [ ] MVP: Popout controls remain visible and usable; no fade required.
- [ ] Phase 2: Controls fade after idle and restore on hover / mouse-move.
- [ ] Phase 4: With whole-window opacity on, the player stays clickable — clicks do **not** pass through. **(Q-8)**
- [ ] Phase 4: Opacity cannot drop below the 45% normal floor without an explicit unlock.

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
- [ ] Phase 2: Open Settings (gear) → **Reset app state** → confirm. Settings/profiles/placement clear, but the YouTube tab is **still signed in** (no re-login, no 2FA). **(REQ-PRIVACY-01 — not in MVP)**
- [ ] Phase 2: Settings → **Clear browser data** is a separate, red-confirmed action; after confirming, reload youtube.com and verify you are **signed out**. **(REQ-PRIVACY-02 — not in MVP)**
- [ ] Phase 2: The two actions are clearly worded as distinct; the Clear confirm warns about signing out; Cancel (not the destructive button) has default focus.
- [ ] Phase 2: With the WebView2 runtime missing (recovery panel showing), the Clear browser data button is disabled or reports "browser isn't ready"; Reset still works.

## 7. Packaging
- [ ] `bin/` and `obj/` excluded from the repo / ZIP.
- [ ] Release binaries signed with the SevIQ code-signing certificate.
- [ ] Release notes mention the WebView2 Evergreen runtime requirement.

## 8. Chrome / visual identity (MVP — REQ-UI-01, REQ-UI-02)

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
