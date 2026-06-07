# Borderless resize zones - implementation plan

**Spec:** `docs/superpowers/specs/2026-06-07-borderless-resize-zones-design.md`

**Goal:** Expand the invisible resize hit area for PiPlay's borderless Source Window and Popout
Player from the previous 6 DIP `WindowChrome` band to a more forgiving native-feeling target:
10 DIP edge resize zones and 32 DIP corner lengths. Preserve opaque WebView2 hosting, current
minimum sizes, and all existing playback/window behavior.

**Result:** Implemented in the working tree. The deterministic test lane and build gate pass; manual
DPI resize QA remains a release-candidate check because it requires live cursor/drag verification at
display scale.

## Tasks

- [x] **Task 1 - Add pure resize-zone policy.**
  - Add a small policy/classifier under `src/PiPlay/Services/` with constants:
    `ResizeBorderDip = 10`, `CornerLengthDip = 32`.
  - Inputs: window size in DIP, point in DIP relative to the window, and whether the window is
    resizable/normal.
  - Outputs: nullable/native hit-test result for `HTLEFT`, `HTRIGHT`, `HTTOP`, `HTBOTTOM`,
    `HTTOPLEFT`, `HTTOPRIGHT`, `HTBOTTOMLEFT`, and `HTBOTTOMRIGHT`.
  - Corners win before edges. The corner length extends along the edge band; it is not a full
    32 x 32 DIP square.
  - Verification: new logic tests for all eight results, near misses, max-window suppression, and
    boundary points.
  - Commit: `feat(window): add resize-zone hit-test policy (REQ-WINDOW-02)`

- [x] **Task 2 - Wire WM_NCHITTEST for borderless windows.**
  - Extend `BorderlessWindowHelper` or add a sibling helper that attaches an HWND hook after
    `SourceInitialized`.
  - Handle `WM_NCHITTEST`, extract signed screen coordinates safely, convert to WPF DIP, run the
    classifier, and return the Win32 result when a resize zone matches.
  - Keep the existing `WM_GETMINMAXINFO` maximize-work-area behavior intact.
  - Do not report resize zones while maximized or when `ResizeMode` is not resizable.
  - Verification: logic tests for coordinate conversion helpers if extracted; WPF construction test
    that helper attachment does not throw.
  - Commit: `feat(window): use native hit testing for larger resize zones`

- [x] **Task 3 - Apply to Source Window and Popout Player.**
  - Enable expanded resize zones from `MainWindow` and `PlayerWindow`.
  - Increase `WindowChrome.ResizeBorderThickness` from `6` to `10` if it remains the edge fallback.
  - Ensure Source caption buttons and Popout Fade/Pin/Close controls remain clickable outside the
    outer 10 DIP band.
  - Verification: `dotnet test --filter Category=Markup` and targeted WPF tests.
  - Commit: `fix(window): enlarge borderless resize target`

- [x] **Task 4 - Update docs, checklist, and changelog.**
  - Update `docs/CHANGELOG.md` with a user-visible fix entry.
  - Confirm `docs/QA_Checklist.md` includes edge/corner resize checks at 100%, 125%, and 150% scale.
  - Confirm product spec `REQ-WINDOW-02` matches the code constants.
  - Verification: docs review plus `rg` to confirm no implementation/test invariant still requires
    `ResizeBorderThickness="6"`.
  - Commit: `docs(window): record borderless resize target`

- [x] **Task 5 - Full deterministic gate.**
  - Run:

    ```powershell
    dotnet test PiPlay.sln --configuration Debug
    .\Build-PiPlay.ps1 -Stage Build -NoVersionBump -NoBuildNumberBump
    ```
  - Verified: `dotnet test PiPlay.sln --configuration Debug` = 254/254 and
    `.\Build-PiPlay.ps1 -Stage Build -NoVersionBump -NoBuildNumberBump` passed with 0 warnings /
    0 errors.
  - Commit/PR summary should call out that no transparency/click-through behavior was added.

- [ ] **Task 6 - Manual release-candidate resize smoke.**
  - Manually verify resize cursor and drag behavior on Source Window and Popout Player at 100%,
    125%, and 150% display scale. Confirm close/minimize/maximize, Pin, Fade, and Source toolbar
    controls still work.
  - If promoting a shareable build, capture evidence under `docs/evidence/`.

## Self-review

- Requirements -> tasks: `REQ-WINDOW-02` constants/classifier in Task 1; native hit testing in Task
  2; both borderless windows in Task 3; docs in Task 4; deterministic gate in Task 5; manual QA in
  Task 6.
- Ownership: window hit testing stays in `Services/BorderlessWindowHelper` or a sibling service;
  window code-behind only opts the windows in; playback, WebView2, settings, and placement services
  stay unchanged.
- Risk: highest risk is stealing clicks from caption/player controls or mishandling DPI/negative
  monitor coordinates. Pure boundary tests, signed-coordinate handling, and manual DPI checks cover
  that risk.
- Verified: deterministic gates passed:
  `dotnet test PiPlay.sln --configuration Debug --filter Category=Logic` = 205/205 and
  `dotnet test PiPlay.sln --configuration Debug --filter Category=Markup` = 26/26,
  `dotnet test PiPlay.sln --configuration Debug --filter Category=Wpf` = 23/23,
  `dotnet test PiPlay.sln --configuration Debug` = 254/254, and
  `.\Build-PiPlay.ps1 -Stage Build -NoVersionBump -NoBuildNumberBump` passed with 0 warnings /
  0 errors. The WPF lane includes a real `WM_NCHITTEST` check proving a 32 DIP corner point returns
  `HTTOPLEFT` through the installed HWND subclass. Manual DPI resize smoke remains a
  release-candidate check.
