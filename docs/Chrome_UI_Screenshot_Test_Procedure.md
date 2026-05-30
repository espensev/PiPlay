# Chrome UI Screenshot Test Procedure

Review date: 2026-05-30  
Workspace: `D:\Development\DesktopApps\PiPlay`  
Build reviewed: `bin\publish\latest\PiPlay.exe`  
Related findings report: `docs/Chrome_UI_Spec_Review.md`  
Earlier first-pass report: `docs/Chrome_UI_Issue_Report.md`

## Purpose

This document describes how the screenshot-backed chrome UI review was performed.
The existing review is not an automated xUnit or Playwright test. It is a manual visual QA pass where the running app is exercised, screenshots are captured for specific UI states, and the visible UI text/control behavior is cross-checked against the current WPF source and product spec.

For maximum efficiency, the preferred path is for ChatGPT to perform as much of this loop as the local tools allow: launch the reviewed build, drive the UI states, capture screenshots into `docs/evidence`, inspect the images, read the source/spec, and write the findings. The user should only need to step in for states that require credentials, personal account decisions, OS permission prompts, or desktop interactions ChatGPT cannot access.

## Role Split

- Preferred: ChatGPT exercises reachable UI states, captures screenshot evidence, reviews the screenshots, reads visible UI text/control states, checks the corresponding source files, compares behavior against the product spec, and writes the findings report.
- User-assisted fallback: the user captures any UI states ChatGPT cannot reach and places the screenshots in `docs/evidence`; ChatGPT then performs the same screenshot/source/spec review.
- The user remains the decision point for sign-in, account-specific YouTube content, permission prompts, and any destructive or privacy-sensitive local action.

## Preferred ChatGPT-Operated Flow

When ChatGPT has local desktop/screenshot access, use this as the default flow:

1. ChatGPT identifies the reviewed executable and records the build path/version.
2. ChatGPT launches the app and waits for the main window to stabilize.
3. ChatGPT captures the base screenshot.
4. ChatGPT drives each reachable UI state: hover buttons, focus the URL box, open the Profiles ComboBox, toggle Pin, open the Save Profile dialog when safe, start popout when a usable video is already available, and inspect the Player Window.
5. ChatGPT saves each screenshot under `docs/evidence` using the naming pattern in this document.
6. ChatGPT opens the screenshots, reads the visible text and control states, and records pass/issue/conditional/skipped results.
7. ChatGPT cross-checks each observation against source and spec before writing findings.
8. ChatGPT asks the user only for blocked states, such as sign-in-required YouTube screens, account-specific content, system permission dialogs, or a state that cannot be triggered from available tools.

This keeps the user out of repetitive screenshot collection and makes the manual review closer to a repeatable semi-automated QA pass.

## Standing Manual Test Method

Until this is replaced by automated UI automation, this is the accepted manual test method for chrome/UI review. It is intentionally written so ChatGPT can run the screenshot pass directly when tool access is available, or can review user-captured screenshots when direct UI control is unavailable.

1. Record the build and environment.

   Fill in:

   - App build path.
   - App version/build number if known.
   - Date and tester.
   - Windows scale/DPI.
   - Monitor count.
   - Whether YouTube was logged in.

2. Start from a fresh visible app state.

   ChatGPT should launch the reviewed executable when it can. Otherwise, the user launches it. Wait for the PiPlay window to finish painting and for WebView2/YouTube content to become visually stable enough that the app chrome can be inspected.

3. Capture one screenshot per UI state.

   ChatGPT should capture screenshots directly into `docs/evidence` when possible. If not, the user captures and places them there. Capture the full PiPlay window unless the issue needs a tight crop. Keep the original screenshot files in `docs/evidence`. Do not edit the screenshots except for file naming.

