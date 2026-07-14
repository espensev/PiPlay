# Session worklog — release-pipeline hardening + v0.7.3 (2026-07-14)

Closes the release-pipeline findings that the same-day efficiency/customization review
(`docs/reviews/review-2026-07-14-efficiency-and-customization.md`) left open, and ships both halves as
v0.7.3 (build 26).

## Request

> "analyze and optimze this"

Disambiguated with the owner: **the open pipeline findings**, not another app-wide sweep.

## Owner decisions taken during the session

- **Signing: leave it ungated.** The owner signs locally ("for real it's not here") — a self-signed cert
  proves nothing a commit hash does not already prove, so gating release evidence on Authenticode would
  add ceremony, not provenance. The review's High finding is therefore knowingly declined, not deferred.
  `docs/AGENTS.md:49` still claims release binaries must be signed; that contradiction is left open, and
  the owner is told when signing would actually matter (it currently never does).
- **Commit the in-flight work first.** The tree held the prior session's verified efficiency changes.
  Committing them was the precondition for exercising the *real* release lane (a clean tree is a publish
  gate), not just the diagnostic one.

## What changed

**Deploy (High — the finding that mattered).** `Publish-Stable.ps1` used to delete the live payload and
copy the new one over the top, so any interruption left the only sanctioned manual-test installation
broken with nothing to fall back to. New `scripts/DeploySwap.ps1`:

1. **Stage** the payload into a sibling `.staging` — the slow copy happens outside the live copy.
2. **Verify** the staged bytes against their own `build-info.json`. A corrupt/short copy dies *here*,
   before the deployed copy is touched at all.
3. **Swap** via same-volume renames through a sibling `.backup`, with rollback on failure.

`Repair-InterruptedDeploy` runs first on every publish and either completes or reverses a swap that a
previous run was killed during. `PiPlayData` is never moved, so the login session survives.

**Publish.** The stable tag is now preflighted *before* the tests, the build, and the deploy (it used to
be checked only at the very end — a collision replaced Stable successfully and then failed). Repo and
deploy root are locked by mutex, so two publishes cannot interleave.

**App.** Log writes go to a bounded queue drained by one background thread that folds a burst into a
single append; `App.OnExit` drains it. Repeated failures (which log per poll tick) no longer mean
recurring synchronous disk I/O on the UI thread, and the rotation check no longer stats the file per
entry. The DWM border-suppression record — a test-only observation that was kept for every top-level
window ever shown and never reclaimed — is now gated off in production entirely.

## The bug the harness caught

`Move-Item` on a directory whose child is locked **half-moves it and still throws**. The first rollback
implementation replayed only the moves it had *recorded as successful*, so the half-moved directory was
skipped — and then the backup holding the only copy of those children was deleted. Real data loss, found
on the first run of `scripts/Test-DeploySwap.ps1` (case C3), not by reading the code.

Rollback now restores from what the backup **actually holds** (`Restore-DeployBackup`), merging into any
directory that still exists.

## Verification

- `dotnet test PiPlay.sln --configuration Debug` — **719/719** (707 before; +12 new).
- `scripts/Test-DeploySwap.ps1` — **29/29**: clean swap, corrupt staged payload (live copy untouched),
  failure mid-swap on a locked file (previous copy rolled back, runtime data intact), both
  interrupted-publish recovery shapes, and the drive-root guard.
- Tag preflight demonstrated against the **live** collision (`stable-v0.7.2-b25` @ `9e602ed` vs HEAD):
  failed in ~1 s at step [0], before tests, build, or deploy.
- `scripts/Publish-Stable.ps1` (exact-source, clean tree) — full lane green: staged swap → 21 artifacts
  re-hash clean → tag `stable-v0.7.3-b26` → final verification with no escape hatch → **RELEASE VERIFIED**.
- Post-deploy: no `.staging`/`.backup` leftovers; `PiPlayData` (settings + WebView2 session) preserved.

## Disposition

- Branch `fix/profile-selector-frame`; commits `4a9863a`, `94f47ae`, `68e8962`, `3fdad1a`.
- Tag `stable-v0.7.3-b26` created **locally**; nothing pushed.
- Manual-test executable: `E:\Dev_test_implemenations\PiPlay\PiPlay.exe` (v0.7.3 b26, RELEASE VERIFIED).

## Still open

- **Owner visual QA** of the deployed copy — no looks have been signed off; this pass verified behaviour
  and provenance, not appearance.
- `docs/AGENTS.md:49` signing claim vs. the (deliberately) unsigned pipeline.
- Branch/upstream release policy (this release is local-only on a fix branch).
