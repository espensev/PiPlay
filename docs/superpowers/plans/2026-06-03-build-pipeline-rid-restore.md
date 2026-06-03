# Build pipeline RID restore implementation plan

**Spec:** `docs/superpowers/specs/2026-06-03-build-pipeline-rid-restore-design.md`

## Tasks

- [x] Document the `dotnet test` -> runtime build restore failure and the narrow fix.
- [x] Patch `scripts/Build-PiPlay.ps1` so runtime restores include `--force`.
- [x] Update `docs/CHANGELOG.md` with the build-pipeline fix.
- [x] Verify with `dotnet test`, non-mutating `Build-PiPlay.ps1 -Stage Build`, and `git diff --check`.
