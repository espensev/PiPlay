# Changelog

Only unreleased work and the current release are retained here. Git history preserves older release notes.

## Unreleased

### Added

- Playlist-only pages can launch a Popout, and return carries current video plus playlist/mix context.
- Normal Popouts retain `RD...` mix/radio queues. Compact URLs omit auto-generated lists; malformed list IDs fall back to one video with a non-blocking note.
- Profile accent now reaches the near-black Source/Popout letterbox, Source room-tone background, individual profile-row washes, and a 1 px inset Popout identity edge. Accent intensity `0` removes the shared background/edge reach; profile-row washes remain at preset `SubtleAlpha`.

### Changed

- Source title wash extends through the washed background instead of ending mid-toolbar.

### Required before release

- Deployed Stable QA for playlist/mix advance and return.
- Owner visual signoff for the new tints at intensity 0/50/100 across all presets and fractional/mixed DPI.

## 0.12.1 — 2026-07-18 (build 36)

- No product change from 0.12.0. Advanced the exact-source development/release stamp after the runtime-audit documentation closeout.
