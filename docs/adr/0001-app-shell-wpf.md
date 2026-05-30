# ADR-0001: Use WPF for the app shell

- **Status:** Accepted
- **Date:** 2026-05-30

## Context
PiPlay is Windows-only. The hard problems are native window behaviors — borderless move/resize, always-on-top, multi-monitor placement and restore, DPI — not web-UI complexity. The existing draft is already WPF and architecturally close to the target.

## Decision
Build the shell in WPF, hosting YouTube via WebView2.

## Consequences
- Mature control over custom chrome, topmost, resize, and monitor placement.
- Custom/borderless chrome means accessibility (UI Automation names, keyboard focus) needs explicit attention — it cannot be deferred like normal-window a11y.
- Not cross-platform, which is acceptable per the non-goals.
