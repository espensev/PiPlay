# ADR-0006: No click-through / pass-through transparency

- **Status:** Accepted
- **Date:** 2026-05-30

## Context
A click-through overlay (via layered-window styles such as `WS_EX_TRANSPARENT` or transparent hit-testing) is a tempting "ghost video" feature, but it creates a real recovery hazard — a window the user cannot click to move, focus, or close — and it directly conflicts with the principle that a visible player must stay interactable (Q-8).

## Decision
Opacity and fade are **visual states only**. A visible Popout Player always remains draggable, resizable, and clickable. Do not implement click-through, mouse pass-through, or a transparent WebView. Whole-window opacity keeps a normal-use floor (45%) and must preserve input. This is reconsidered only after the normal player is excellent and there is a clear escape / recovery design.

## Consequences
- Predictable, always-recoverable UX; no "lost" invisible windows.
- Whole-window opacity (section 7.3) is allowed as an advanced setting because it keeps input; true click-through (section 7.4) stays out of scope and is a hard non-goal (section 8).
