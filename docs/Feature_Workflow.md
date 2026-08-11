# Feature workflow

Use this for non-trivial product, UI, reliability, or pipeline changes.

## 1. Bound the change

Read:

- `docs/AGENTS.md` for repository constraints and terminology.
- `docs/PiPlay_Product_Engineering_Spec.md` for normative behavior.
- `docs/DECISIONS.md` before changing architecture, platform, WebView2, packaging, or window policy.
- `docs/SPEC_GAPS_AND_OWNERSHIP.md` for unresolved work and ownership.
- `docs/YouTube_Compliance.md` for YouTube/page-script changes.

## 2. Record the contract

Add `docs/superpowers/specs/YYYY-MM-DD-<topic>-design.md` from `docs/superpowers/templates/feature-design-template.md`. It must state:

- goals and observable acceptance criteria;
- requirement IDs (`Q-*`, `REQ-*`, or `tooling/docs`);
- settled decisions and non-goals;
- affected files and ownership seams;
- automated versus deployed/manual verification;
- documentation/changelog impact;
- unresolved questions, or `none`.

Multi-step work requires `docs/superpowers/templates/plan-template.md` for temporary coordination. Remove a completed plan after its durable decisions, constraints, commands, and open work are in canonical docs. Do not retain worklogs or narrative status reports.

The PR gate fails code changes under `src/`, `scripts/`, or `tests/` without a changed dated design spec. Override only with a PR-body line `Spec-Exception: <reason>`. The gate proves that a spec changed, not that it is correct; reviewers still read it.

## 3. Implement through existing seams

- URL parsing/targets: `YouTubeUrlHelper`.
- YouTube DOM scripts: `YouTubeDomBridge`.
- Navigation: `NavigationPolicy` and `PopoutNavigationPolicy`.
- Atomic settings/recovery: `SettingsService`.
- Profiles: `ProfileService`.
- Placement/DPI: `WindowPlacementService` and `PlacementMath`.
- Local redacted diagnostics: `LoggingService`.
- Update `docs/CHANGELOG.md` for user-visible changes.

## 4. Verify

```powershell
# Fast development filters
dotnet test --filter Category=Logic
dotnet test --filter Category=Markup
dotnet test --filter Category=Wpf

# Canonical local/CI gate
pwsh -NoProfile -File .\scripts\Test-LocalCI.ps1
```

Release candidates additionally require the verified deployed Stable copy and `docs/QA_Checklist.md`:

```powershell
# Commit VERSION, BUILD_NUMBER, and docs/CHANGELOG.md first.
.\scripts\Publish-Stable.ps1
.\scripts\Verify-StableDeploy.ps1
pwsh -File .\scripts\Test-UiSmoke.ps1 -ExePath E:\Dev_test_implemenations\PiPlay\PiPlay.exe
```

Never use repo build output as manual QA. Signing is optional via `Publish-Stable.ps1 -SignScript <path>`; it must precede manifest hashes.

Version rules:

- user-visible feature/behavior: minor;
- breaking change/milestone: major;
- fix or small tweak: patch;
- identical-source rebuild/doc-only deploy: keep `VERSION`, increment `BUILD_NUMBER`.

`Publish-Stable.ps1` is exact-source by default, preserves deployed `PiPlayData`, and creates `stable-vX.Y.Z-bN`. Push the commit/tag only after `VERDICT: RELEASE VERIFIED`. `-AllowVersionBump` and `-AllowDirty` are diagnostic and not release evidence.

```powershell
# Diagnostic-only deploys; never release evidence.
.\scripts\Publish-Stable.ps1 -AllowVersionBump -Version patch
.\scripts\Publish-Stable.ps1 -AllowDirty
```

Publishing refuses concurrent runs and preflights tag collisions. It copies to a sibling staging directory, re-hashes the staged payload, then swaps with backup/rollback. A failed or interrupted swap restores the previous copy when possible; an incomplete rollback preserves and reports the backup path for manual recovery.

## 5. Open the PR

Complete `.github/pull_request_template.md`: design spec, requirements, acceptance criteria, commands/results, docs/changelog impact, and manual evidence. Workflow checks:

- `Build and test (Windows)` calls `scripts/Test-LocalCI.ps1`. Public PRs use hosted `windows-latest`; trusted `main`/manual events may use repository variable `PIPLAY_WINDOWS_RUNNER`. Keep it unset until a dedicated disposable runner is ready; an offline selected runner does not fail over automatically.
- To enable that runner later, set `PIPLAY_WINDOWS_RUNNER=piplay-ci`, manually dispatch CI, verify the intended runner through the Actions Jobs API, then clear the variable and rerun to prove hosted recovery. The variable is one scalar label; a label array requires `fromJSON`.
- `Require design spec` runs unconditionally and decides from the changed-file list so required-check status cannot remain pending on docs-only PRs.

When branch protection is enabled, require both `Build and test (Windows)` and `Require design spec` on `main`.
