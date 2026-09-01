# PiPlay decisions in force

Supersede an ADR here; do not silently contradict it. New decisions use the next unused `ADR-NNNN` ID.

## ADR-0001 — WPF shell (accepted)

PiPlay is Windows-only WPF hosting YouTube with WebView2. Native chrome owns move/resize, topmost, placement, DPI, focus, and UI Automation names. (`src/PiPlay/PiPlay.csproj`, `MainWindow`, `PlayerWindow`.)

## ADR-0002 — .NET 10 without aggressive packaging (accepted)

Target `net10.0-windows`; keep `Nullable` and `ImplicitUsings` enabled, `SelfContained=false`, `PublishTrimmed=false`, `PublishSingleFile=false`, and no NativeAOT. (`src/PiPlay/PiPlay.csproj`.)

## ADR-0003 — WebView2 Evergreen (accepted)

Use Microsoft WebView2 Evergreen through the package version in `src/PiPlay/PiPlay.csproj`, with no fixed browser executable. Source and Popout share one environment and channel-resolved data root. Missing runtime must surface install/retry recovery. (`WebViewEnvironmentService`.)

## ADR-0004 — Native Popout (accepted)

The borderless Popout owns a second WebView2. Source playback is muted/paused, Source WebView is hidden behind the Source Placeholder, and the shared environment/session is retained. Browser-native PiP is not used. (`MainWindow.xaml.cs`, `PlayerWindow.xaml.cs`.)

## ADR-0005 — One Popout Player (accepted)

Exactly one Popout exists. `_popoutInProgress`, `_returnInProgress`, and `_player` are the lifecycle ownership guards. **Show Popout** activates the existing player; **Bring video back** captures state, closes it, and returns playback. A playable Popout navigation retargets that player. (`MainWindow.xaml.cs`, `ReturnPolicy`.)

## ADR-0006 — No click-through (accepted)

Fade and opacity are visual only. Do not set `WS_EX_TRANSPARENT`, pass through mouse input, or use a transparent WebView. Settings stops at `0.45`; hand-edited persisted values from `0.10` through below `0.45` remain honored. (`WindowOpacityPolicy`, `WindowOpacityPolicyTests`.)

## ADR-0007 — Stable channel and portable data (accepted)

`PiPlayChannel` is baked into assembly metadata; `PIPLAY_CHANNEL` is a test/diagnostic override. `PIPLAY_DATA_ROOT` overrides data location. Otherwise Stable uses `<exeDir>\PiPlayData`, Default uses `%LOCALAPPDATA%\PiPlay`. Each channel has its own per-session mutex; Default and Stable may run side by side. Stable deployment uses `PIPLAY_STABLE_ROOT`, `Publish-Stable.ps1`, and `Verify-StableDeploy.ps1`; `PiPlayData` stays in place during staged replacement. (`AppChannel`, `AppPaths`, `DeploySwap.ps1`.)

## ADR-0008 — Rounded Popout region (accepted)

Only a floating Popout with effective `Round` corners receives the DPI-scaled native region: `22 DIP` for Soft Glass/explicit Round. Resize/DPI refreshes it; maximize/snap clears it and floating restore reapplies it. Keep standard WebView2, `AllowsTransparency=False`, native opacity, and the resize subclass. The region does not promise a curve-following DWM border/shadow; composition hosting remains deferred. (`RoundedWindowRegionPolicy`, `RoundedWindowRegionApplier`, WPF tests.)
