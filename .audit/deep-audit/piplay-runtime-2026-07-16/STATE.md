# Deep Audit State

Reading map: [REPORT.md](REPORT.md) is the findings-first synthesis; [STATE.md](STATE.md) is resume truth; [MEASUREMENTS.md](MEASUREMENTS.md) owns measurement contracts and results; [COVERAGE.md](COVERAGE.md) owns scope gaps.

Schema: deep-audit/v1  
Audit slug: `piplay-runtime-2026-07-16`  
Repository root: `D:\Development\DesktopApps\PiPlay`  
Audited source/runtime boundary: product baseline `8015ba4`; verified remediation `99f9834`; current/released `HEAD` `e16c0f3`. Deployed Stable at `E:\Dev_test_implemenations\PiPlay` is exact-source `e16c0f3` (`v0.12.0 b35`, release evidence) and passed the mandatory verifier from clean temporary checkout `D:\tmp\piplay-audit-e16c0f3-20260716`. Runtime samples are accepted only when their executable and build identity match that Stable boundary.
Baseline revision: `8015ba4be67a0e2cf16d6cc9652efb9ae30e7660`  
Current revision: `e16c0f351a1e8f76a6ab396aa1eba079a521bf54`
Branch/worktree: `main`, aligned with `origin/main`, tagged `stable-v0.12.0-b35`; primary worktree
Authority files read: shared `C:\Users\Sev\OneDrive\common\common_dev\AGENTS.md`, root `CLAUDE.md`, `docs/AGENTS.md`, deep-audit skill and mode/state contracts
Dirty-state fingerprint: pre-existing release-stamp changes in `VERSION` (`0.12.0` -> `0.12.1`) and `BUILD_NUMBER` (`35` -> `36`) predate this closeout; audit-state files are intentionally modified by the audit. No product-code file is changed.
Scope: startup, Source and Popout steady state, WebView/DOM work, timers and scheduling, local I/O, native-window lifecycle, failure/retry, cancellation, return, shutdown, and authorized passive runtime measurement
Exclusions: build/publish/CI efficiency, security assessment, UI style, YouTube/network internals, manual QA, production load, fault injection, deployment, and unrelated product changes
Workload assumptions: one app instance, one Source Window, at most one Popout Player, ordinary YouTube watch navigation, optional Auto/Focused/fade/opacity customization, and repeated open/close as a scalability boundary  
Environment: Windows 11, WPF `net10.0-windows`, WebView2 Evergreen, 32 logical processors
Audit started: 2026-07-16 Europe/Berlin  
Last updated: 2026-07-19 Europe/Berlin
Product-code writes: not allowed in this closeout; the previously authorized remediation is landed and released
Audit-state writes: allowed  
Runtime measurements: M-002 unattended A/B profiling and the M-005 low-rate passive follow-up explicitly authorized; no fault injection or interactive lifecycle driver authorized

## Current priority

- IDs: M-002 is closed as partial/inconclusive; M-005 is running. M-001, M-003, and M-004 remain optional planned measurements. F-001 through F-005 are landed, released, and verified.
- Reason: M-002 produced one valid four-hour Standard block but failed before any accepted Focused sample. The Standard run is useful counter-evidence to unbounded healthy-session growth, but it cannot determine Focused incremental cost. M-005 now observes long-term process trends without forcing mode or relaunches; its configured-presentation field is context only, not proof that a Popout was active.
- Current mode: `profile M-005` — scheduled logger plus independent recovery watchdog, securely installed and last live-verified healthy on 2026-07-18 Europe/Berlin.
- Exact next mode: `profile M-005` analysis after at least three long sessions with at least two post-settling hours each. Treat threshold crossings as anomaly signals requiring `deepen D-004` or `deepen D-008` and a targeted measurement; quiet passive data can narrow general process-retention concern but cannot reject Focused DOM cost.

## Revision drift

- Baseline: `8015ba4`
- Current: `e16c0f3`
- Relevant changed paths: remediation landed at `99f9834`; audit state landed at `19bcfe8`; `e16c0f3` adds release stamps/changelog. Current product code is unchanged.
- Records requiring revalidation: none for source behavior. M-005 records version/build/source identity for each newly detected Stable session so a later deployment is not silently mixed with `e16c0f3`.
- Prior evidence drift: landing/release and deployed-runtime provenance changed after the static audit; the static claims and V-001 through V-006 did not drift.

## Progress

- Runtime roots mapped: startup/single-instance, Source browser, Auto, Popout, Focused DOM surface, appearance/native window, persistence/logging, return, privacy, and shutdown
- Open discoveries: none; D-001 through D-008 are classified
- Completed traces: T-001 through T-008
- Ranked findings: F-001 through F-005; all landed, released, and verified
- Completed verifications: V-001 through V-006
- Measurements: M-002 partial/inconclusive and closed; M-005 running; M-001/M-003/M-004 planned
- Rejected hypotheses: R-001 through R-003
- Known coverage gaps: Focused-vs-Standard process cost under matched active playback, callback-family duration, long tasks/frame loss, JS heap/node counts, repeated open/close settling, persistent WebView failure cost, and realized WPF accent-preview frame cost

