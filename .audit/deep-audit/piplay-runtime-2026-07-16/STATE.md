# Deep Audit State

Schema: deep-audit/v1  
Audit slug: `piplay-runtime-2026-07-16`  
Repository root: `D:\Development\DesktopApps\PiPlay`  
Audited source/runtime boundary: product runtime baseline at source `8015ba4`; focused remediation is authorized in the working tree; deployed Stable at `E:\Dev_test_implemenations\PiPlay` remains two commits behind at `d11eac5` and is not valid for current-source testing  
Baseline revision: `8015ba4be67a0e2cf16d6cc9652efb9ae30e7660`  
Current revision: `8015ba4be67a0e2cf16d6cc9652efb9ae30e7660`  
Branch/worktree: `main`, two commits ahead of `origin/main`; primary worktree  
Authority files read: shared `C:\Users\Sev\OneDrive\common\common_dev\AGENTS.md`, root `CLAUDE.md`, `docs/AGENTS.md`, `docs/Feature_Workflow.md`, deep-audit skill and mode/state contracts  
Dirty-state fingerprint: pre-existing `?? docs/reviews/review-2026-07-16-parked-tree-landing-readiness.md`; this pass adds `.audit/deep-audit/piplay-runtime-2026-07-16/` plus the dated runtime-failure design/plan and subsequent focused remediation paths  
Scope: startup, Source and Popout steady state, WebView/DOM work, timers and scheduling, local I/O, native-window lifecycle, failure/retry, cancellation, return, and shutdown  
Exclusions: build/publish/CI efficiency, security assessment, UI style, YouTube/network internals, manual QA, production load, live fault injection/profiling, deployment, and unrelated product changes  
Workload assumptions: one app instance, one Source Window, at most one Popout Player, ordinary YouTube watch navigation, optional Auto/Focused/fade/opacity customization, and repeated open/close as a scalability boundary  
Environment: Windows 11, WPF `net10.0-windows`, WebView2 Evergreen; no PiPlay process observed during PASS 0  
Audit started: 2026-07-16 Europe/Berlin  
Last updated: 2026-07-16 Europe/Berlin  
Product-code writes: allowed for supported findings by the user's "EVALUET AND ADRESS" request  
Audit-state writes: allowed  
Runtime measurements: plan-only except passive inspection and existing safe automated tests

## Current priority

- IDs: none; F-001 through F-005 are fixed and verified in the working tree.
- Reason: static audit/remediation scope is complete and `REPORT.md` (Final, completion gate met) is persisted. Remaining M-001 through M-004 require a current-source Stable deployment and explicit live measurement authority.
- Current mode: `report` — complete and persisted 2026-07-16 (resume pass).
- Exact next mode: `profile M-004`/`profile M-002` (then M-001/M-003) only after the recorded blockers are cleared; otherwise owner review/ship is the next external decision.

## Revision drift

- Baseline: `8015ba4`
- Current: `8015ba4`
- Relevant changed paths: `App.xaml.cs`, `MainWindow.xaml.cs`, DOM/privacy/pipe/policy/theme services, `SettingsWindow.xaml.cs`, focused tests, changelog, dated design/plan/worklog, and this audit state
- Records requiring revalidation: none for the current working tree; revalidate V-001 through V-006 after any overlapping edit
- Prior evidence drift: the 2026-07-14 efficiency review predates the Focused surface and Source-return work; the 2026-07-15 technical audit matches the Focused remediation but predates commits `bf9f9ed` and `8015ba4`.

## Progress

- Runtime roots mapped: startup/single-instance, Source browser, Auto, Popout, Focused DOM surface, appearance/native window, persistence/logging, return, privacy, shutdown
- Open discoveries: none; D-001 through D-008 are classified
- Completed traces: T-001 through T-008
- Ranked findings: F-001 through F-005; all fixed/verified in the working tree
- Completed verifications: V-001 through V-006
- Open measurements: M-001 through M-004; execution not authorized
- Rejected hypotheses: R-001 through R-003
- Known coverage gaps: live Focused soak, repeated open/close settling, persistent WebView failure cost, WebView2 internal clear scheduling, and realized WPF accent-preview frame cost

## PASS 0 evidence

- `git status`: `main...origin/main [ahead 2]` plus one pre-existing untracked review.
- Stable verifier: all 21 deployed artifacts re-hashed clean, but the deployed `v0.11.0 b34`/`d11eac5` copy is two commits behind current source; verifier verdict was FAIL for current testing.
- Automated baseline: `dotnet test PiPlay.sln --configuration Debug --nologo` passed 959/959 on revision `8015ba4`.
- No product runtime was launched. Existing unrelated `msedgewebview2` processes were not attributed to PiPlay.

## Remediation verification

- Focused policy/WPF gate: 26/26 passed.
- Full Debug solution: 985/985 passed, up from the 959-test PASS-0 baseline.
- Canonical local CI: PASS; Release build completed with 0 warnings and 0 errors.
- Design-spec preflight: PASS.
- Independent production and test/audit re-reviews: PASS with no actionable findings.
- No live app, deployed Stable copy, fault injection, profiling workload, commit, push, or release was performed.

## Last handoff

- Mode and target: `resume` follow-up pass (2026-07-16, third pass). Revalidated PASS-0 truth (revision `8015ba4` unchanged, dirty fingerprint exact match, zero drift since the report), re-ran the canonical gate fresh — `Test-LocalCI.ps1` → **LOCAL CI: PASS**, 985/985 tests (0 failed, 0 skipped), Release build 0 warnings/0 errors — walked the resume selection order (items 1–6 empty or authority-blocked), and assembled the landing plan for the owner: commit remediation + dated docs (+ `.audit/` disposition), stamp v0.12.0/b35 (minor — unreleased `bf9f9ed` feature rides along per version discipline), `Publish-Stable.ps1`, separate push yes, then `profile M-004`/`profile M-002` under explicit authority.
- Scope completed: state revalidation, fresh gate evidence, and owner-decision surfacing; no new discoveries, traces, findings, verifications, measurements, or rejections were warranted, and none were invented.
- Evidence inspected: `STATE.md`/`REPORT.md`/`MEASUREMENTS.md`, current git identity/dirty state/diff stat, drafted changelog entry, worklog disposition, `VERSION`/`BUILD_NUMBER`, `.gitignore`, and a fresh canonical-gate run on the exact working tree.
- Remaining uncertainty: live operational cost and resource settling are unmeasured; no current-source deployed runtime exists (M-001 through M-004 remain blocked on deployment + explicit authority).
- Highest-priority next item: owner review/ship decision on the working-tree remediation (push to protected `main` needs a separate explicit yes); thereafter `profile M-004`/`profile M-002` under explicit authority.
- Exact next mode: awaiting owner decision; on "ship" → landing sequence, then `profile M-004`.
