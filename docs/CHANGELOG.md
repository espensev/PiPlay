# Changelog

## 0.14.0 — 2026-10-19 (build 40)

- `PiPlay.exe --help`, `-h`, and `/?` show native usage and exit before normal startup; command-line launch targets remain limited to values accepted by the shared YouTube parser.
- Refreshed the `sharp-dark`, `minimal`, and `soft-glass` theme palettes with distinct temperature/elevation hierarchies, live `AccentSubtle` selection tokens, and side-by-side Settings preset cards.
- Improved profile identity and workflow: quick-saving a new profile auto-seeds a distinct curated accent, active profiles can be cleared from the profile menu, canceling or aborting Edit Profile restores the live accent preview, and Popout launches honor matching profile `Topmost`, `FadeEnabled`, and `Placement` overrides.
- Removed Popout edge borders in borderless/auto-hidden/focused/rounded states via `RoundedWindowRegionPolicy.ShouldShowPopoutEdgeBorder`, switched borderless Popout letterboxing to pure black and Source letterboxing to neutral `AppBackgroundWash`, added 6 DIP resize insets when the Popout strip collapses, and enabled full-monitor maximize (`useWorkArea: false`) for `PlayerWindow`.
- Refined the Focused DOM overlay (`:focus-visible,:active` attention gating, distinct muted/restore icons, zero-volume unmute recovery, immediate seek fill), added `DarkSlider` and `DarkCheckBox` control styles with keyboard focus rings, and fixed non-primary taskbar and mixed-DPI window placement math.
- Hardened the build/release pipeline and test suite: synced `build-info.json` and `VERSION_TABLE.json` into release archives prior to latest-snapshot promotion, removed uncalled production shims, added `ContrastBrushConverterTests`, wired real Focused DOM `buildRoot()`/`configure` execution into the Node VM harness, and wired behavioral PowerShell lock/swap/metadata test harnesses into `dotnet test`.

## 0.13.2 — 2026-08-23 (build 39)

- The deployed UI smoke now isolates browser/app data, captures the foreground PiPlay HWND in the correct mixed-DPI coordinate space, and fails closed on blank or uniform frames.

## 0.13.1 — 2026-08-23 (build 38)

- Playlist-only launches start the adopted first playable item at the beginning instead of inheriting an unrelated miniplayer, preview, or playlist-URL timestamp.
- Project guidance and the Stable release path are slimmer: one local CI command, an explicit machine-local deployment root, and no single-file/self-contained packaging.

## 0.13.0 — 2026-08-20 (build 37)

- Playlist-only pages can launch the first rendered playable item; return preserves the current video and playlist/mix context.
- Normal Popouts retain `RD...` mix/radio queues. Compact builders omit auto-generated lists; malformed list IDs fall back to one video with a non-blocking reason.
- Profile accents reach the Source/Popout letterbox, Source background wash, profile-row wash, and 1 px Popout identity edge. Intensity `0` removes shared background/edge reach while retaining primary-action identity.
- Source title wash extends through the washed background.