## Current provenance

- Git truth: `main`/`origin/main` at `e16c0f3`; current non-audit dirt is limited to the pre-existing `VERSION` and `BUILD_NUMBER` release-stamp changes.
- Stable verifier: all 21 artifacts re-hashed clean; clean checkout `D:\tmp\piplay-audit-e16c0f3-20260716` produced `VERDICT: RELEASE VERIFIED` for deployed `v0.12.0 b35` and tag `stable-v0.12.0-b35`.
- Diagnostic PID 17740 from `bin\publish\latest` was excluded before profiling. M-002 accepted only exact Stable PID 64176; no diagnostic sample is included.
- M-002 restored the original portable settings hash and Focused state. Newer live settings were preserved during closeout; exact evidence remains in [MEASUREMENTS.md](MEASUREMENTS.md).

## Measurement status

| ID | Status | Durable conclusion | Next action |
|---|---|---|---|
| M-002 | Closed, partial/inconclusive | One mostly-idle four-hour Standard block plateaued after settling. No accepted Focused comparator exists; the harness failed before the later reboots. | Do not rerun the forced campaign. |
| M-005 | Retired 2026-07-19 | The scheduled logger opened a visible Windows Terminal at sign-in. Both the logger and watchdog tasks were removed; retained files are historical evidence only. | Do not reinstall a scheduled shell-based logger. |

M-005 retained surfaces:

- Removed tasks: `PiPlay-Passive-Runtime-Logger` and `PiPlay-Passive-Runtime-Watchdog`. No PiPlay passive-runtime task or logger process remained after removal on verified machine `snd-desk`.
- Preserved scripts/status: `C:\ProgramData\PiPlayPassiveRuntime\PiPlayPassiveLogger.ps1`, `Ensure-PiPlayPassiveLogger.ps1`, and `Get-PiPlayPassiveLoggerStatus.ps1`. They are not scheduled and must not be treated as live instrumentation.
- Preserved output: `C:\ProgramData\PiPlayPassiveRuntime\logs`. Existing files remain available for historical inspection; no further collection is expected.
- Integrity/privacy: only Sev, SYSTEM, and Administrators can modify the protected installation; Users have read/execute only. No execution-policy bypass, URLs, settings payloads, command lines, or network telemetry.
- Attribution limits: samples split on boot, root process, configured presentation, and Stable identity. CPU is a sampled-survivor lower bound. LibreHardwareMonitor is preserved as independent timestamp context only.
- Retirement reason: an interactive scheduled PowerShell action created a visible blank Windows Terminal despite `-WindowStyle Hidden`; the exact Terminal process was closed and machine-wide enabled interactive shell-task/startup checks were brought to zero.

## Remediation verification

- Focused policy/WPF gate: 26/26 passed.
- Full Debug solution: 985/985 passed.
- Canonical local CI: PASS; Release build completed with 0 warnings and 0 errors.
- Design-spec preflight and independent production/test reviews: PASS with no actionable findings.
- These statements describe source/remediation verification. Subsequent Stable profiling evidence is recorded separately under M-002/M-005 and is not retroactively used as source-build proof.

## Last handoff

- Mode and target: `profile M-002` completion analysis, then `profile M-005` setup.
- Scope completed: proved M-002 inactive; preserved current settings; reconstructed failure and reboot timing; analyzed the valid Standard block and LibreHardwareMonitor correlation; installed the low-rate restart-persistent follow-up.
- Evidence inspected: campaign manifest/progress/result/error, raw Standard CSV and Focused sample-zero artifact, PiPlay initialization/cleanup logs, LibreHardwareMonitor cursors/CSV continuity, System restart events, current process/task inventory, settings hashes, task definition, and logger self-test/status.
- Discoveries opened/closed: none.
- Traces completed: no new trace; T-004 gained runtime context.
- Findings created/changed: none; no threshold supports promotion.
- Verifications completed: none; measurement records are not verification targets.
- Measurements opened/completed: M-002 closed partial/inconclusive; M-005 opened and running.
- Hypotheses rejected: none. Standard unbounded healthy-session retention is narrowed by counter-evidence, not globally rejected.
- Coverage gaps: no accepted Focused comparator; natural workload is uncontrolled; callback/heap/node/frame attribution remains unavailable; M-004 still lacks a safe unattended lifecycle driver.
- Remaining uncertainty: whether Focused materially changes process/renderer CPU under an actually active Popout, plus whether natural long-running PiPlay sessions show post-settling resource drift.
- Highest-priority next item: leave M-005 passive until its long-session acceptance threshold is met, then inspect trends without stitching across root PID, boot, or executable identity. Use configured presentation only as a contextual partition.
- Exact next mode: `profile M-005`; on an anomaly, `deepen D-004`/`deepen D-008` and design a targeted measurement; otherwise refresh `report` with general-retention narrowing only.
