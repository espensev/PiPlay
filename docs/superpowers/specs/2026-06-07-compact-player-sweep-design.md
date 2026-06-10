# Compact player sweep - design

## Goals

Plan the larger Phase 3 compact-player upgrade as a staged, testable sweep rather than a single
embed-URL toggle. Phase 3 should give PiPlay a polished compact playback path while preserving the
current normal YouTube page mode as the default, reliable fallback.

This design covers the full sweep:

- Settle compact-mode placement: global default plus optional per-profile override.
- Ship a conservative direct-embed mode first, using the existing Popout Player lifecycle.
- Add a local `player.html` shell served through WebView2 virtual-host mapping.
- Use the YouTube IFrame API in that shell for playback state, timestamp, and control messaging.
- Keep YouTube controls/branding/compliance intact.
- Keep `Q-1`, `Q-2`, `Q-5`, `Q-6`, `Q-7`, and `Q-8` intact.

Normal page mode remains the default path. A broken, restricted, or unavailable compact player must
degrade to a clear error/fallback, not strand the user or create duplicate playback.

## Requirements served

- Spec section 10.2 - compact embed mode.
- Spec section 10.3 - local `player.html` shell with YouTube IFrame API and WebView2 messaging.
- Spec section 15.1 - shared WebView2 environment and session.
- Spec sections 13 and 14 / `Q-1`, `Q-2` - no duplicate audio; return preserves timestamp/play state.
- Spec section 16 / `Q-7` - native window quality, sizing, monitor restore, and compact-mode minimum.
- `Q-5` - use the official embedded player surface; do not remove required controls/branding or bypass restrictions.
- `Q-6` - compact player failure produces understandable fallback behavior.
- `Q-8` and ADR-0006 - compact mode is still directly interactable; no click-through or transparent WebView2.
- `REQ-PROFILE-01` - profile fields override global defaults per field.

## Settled placement decision

Compact mode is both a global player preference and an optional profile override:

| Surface | Field | Meaning |
|---|---|---|
| Global player setting | `PlayerSettings.CompactMode` | Default for new popouts. Off by default. |
| Profile override | `Profile.Mode` | `null` = use global, `normal` = force normal page mode, `compact` = force compact mode. Legacy/internal `embed` is accepted as an alias for `compact` during sanitization. |

Reasoning: users need a simple global preference, but saved profiles are launch targets and should
be able to force compact or normal independently. This follows the existing profile per-field
precedence model without making compact mode mandatory for every profile.

## Playback mode model

Use explicit internal mode names:

| Mode | Internal value | User meaning | Implementation path |
|---|---|---|---|
| Normal | `normal` | Full YouTube page in the Popout Player | Existing `watch` URL path. |
| Compact | `compact` | Embedded YouTube player in the Popout Player | Phase 3 direct embed, then local shell. |

Do not use `embed` as the long-term product or model name. Treat it only as a backward-compatible
alias because `Profile.Mode` already reserved `"embed"` in source comments.

## Acceptance criteria

- Normal page mode remains default for existing users and existing profiles.
- Settings exposes a global compact-player preference, off by default.
- Profile editing exposes mode override as `Use global`, `Normal`, or `Compact`.
- Effective mode resolves as `profile.Mode ?? global PlayerSettings.CompactMode`.
- Invalid mode values sanitize to `null` on profiles and to normal/off on global settings.
- Compact mode launches supported videos and playlists through a YouTube embedded player path.
- Compact mode uses at least a 480 x 270 minimum size unless a later design explicitly approves a different compact minimum.
- If a saved compact placement is smaller than the compact minimum, the window clamps up instead of opening unusably small.
- Closing compact mode returns to the Source Window with the best-known timestamp and the correct resume/pause decision.
- Compact mode does not trigger duplicate source playback.
- Compact mode does not auto-pop Shorts or embeds from the Source Window; Auto still triggers only from `/watch`.
- Local shell mode is served from a WebView2 virtual host such as `https://piplay.local/player.html`.
- The embedded player uses `enablejsapi=1` and an `origin` value that matches the shell origin.
- Host-shell messages are versioned, minimal, and local-only: ready, state/time update, command result, and error.
- The app never inspects credentials, bypasses YouTube controls, removes required branding, blocks ads, or downloads media.
- Restricted/unavailable/embed-disabled videos show a compact in-app error with a clear fallback to normal mode.
- No click-through, `WS_EX_TRANSPARENT`, transparent WebView2, or whole-window opacity behavior is introduced.

## Staging

### Stage 1 - Policy and direct embed

Add mode resolution and direct compact embed launch while keeping `PlayerWindow` structurally close
to today. This proves the user-facing setting, profile precedence, URL resolution, minimum sizing,
and return lifecycle before adding a local shell.

Stage 1 can use `YouTubeUrlHelper.BuildEmbedUrl` directly. It must still keep normal mode as default.

### Stage 2 - Local shell and virtual host

Add a local `player.html` shell under the app source tree and map it through WebView2 virtual-host
mapping. The shell owns the embedded iframe and loads the YouTube IFrame API.

The shell URL should carry only non-sensitive playback target data: video id, playlist id, start
seconds, and a mode/session nonce if needed. Do not put credentials or cookies in the URL.

### Stage 3 - IFrame API bridge

Move compact-mode timestamp/state reads from DOM scraping to explicit IFrame API messages. The host
listens for shell messages and sends commands such as play, pause, seek, and request state.

Normal mode keeps using `YouTubeDomBridge`; compact mode uses the shell bridge.

### Stage 4 - Polish and release proof

