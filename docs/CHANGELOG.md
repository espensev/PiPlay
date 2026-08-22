# Changelog

Only current unreleased user-visible changes belong here.

## Unreleased

- Popout launch abandons a captured playlist if the Source moves before first-item resolution, preventing a stale list/video pairing (`MainWindow.StartVideoPopoutAsync`; `PopoutTargetResolverTests.A_captured_playlist_stands_only_while_the_live_source_shows_the_same_playlist_page`; 2026-08-20).
- Playlist-only pages can launch a Popout from the first playable item, fall back to the playlist page when none is rendered, and return current video plus playlist context (`PopoutTargetResolver.WithFirstPlaylistItem`; `PopoutTargetResolverTests`; `WpfRuntimeTests.Popout_source_change_tracks_the_playlist_context`).
- Normal Popouts retain `RD...` mix/radio queues; malformed list IDs fall back to one video with a non-blocking note (`YouTubeUrlHelper`; `YouTubeUrlHelperTests`).
- Profile accent reaches the near-black Source/Popout letterbox, Source background, profile-row washes, and a 1 px Popout edge. Intensity `0` removes background/edge reach while rows retain preset `SubtleAlpha` (`ThemeColors.DeriveAccentSet`; `ThemeColorsTests`; `WpfRuntimeTests`).
- The Source title wash extends into the washed background instead of ending mid-toolbar (`MainWindow.xaml:MainBarBackdrop`; `XamlInvariantTests.Source_title_bar_carries_global_accent_as_a_gradient_wash_REQ_UI_01`).
