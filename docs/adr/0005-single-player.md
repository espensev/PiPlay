# ADR-0005: Single Popout Player for now

- **Status:** Accepted
- **Date:** 2026-05-30

## Context
Multiple simultaneous Popout Players multiply every hard problem at once: lifecycle guards, focus handling, audio routing, timestamp tracking, and window-state restore. At this stage the reliability of one player matters far more than the ability to run several.

## Decision
Support exactly one Popout Player. A Video Popout request while a player already exists **activates the existing player** rather than opening a second one (`if (_player is not null) { _player.Activate(); return; }`). Multi-player is a deliberate non-goal until the single-player lifecycle is excellent.

## Consequences
- Simpler, more testable guards (`_popoutInProgress`, a single `_player` reference) and predictable audio/return behavior.
- Closes off "wall of videos" use cases for now; this is listed as a hard non-goal (section 8) and can be revisited with its own ADR.
