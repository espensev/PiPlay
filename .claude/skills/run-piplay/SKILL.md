---
name: run-piplay
description: Launch and drive the PiPlay WPF (.NET 10 / WebView2) desktop app to see a change working in the real app — build it, start it (optionally on a YouTube URL), invoke the Video Popout via UIAutomation, and screenshot each window. Use when asked to run, start, screenshot, or confirm a change in the actual PiPlay app (not the test suite).
allowed-tools: Read, Bash, Edit, Write
---

# Run PiPlay

PiPlay is a Windows-native **WPF (.NET 10) app hosting WebView2** — a GUI, not a CLI or server.
"Running it" means launching the window and driving it to the **Video Popout** loop, then
**looking at a screenshot** (a blank or all-black frame is a failure to launch, not a pass).

This recipe is verified on Windows 11 + WebView2 Evergreen. The built-in `/run` fallbacks don't
cover a WPF + WebView2 window — this skill is the repo's verified path; follow it verbatim.

> **Scope: automated change-verification only.** The repo Debug build this skill launches exists to
> confirm a code change works while developing. Any **manual/human testing, QA-checklist pass, or
> release verification** instead uses the deployed Stable copy at
> `E:\Dev_test_implemenations\PiPlay\PiPlay.exe` (deploy: `scripts\Publish-Stable.ps1`; confirm
> what's deployed first: `scripts\Verify-StableDeploy.ps1`). Never present a repo-built launch as
> manual-QA or release evidence — stale repo binaries are the classic false pass (root `CLAUDE.md`).

## Prerequisites (check, don't assume)

- **WebView2 Evergreen Runtime** — folder `C:\Program Files (x86)\Microsoft\EdgeWebView\Application\<ver>`.
  If missing, PiPlay shows an in-app "WebView2 Runtime is required" panel instead of YouTube.
- **.NET 10 desktop SDK/runtime** — `dotnet --list-sdks` shows a `10.0.x`.
- **Network** — WebView2 loads live `youtube.com`; confirm reachability first.

## 1. Build

```powershell
dotnet build src\PiPlay\PiPlay.csproj -c Debug
```

Exe: `src\PiPlay\bin\Debug\net10.0-windows\PiPlay.exe` (Default dev channel — title bar reads
"PiPlay"; data under `%LOCALAPPDATA%\PiPlay`). To drive the **deployed Stable copy** instead
(manual-QA territory — see the scope note above), pass
`-Exe E:\Dev_test_implemenations\PiPlay\PiPlay.exe` to `launch-and-capture.ps1`
("PiPlay — Stable vX.Y.Z (bN)" title; data in `PiPlayData` beside the exe).

## 2. Launch + drive + screenshot

Run the driver under **Windows PowerShell 5.1 (`powershell.exe -STA`), NOT pwsh 7** — the
.NET-Framework host loads `UIAutomationClient` reliably, and STA is required for UIAutomation:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File `
  .claude\skills\run-piplay\scripts\launch-and-capture.ps1 `
  -Url "https://www.youtube.com/watch?v=jNQXAC9IVRw"
```

It starts the exe (the URL arg flows through `App.ExtractUrlArg` → `MainWindow.NavigateTo`), waits
for the window handle, takes staged shots (shell → page), invokes **Pop out video**
(`AutomationId=PopOutButton`) via UIAutomation, then captures every top-level PiPlay window to
`%TEMP%\piplay_run\`. **Read each PNG** and report literally what rendered.

Useful switches: **`-NoPopout`** (launch + screenshot but do *not* click Pop out — use this to test
Auto, or to just see the source window without spawning a player), **`-Build`** (build first),
**`-KillExisting`** (stop a running instance so the launch isn't swallowed by single-instance
hand-off), and **`-Url`**.

If PiPlay is **already running** and you only want to snapshot the current state without stealing
the user's focus (e.g. they're mid-use), use the passive capture instead:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File `
  .claude\skills\run-piplay\scripts\capture.ps1
```

## 3. Verify from the log (fastest source of truth)

`%LOCALAPPDATA%\PiPlay\logs\piplay.log` (Default channel). A healthy startup logs
`Source browser initialized`. A successful popout logs:

```
Video Popout started at t=<n>s, wasPlaying=<bool>
Popout Player initialized
```

## 4. Drive Settings (optional)

Once PiPlay is up, the Settings dialog can be opened scripted. Run under the same
`powershell.exe -STA` host as the other drivers (UIAutomation needs STA):

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File `
  .claude\skills\run-piplay\scripts\open-settings.ps1
```

It finds the main PiPlay window (the `PiPlay*` window that is **not** the Popout), locates the
**`SettingsButton`** AutomationId, and invokes it. Output tokens:

- `SETTINGS|INVOKED` — the click landed and the call returned. **This is the usual result.**
- `SETTINGS|INVOKED-WITH-TIMEOUT|<ExceptionType>` — **also success.** The dialog is modal
  (`ShowDialog`), so the UIA `Invoke()` can block while the dialog stays open and time out; the click
  still landed. Treat a timeout-family exception here as "Settings is open," not a failure. The
  disabled-button case is *not* routed here (it goes to `FAIL`, below), so this token always means the
  dialog opened.
- `SETTINGS|FAIL|...` — a real failure: main window or `SettingsButton` not found, **or** the button
  was disabled when invoked (the script pre-checks `IsEnabled` and also catches
  `ElementNotEnabledException`, so a disabled-button click reports `FAIL`, never a false success).

Pass `-ProcessId <pid>` to disambiguate if more than one PiPlay is running. Two caveats: the modal
dialog blocks the WPF UI thread while open (close it before driving the main window again), and
`SettingsButton` is briefly disabled while a privacy action is awaiting (`IsEnabled` flips false→true).
An invoke fired during that window does **not** open Settings — `Invoke()` throws
`ElementNotEnabledException`, which the script reports as `SETTINGS|FAIL` (not a silent no-op, not a
false success) — so let the source page settle first, then retry.

Then **capture and read the shot literally** — a blank/all-black dialog frame is a failure, not a pass.
Use the passive `capture.ps1` (no focus steal):

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File `
  .claude\skills\run-piplay\scripts\capture.ps1
```

The Settings dialog is a **modal window centred over the main window**, so `capture.ps1` does *not*
enumerate it as its own window — it shows up **inside the `name=PiPlay` main-window shot**
(positional index: `live-win0.png` when no Popout is open, otherwise the higher-numbered
`name=PiPlay` shot), and that window is reported `occluded=true`, which here is **expected** (the dialog
is sitting on top of it). Read the `name=PiPlay` shot — not the `PiPlay Video Popout` one — to see
Settings. (Verified live: `open-settings.ps1` → `SETTINGS|INVOKED`, then this shot shows the dialog.)

`capture-hwnd.ps1 -Hwnd <hwnd>` is the general single-window capture helper (same DWM-bounds +
`CopyFromScreen` recipe) when you already hold a window handle — e.g. the `handle=` from
`launch-and-capture.ps1`'s `MAINWIN|...` line. It raises only via `SetForegroundWindow`, which (see
Gotchas) is blocked from a background process, so it does **not** reliably bring an occluded window to
front; for the modal Settings dialog prefer `capture.ps1` and read the main-window shot.

> **Verify this path against a live instance before treating it as the recipe** — don't edit step 4
> from the script source alone. Bring a source window up (`launch-and-capture.ps1 -NoPopout`), run
> `open-settings.ps1`, capture, and confirm the PNG actually shows the Settings dialog. Here the
> `name=PiPlay` shot is *expected* to read `occluded=true` (the dialog itself is the occluder), so read
> that shot — don't reject it. The real failure is a frame showing the **bare main window with no
> dialog**, or one whose occluder is something **other** than Settings (another app on top); `capture.ps1`
> can't tell those apart from the dialog, so confirm by eye that the dialog is what rendered.

## 5. Drive a dropdown / popup (optional — theme shadow + popup render-smoke)

To inspect a control's **dropdown popup** — the ComboBox inner-elevation shadow (theme-v2 Phase C),
dropdown rounding, or item rendering — expand it by `AutomationId` and screenshot the opened popup.
Same `powershell.exe -STA` host (UIAutomation needs STA):

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File `
  .claude\skills\run-piplay\scripts\capture-dropdown.ps1 -AutomationId ProfilesCombo
```

It finds the PiPlay main window, finds the control by `AutomationId`, raises the window
(minimize→restore — `SetForegroundWindow` is blocked from a background process), invokes its
`ExpandCollapsePattern.Expand()`, and captures the region around it (control + opened dropdown +
shadow margin) to `%TEMP%\piplay_run\dropdown-<AutomationId>.png`. Output tokens:

- `DROPDOWN|OK|rect=…|cap=…|<png>` — expanded and captured; **read the PNG literally**.
- `DROPDOWN|FAIL|<reason>` — main window / control not found, or the control has no
  `ExpandCollapsePattern` (not an expandable control).

Switches: **`-AutomationId`** (any expandable control; default `ProfilesCombo`), **`-ProcessId`**
(disambiguate), **`-Out`**, and `-PadX`/`-PadTop`/`-Height` to widen the capture window.

The **`ElevationPopup` shadow only shows under a theme that has inner elevation** — Soft Glass (blur 16)
or Minimal (blur 8); **Sharp Dark is intentionally flat** (`Elevation = null`). To see the shadow, put
the dev profile on Soft Glass first (Settings → Soft Glass, or set `theme.themeId="soft-glass"` in
`%LOCALAPPDATA%\PiPlay\settings.json` before launch). The empty profiles dropdown ("No saved profiles
yet") still renders the bordered popup + shadow, so no saved profile is needed.

> **Scope reminder (the two-layer split — see the spec's Manual-smoke note):** this is
> **change-verification only** — it proves the popup *renders as coded* (shadow present + not clipped
> flush, rounding, legibility), **never an aesthetic sign-off** and **not** the deployed-Stable binary.
> Record it "renders as coded". **Verified live 2026-06-16:** soft-glass `ProfilesCombo` dropdown shows
> the `ElevationPopup` shadow, unclipped, with the inset margin.

## Gotchas (all verified, all real PiPlay behaviors)

- **Single-instance per channel.** A second launch hands its URL to the running instance and
  exits (mutex `Local\PiPlay.SingleInstance.v1` for Default). To force a fresh instance:
  `Stop-Process -Name PiPlay` first.
- **Fresh profile → YouTube consent / sign-in wall.** First load shows "Before you continue to
  YouTube" or Google sign-in, not the video. That still proves WebView2 renders — read the shot
  literally; don't report "the video plays" when it's a consent page.
- **Pop out resolves the video id from the page URL** (`ResolvePopoutTargetAsync`: canonical link
  first, else the address bar). It only spawns a player when the WebView is on a
  `youtube.com/watch?v=...` URL. On the consent / sign-in / home page it **correctly declines**
  ("Open a YouTube video first.") with no log line. So to see the popout, the page must be on a
  watch URL when the button fires — past a fresh-profile consent wall that means a human has to
  dismiss consent / sign in first; the automated invoke alone will just decline.
- **PopOutButton** ships `IsEnabled=False` and flips true once the browser core initializes — it
  is *not* gated on a video actually playing.
- **Screenshots:** capture each window via `DwmGetWindowAttribute(hwnd, 9, ...)`
  (DWMWA_EXTENDED_FRAME_BOUNDS) + `Graphics.CopyFromScreen`. **`PrintWindow` returns black for the
  WebView2 surface** — use CopyFromScreen. Make the capturing process DPI-aware
  (`SetProcessDpiAwareness`) or the rect is offset/clipped. This machine renders at **150% DPI**
  (a 1180×760 logical window captures at 1770×1140 physical).
- **Occlusion (the #1 screenshot trap):** `CopyFromScreen` grabs whatever pixels sit at the window's
  rect — so if another window is on top, the shot is the *occluder*, not PiPlay, even though the
  capture still reports `name=PiPlay`. `SetForegroundWindow` alone is **blocked from a background
  process** and will not raise the window; a **minimize→restore cycle** (`ShowWindow SW_MINIMIZE` then
  `SW_RESTORE`) reliably does. `launch-and-capture.ps1` now does this and tags a shot `OCCLUDED` if the
  window still isn't on top; the passive `capture.ps1` reports `occluded=true/false` per window (via
  `WindowFromPoint` at the window centre). If a shot is occluded, raise PiPlay (or re-run
  `launch-and-capture.ps1`) and capture again — never trust an `OCCLUDED`/`occluded=true` frame.
- **Cleanup:** the app keeps running after the script — leave it for the user, or
  `Stop-Process -Name PiPlay`. If the user is actively using it, prefer `capture.ps1` (passive,
  no focus steal).

## What this exercises / doesn't

Exercises: launch, WebView2 + live YouTube, pop-**out** to the floating player and the source
"Playing in Video Popout" placeholder, **opening Settings** (scripted, modal — see step 4; read the
dialog shot literally), and **expanding the profiles dropdown** to render-smoke its popup/elevation
shadow (step 5). Not exercised: return-to-source (close the popout), Pin, Fade, picking/saving a
profile, and the controls *inside* Settings — drive those manually if the change touches them.
