# ADR-0005: Single Popout Player for now

- **Status:** Accepted
- **Date:** 2026-05-30

## Context
Multiple simultaneous Popout Players multiply every hard problem at once: lifecycle guards, focus handling, audio routing, timestamp tracking, and window-state restore. At this stage the reliability of one player matters far more than the ability to run several.

## Decision
Support exactly one Popout Player. A request to create another player while one exists must never open a
second window. The Source exposes two explicit existing-player commands instead:

- **Show Popout** restores and activates the existing player without changing playback ownership.
- **Bring video back** captures current player state, closes it, and returns playback to the Source.

The Source's primary transfer action becomes **Bring video back** while the player is open; it is not a
second creation request. Multi-player is a deliberate non-goal until the single-player lifecycle is
excellent.

## Consequences
- Simpler, more testable guards (`_popoutInProgress`, `_returnInProgress`, and a single `_player`
  reference) and predictable audio/return behavior.
- Focus recovery and playback transfer remain distinct actions, so focusing a minimized player cannot
  accidentally close it and returning cannot accidentally create another.
- Closes off "wall of videos" use cases for now; this is listed as a hard non-goal (section 8) and can be revisited with its own ADR.
