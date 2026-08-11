# YouTube usage and compliance

PiPlay is an independent, unaffiliated desktop client. It displays real `youtube.com` pages through Microsoft Edge WebView2 and adds native window behavior. This engineering policy is not legal advice.

## Allowed behavior

- Normal YouTube pages, login/session/Premium behavior, playlists, mixes, captions, settings, quality, branding, and ads remain YouTube-owned.
- PiPlay may centrally read/apply local playback state, detect trusted passive-surface drag, and present the optional Focused layout over the real `/watch` player.
- Popout uses a standard YouTube URL. PiPlay does not proxy, extract, replace, or re-host media.

## Prohibited behavior

- Downloading, ripping, offline copying, proxying, or extracting video/audio.
- Blocking, skipping, accelerating, hiding, or otherwise altering ads or monetization.
- Bypassing DRM, age, region, login, or playback restrictions.
- Removing required controls/branding or intercepting, inspecting, or storing Google/YouTube credentials.
- Faking platform behavior in a way that creates compliance/account risk.

## Standard and Focused

- Standard is default. Focused is a reversible, best-effort local presentation of the same top-level HTTPS YouTube `/watch` page; it is not Compact/IFrame playback.
- Focused uses `object-fit: contain`, never default `cover`; nonmatching ratios letterbox rather than crop.
- Empty overlay pixels pass input. YouTube controls, branding, settings, quality, captions, fullscreen, ad UI/disclosures/links, and native Skip controls remain visible and reachable.
- Captions/Next convenience actions delegate to native YouTube controls and do nothing if unavailable. Selector/injection failure restores the ordinary page and native Popout strip.

## Advertisement invariant

- While `ad-showing`/`ad-interrupting`, PiPlay must not write `currentTime`, change playback rate, invoke Next, or issue another skip-capable action.
- Custom progress/Next surfaces are hidden/disabled and recheck ad state before action. Unknown ad state fails closed.
- User play/pause/mute may delegate to ordinary YouTube controls; PiPlay must not automate them to change ad delivery.

## Passive drag boundary

- Only a real trusted (`event.isTrusted`) primary mouse/pen gesture in the current top document may arm on passive player pixels.
- Exclude buttons, links, inputs, progress/seek/volume, menus, captions, settings/fullscreen, end cards, ads with actions, and PiPlay overlay controls.
- Preserve clicks below system drag threshold. Synthetic, stale, child-frame, touch, non-primary, or released gestures never move the native window or suppress a click.
- The one closed drag request contains no coordinates; host accepts it only while the physical left button is down and the Popout is movable.

## Native action boundary

Focused may request only `close`, `pinToggle`, `fullscreenToggle`, and `settings`. Every message must be exact-schema/version checked, carry the window nonce and independently rotated current-document token, come from the current trusted top-level HTTPS YouTube source, and originate from trusted input.

Reject unknown fields/actions, malformed payloads, nonce/token mismatch, old navigation, foreign sources, and synthetic events. Never expose arbitrary URLs, scripts, shell commands, credentials, or filesystem operations. Defer reentrant Close/Settings work outside the WebView callback.

## Release gate

- Keep all selectors/scripts in `YouTubeDomBridge`; test generated syntax plus executable DOM behavior for ads, stale/synthetic messages, drag exclusions, and selector failure.
- Before public release, re-check current [YouTube Terms of Service](https://www.youtube.com/static?template=terms) and [IFrame API documentation](https://developers.google.com/youtube/iframe_api_reference).
- Exercise Standard and Focused with ordinary playback and live ads on a verified deployed Stable build. If a requested feature requires prohibited behavior, stop and make an explicit architecture/product decision.
