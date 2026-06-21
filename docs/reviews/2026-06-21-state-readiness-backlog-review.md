# PiPlay state, readiness, and backlog review

Date: 2026-06-21

Scope: current `main` checkout after the `stable-v0.5.0-b21` merge, including the dirty PopOutButton text-rendering regression fix, release/deploy provenance, active docs, review records, and tracked backlog.

## Verdict

The current code candidate is healthy at the deterministic build/test level, but it is not clean-release ready.

- Source/tests: green. Full `dotnet test PiPlay.sln --configuration Debug` passed with 679 tests, and the Release build gate passed with 0 warnings / 0 errors.
- Landing workflow: resolved during cleanup. The first review pass found the spec preflight red; the cleanup added `docs/superpowers/specs/2026-06-21-popout-button-rendering-fix-design.md`, and `scripts/Preflight-SpecGate.ps1 -Quiet` now passes.
- Stable deploy: diagnostics-only. The deployed Stable copy is `v0.5.0 b21 @ 52fa123` and re-hashes clean, but its manifest has `releaseEvidence=false`, `sourceDirty=true`, and `signing.enabled=false`.
- Public distribution: blocked until a signed, clean-tree Stable publish is produced and verified.

## Findings

### P1 - Local spec preflight needed a dated spec (resolved)

`powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Preflight-SpecGate.ps1 -Quiet` failed:

```text
[FAIL] This branch changes code under src/, scripts/, or tests/ but adds no dated design spec matching docs/superpowers/specs/YYYY-MM-DD-*-design.md (see docs/Feature_Workflow.md).
[FAIL] Add one, or add a line 'Spec-Exception: <reason>' to the PR description to override deliberately.
[FAIL] Spec gate would FAIL.
```

The current diff is a narrow PopOutButton rendering/height fix plus tests. Cleanup resolution: added `docs/superpowers/specs/2026-06-21-popout-button-rendering-fix-design.md`, then reran the local preflight successfully.

### P1 - Stable deploy is diagnostics-only, not release evidence

`scripts\Verify-StableDeploy.ps1 -AllowNonReleaseEvidence` passed only as diagnostics:

- Version/build: `v0.5.0` build `21`
- Commit/tag: `52fa1238aed8284f2f5f6378d83f0479df5b1b5c`, `stable-v0.5.0-b21`
- Deploy path: `E:\Dev_test_implemenations\PiPlay`
- SHA256: `ED09898AB3ECCB4ACFBF1BBF3D1DAC83EEDDA2F96D1F834CDF79FB59799059D6`
- Warnings: manifest says not release evidence, manifest source tree was dirty, current repo tree is dirty

The deployed `build-info.json` confirms `releaseEvidence=false`, `sourceDirty=true`, and dirty source entries for:

- `src/PiPlay/MainWindow.xaml`
- `src/PiPlay/Theme/ControlStyles.xaml`
- `tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs`
- `tests/PiPlay.Tests/Ui/XamlInvariantTests.cs`

This is acceptable for internal diagnostics, but release-candidate QA should wait for a clean exact-source publish.

### P1 - Public distribution remains blocked on signing

`Get-AuthenticodeSignature E:\Dev_test_implemenations\PiPlay\PiPlay.exe` returns `NotSigned`, and the deployed manifest has:

```json
"signing": {
  "enabled": false,
  "reason": "not configured"
}
```

This is fine for internal QA only. Public distribution needs a signed publish through the pre-manifest signing path so the manifest hashes match signed bytes.

### P2 - v0.5.0 release evidence is incomplete as a repo artifact

The changelog has a `0.5.0` section and the deployed manifest records `v0.5.0 b21`, but there is no retained `docs/evidence/*0.5.0*` or `*b21*` release evidence note. The strongest retained release evidence note is still `docs/evidence/phase1-release-v0.4.3-b20.md`.

If `v0.5.0 b21` is meant to become the current QA anchor, add a dedicated evidence note after the clean publish/verifier pass and record signing state, exact source, smoke results, and any owner-gated manual rows.

### P2 - Current docs contain stale status/closure markers

- `docs/README.md` reported `Beta candidate (v0.4.0-beta)` even though `VERSION`/`BUILD_NUMBER` are `0.5.0`/`21` and `HEAD` has `stable-v0.5.0-b21`. Cleanup resolution: changed this to `Beta candidate (v0.5.0)`.
- `docs/reviews/2026-06-14-theme-v2-spec-eval.md` still describes TG-4 as an open hue-wheel safety gap, while the current implementation has the color wheel, readability policy, and hue-sweep coverage.
- `docs/superpowers/plans/2026-06-14-theme-v2-tight-scope.md` still shows Task 4a / Task 8 / Task 9 / Task 10 unchecked.
- `docs/superpowers/plans/2026-06-20-profile-accent-color-wheel.md` still has unchecked final verification rows even though the feature has shipped into `0.5.0`.

