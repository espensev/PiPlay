# Review - Merged PRs #33, #34, #35 (docs consolidation + v0.13.0 release)

**Date:** 2026-08-22
**Surface:** Three most recently merged PRs on `main` — #33 (`f45b60d`), #34 (`18e7a4c`), #35 (`a9bbe37`, HEAD of `origin/main`, tag `stable-v0.13.0-b37`)
**Spec source:** PR bodies #33/#34/#35; `docs/PiPlay_Product_Engineering_Spec.md` (§12.4, §13, §22.1)
**Standards sources:** `CLAUDE.md`, `docs/AGENTS.md`, `docs/DECISIONS.md`, `docs/QA_Checklist.md`
**Verdict:** PASS WITH NOTES

Note on method: the working tree was dirty with uncommitted docs/scripts/tests edits during this review. No working-tree content was used as evidence; all file state was read via `git show <commit>:<path>` and `gh pr diff <N>`. CI verification ran in a clean temporary worktree pinned to `a9bbe37` (removed afterward).

## Findings

### High

None.

### Medium

None.

### Low

1. **[spec] PR #33 — `docs/SPEC_GAPS_AND_OWNERSHIP.md`** — Two durable caveats from the retired `.audit/deep-audit/piplay-runtime-2026-07-16/COVERAGE.md` were dropped without carryover: "Privacy clear — WebView2 internal disk scheduling is unmeasured" and "Logging — disk-failure behavior not profiled". Every other COVERAGE gap maps to a carried row (M-001 fault authority, M-004 mixed-DPI lifecycle, deferred-reach).
   Evidence: deleted COVERAGE.md table; `git grep -i disk f45b60d -- docs/` finds only the settings-persistence bullet.
   Impact: minor over-breadth of "canonical documents retain durable facts"; two unmeasured-state caveats become undiscoverable.
   Recommendation: optionally add one line each to the SPEC_GAPS maintenance section, or accept as historical audit state.

2. **[standards] PR #33 — `docs/SPEC_GAPS_AND_OWNERSHIP.md:9`** — Acceptance phrasing "Exact shell-authority guidance rejects YouTube, lookalike-host, and alternate-port sources" can read as if a check exists. `PlayerShellBridge.OnWebMessageReceived` at `f45b60d` (src/PiPlay/Services/PlayerShellBridge.cs:50-73) has no `e.Source` validation; the row itself correctly frames this as pre-revival Compact work, and the proposed `Uri.GetLeftPart(UriPartial.Authority)` mechanics are sound (accepts `https://piplay.local/player.html`; rejects lookalike hosts, alternate ports, youtube.com).
   Impact: minimal; a skimming reader could believe the enforcement exists.
   Recommendation: if touched again, phrase as "the specified authority check would reject …".

3. **[spec] PR #34 — `docs/SPEC_GAPS_AND_OWNERSHIP.md:53-55` (M-003/M-004)** — Recorded-metrics lists were compressed: M-003 drops "dispatcher delay" and abbreviates allocation/GC metric names; M-004 compresses attributed memory and child-process/latency detail. All confirm/reject thresholds survive value-for-value, so the decisions stay reproducible — but a future run following only the new text records less protocol detail.
   Evidence: `git show 18e7a4c^1:docs/SPEC_GAPS_AND_OWNERSHIP.md` vs merge-commit text.
   Impact: minor loss of measurement-protocol fidelity.
   Recommendation: restore explicit metric names if M-003/M-004 authorization runs need the old protocol depth.

4. **[regression] PR #35 — `src/PiPlay/MainWindow.xaml.cs:1467,1520` with `src/PiPlay/Services/YouTubeUrlHelper.cs:73`** — An unrelated Source timestamp can ride the first-item launch URL. `seconds = launchState?.CurrentTime` is read from the browse page's fallback `video` selector (non-null when a docked miniplayer/hover preview is on the playlist page); `BuildWatchUrl` appends `&t={s}s` for any `s > 0`, so the adopted first item starts mid-video at an unrelated video's offset. The same can occur via `target.StartSeconds` on the playlist-page URL. Pre-PR-35, the playlist-only branch of `BuildWatchUrl` ignored `seconds` entirely. No test pins the launch URL's `t=` for playlist-only launches.
   Impact: corner case; wrong start offset for the first playable item. No Q-1..Q-8 breach; return path handles timestamps correctly.
   Recommendation: zero the launch timestamp for `IsPlaylistOnly` targets (carry `seconds` only when the Source actually showed the adopted video), and add a resolver/URL test pinning the playlist-only launch URL shape.

