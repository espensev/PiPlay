# PiPlay — Build pipeline RID restore design

**Date:** 2026-06-03
**Status:** Implemented in PR #2 follow-up
**Requirements:** Release pipeline reliability, spec section 21 (Packaging and release strategy), Q-6 (recover cleanly)

---

## Goals

Fix a merge-gate failure found after the Phase 2 privacy polish doc cleanup:

1. `dotnet test PiPlay.sln -c Debug` restored the app project without a runtime identifier.
2. `Build-PiPlay.ps1 -Stage Build -NoVersionBump -NoBuildNumberBump` then ran `dotnet restore -r win-x64`, but the SDK reported restore as up to date.
3. The script's following `dotnet build ... -r win-x64 --no-restore` failed with `NETSDK1047` because `project.assets.json` lacked the `net10.0-windows/win-x64` target.

The pipeline should be deterministic regardless of whether a previous command restored no-RID assets.

## Change

When `-Runtime` is configured, append `--force` to the restore step:

```powershell
dotnet restore <project> -r win-x64 --force
```

That forces the runtime-specific target back into `project.assets.json` before the script intentionally uses `--no-restore` for build and publish.

## Non-Goals

- No release-stage behavior change beyond restore determinism.
- No change to `VERSION`, `BUILD_NUMBER`, publish layout, metadata shape, signing posture, or runtime defaults.

## Verification

Run the sequence that reproduced the issue:

```powershell
dotnet test PiPlay.sln -c Debug
.\Build-PiPlay.ps1 -Stage Build -NoVersionBump -NoBuildNumberBump
git diff --check
```
