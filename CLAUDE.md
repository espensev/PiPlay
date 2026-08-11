# PiPlay repository rules

Read `docs/AGENTS.md`, `docs/PiPlay_Product_Engineering_Spec.md`, and `docs/DECISIONS.md` before changing code.

## Manual tests use Stable only

- Manual/human testing must use `E:\Dev_test_implemenations\PiPlay\PiPlay.exe`.
- Deploy only with `.\scripts\Publish-Stable.ps1`; verify first with `.\scripts\Verify-StableDeploy.ps1`.
- Never present `src\...\bin\...` or `bin\publish\...` as manual QA or release evidence. Repo output is for `dotnet run` and the automated gate:

  ```powershell
  pwsh -NoProfile -File .\scripts\Test-LocalCI.ps1
  ```

## Release stamps

- `VERSION` is semantic version; `BUILD_NUMBER` is monotonic and advances on every publish.
- For a release candidate, choose the version move, update `VERSION`, `BUILD_NUMBER`, and `docs/CHANGELOG.md`, then commit them.
- Run `.\scripts\Publish-Stable.ps1` from a clean tree. It uses the committed stamps and creates `stable-vX.Y.Z-bN` on that exact commit. Push the commit and tag only after verification.
- `-AllowVersionBump` and `-AllowDirty` are diagnostic only and never release evidence.
