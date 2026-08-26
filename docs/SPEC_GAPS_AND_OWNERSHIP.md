# Open issues and ownership

This file contains current unresolved items only. Accepted behavior is in [`PiPlay_Product_Engineering_Spec.md`](PiPlay_Product_Engineering_Spec.md) and [`DECISIONS.md`](DECISIONS.md). Source, tests, and fresh runtime evidence outrank this list.

## Verified implementation gaps

- **Q-1 live audio proof:** the Source suppression guard reasserts every `1 s` (`MainWindow.xaml.cs`), but automated tests and visual smoke cannot certify the absence of a brief acoustic overlap during ads, autoplay-next, SPA rerenders, or `start_radio=1`. On Stable v0.13.2 b39 a plain-video pop-out and return produced no overlap by ear (`docs/reviews/qa-2026-08-26-stable-v0.13.2-b39-listening.md`); those other paths remain unobserved, per [`QA_Checklist.md`](QA_Checklist.md).
- **Compact message source check:** `PlayerShellBridge.OnWebMessageReceived` parses the payload but does not inspect `CoreWebView2WebMessageReceivedEventArgs.Source`. Compact is currently disabled by `PlaybackModePolicy.CompactPlayerEnabled`; add exact shell-origin validation before revival (`PlayerShellBridge`, `WebViewEnvironmentService.ShellOrigin`).
- **Dispatcher fault policy:** `App.OnDispatcherUnhandledException` marks every UI exception handled and shows a new message box. Recoverable classes and repeated-dialog coalescing are not defined.
- **Runtime-scheme policy:** `NavigationPolicy.IsAllowed` permits top-level `about:`, `data:`, and `blob:` on both surfaces, and `NavigationPolicyTests` pins that behavior. The product has no recorded need for top-level `data:`; remove it only after a deliberate product decision and test update.

## Pending acceptance evidence

- **Profile-selector shadow clipping:** the popup shadow inset in `Theme/ControlStyles.xaml` remains provisional until the selector is opened in a verified Stable build across the applicable themes. If the inset is accepted, replace the provisional comment with durable rationale; if it clips, adjust it and repeat the deployed visual check.

## Deferred product decisions

- Exact playlist queue position is not part of `PlayerReturnState`; current return preserves video/list IDs, not an index contract.
- Compact revival requires source validation, shell timeout/timestamp behavior, RD-list policy, and deployed acceptance; it stays off until those are defined.
- A video-opaque/chrome-only transparency design and curve-following outer DWM border/shadow require an explicit trade-off decision; current opacity affects the Popout video and ADR-0008 makes no shadow guarantee.
- Browse/Cinema/alternate main-window layouts and optional crop/fill remain unimplemented; do not confuse them with dormant Compact playback.

## Ownership

| Owner | Owns |
|---|---|
| `MainWindow` | Source browser, profiles, launch/return, shared Settings dialog. |
| `PlayerWindow` | Popout chrome/playback, placement, polling, close report. |
| `YouTubeUrlHelper` / navigation policies | URL parsing/building and allowed destinations. |
| `YouTubeDomBridge` | YouTube selectors/scripts and best-effort page operations. |
| Page bridges/protocols | Source/schema/version/nonce/document-token validation and closed requests. |
| `SettingsService` / `ProfileService` | Atomic settings and profile persistence/validation. |
| Native helpers | DPI bounds, hit testing, regions, opacity. |
| `LoggingService` | Bounded, redacted local diagnostics; never telemetry or credentials. |
