# Changelog

Only unreleased user-visible changes belong here. `VERSION` and `BUILD_NUMBER` own the current release stamp; unresolved gates live in `SPEC_GAPS_AND_OWNERSHIP.md`, and Git preserves released notes.

## Unreleased

- Playlist-only pages can launch a Popout, and return carries current video plus playlist/mix context.
- Normal Popouts retain `RD...` mix/radio queues. Compact URLs omit auto-generated lists; malformed list IDs fall back to one video with a non-blocking note.
- Profile accent now reaches the near-black Source/Popout letterbox, Source room-tone background, individual profile-row washes, and a 1 px inset Popout identity edge. Accent intensity `0` removes the shared background/edge reach; profile-row washes remain at preset `SubtleAlpha`.
- Source title wash extends through the washed background instead of ending mid-toolbar.
