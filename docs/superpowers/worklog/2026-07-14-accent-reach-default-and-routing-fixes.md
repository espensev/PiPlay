# Session worklog — accent reach default and profile-routing fixes (2026-07-14)

Saved record of the review-and-fix session on `feat/accent-reach-dial`.

## Request

> "The two things I need you for" — preserve the shipped v0.9.0 look at the dial default and stop
> Settings → Done from consuming the global accent under a colored profile.

The owner then clarified that prior visual/product direction should be reused when it still applies:

> "we can and should look at such things I said long ago"

## What was reviewed

- The immutable `stable-v0.9.0-b31...c8a9c9f` dial commit, all 16 changed files, and its runtime callers.
- Repo rules, product spec §17/§25.2, QA accent rows, the completed v0.9.0 design, current-code theme map,
  retained reviews, ownership notes, changelog, and saved PiPlay context.
- Git history around v0.5.0, v0.6.0, and v0.9.0 to locate when the global-write guard disappeared and
  when profile-driven accents made that omission harmful.

## Decisions

- Keep the wash linear across 0–100; make glyph reach finish at 50 so default 50 is byte-identical to
  v0.9.0 (`#2BAED0` glyph and `#12343F` wash).
- Keep `ProfileAccentService` as the sole profile-vs-global commit owner.
- Reapply the resolved active accent after a full theme palette replacement, without sending a duplicate
  accent-only update to an open Popout Player.
- Carry accent ownership as a Settings behavior flag; never infer it from the user-facing hint string.
- Keep the global preset-default rule active behind a colored profile: the profile stays exact, while an
  untouched hidden global fallback advances and a custom global remains unchanged.
- Treat earlier owner statements as usable context, while marking the June identity-only direction
  explicitly superseded by the later P2 decision.
- Preserve completed design records and create a separate follow-up spec/plan/review trail.

## Implementation

- New:
  - `docs/superpowers/specs/2026-07-14-accent-reach-default-and-routing-fixes-design.md`
  - `docs/superpowers/plans/2026-07-14-accent-reach-default-and-routing-fixes.md`
  - `docs/reviews/review-2026-07-14-accent-reach-dial-and-routing.md`
  - this worklog
- Edited:
  - theme derivation/default seeds and their Settings/model descriptions;
  - Settings/MainWindow/profile-aware persistence and repaint flow;
  - logic/WPF/resource/preset-switch regression tests;
  - changelog, product/QA/current-theme/ownership docs and review index.

## Verification

- Red loop observed the intended failures: global `#2BAED0` became profile `#A78BFA`, default glyph was
  pale `#90D2E5`, curve anchors failed, and the new profile-ownership constructor contract was absent.
- Focused post-fix loop: 68/68 passed.
- `dotnet test PiPlay.sln --configuration Debug --nologo`: 769/769 passed.
- `.\Build-PiPlay.ps1 -Stage Build -NoVersionBump -NoBuildNumberBump`: passed, 0 warnings/errors; version
  `0.9.0` and build `31` remained unchanged.
- `.\scripts\Preflight-SpecGate.ps1`: pass.
- Deployed-Stable visual QA: pending; the requested test deployment is diagnostics-only, not release evidence.

## Disposition

- Branch: `feat/accent-reach-dial`, based on `stable-v0.9.0-b31`.
- Existing dial commit: `c8a9c9f` (already on `origin/feat/accent-reach-dial`).
- Follow-up fixes ship with this worklog in the next commit on the branch; the exact identity is recorded by Git history.

## Commits

- `c8a9c9f` — `feat(ui): make the accent's reach a user dial (0-100), wired end to end`
- Follow-up fix commit: this worklog is included in it.