4. Name screenshots by build and state.

   Use a stable naming pattern:

   - `chrome-<build>-base.png`
   - `chrome-<build>-back-tooltip.png`
   - `chrome-<build>-reload-tooltip.png`
   - `chrome-<build>-home-tooltip.png`
   - `chrome-<build>-url-focused.png`
   - `chrome-<build>-profiles-open-empty.png`
   - `chrome-<build>-profile-save-dialog.png`
   - `chrome-<build>-pin-enabled.png`
   - `chrome-<build>-popout-source-placeholder.png`
   - `chrome-<build>-player-base.png`

   Existing evidence from the first pass used `chrome-current-*` names. New runs can keep that pattern for a single current run, or include the build number when preserving multiple runs.

5. Read visible UI text and control state from each screenshot.

   ChatGPT should open each captured image and inspect:

   - Visible text.
   - Icon/glyph rendering.
   - Hover, focus, enabled, disabled, selected, checked, and open states.
   - Popup, tooltip, and dialog theme.
   - Whether text is clipped, unreadable, low contrast, or overlapped.
   - Whether any visible element looks like a default OS control leaking into the dark app shell.

6. Cross-check the screenshot against the source.

   Use the current source files to confirm what the UI element is supposed to be:

   - `src\PiPlay\MainWindow.xaml`
   - `src\PiPlay\MainWindow.xaml.cs`
   - `src\PiPlay\PlayerWindow.xaml`
   - `src\PiPlay\PlayerWindow.xaml.cs`
   - `src\PiPlay\Prompt.cs`
   - `src\PiPlay\Theme\ControlStyles.xaml`
   - `src\PiPlay\Theme\Colors.xaml`

7. Cross-check the behavior against the spec.

   Check the current product/spec docs before calling a screenshot observation a failure:

   - `docs\PiPlay_Product_Engineering_Spec.md`
   - `docs\SPEC_GAPS_AND_OWNERSHIP.md`
   - `docs\adr\0001-app-shell-wpf.md`
   - `docs\QA_Checklist.md`

8. Classify each observation.

   Use one of these outcomes:

   - `Pass`: screenshot, source, and spec agree.
   - `Issue`: visible runtime behavior violates source intent or spec.
   - `Conditional`: screenshot shows a problem, but source/spec evidence makes the root cause uncertain.
   - `Skipped`: state could not be reached in that run.
   - `Needs live repro`: screenshot alone is insufficient, and the state must be re-exercised.

9. Write or update the report.

   For each issue, include:

   - Screenshot filename.
   - What was visible.
   - Source location.
   - Spec location.
   - Pass/fail assessment.
   - Confidence level.

## Expanded Manual UI Element Coverage

These cases extend the first screenshot pass so most identifiable source-window and player-window elements are covered. They should be run as a manual UI test until further notice.

### MUI-01 Main Window Base Chrome

Action:

1. Launch `bin\publish\latest\PiPlay.exe`.
2. Wait for the main window and YouTube content to appear.
3. Capture `chrome-<build>-base.png`.

Inspect:

- App icon is visible at the upper left.
- Title text reads `PiPlay`.
- Main window background and toolbar are dark.
- Caption buttons are visible at the upper right.
- Toolbar contains Back, Reload, Home, URL box, Profiles ComboBox, Save Profile, Pin, and `Pop out video`.
- No control text is clipped, overlapped, or unreadable.
- No icon renders as an empty `.notdef` box.

Source references:

- `src\PiPlay\MainWindow.xaml`
- `src\PiPlay\Theme\ControlStyles.xaml`
- `src\PiPlay\Theme\Colors.xaml`

### MUI-02 Caption Buttons and Tooltips

Action:

1. Hover `Minimize`.
2. Capture `chrome-<build>-minimize-tooltip.png`.
3. Hover `Maximize` or `Restore`.
4. Capture `chrome-<build>-maximize-tooltip.png`.
5. Hover `Close`.
6. Capture `chrome-<build>-close-tooltip.png`.

Inspect:

- Tooltips read `Minimize`, `Maximize`, and `Close`.
- The hovered button has a visible hover state.
- Caption glyphs render as real icons, not empty boxes.
- Tooltips match the dark app styling.
- Tooltips do not cover neighboring caption buttons in a way that blocks inspection or use.
- Maximize changes to Restore after the window is maximized.

