# PiPlay — YouTube usage & compliance

**Status:** Beta candidate. Review before every public release. This is an engineering policy statement, not legal advice.

PiPlay is an independent, unaffiliated desktop client. It is not endorsed by or connected to YouTube or Google. It displays `youtube.com` inside Microsoft Edge WebView2 and adds native window behavior (move, resize, pin, fade) on top. The goal is to make normal YouTube playback behave like a desktop tool — not to alter, replace, or extract from YouTube.

## What PiPlay does
- Loads and displays standard `youtube.com` pages in WebView2 with normal login / session / Premium behavior.
- Runs a small, isolated amount of JavaScript on the page for **local playback control only**: read current time, pause, play, seek, read the canonical URL (spec section 12.5).
- Plays the popped-out video in a second WebView pointed at a standard YouTube URL.

## What PiPlay does not do
- No downloading, ripping, or offline copying of video or audio.
- No blocking, skipping, or altering of ads; no change to monetization.
- No bypassing of DRM, age gates, region restrictions, or playback restrictions.
- No removal of required player controls or branding.
- No interception, inspection, or storage of YouTube / Google credentials.
- No faking of platform behavior in ways that create account or compliance risk.

These map to quality principle Q-5 ("no invasive YouTube behavior") and spec section 19 (Security and privacy).

## Notes for contributors
- Keep injected JavaScript limited to the playback-control surface above; centralize and test it (Q-3).
- Treat the YouTube Terms of Service and any embedded-player / IFrame API terms as the governing rules, and re-check them before a release. Reference links are in spec section 27.
- If a feature request would require anything under "does not do," it is out of scope — raise it as an ADR before acting.
