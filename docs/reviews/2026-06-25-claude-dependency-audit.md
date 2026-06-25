# Claude Dependency Audit - 2026-06-25

## Scope

Audit target: Claude-attributed and Claude-co-authored work currently reachable from `main`, plus the visible
remote Claude branches:

- `main` / `origin/main` at `5bff7d8` (`chore: gitignore the v0.7.2-b25 audit brief (local review packet)`).
- Release anchor `stable-v0.7.2-b25` at `9e602ed`.
- Remote Claude branches checked: `origin/claude/determined-boyd-94fad8`,
  `origin/claude/wizardly-cori-2997c3`.

This audit is dependency-focused. It does not re-review UI behavior, theme correctness, or release
provenance except where those surfaces affect dependency or package risk.

## Verdict

No dependency-manifest change was found in Claude's currently merged work.

The package graph is small and NuGet advisory status is clean as of this audit:

- `dotnet list PiPlay.sln package --vulnerable --include-transitive`: no vulnerable packages for
  `PiPlay` or `PiPlay.Tests`.
- `dotnet list PiPlay.sln package --deprecated --include-transitive`: runtime project clean; test
  project reports `xunit` 2.9.3 and its v2 transitive packages as `Legacy` with xUnit v3 alternatives.
- `dotnet list PiPlay.sln package --outdated --include-transitive`: update opportunities exist, but
  no blocking dependency issue was found.

## Dependency Surface

Only three dependency manifest files are present:

- `global.json`
- `src/PiPlay/PiPlay.csproj`
- `tests/PiPlay.Tests/PiPlay.Tests.csproj`

There is no `Directory.Packages.props`, `packages.lock.json`, `NuGet.config`, `package.json`, npm/pnpm/yarn
lock file, or other package manager surface in the current tree.

The app project targets `net10.0-windows`, uses WPF, and publishes framework-dependent:

- `SelfContained=false`
- `PublishTrimmed=false`
- `PublishSingleFile=false`

The current SDK resolution is controlled by `global.json` requesting `10.0.300` with
`rollForward=latestFeature`; on this machine it resolved to SDK `10.0.301`.

## Current Packages

Runtime project:

| Project | Package | Requested | Resolved |
| --- | --- | ---: | ---: |
| `PiPlay` | `Microsoft.Web.WebView2` | `1.0.3967.48` | `1.0.3967.48` |

Test project:

| Project | Package | Requested | Resolved |
| --- | --- | ---: | ---: |
| `PiPlay.Tests` | `coverlet.collector` | `6.0.4` | `6.0.4` |
| `PiPlay.Tests` | `Microsoft.NET.Test.Sdk` | `17.14.1` | `17.14.1` |
| `PiPlay.Tests` | `xunit` | `2.9.3` | `2.9.3` |
| `PiPlay.Tests` | `xunit.runner.visualstudio` | `3.1.4` | `3.1.4` |
| `PiPlay.Tests` | `Xunit.StaFact` | `1.1.11` | `1.1.11` |

Transitive packages observed in `PiPlay.Tests`:

- `Microsoft.CodeCoverage` `17.14.1`
- `Microsoft.TestPlatform.ObjectModel` `17.14.1`
- `Microsoft.TestPlatform.TestHost` `17.14.1`
- `Microsoft.Web.WebView2` `1.0.3967.48`
- `Newtonsoft.Json` `13.0.3`
- `xunit.abstractions` `2.0.3`
- `xunit.analyzers` `1.18.0`
- `xunit.assert` `2.9.3`
- `xunit.core` `2.9.3`
- `xunit.extensibility.core` `2.9.3`
- `xunit.extensibility.execution` `2.9.3`

## Claude Impact Check

Manifest history:

- `git log -- src/PiPlay/PiPlay.csproj tests/PiPlay.Tests/PiPlay.Tests.csproj global.json` shows the
  last manifest edits came from non-Claude-authored commits dated 2026-05-31 through 2026-06-07.
- `git diff stable-v0.4.3-b20..main -- src/PiPlay/PiPlay.csproj tests/PiPlay.Tests/PiPlay.Tests.csproj global.json`
  is empty.
- `git diff stable-v0.5.0-b21..main -- ...` is empty.
- `git diff stable-v0.7.1-b24..main -- ...` is empty.
- `git diff main...origin/claude/determined-boyd-94fad8` and
  `git diff main...origin/claude/wizardly-cori-2997c3` are empty.

Conclusion: Claude's reachable work did not add packages, change package versions, add package sources,
introduce a JavaScript package surface, or alter the .NET SDK/runtime dependency declaration.

## Findings

No blocking dependency findings.

Follow-up candidates:

1. `xunit` v2 is now reported as deprecated/legacy by NuGet. This is test-only, not runtime risk, but a
   future test-stack refresh should evaluate moving to xUnit v3 and `Xunit.StaFact` 3.x together rather
   than piecemeal.
2. `Microsoft.Web.WebView2` has a newer NuGet package available (`1.0.4022.49` versus `1.0.3967.48`).
   Because PiPlay intentionally uses the Evergreen WebView2 runtime, treat this as a normal maintenance
   update, not an urgent runtime-bundling change.
3. The repo has no NuGet lock file. Current restore is simple, but if supply-chain repeatability becomes
   a release requirement, consider adding locked restore deliberately and documenting the workflow impact.

## Evidence Commands

Commands run from `D:\Development\DesktopApps\PiPlay`:

```powershell
git status --short --branch
git log --oneline --decorate -8
git worktree list --porcelain
git branch --all --verbose --no-abbrev
rg --files -g "*.csproj" -g "*.props" -g "*.targets" -g "global.json" -g "NuGet.config" -g "packages.lock.json" -g "Directory.Packages.props" -g "package.json" -g "package-lock.json" -g "pnpm-lock.yaml" -g "yarn.lock"
git diff stable-v0.4.3-b20..main -- src\PiPlay\PiPlay.csproj tests\PiPlay.Tests\PiPlay.Tests.csproj global.json
git diff stable-v0.5.0-b21..main -- src\PiPlay\PiPlay.csproj tests\PiPlay.Tests\PiPlay.Tests.csproj global.json
git diff stable-v0.7.1-b24..main -- src\PiPlay\PiPlay.csproj tests\PiPlay.Tests\PiPlay.Tests.csproj global.json
dotnet --info
dotnet list PiPlay.sln package --include-transitive
dotnet list PiPlay.sln package --vulnerable --include-transitive
dotnet list PiPlay.sln package --deprecated --include-transitive
dotnet list PiPlay.sln package --outdated --include-transitive
```