Source references:

- `src\PiPlay\MainWindow.xaml`
- `src\PiPlay\MainWindow.xaml.cs`

### MUI-03 Navigation Toolbar Buttons

Action:

1. Hover `Back`.
2. Capture `chrome-<build>-back-tooltip.png`.
3. Hover `Reload`.
4. Capture `chrome-<build>-reload-tooltip.png`.
5. Hover `Home`.
6. Capture `chrome-<build>-home-tooltip.png`.
7. Click Reload and confirm the WebView remains in a valid YouTube state.
8. Click Home and confirm YouTube home loads.

Inspect:

- Tooltips read `Back`, `Reload`, and `YouTube home`.
- Glyphs render as icons, not empty boxes.
- Hover state is visible and consistent across all three buttons.
- Disabled or ineffective states still look intentional.
- Clicking Home does not navigate to an off-brand or blank page.

Source references:

- `src\PiPlay\MainWindow.xaml`
- `src\PiPlay\MainWindow.xaml.cs`
- `src\PiPlay\Services\NavigationPolicy.cs`

### MUI-04 URL Box Readability and Entry

Action:

1. Click the URL box.
2. Capture `chrome-<build>-url-focused.png`.
3. Type a YouTube URL or search text.
4. Press Enter.
5. Capture `chrome-<build>-url-after-navigation.png` after navigation starts or completes.
6. Hover the URL box.
7. Capture `chrome-<build>-url-tooltip.png`.

Inspect:

- URL box focus state is visible.
- Existing and typed text are readable.
- Caret and selection are visible.
- Tooltip reads `Paste a YouTube URL or type to search, then press Enter`.
- Long URL text does not become clipped into unreadable fragments.
- The text color and background appear consistent with the dark theme.

Source references:

- `src\PiPlay\MainWindow.xaml`
- `src\PiPlay\MainWindow.xaml.cs`
- `src\PiPlay\Theme\ControlStyles.xaml`
- `src\PiPlay\Theme\Colors.xaml`

### MUI-05 Profiles ComboBox Empty State

Action:

1. Start with no saved profiles or a settings file whose `Profiles` list is empty.
2. Hover the Profiles ComboBox.
3. Capture `chrome-<build>-profiles-tooltip.png`.
4. Open the Profiles ComboBox.
5. Capture `chrome-<build>-profiles-open-empty.png`.

Inspect:

- Tooltip reads `Saved profiles`.
- Closed ComboBox uses the dark app theme.
- Open popup uses the dark app theme.
- Empty popup looks intentional and not like a blank white OS dropdown.
- Popup does not hide unrelated toolbar controls in a confusing way.

Source references:

- `src\PiPlay\MainWindow.xaml`
- `src\PiPlay\MainWindow.xaml.cs`
- `src\PiPlay\Models\AppSettings.cs`
- `src\PiPlay\Theme\ControlStyles.xaml`

### MUI-06 Save Profile Dialog and Populated Profiles

Action:

1. Navigate to a YouTube page that can be saved.
2. Hover Save Profile.
3. Capture `chrome-<build>-save-profile-tooltip.png`.
4. Click Save Profile.
5. Capture `chrome-<build>-profile-save-dialog.png`.
6. Type a short profile name.
7. Click `Save`.
8. Open the Profiles ComboBox.
9. Capture `chrome-<build>-profiles-open-populated.png`.

Inspect:

- Tooltip reads `Save current page as a profile`.
- The Save Profile glyph renders as an icon, not an empty box.
- Dialog title reads `Save profile`.
- Dialog message reads `Name this profile:`.
- Dialog buttons read `Save` and `Cancel`.
- Dialog uses dark PiPlay styling.
- Saved profile appears in the ComboBox list.
- Populated ComboBox list uses dark styling and readable text.

Source references:

- `src\PiPlay\MainWindow.xaml`
- `src\PiPlay\MainWindow.xaml.cs`
- `src\PiPlay\Prompt.cs`
- `src\PiPlay\Services\ProfileService.cs`