Treat the implementation records as useful history, but do not use unchecked boxes in older plans as the only source of current state. The active state needs a short docs cleanup pass after the PopOutButton fix lands.

## Current dirty diff

Working tree before this review artifact:

```text
## main...origin/main
 M src/PiPlay/MainWindow.xaml
 M src/PiPlay/Theme/ControlStyles.xaml
 M tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs
 M tests/PiPlay.Tests/Ui/XamlInvariantTests.cs
```

The diff is coherent:

- `src/PiPlay/MainWindow.xaml` lowers PopOutButton vertical margin from `6,9,10,9` to `6,6,10,6` and applies `TextOptions.TextFormattingMode="Display"`, `TextHintingMode="Fixed"`, and `TextRenderingMode="Grayscale"` directly to `PopOutButtonIcon` and `PopOutButtonText`.
- `src/PiPlay/Theme/ControlStyles.xaml` applies the same text rendering options to `AccentButton` and its `ContentPresenter`.
- `tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs` adds runtime coverage proving the nested PopOutButton text elements get the intended rendering options.
- `tests/PiPlay.Tests/Ui/XamlInvariantTests.cs` adds markup coverage for `AccentButton`, nested PopOutButton text rendering options, and a PopOutButton height budget check against the largest theme control density.
- `docs/superpowers/specs/2026-06-21-popout-button-rendering-fix-design.md` records the narrow design so the workflow gate has a repo-backed rationale.
- `docs/CHANGELOG.md` records the visible fix under `Unreleased`.
- `docs/README.md` updates the stale beta version text.

I did not find a code-level correctness blocker in this diff.

## Validation run

- `git fetch --all --prune`: passed; pruned remote topic refs for the merged/deleted profile accent and release branches.
- `git status --short --branch`: dirty on `main`, tracking `origin/main`.
- `git diff --check`: no whitespace errors; Git emitted LF-to-CRLF normalization warnings for changed text files.
- `dotnet test PiPlay.sln --configuration Debug`: passed, 679 total.
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\Build-PiPlay.ps1 -Stage Build -NoVersionBump -NoBuildNumberBump`: passed, Release build 0 warnings / 0 errors. One earlier parallel run failed with `NETSDK1047` because it raced a simultaneous `dotnet test` restore; rerunning serially passed.
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Preflight-SpecGate.ps1 -Quiet`: passed after adding the dated design spec.
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Verify-StableDeploy.ps1 -AllowNonReleaseEvidence`: diagnostics pass with 3 warnings; not release evidence.
- `Get-AuthenticodeSignature E:\Dev_test_implemenations\PiPlay\PiPlay.exe`: `NotSigned`.

## Outstanding / backlog

Immediate before landing the current PopOutButton fix:

1. Commit the source/test fix plus the dated design spec, changelog note, README status correction, and this review artifact.

Immediate before using Stable as current release-candidate evidence:

1. Publish from a clean exact-source tree with `scripts\Publish-Stable.ps1`.
2. Run `scripts\Verify-StableDeploy.ps1` without diagnostics overrides and record `VERDICT: RELEASE VERIFIED`.
3. Add a retained `v0.5.0-b21` or successor evidence note under `docs/evidence/`.
4. If the audience is public, configure signing and publish through `-SignScript` before manifest hashes are written.

Owner/manual QA still pending or environment-gated:

1. Live YouTube/account/autoplay matrix from `docs/QA_Checklist.md`.
2. Compact-player live rows: playlists, restricted/embed-disabled handling, recommendation retarget, fallback, watchdog/auto-dismiss, timestamp-carrying fallback.
3. DPI/mixed-monitor resize and pointer hit-test rows.
4. Screen-reader/accessibility pass for all icon-only controls.
5. Destructive privacy rows: reset app state and clear browser data against a controlled signed-in state.

Tracked backlog:

1. `TEST-BACKLOG-01`: add a PowerShell behavior test/smoke for `scripts/NativeCommand.ps1` proving stderr under `$ErrorActionPreference="Stop"` does not abort on exit `0`, and non-zero exit still fails.
2. `PERF-BACKLOG-01`: monitor accent drag preview for stutter before adding throttling.
3. Theme-V2/profiles plan closure cleanup: mark TG-4 / Task 9 and related final verification rows done or add a disposition note explaining that the plan files are immutable history.
