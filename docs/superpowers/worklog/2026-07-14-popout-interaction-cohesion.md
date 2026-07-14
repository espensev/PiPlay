# Session worklog — popout interaction cohesion (2026-07-14)

Saved record of the owner test-feedback pass following the accent-reach and profile-routing deploy.

## Request

> "there is a lot of quirks" — Popout appearance options were not reachable from the Popout,
> Bring video back immediately triggered Auto again unless Auto was disabled, the Normal-mode bar did
> not disappear, the two window control surfaces felt awkwardly separated, and the three presets still
> felt too similar. The owner then clarified that the app otherwise looks good and works, the accent
> gradient is fine, and opacity should reach the main bar too.

## What was reviewed

- Repo rules, product/ownership specs, UI-priority roadmap, current theme difference inventory, and
  the previous efficiency/customization worklog.
- Deployed diagnostics-only Stable settings and sanitized lifecycle log evidence under
  `E:\Dev_test_implemenations\PiPlay\PiPlayData`.
- Source/Popout XAML, Auto detection/launch/return state, Fade/strip collapse, opacity application,
  Settings ownership, theme catalog/application, and existing Logic/Markup/WPF seams.
- Three independent read-only traces: Auto/return identity, Popout fade/settings reachability, and
  Source-vs-Popout command-surface cohesion.

## Decisions

- Preserve the approved accent curve; it is no longer the problem under test.
- Fix Auto with one launch identity plus a return-boundary latch, based on deployed log evidence.
- Add one familiar Settings gear to the Popout instead of redesigning the two strips or adding menus.
- Default Fade to reclaim the strip row, while retaining the explicit keep-row override.
- Reuse Active opacity for the Source title-bar background; do not make browser content transparent.
- Make the three existing presets truthful/live and more clearly stepped; do not add a fourth preset.

## Implementation

- New: design, implementation plan, worklog, and pure popout-target selection seam.
- Auto now carries the Source-resolved target through launch and re-arms the returned video id before
  any asynchronous Source playback work.
- Popout now exposes the Source-owned Settings dialog through a guarded request; Fade defaults to
  reclaiming its row in all presets while retaining the explicit override.
- Active opacity now reaches the Source title-bar backdrop as well as the active whole Popout; preset
  selection previews complete appearance resources and the three presets have stepped behavior roles.
- Added logic, markup, and WPF regressions for each owner-reported failure mode.

## Verification

- Deployed log diagnosis: confirmed immediate Auto re-pop 50 ms after return with mismatched Source
  and canonical identities.
- Focused regression set: 86 passed.
- Full deterministic suite: 781 passed, 0 failed, 0 skipped.
- Release build: succeeded with 0 warnings and 0 errors.
- Spec preflight: passed.
- Independent final review: passed with no remaining findings after the Settings z-order and truthful
  Fade-copy follow-ups were fixed and regression-tested.
- Diagnostics Stable deploy: `20260714-160940-v0.9.0-b31-stable`; all 21 artifacts re-hashed clean,
  executable SHA-256 `EB9890D3DFBA359AF6785C3C9825DB074D115EC90E19367D4C823DD85EE60AB7`.
- Deployed process started successfully from `E:\Dev_test_implemenations\PiPlay\PiPlay.exe`; portable
  `PiPlayData` was preserved. This dirty-tree build is diagnostics-only and not release evidence.

## Disposition

- Branch: `feat/accent-reach-dial`, continuing from pushed follow-up `b5f1b05`.
- Delivery: the owner accepted cleanup/commit after the verified diagnostics deployment.
- Deploy: diagnostics-only Stable replacement verified and running at the sanctioned E: path.

## Commits

- This record ships with the consolidated popout interaction-cohesion commit on
  `feat/accent-reach-dial`; use Git history for its immutable commit id.