### MUI-07 Source Window Pin State

Action:

1. Hover the source-window Pin button.
2. Capture `chrome-<build>-pin-tooltip.png`.
3. Click Pin.
4. Capture `chrome-<build>-pin-enabled.png`.
5. Click Pin again.
6. Capture `chrome-<build>-pin-disabled.png`.

Inspect:

- Tooltip reads `Pin PiPlay on top`.
- Pin glyph renders as an icon, not an empty box.
- Enabled/checked state is visually obvious.
- Title bar shows `Pinned` only when Pin is enabled.
- Unpin removes the `Pinned` hint.

Source references:

- `src\PiPlay\MainWindow.xaml`
- `src\PiPlay\MainWindow.xaml.cs`

### MUI-08 Pop Out Button States

Action:

1. Launch the app before WebView2 is ready, if visible.
2. Capture `chrome-<build>-popout-disabled.png` if the disabled state can be observed.
3. Navigate to YouTube home or a non-watch page.
4. Capture `chrome-<build>-popout-enabled-non-video.png`.
5. Hover `Pop out video`.
6. Capture `chrome-<build>-popout-hover.png`.
7. Click `Pop out video` without a playable video, if applicable.
8. Capture `chrome-<build>-popout-no-video-message.png`.
9. Navigate to a playable YouTube video.
10. Click `Pop out video`.

Inspect:

- Button text reads `Pop out video`.
- Button icon renders.
- Disabled state is visually distinct from enabled state.
- Hover state is visible.
- No-video message reads `Open a YouTube video first, then press Pop out video.` when that guard is reached.
- Starting popout does not create duplicate player windows.

Source references:

- `src\PiPlay\MainWindow.xaml`
- `src\PiPlay\MainWindow.xaml.cs`
- `src\PiPlay\Services\YouTubeUrlHelper.cs`
- `src\PiPlay\Services\YouTubeDomBridge.cs`

### MUI-09 Source Placeholder During Popout

Action:

1. Navigate to a playable video.
2. Start `Pop out video`.
3. Wait for the Source Window WebView to hide and the placeholder to appear.
4. Capture `chrome-<build>-source-placeholder.png`.

Inspect:

- Placeholder icon renders.
- Placeholder heading reads `Playing in Video Popout`.
- Helper text reads `Close the popout window to bring the video back here.`
- Placeholder background is black/dark.
- No WebView video bleeds through behind the placeholder.
- Source audio is paused while the player is open.

Source references:

- `src\PiPlay\MainWindow.xaml`
- `src\PiPlay\MainWindow.xaml.cs`

### MUI-10 Player Window Chrome

Action:

1. Start a video popout.
2. Capture `chrome-<build>-player-base.png`.
3. Hover the player Pin button.
4. Capture `chrome-<build>-player-pin-tooltip.png`.
5. Hover the player Close button.
6. Capture `chrome-<build>-player-close-tooltip.png`.
7. Toggle player Pin.
8. Capture `chrome-<build>-player-pin-enabled.png`.
9. Close the player.
10. Capture `chrome-<build>-return-to-source.png`.

Inspect:

- Player title strip reads `PiPlay`.
- Player Pin tooltip reads `Pin on top`.
- Player Close tooltip reads `Close popout (return video)`.
- Player Pin and Close glyphs render as icons, not empty boxes.
- Player tooltips are styled consistently with the app.
- Chrome strip remains usable.
- Video area is not hidden by the chrome strip.
- Closing the player returns the source window from placeholder to normal WebView.

Source references:

- `src\PiPlay\PlayerWindow.xaml`
- `src\PiPlay\PlayerWindow.xaml.cs`
- `src\PiPlay\MainWindow.xaml.cs`

### MUI-11 Runtime Error Panel

Action:

1. Run this case only when WebView2 initialization can be forced to fail safely, or when a real runtime failure occurs.
2. Capture `chrome-<build>-runtime-error.png`.
3. Click `Retry` if the runtime is available again.

