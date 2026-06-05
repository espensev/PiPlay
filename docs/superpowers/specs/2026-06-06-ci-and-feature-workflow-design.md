# CI and feature workflow - design

## Goals

Close the repository workflow gap: contributors already have a deterministic local Lane A
(`dotnet test`) and a non-mutating build gate, but GitHub has no CI workflow. A red commit can
therefore land if local verification is skipped or run differently. This pass adds a GitHub
Actions CI gate and documents the feature workflow in one obvious place.

## Requirements served

- `Q-6` recover-cleanly posture, indirectly: tests and build must catch regressions before merge.
- `REQ-UI-01` / `REQ-UI-02`, indirectly: the existing Markup and Wpf test categories remain in
  the default `dotnet test` lane.
- `docs/Regression_Test_Suite_Design.md`: promote the already-designed Lane A into CI while
  preserving Lane B as manual release smoke.

## Settled decisions

1. **CI runs on Windows only.** PiPlay targets `net10.0-windows` with WPF and WebView2, so a
   Linux/macOS matrix would be noisy instead of useful.
2. **CI mirrors the deterministic local gate.** The job restores the solution, runs
   `dotnet test PiPlay.sln --configuration Debug --no-restore`, then runs
   `.\Build-PiPlay.ps1 -Stage Build -NoVersionBump -NoBuildNumberBump`. The build stage checks
   the Release/RID path without publishing, signing, mutating `VERSION`, or mutating
   `BUILD_NUMBER`.
3. **No path filters.** Required checks that are skipped by path filters can block PRs in a
   pending state. Running the small deterministic lane on every push and PR is simpler and safer.
4. **Use `global.json`.** The workflow installs the SDK version/roll-forward policy from the
   repo rather than duplicating the .NET version in YAML.
5. **Manual smoke stays manual.** `scripts/Test-UiSmoke.ps1` requires an interactive desktop,
   network, and WebView2 runtime. It remains the Lane B release gate documented in
   `tests/README.md` and `docs/QA_Checklist.md`.

## Changes by file

| File | Change |
|---|---|
| `.github/workflows/ci.yml` | Add GitHub Actions CI job for restore, deterministic tests, and non-mutating build gate. |
| `docs/Feature_Workflow.md` | Add the contributor path for adding features, testing, PR notes, and CI expectations. |
| `docs/README.md` | Link the feature workflow from the documentation index. |
| `README.md` | Link the feature workflow from the root start-here list. |
| `docs/AGENTS.md` | Mention the local/CI gate in the working rules. |
| `docs/Regression_Test_Suite_Design.md` | Mark the original "no CI yet" note as superseded by the new workflow. |
| `docs/superpowers/plans/2026-06-06-ci-and-feature-workflow.md` | Track this implementation pass. |

## Out of scope

- Release publishing, code signing, and artifact upload.
- Running real YouTube playback or screenshot smoke in GitHub-hosted CI.
- GitHub branch-protection configuration, which must be enabled in the repository settings after
  the first workflow run exists.
