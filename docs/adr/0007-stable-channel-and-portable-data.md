# ADR-0007: Stable channel + portable data for deployed copies

- **Status:** Accepted
- **Date:** 2026-06-06

## Context
We want to publish a *stable* PiPlay copy to an external test location (`E:\Dev_test_implemenations\PiPlay`)
that can run **side by side** with the day-to-day dev/installed app and be told apart at a glance — a
sibling app (AppZone) already deploys this way, with its runtime data beside the exe.

Three things made this a real decision, not just a file copy:

- **Single-instance guard (REQ-APP-01).** `App` holds a per-user mutex (`Local\PiPlay.SingleInstance.v1`)
  and a named pipe so a second launch hands its URL to the running instance and exits. With one hardcoded
  identity, launching the stable copy while the dev app runs would make the stable exe hand off and exit
  *without opening its own window*. We want to **keep** single-instance, but per copy.
- **Shared data root.** Both copies defaulting to `%LOCALAPPDATA%\PiPlay` means one settings file, one
  WebView2 session/login, one log — the two copies would be indistinguishable and could contend for the
  WebView2 user-data folder.
- **No visible marker.** A plain "PiPlay" title gives the user no way to know which window is the stable copy.

## Decision
Introduce a **release channel baked into the binary** at build time, plus **portable data** for non-default
channels:

- The channel is an MSBuild property (`PiPlayChannel`, default `Default`) emitted as
  `[AssemblyMetadata("PiPlay.Channel", …)]` and read at runtime by `AppChannel`. `Build-PiPlay.ps1 -Channel
  Stable` (used by `Publish-Stable.ps1`) bakes `Stable` in, so a copied exe keeps its identity. A
  `PIPLAY_CHANNEL` env var overrides for tests/diagnostics, mirroring `PIPLAY_DATA_ROOT`.
- **Data root** (`AppPaths.Root`): `PIPLAY_DATA_ROOT` wins; else a portable channel (Stable) uses
  `<exeDir>\PiPlayData` (self-contained, isolated from the dev profile); else `%LOCALAPPDATA%\PiPlay`
  (the normal app — unchanged).
- **Single-instance identity** is scoped per channel: `Default` keeps the original `…SingleInstance.v1`
  mutex/pipe; other channels (e.g. `Stable`) get their own. Each channel stays single-instance; they do
  not collide, so dev + stable run together.
- **Title** surfaces a non-default channel as `PiPlay — Stable v0.3.0 (b8)` (the XAML title stays `"PiPlay"`;
  the label is set at runtime so the Default channel's behavior is unchanged).

We chose **build-time baking** over a deploy-time marker file (identity travels in the binary, can't be
lost) and **portable data beside the exe** over a `%LOCALAPPDATA%\PiPlay-Stable` subfolder (the deployment
is self-contained and can be wiped/cloned atomically, matching the AppZone precedent).

## Consequences
- Dev and Stable run simultaneously, each single-instance within its channel, each with its own settings,
  WebView2 session, and logs; the title bar/taskbar identify which is which.
- The Default channel is completely unchanged (data location, mutex name, and title are all identical to
  before), so there is zero risk to the installed app.
- Portable mode requires a **writable app directory** (fine on `E:`; not suitable for `Program Files`).
- A single Stable deployment location is assumed. Two Stable copies in *different* folders would share the
  channel-scoped single-instance identity (one would hand off to the other) even though their data folders
  differ; if we ever need multiple simultaneous Stable deployments, key the mutex to the resolved data root
  instead of the channel name.
- The publish posture is unchanged: framework-dependent, no trimming/single-file/AOT (ADR-0002), so the
  target machine needs the .NET 10 runtime.