Inspect:

- Heading reads `WebView2 Runtime is required`.
- Message explains that the Microsoft Edge WebView2 Evergreen Runtime is needed.
- Buttons read `Get WebView2 Runtime` and `Retry`.
- Panel uses dark PiPlay styling.
- `Get WebView2 Runtime` opens the external Microsoft download page.
- `Retry` reattempts initialization without crashing.

Source references:

- `src\PiPlay\MainWindow.xaml`
- `src\PiPlay\MainWindow.xaml.cs`

### MUI-12 Resize, DPI, and Text Fit Spot Check

Action:

1. Test the source window at normal size.
2. Resize to near minimum size.
3. Maximize and restore.
4. Repeat at 100%, 125%, and 150% Windows scale when possible.
5. Capture one screenshot per size/DPI state that reveals a layout problem.

Inspect:

- Title text, URL text, profile control, Pin, and `Pop out video` stay readable.
- Caption and toolbar controls do not overlap.
- Player minimum size remains usable.
- Tooltips stay near their owning controls without blocking adjacent controls.
- Text does not escape button, dialog, or placeholder bounds.

Source references:

- `src\PiPlay\MainWindow.xaml`
- `src\PiPlay\PlayerWindow.xaml`
- `docs\QA_Checklist.md`

## Manual Test Result Template

Use this template when updating `docs\Chrome_UI_Spec_Review.md` or creating a build-specific manual QA note.

```text
Build:
Date:
Tester:
Windows scale / DPI:
Monitor setup:
YouTube login state:

Manual UI screenshots captured:
- MUI-01:
- MUI-02:
- MUI-03:
- MUI-04:
- MUI-05:
- MUI-06:
- MUI-07:
- MUI-08:
- MUI-09:
- MUI-10:
- MUI-11:
- MUI-12:

Findings:
1. [Pass/Issue/Conditional/Skipped] [MUI id] [short title]
   Evidence:
   Source:
   Spec:
   Notes:
```

## Evidence Files

The screenshots used by the review are stored in `docs/evidence`.

| Screenshot | UI state captured | Main checks performed |
|---|---|---|
| `chrome-current-base.png` | Normal main window after launch/navigation. | Broken icon glyphs, light profile selector, address-bar readability, visible `Pop out video` label/icon. |
| `chrome-current-back-tooltip-active.png` | Mouse hover over the Back toolbar button until its tooltip appears. | Back button glyph renders as an empty box; tooltip text says `Back`; tooltip uses light system styling. |
| `chrome-current-minimize-tooltip-active.png` | Mouse hover over the Minimize caption button until its tooltip appears. | Caption glyphs render as empty boxes; tooltip text says `Minimize`; tooltip overlaps the caption-button area. |
| `chrome-current-profiles-combo-open-active.png` | Profile ComboBox opened from the toolbar. | ComboBox popup is light themed, large, and empty over the dark chrome/content. |
| `chrome-current-back-tooltip.png` | Supporting capture around Back hover. | Kept as additional visual evidence for the same Back tooltip state. |
| `chrome-current-minimize-tooltip.png` | Supporting capture around Minimize hover. | Kept as additional visual evidence for the same caption-button tooltip state. |
| `chrome-current-profiles-combo-open.png` | Supporting capture around the open profile ComboBox. | Kept as additional visual evidence for the same profile popup state. |

## Step-by-Step Procedure

1. Build or select the app build to review.

   The reviewed build was the published executable at `bin\publish\latest\PiPlay.exe`. The review used that executable as the runtime source of truth for the captured screenshots.

2. Launch the app.

   Run `bin\publish\latest\PiPlay.exe` from the workspace or from File Explorer. Wait for the main PiPlay window to appear and for the embedded WebView2 content to finish painting enough that the toolbar and page content are visible.

