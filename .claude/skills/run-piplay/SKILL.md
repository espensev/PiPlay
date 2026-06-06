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
"PiPlay"; data under `%LOCALAPPDATA%\PiPlay`). For the Stable channel instead, see
`scripts\Publish-Stable.ps1` and run the deployed copy ("PiPlay — Stable vX.Y.Z" title).

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
- **Cleanup:** the app keeps running after the script — leave it for the user, or
  `Stop-Process -Name PiPlay`. If the user is actively using it, prefer `capture.ps1` (passive,
  no focus steal).

## What this exercises / doesn't

Exercises: launch, WebView2 + live YouTube, pop-**out** to the floating player and the source
"Playing in Video Popout" placeholder. Not exercised: return-to-source (close the popout), Pin,
Fade, profiles, Settings — drive those manually if the change touches them.
