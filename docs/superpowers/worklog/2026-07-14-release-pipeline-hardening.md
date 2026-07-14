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

## Bugs the tests caught that reading the code did not

**1. Half-moved directories (harness case C3, found on the harness's first run).** `Move-Item` on a
directory whose child is locked **half-moves it and still throws**. The first rollback replayed only the
moves it had *recorded as successful*, so the half-moved directory was skipped — and then the backup
holding the only copy of those children was deleted. Rollback now restores from what the backup
**actually holds** (`Restore-DeployBackup`), merging into any directory that still exists.

**2. The same shape again, on the other side (harness case H).** An adversarial review found the rollback
still deleted the backup *unconditionally*. Every rollback step is necessarily best-effort, so a second
lock (an AV scan of freshly written binaries is the realistic one) can defeat both the removal of a
moved-in file and the restore over it — and the backup was then destroyed anyway while the publish
reported "the previous copy was rolled back". The backup is now deleted **only once it is empty**;
otherwise it is preserved and its path reported.

**3. The publish lock never let go (`Test-PublishLock.ps1` case 3).** A mutex belongs to the thread that
took it, and PowerShell's console host reuses its prompt thread — so a finished publish left the mutex
*owned*, and the next publish from any other process was told "another publish is already running" when
none was, clearing only when the GC happened to finalize the handle. Safety was never at risk; liveness
was, nondeterministically, in the only sanctioned promote path. Now released from a `finally`.

**4. Two logging races.** The writer thread re-read shared state instead of capturing the queue it was
started for, so a fast start-then-exit could null it out from under a not-yet-scheduled writer, which
then returned without draining — losing every queued entry silently. And a transient write failure
discarded the whole coalesced batch *and* the overflow accounting meant to make the loss visible.

The adversarial pass raised 12 candidate findings; 4 survived refutation, all 4 were real, and all 4 were
bugs in this session's own work.

## Verification

- `dotnet test PiPlay.sln --configuration Debug` — **721/721** (707 before; +14 new).
- `scripts/Test-DeploySwap.ps1` — **35/35**: clean swap, corrupt staged payload (live copy untouched),
  failure mid-swap on a locked file, a rollback that *cannot* restore (backup preserved, not deleted),
  both interrupted-publish recovery shapes, and the drive-root guard.
- `scripts/Test-PublishLock.ps1` — **8/8**: the lock really blocks another process, and releasing frees
  it immediately rather than at the GC's convenience.
- Tag preflight demonstrated against the **live** collision (`stable-v0.7.2-b25` @ `9e602ed` vs HEAD):
  failed in ~1 s at step [0], before tests, build, or deploy.
- `scripts/Publish-Stable.ps1` (exact-source, clean tree) — full lane green: staged swap → 21 artifacts
  re-hash clean → tag `stable-v0.7.3-b27` → final verification with no escape hatch → **RELEASE VERIFIED**.
- Post-deploy: no `.staging`/`.backup` leftovers; `PiPlayData` (settings + WebView2 session) preserved;
  the real publish lock confirmed FREE from a separate process.

## Disposition

- Branch `fix/profile-selector-frame`; commits `4a9863a`, `94f47ae`, `68e8962`, `3fdad1a`, `8d6becd`,
  `9a52395`.
- Tag `stable-v0.7.3-b27` created **locally**; nothing pushed.
- `stable-v0.7.3-b26` also exists locally. Its provenance is sound, but it is the build containing the
  three defects above, and it is superseded — safe to delete (`git tag -d stable-v0.7.3-b26`).
- Manual-test executable: `E:\Dev_test_implemenations\PiPlay\PiPlay.exe` (v0.7.3 b27, RELEASE VERIFIED).

## Still open

- **Owner visual QA** of the deployed copy — no looks have been signed off; this pass verified behaviour
  and provenance, not appearance.
- `docs/AGENTS.md:49` signing claim vs. the (deliberately) unsigned pipeline.
- Branch/upstream release policy (this release is local-only on a fix branch).