3. Capture the base chrome state.

   With no tooltip or dropdown open, take a screenshot of the main window and save it as `docs/evidence/chrome-current-base.png`.

   Check these visible UI elements:

   - App title area shows `PiPlay`.
   - Caption buttons on the upper right are visible but render as empty-box glyphs.
   - Toolbar navigation/action buttons render as empty-box glyphs.
   - Address bar is present, but the URL text appears clipped or faint rather than readable.
   - Profile selector appears as a light native-looking control inside the dark toolbar.
   - `Pop out video` button text is visible, and its icon renders.

4. Capture the Back tooltip state.

   Move the mouse over the Back toolbar button and wait for the tooltip to appear. Take a screenshot and save it as `docs/evidence/chrome-current-back-tooltip-active.png`.

   Check these visible UI elements:

   - The Back button hit target highlights on hover.
   - The Back button glyph is still an empty box.
   - The tooltip text reads `Back`.
   - The tooltip uses a light system style instead of the app's dark visual style.

5. Capture the Minimize tooltip state.

   Move the mouse over the Minimize caption button and wait for the tooltip to appear. Take a screenshot and save it as `docs/evidence/chrome-current-minimize-tooltip-active.png`.

   Check these visible UI elements:

   - Caption button hit target highlights on hover.
   - Caption button glyphs still render as empty boxes.
   - The tooltip text reads `Minimize`.
   - The tooltip appears as a light system tooltip.
   - The tooltip overlaps or crowds the caption-button area.

6. Capture the profile ComboBox open state.

   Click the profile selector in the toolbar. With the dropdown still open, take a screenshot and save it as `docs/evidence/chrome-current-profiles-combo-open-active.png`.

   Check these visible UI elements:

   - The closed ComboBox area uses a light theme while the surrounding toolbar is dark.
   - The opened popup/list is also light themed.
   - The popup appears blank because the current profile list is empty.
   - The blank popup has no empty-state text, so the control looks broken rather than intentionally empty.

7. Compare the screenshots against the current WPF source.

   The review then checked whether the visible behavior matched the current source:

   - `src\PiPlay\PiPlay.csproj` confirms this is a WPF app through `<UseWPF>true</UseWPF>`.
   - `src\PiPlay\MainWindow.xaml` confirms the main window uses `WindowChrome`.
   - `src\PiPlay\Theme\ControlStyles.xaml` defines `IconButton` with `FontFamily="Segoe MDL2 Assets"`.
   - `src\PiPlay\MainWindow.xaml` assigns Private Use Area glyph values as string button content for the caption, navigation, save, and pin buttons.
   - `src\PiPlay\MainWindow.xaml` defines the profile selector as a WPF `ComboBox`, not as a WebView2 page control.
   - `src\PiPlay\Theme\ControlStyles.xaml` defines only basic `DarkComboBox` properties and does not define a full popup/list template.
   - `src\PiPlay\Models\AppSettings.cs` shows `Profiles` defaults to an empty list.
   - `src\PiPlay\MainWindow.xaml.cs` shows `LoadProfilesIntoCombo()` binds the settings profile list and clears selection.
   - `src\PiPlay\MainWindow.xaml` shows `UrlBox` uses `DarkTextBox`.
   - `src\PiPlay\Theme\Colors.xaml` defines the address-bar foreground/background tokens used for contrast analysis.
   - `src\PiPlay\MainWindow.xaml` and `src\PiPlay\PlayerWindow.xaml` assign tooltips as plain string `ToolTip` values.
   - `src\PiPlay\Theme\ControlStyles.xaml` does not define a custom app-level `ToolTip` style, template, or placement policy.

8. Check the address-bar contrast claim.

   The screenshot made the URL text look unreadable. Before calling it a source color-token bug, the review checked the declared colors:

   - Text token: `#F3F5F7`
   - Surface token: `#1C2025`
   - Calculated contrast: approximately `14.98:1`
   - WCAG normal-text minimum: `4.5:1`

   Result: the screenshot still shows a real runtime readability problem, but the declared source color pair passes contrast. The issue was therefore framed as runtime rendering, clipping, resource application, or build-state mismatch rather than a confirmed color-token contrast failure.

