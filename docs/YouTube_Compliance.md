# PiPlay — YouTube usage & compliance

**Status:** Beta candidate. Review before every public release. This is an engineering policy statement, not legal advice.

PiPlay is an independent, unaffiliated desktop client. It is not endorsed by or connected to YouTube or Google. It displays `youtube.com` inside Microsoft Edge WebView2 and adds native window behavior (move, resize, pin, fade) on top. The goal is to make normal YouTube playback behave like a desktop tool — not to alter, replace, or extract from YouTube.

## What PiPlay does
- Loads and displays standard `youtube.com` pages in WebView2 with normal login / session / Premium behavior.
- Runs isolated, centralized JavaScript for local playback state/control, passive surface-drag detection,
  and the optional Focused presentation described below (spec section 12.5).
- Plays the popped-out video in a second WebView pointed at a standard YouTube URL. PiPlay does not
  replace, proxy, extract, or re-host the media.

## What PiPlay does not do
- No downloading, ripping, or offline copying of video or audio.
- No blocking, skipping, or altering of ads; no change to monetization.
- No bypassing of DRM, age gates, region restrictions, or playback restrictions.
- No removal of required player controls or branding.
- No interception, inspection, or storage of YouTube / Google credentials.
- No faking of platform behavior in ways that create account or compliance risk.

These map to quality principle Q-5 ("no invasive YouTube behavior") and spec section 19 (Security and privacy).

## Standard and Focused presentations

Standard is the default and leaves the normal watch page presentation intact. Focused is an optional,
local presentation of that same real YouTube `/watch` player; it is not an alternate player and does not
enable the dormant Compact/IFrame architecture.

Focused presentation must remain:

- limited to a supported top-level HTTPS YouTube watch document;
- idempotent, reversible, and best-effort, with selector or injection failure leaving the ordinary page
  and native Popout controls usable;
- no-crop (`object-fit: contain`), never `cover` by default, and never a media replacement or extraction;
- pointer-transparent outside its actual controls; and
- compatible with YouTube's required player controls, branding, settings, quality and captions surfaces,
  fullscreen path, and complete ad UI. PiPlay styling or auto-hide behavior must not remove, disable, or
  permanently obscure those surfaces.

The Focused overlay may provide convenience controls, but it does not replace YouTube's complete control
system. Best-effort actions such as captions and Next hand off to the corresponding native YouTube control;
if that control is unavailable, PiPlay must do nothing rather than simulate or bypass it.

## Advertisement invariants

PiPlay must never skip, seek through, accelerate, suppress, hide, or otherwise alter an advertisement or
its monetization. In particular:

- custom code must not write `HTMLMediaElement.currentTime`, change playback rate, invoke Next, or trigger
  another skip-capable action while an ad is active;
- the Focused progress rail and other skip-capable convenience actions must be disabled and non-interactive
  whenever an ad is active; if ad state cannot be determined reliably, they fail closed;
- required native ad controls, skip buttons where YouTube supplies them, disclosures, branding, links, and
  click targets remain visible and reachable; and
- ordinary user-initiated play/pause or mute behavior may be handed to YouTube's normal player controls, but
  PiPlay must not automate those actions to change ad delivery.

These constraints apply even if YouTube currently rejects a prohibited media write itself. PiPlay must
enforce the invariant rather than rely on page behavior.

## Passive surface drag

Whole-surface dragging is a native-window convenience, not page input interception. The injected drag
detector must:

- run only in the top document and arm only from a real, trusted (`event.isTrusted`) primary mouse or pen
  gesture over passive player pixels;
- exclude controls, progress/seek surfaces, links, menus, captions, end cards, ad actions, and PiPlay overlay
  controls;
- preserve an ordinary click and begin a move only after the pointer crosses the documented threshold; and
- emit one closed-protocol request which the host accepts only while the physical left button is down and
  the Popout is in a movable state.

Synthetic, stale, replayed, child-frame, touch, and non-primary gestures must not initiate a native move or
cause PiPlay to suppress the page's click.

## Native Focused actions

The Focused page-to-host bridge is a narrow capability boundary. Its only accepted actions are `close`,
`pinToggle`, `fullscreenToggle`, and `settings`. Every request must be exact-schema and version checked,
carry both the window nonce and the independently rotated current-document token, come from the current
top-level trusted HTTPS YouTube navigation, and originate from a trusted user event. Synthetic `.click()`
or dispatched pointer/keyboard events must not invoke native behavior.

The host must reject unknown fields/actions, untrusted sources, old-navigation messages, malformed payloads,
and nonce mismatches. It must not expose arbitrary URLs, script execution, shell commands, or filesystem
operations through this bridge. Reentrant actions such as Close and Settings are deferred outside the
WebView callback.

## Notes for contributors
- Keep injected JavaScript limited to the surfaces above; centralize it and test both generated syntax and
  executable DOM behavior (Q-3). Tests must cover ads, synthetic events, stale navigation messages,
  excluded drag targets, and selector failure.
- Treat the YouTube Terms of Service and any embedded-player / IFrame API terms as the governing rules, and re-check them before a release. Reference links are in spec section 27.
- If a feature request would require anything under "does not do," it is out of scope — raise it as an ADR before acting.
- Before public release, manually exercise Standard and Focused against ordinary playback and live ad states
  in a clean deployed Stable build. Confirm that all required YouTube/ad controls remain reachable and that
  custom seek/next actions fail closed during ads.