## Per-PR verdicts

| PR | Title | Verdict | Claims |
|---|---|---|---|
| #33 | docs: consolidate canonical project guidance | PASS WITH NOTES | All code-anchored claims confirmed (comment-only src/ hunks verified mechanically; HwndSource test isolation confirmed structurally; 9 retired artifacts and zero dangling references confirmed); CI figures taken on PR-body assertion at review time, now independently reproduced (below) |
| #34 | docs: keep project guidance factual | PASS WITH NOTES | All 4 claims confirmed against code/tags; "17 Markdown surfaces" plausible (exactly 17 tracked .md at 18e7a4c); CI figures asserted in PR body, not re-run for this docs-only PR (see Verification) |
| #35 | release: v0.13.0 (build 37) — playlist-only popout first playable item | PASS WITH NOTES | Behaviors (a)-(i) confirmed with test evidence: charset-validated host-side id adoption; foreign-`list=` rejection; browse-page degrade; launched-without-video return keying (never same-video-seek); best-effort browse-page suppression (Q-1); post-await stale-playlist abandonment; 13 new test cases counted exactly; VERSION/BUILD_NUMBER/CHANGELOG/tag all agree; DeriveAccentSet doc summary reattached |

Unverifiable claims (external or transient, no repo artifact): the `playnext=1` probe, the adversarial multi-agent review pass in #35, and manual desk QA runs.

## Verification

- `pwsh -NoProfile -File .\scripts\Test-LocalCI.ps1` (clean worktree at `a9bbe37`, Node v24.19.0, SDK 10.0.400) — **pass**: `LOCAL CI: PASS`; `Passed: 1038, Failed: 0, Skipped: 0`; Release build `0 Warning(s), 0 Error(s)`. Exact match with PR #35's claimed 1038/1038. Worktree removed; main dirty tree untouched.
- Manual desk QA (QA_Checklist playlist rows against `E:\Dev_test_implemenations\PiPlay\PiPlay.exe`) — not run: requires deployed Stable and a human at the desk; recommend before treating v0.13.0 as verified beyond the automated gate.
- PR #33/#34 CI figures (1025/0/0) — not re-run per-commit; superseded by the green 1038/1038 run at HEAD which includes both.

## Coverage Notes

- All 29 files of #33, 3 files of #34, and 11 files of #35 were deep-reviewed at their merge commits (full lists in the per-PR subagent reports preserved in session logs). No changed file was skipped.
- Standards and grounding context sampled: `PlayerShellBridge.cs`, `WebViewEnvironmentService.cs`, `NavigationPolicy.cs`, `YouTubeUrlHelper.cs`, `ReturnPolicy.cs`, `PlaybackModePolicy.cs`, `PlayerWindow.xaml.cs` (nudge/sync), `ThemeCatalog.cs`, `AppSettings.cs`, `SettingsWindow.xaml`, spec §11/§12.4/§13/§22.1, `.github/workflows/spec-check.yml`, `scripts/Preflight-SpecGate.ps1`.

## Open Questions

- Should the two dropped audit caveats (privacy-clear disk scheduling; log disk-failure) re-enter SPEC_GAPS (Finding 1)?
- Does playlist-only launch want the timestamp-zeroing fix (Finding 4) in a 0.13.1, or folded into the next feature PR?

## Resolution — 2026-08-23

- Finding 4 was accepted for `0.13.1`: playlist-only first-item launch now clears URL-derived time and rejects unrelated Source/miniplayer time through `PopoutTargetResolver.ResolveLaunchSeconds`; regression coverage pins both the adopted-item URL and ordinary-video timestamp fallback.
- Findings 1–3 remain non-blocking documentation notes. They do not change runtime behavior or release provenance.
