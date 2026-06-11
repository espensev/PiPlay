# PiPlay — repo rules (read first)

Full orientation: `docs/AGENTS.md` (terminology, quality bar, conventions) and the product spec
`docs/PiPlay_Product_Engineering_Spec.md`. The two rules below are absolute and override habit.

## 1. Manual testing happens OUTSIDE the repo

- ALL manual/human testing — anything beyond the automated in-repo smoke runs — uses the deployed
  Stable copy at `E:\Dev_test_implemenations\PiPlay\PiPlay.exe`.
- Deploy ONLY via `.\scripts\Publish-Stable.ps1` (ADR-0007 — the only sanctioned promote path).
- NEVER present a launch of repo build output (`src\...\bin\...` or `bin\publish\...`) as manual-QA
  or release verification. Stale binaries lie about what the code does.
- Repo builds are for the automated dev loop only: `dotnet run`, the deterministic test gate, and
  the `run-piplay` skill's change-verification smoke.
- Before any manual test pass, verify what is actually deployed:

  ```powershell
  .\scripts\Verify-StableDeploy.ps1
  ```

  One command: re-hashes every deployed artifact, confirms exe/marker/manifest agree, and prints
  the exact version, build number, and source commit — including how far the deploy lags HEAD.

## 2. Version discipline

- `VERSION` is the semantic version (what changed for users); `BUILD_NUMBER` is the monotonic
  counter (moves on every publish). Both live as plain files at the repo root.
- Choosing the bump when publishing (`.\scripts\Publish-Stable.ps1`):
  - New user-visible feature or behavior → `-Version minor` (breaking change / milestone → `-Version major`).
  - Fixes and small tweaks only → default (patch bump).
  - Rebuild of identical source (re-deploy, doc-only) → `-NoVersionBump` (build number still moves).
  - Exact pre-stamped source identity → commit `VERSION`/`BUILD_NUMBER` first, then publish with
    `-NoVersionBump -NoBuildNumberBump`.
- Normal publishes bump `VERSION`/`BUILD_NUMBER` in the working tree but NEVER commit them. After a
  stable deploy, commit the bumped files (with the CHANGELOG entry) and tag the source commit
  `stable-vX.Y.Z-bN`. For an exact current-HEAD deploy, pre-commit the stamps and publish with
  `-NoVersionBump -NoBuildNumberBump`; then tag that committed source. Without this, the deployed
  version exists in no commit and provenance is lost.
