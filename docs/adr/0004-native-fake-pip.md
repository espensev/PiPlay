# ADR-0004: Native "fake-PiP" architecture

- **Status:** Accepted
- **Date:** 2026-05-30

## Context
Browser-native picture-in-picture and OS PiP surfaces give the app almost no control over window placement, size, always-on-top behavior, close/return, saved profiles, or multi-monitor restore, and they lean on undocumented browser-native PiP internals — an explicit non-goal (section 4). The hard product value of PiPlay is native window quality (Q-7), not video decoding.

## Decision
Use a native "fake-PiP": the **Popout Player** is a borderless WPF window hosting its own WebView2 that plays the popped-out YouTube video, while the **Source Window** pauses its video and shows a black **Source Placeholder**. Both WebViews share one `CoreWebView2Environment` / user-data folder so session and login state stay consistent. PiPlay does not depend on browser-native PiP.

## Consequences
- Full, intentional control over move, resize, topmost, close/return, profiles, and monitor restore.
- The price is managing two WebViews, a timestamp hand-off, and the source-pause / placeholder logic; the DOM interaction that reads timestamp and pauses/plays is best-effort and must never crash the app (Q-3).
- Directly serves Q-1 (no duplicate audio), Q-2 (clean return), and Q-7 (native window quality). See spec sections 9.4, 11, 13, and 14.
