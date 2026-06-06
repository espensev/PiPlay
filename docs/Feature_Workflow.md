# PiPlay feature workflow

Use this path for any non-trivial product, UI, quality, or pipeline change.

## 1. Orient

Read these first:

- `docs/AGENTS.md` for repo rules, vocabulary, quality bar, and ownership boundaries.
- `docs/PiPlay_Product_Engineering_Spec.md` for normative behavior and requirement IDs.
- `docs/SPEC_GAPS_AND_OWNERSHIP.md` for open product decisions and code ownership.
- `docs/adr/` before changing architecture, platform, WebView2, packaging, or window policy.

If a request touches YouTube behavior, also read `docs/YouTube_Compliance.md`.

## 2. Write the change note

Before code for a non-trivial change, add:

- A dated design spec at `docs/superpowers/specs/YYYY-MM-DD-<topic>-design.md`.
  Start from `docs/superpowers/templates/feature-design-template.md`.
- A dated implementation plan at `docs/superpowers/plans/YYYY-MM-DD-<topic>.md` when the work spans multiple steps.

Link the spec from the PR.

### Definition of Ready

A change is ready to implement once its design spec settles all of:

- **Goals** — the gap closed and what "done" looks like; what stays unchanged.
- **Requirement IDs served** — `REQ-*`, `Q-*`, or spec § numbers (or "tooling/docs" with the motivating note).
- **Acceptance criteria** — observable, checkable conditions for "done".
- **Non-goals** — what this pass deliberately does not do.
- **Affected files** — the expected change-by-file list.
- **Test plan** — which layer covers what, and what is verified live vs locally.
- **Docs / changelog impact** — `docs/CHANGELOG.md` for user-visible changes; any ADRs.
- **Unresolved decisions** — open questions, or "none".

CI enforces the spec half of this: any PR touching `src/`, `scripts/`, or `tests/` must ship a changed
dated spec (see step 5).

## 3. Implement inside the ownership boundaries

Keep product terms stable: Video Popout, Popout Player, Source Window, Source Placeholder,
Pin, Fade, and Auto. Do not surface internal names such as `MainWindow`, `PlayerWindow`, or
`Detach` in user-facing UI.

Prefer existing seams:

- URL parsing and YouTube target construction: `YouTubeUrlHelper`.
- JavaScript snippets: `YouTubeDomBridge`.
- Navigation policy: `NavigationPolicy`.
- Atomic settings and recovery: `SettingsService`.
- Profile persistence and validation: `ProfileService`.
- Placement math and monitor restore: `WindowPlacementService` / `PlacementMath`.
- Local diagnostics and URL redaction: `LoggingService`.

Update `docs/CHANGELOG.md` for user-visible changes.

## 4. Test locally

Run the same deterministic lane that CI runs:

```powershell
dotnet test PiPlay.sln --configuration Debug
.\Build-PiPlay.ps1 -Stage Build -NoVersionBump -NoBuildNumberBump
```

Use narrower filters while developing:

```powershell
dotnet test --filter Category=Logic
dotnet test --filter Category=Markup
dotnet test --filter Category=Wpf
```

Release candidates still need the manual lane:

```powershell
.\Build-PiPlay.ps1 -Stage Publish
pwsh -File scripts\Test-UiSmoke.ps1
```

Then run `docs/QA_Checklist.md` for shareable builds.

### Stable publish (deploy a differentiable copy)

To cut a **stable** copy that runs side by side with the dev app and deploy it for test use:

```powershell
.\scripts\Publish-Stable.ps1            # deploys to E:\Dev_test_implemenations\PiPlay (override with -DeployRoot)
```

This test-gates, builds the **Stable** channel (baked in via `-p:PiPlayChannel=Stable`), validates the
publish metadata, and deploys a runnable copy — replacing binaries but **preserving** the `PiPlayData`
runtime folder. A Stable copy is differentiable from dev: data beside the exe, its own single-instance
identity, and a `PiPlay — Stable vX.Y.Z (bN)` title. After deploying, launch the deployed `PiPlay.exe` and
confirm it opens its own window with an isolated `PiPlayData` folder beside it. Background and trade-offs:
`docs/adr/0007-stable-channel-and-portable-data.md`.

Heads-up: the build stage force-stops every running `PiPlay.exe` (including the dev app) to free the build
tree, so close the dev app before publishing — or expect it to be stopped mid-publish, losing its unsaved
window state — and relaunch it afterward.

## 5. Open the PR

The PR description is pre-filled from `.github/pull_request_template.md`; fill every section:

- Design spec link.
- Requirement IDs served, such as `Q-6`, `REQ-UI-01`, or `REQ-PROFILE-01`.
- Acceptance criteria.
- Local verification commands and results.
- Docs / changelog impact, and any manual QA evidence paths under `docs/evidence/` for
  release-candidate or visual work.

Two GitHub Actions checks run on every pull request:

- **Build and test (Windows)** (`.github/workflows/ci.yml`) — the deterministic test lane and the
  non-mutating build gate.
- **Require design spec** (`.github/workflows/spec-check.yml`) — fails a PR that changes `src/`,
  `scripts/`, or `tests/` without a changed dated spec matching
  `docs/superpowers/specs/YYYY-MM-DD-*-design.md`. To override deliberately (a trivial or urgent
  change), add a line `Spec-Exception: <reason>` to the PR description; the check then passes and
  records the reason.

After a check's first successful run on GitHub, make it a required branch-protection check for `main`
so red commits cannot merge unnoticed.