Finish compact-specific minimum sizing, manual QA, visual evidence, fallback/error states, and
release-candidate checks.

## Settled decisions

1. **Global default plus profile override.** This resolves the open compact-placement decision while
   matching the existing profile precedence model.

2. **Normal mode stays default.** Compact is a quality upgrade path, not a forced migration. Normal
   mode is the fallback for unsupported videos and any shell/API failure.

3. **Use `compact`, not `embed`, as the durable mode name.** `compact` is user-meaningful and leaves
   room to change implementation from direct embed to local shell without changing saved intent.

4. **Stage direct embed before local shell.** This reduces risk by proving mode resolution and window
   behavior before adding a second host/JavaScript messaging layer.

5. **Use a WebView2 virtual host for local shell content.** It gives the shell a stable HTTPS origin
   for `origin` and IFrame API control, instead of relying on `file://` behavior.

6. **Do not hide YouTube controls or branding.** Compact means less PiPlay chrome, not a custom
   replacement for required YouTube player surfaces.

7. **Keep compact minimum separate from normal minimum.** Normal page mode remains 320 x 180;
   compact mode starts at 480 x 270 to keep the embedded player controls usable.

## Non-goals / out of scope

- Multiple simultaneous Popout Players.
- Downloading, extracting, blocking, skipping, or modifying YouTube media/ads.
- Removing YouTube controls/branding.
- Making WebView2 transparent.
- Click-through or pointer pass-through.
- Whole-window opacity.
- Direct profile launch without a Source Window.
- Global hotkeys or tray mode.
- Replacing the Source Window YouTube page with an embed.

## Testing approach

- **Logic tests:** add `PlaybackModePolicy` tests for global/profile resolution, accepted values,
  legacy `embed` alias, invalid-value sanitization, and compact minimum sizing. Extend
  `YouTubeUrlHelperTests` if shell URL construction needs a new helper.
- **Settings tests:** prove existing settings default to normal mode and invalid profile modes
  sanitize safely.
- **Markup tests:** assert Settings/Profile mode controls exist once UI lands, with tooltips and
  accessible names.
- **WPF runtime tests:** construct normal and compact `PlayerWindow` paths without showing WebView2
  network content; verify minimum size and mode-specific dependencies.
- **Shell asset tests:** static checks for `player.html` and shell JavaScript: required message names,
  no external dependencies except the YouTube IFrame API script, no credential-bearing strings, and
  expected `enablejsapi` / `origin` use.
- **Manual live QA:** real YouTube watch video, playlist, restricted/unavailable video, signed-in and
  signed-out sessions, return/resume behavior, Pin/Fade behavior, resize minimum, DPI, and fallback
  to normal mode.

## Changes by file

| File | Change |
|---|---|
| `src/PiPlay/Models/AppSettings.cs` | Keep `PlayerSettings.CompactMode`; document it as global compact default. |
| `src/PiPlay/Models/Profile.cs` | Define durable mode values: `null`, `normal`, `compact`; sanitize legacy `embed` alias. |
| `src/PiPlay/Services/PlaybackModePolicy.cs` | New helper for mode normalization, profile/global precedence, and compact minimum size. |
| `src/PiPlay/Services/SettingsService.cs` | Sanitize compact mode and profile mode values. |
| `src/PiPlay/Services/YouTubeUrlHelper.cs` | Reuse or extend embed/shell URL builders. |
| `src/PiPlay/Services/WebViewEnvironmentService.cs` | Add virtual-host mapping for local shell content. |
| `src/PiPlay/Services/PlayerShellBridge.cs` | New host-side compact shell messaging seam. |
| `src/PiPlay/PlayerShell/player.html` | Local shell hosting the YouTube IFrame API player. |
| `src/PiPlay/PlayerShell/player-shell.js` | Local shell script for IFrame API setup and host messages. |
| `src/PiPlay/PlayerWindow.xaml(.cs)` | Mode-aware initialization, compact minimum sizing, shell bridge hookup, and return-state capture. |
| `src/PiPlay/MainWindow.xaml(.cs)` | Resolve effective mode when starting Video Popout; pass mode to PlayerWindow; keep Auto `/watch` only. |
| `src/PiPlay/SettingsWindow.xaml(.cs)` | Add global compact-player preference. |
| `src/PiPlay/Prompt.cs` or profile editor seam | Add profile mode override UI. |
| `tests/PiPlay.Tests/*` | Add logic, settings, markup, WPF, and shell asset coverage. |
| `docs/CHANGELOG.md` | Add Phase 3 compact-player entry when implementation lands. |
| `docs/QA_Checklist.md` | Add compact-player live QA rows. |

## Docs & changelog impact

Implementation should update `docs/CHANGELOG.md`, `docs/QA_Checklist.md`,
`docs/SPEC_GAPS_AND_OWNERSHIP.md`, and the product spec history. No new ADR is required unless the
implementation changes ADR-0004's native fake-PiP architecture or ADR-0006's no-click-through rule.

## Reference notes

- Official YouTube IFrame API docs say IFrame API control requires `enablejsapi=1` or the equivalent
  iframe attribute, and recommend an `origin` parameter for safer API control.
- YouTube player parameter docs cover `autoplay`, `start`, `list`, and `videoseries` URL shapes.
- YouTube notes that programmatic playback is not the same as a native user-initiated play for view
  count purposes; compact mode should not make promises about view-count behavior.

## Unresolved decisions

- Whether direct embed remains as a fallback after local shell ships, or whether normal mode is the
  only fallback.
- Whether compact mode gets a dedicated visual indicator in the Popout Player chrome beyond the
  Settings/Profile controls.
