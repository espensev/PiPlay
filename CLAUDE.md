# PiPlay — repo rules (read first)

Full orientation: `docs/AGENTS.md` (terminology, quality bar, conventions) and the product spec
`docs/PiPlay_Product_Engineering_Spec.md`. The two rules below are absolute and override habit.

## 1. Manual testing happens OUTSIDE the repo

- ALL manual/human testing — anything beyond the automated in-repo smoke runs — uses the deployed
  Stable copy at `E:\Dev_test_implemenations\PiPlay\PiPlay.exe`.
- Deploy ONLY via `.\scripts\Publish-Stable.ps1` (ADR-0007 — the only sanctioned promote path).
- NEVER present a launch of repo build output (`src\...\bin\...` or `bin\publish\...`) as manual-QA
  or release verification. Stale binaries lie about what the code does.
- Repo builds are for the automated dev loop only: `dotnet run` and the deterministic test/build
  gate (`pwsh -NoProfile -File .\scripts\Test-LocalCI.ps1`).
- Before any manual test pass, verify what is actually deployed:

  ```powershell
  .\scripts\Verify-StableDeploy.ps1
  ```

  One command: re-hashes every deployed artifact, confirms exe/marker/manifest agree, and fails
  closed unless the deployed version/build/source commit/tag match the clean repo state.

## 2. Version discipline

- `VERSION` is the semantic version (what changed for users); `BUILD_NUMBER` is the monotonic
  counter (moves on every publish). Both live as plain files at the repo root.
- Release-candidate publishes are exact-source by default:
  1. Choose the version move first: feature → minor, fix → patch, breaking/milestone → major.
  2. Edit `VERSION` and increment `BUILD_NUMBER`, update `docs/CHANGELOG.md` (there is no root
     changelog — that is deliberate), and commit those stamps.
  3. Run `.\scripts\Publish-Stable.ps1` from a clean tree. The script uses the committed stamps,
     refuses dirty release evidence, deploys Stable, verifies it, and creates the local tag
     `stable-vX.Y.Z-bN` on that exact commit.
  4. Push the commit and tag after verification.
- `-AllowVersionBump` and `-AllowDirty` are diagnostic escape hatches only. Their manifests and
  verifier output are marked **not release evidence**; do not use them for release-candidate QA.
