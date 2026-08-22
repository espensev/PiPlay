# Final release acceptance

Automation owns intermediate verification. Do not pause for manual approval between steps or repeat a manual row for behavior already covered by source tests and scripts.

Set `PIPLAY_STABLE_ROOT` to the deployed Stable directory and run:

```powershell
$stableRoot = $env:PIPLAY_STABLE_ROOT
if ([string]::IsNullOrWhiteSpace($stableRoot)) { throw 'Set PIPLAY_STABLE_ROOT first.' }
.\scripts\Publish-Stable.ps1 -DeployRoot $stableRoot
.\scripts\Verify-StableDeploy.ps1 -DeployRoot $stableRoot
pwsh -NoProfile -File .\scripts\Test-UiSmoke.ps1 -ExePath (Join-Path $stableRoot 'PiPlay.exe')
```

Stable publish runs the local gate (restore, tests, isolated test data, Node enforcement, and Release build), then performs the exact-source/staged-swap path. Stable verification re-checks manifest identity, artifact hashes, version/build, tag, source commit, and tree state. UI smoke checks the five named Source controls and captures the rendered window. These commands are the release evidence; repo output is not manual evidence.

## The user checks the result

On the verified deployed copy, perform only the end-result checks that automation cannot observe:

1. Pop out a playing video, listen through launch and return/close, and confirm there is no duplicate audio (Q-1).
2. Repeat once with a playlist or mix when available; confirm the current video/list context returns. Use the final Popout state, not an intermediate step, as the acceptance target.
3. If saved profiles or live ads/account flows are unavailable, record **not run**. Never create synthetic state or call unavailable coverage a pass.

Record the Stable version/build, source commit/tag, command results, date, and any unavailable or failed end-result check near the evidence. The open Q-1 listening gap and other unresolved work belong in [`SPEC_GAPS_AND_OWNERSHIP.md`](SPEC_GAPS_AND_OWNERSHIP.md); this file is not a backlog.
