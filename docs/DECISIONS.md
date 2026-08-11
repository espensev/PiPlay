# PiPlay decisions in force

Stable decision IDs are retained from the former ADR files. Change or supersede a decision here; do not silently contradict it in code. New decisions receive the next unused `ADR-NNNN` ID.

## ADR-0001 — WPF app shell (accepted)

- PiPlay is Windows-only. The shell is WPF and hosts YouTube with WebView2.
- Native custom chrome owns move/resize, topmost, placement, DPI, keyboard focus, and UI Automation names explicitly.

## ADR-0002 — .NET 10 without aggressive packaging (accepted)

- Target `net10.0-windows` with `UseWPF=true`, `Nullable=enable`, `ImplicitUsings=enable`, and `SelfContained=false` by default.
- Keep `PublishTrimmed=false`, `PublishSingleFile=false`, and no NativeAOT. Revisit size optimization only after WPF/XAML/WebView2 loading and diagnostics remain reliable.

## ADR-0003 — WebView2 Evergreen (accepted)

- Use Microsoft Edge WebView2 Evergreen through `Microsoft.Web.WebView2` `1.0.3967.48`; `browserExecutableFolder` remains `null`.
- Source and Popout WebViews share one `CoreWebView2Environment` and the channel-resolved user-data root.
- Missing runtime must show install/retry recovery. Fixed Version remains deferred unless an offline, kiosk, or exact-runtime requirement appears.

## ADR-0004 — Native fake-PiP (accepted)

- The borderless WPF Popout Player owns a second WebView2. The Source Window pauses/mutes its video, hides the Source WebView, and shows the Source Placeholder.
- Both WebViews share the environment/session. Timestamp and playback DOM work stays centralized, best-effort, and non-crashing.
- Browser-native PiP is not used; PiPlay owns placement, Pin, profiles, close/return, and monitor restore.

## ADR-0005 — One Popout Player (accepted)

- Exactly one Popout Player may exist. Preserve `_popoutInProgress`, `_returnInProgress`, and the single `_player` ownership model.
- **Show Popout** restores/activates the existing player without transferring playback. **Bring video back** captures current state, closes it, and returns playback. A playable Popout navigation retargets that player in place.
- Multiple players require a new decision.

## ADR-0006 — No click-through (accepted)

- Fade and opacity are visual only. Never set `WS_EX_TRANSPARENT`; do not add mouse pass-through, a transparent WebView, or a visible player the user cannot directly control.
- The Settings opacity floor is `0.45`. Persisted hand-edited values in `[0.10, 0.45)` are the explicit unlock range and must still preserve input.
- Reconsider only with an explicit escape/recovery design.

## ADR-0007 — Stable channel and portable data (accepted)

- Build channel property: `PiPlayChannel`, values `Default` or `Stable`, emitted as assembly metadata `PiPlay.Channel`. `PIPLAY_CHANNEL` overrides it only for tests/diagnostics.
- `Build-PiPlay.ps1 -Channel Stable` bakes the Stable channel; `Publish-Stable.ps1` uses that form.
- Data-root precedence: `PIPLAY_DATA_ROOT`; otherwise Stable uses `<exeDir>\PiPlayData`, while Default uses `%LOCALAPPDATA%\PiPlay`.
- Default mutex: `Local\PiPlay.SingleInstance.v1`; Stable mutex: `Local\PiPlay.SingleInstance.Stable`. Pipes use `PiPlay.SingleInstance.<suffix>.Session<sessionId>`.
- Default title is `PiPlay`. Stable is `PiPlay — Stable vX.Y.Z (bN)`, omitting the version segment when blank and the build segment when `0`. Each channel is single-instance per Windows logon session; Default and Stable can run side by side.
- Publish with `.\scripts\Publish-Stable.ps1`; verify with `.\scripts\Verify-StableDeploy.ps1`. The sanctioned manual-test target is `E:\Dev_test_implemenations\PiPlay\PiPlay.exe`; redeploy preserves `PiPlayData`.
- Stable is framework-dependent and its directory must be writable. Two Stable copies in different folders still collide within one logon session. `PIPLAY_DATA_ROOT` can intentionally make data root and channel identity differ.
- Do not derive instance identity from a path without explicit Windows path canonicalization: case, 8.3 aliases, mapped/UNC paths, symlinks, and trailing separators can otherwise let two instances share one WebView2 root.

## ADR-0008 — Rounded Popout window region (accepted)

- Only a floating Popout whose effective corner mode is `Round` receives a top-level Win32 window region. Its radius is resolved `ThemeRadii.PopoutFrame`; Soft Glass/explicit Round is `22 DIP`, converted with current DPI.
- Refresh after resize/DPI changes; clear for maximized or snap-like layouts; restore when floating.
- Keep standard WebView2, `AllowsTransparency=False`, native opacity, and the resize subclass. Other Popouts and Source/Settings/prompt windows retain DWM corner handling.
- The region clips the child HWND and preserves normal frame-rate/DRM behavior, but does not guarantee a curve-following DWM border or shadow. Composition hosting remains deferred because of lower-frame-rate and DRM limitations.
- Deployed inspection at 100%, 125%, 150%, and mixed DPI remains a release gate; the outer border/shadow direction is unresolved.