9. Compare the findings against the product spec and ADRs.

   The review checked the findings against the spec requirements for:

   - Quiet, dark, polished, utility-first visual identity.
   - Coherent icon style for action and title-bar buttons.
   - Source Window as a dark native WPF host.
   - Tooltips or labels for controls.
   - Sufficient contrast in the dark UI.
   - MVP visual identity with dark shell and coherent icons.
   - ADR guidance that custom/borderless WPF chrome needs explicit accessibility attention.

10. Check primary external references for technical claims.

   Two external references were used for the technical framing:

   - Microsoft Learn Segoe MDL2 Assets documentation, to confirm that icon glyphs are Private Use Area values and depend on the intended icon font being applied.
   - W3C WCAG 2.2 contrast guidance, to use the correct `4.5:1` normal-text contrast threshold.

11. Write the findings report.

   The final report grouped the evidence into four findings:

   - Caption, navigation, save, and pin icons render as `.notdef` empty boxes.
   - Profile ComboBox renders as a light native control and opens a light empty popup.
   - Address-bar URL text is illegible in the runtime capture, even though the declared source colors pass contrast.
   - Tooltips render light on dark chrome, and the Minimize tooltip overlaps the caption-button area.

   Each finding was written with:

   - Current visual evidence from the screenshot files.
   - Current source evidence from WPF XAML/C# files.
   - Spec assessment.
   - Confidence level.

## What Was Actually Verified

The first review verified the visible runtime state of the chrome UI and the source/spec explanation for that state. That pass did not automatically drive the UI, assert pixels, or run OCR assertions. Future runs may be ChatGPT-operated for screenshot capture and state setup, but they remain manual screenshot-backed tests until replaced by true UI automation.

Verified by screenshot:

- Broken empty-box icon glyphs.
- Light profile selector and light profile popup.
- Blank profile popup state.
- Light Back and Minimize tooltips.
- Tooltip text values `Back` and `Minimize`.
- Address-bar text readability problem.
- `Pop out video` label and icon were visible.

Verified by source inspection:

- WPF app shell and custom `WindowChrome`.
- Segoe MDL2 glyph usage in string button content.
- WPF ComboBox profile selector with incomplete dark styling.
- Empty profiles are a valid default data state.
- DarkTextBox color tokens have high declared contrast.
- Tooltips use plain string values and default WPF styling.

Not verified by this pass:

- Automated UI automation IDs.
- Programmatic OCR of the screenshot text.
- Pixel-level contrast sampling from the rendered screenshot.
- A root-cause fix for the glyph, ComboBox, or tooltip issues.
- Whether the unreadable URL text is caused by clipping, opacity, stale build output, missing resources, or another runtime rendering problem.

## How to Repeat the Review

1. Rebuild or republish PiPlay.
2. Prefer ChatGPT-operated capture: have ChatGPT launch the executable, drive reachable UI states, save screenshots into `docs/evidence`, inspect the images, and write the result notes.
3. If ChatGPT cannot drive a state, the user captures that state manually and places the screenshot in `docs/evidence`.
4. At minimum, capture the original four active states: base window, Back tooltip, Minimize tooltip, and open profile ComboBox.
5. For fuller coverage, run MUI-01 through MUI-12 in this document and mark blocked states as `Skipped` or `Needs live repro`.
6. Save screenshots with a date/build-specific naming scheme if preserving multiple runs.
7. Compare screenshots against `src\PiPlay\MainWindow.xaml`, `src\PiPlay\Theme\ControlStyles.xaml`, `src\PiPlay\Theme\Colors.xaml`, `src\PiPlay\Models\AppSettings.cs`, `src\PiPlay\MainWindow.xaml.cs`, `src\PiPlay\PlayerWindow.xaml`, and `src\PiPlay\Prompt.cs`.
8. Check the current spec and ADR text before deciding whether each visible problem is out of spec.
9. Update `docs\Chrome_UI_Spec_Review.md` with the new evidence, source locations, verdicts, and confidence levels.
