# Runtime audit closeout

- Audit: `piplay-runtime-2026-07-16` (`deep-audit/v1`).
- Source: baseline `8015ba4`; remediation `99f9834`; released `e16c0f3` on `main`.
- Runtime boundary: exact-source Stable `E:\Dev_test_implemenations\PiPlay\PiPlay.exe`, `v0.12.0 b35`, verified from a clean temporary checkout.
- Result: five Medium/Low findings were fixed, verified, landed, and released. No Critical/High finding remains.
- Detail: [STATE.md](STATE.md) and [MEASUREMENTS.md](MEASUREMENTS.md) retain source and measurement records; their 2026-07-19 M-005 retirement entries supersede all earlier M-005 operational-status, authorization, and next-mode text. [COVERAGE.md](COVERAGE.md) lists residual scope gaps.

## Fixed findings

| ID | Severity | Resolution |
|---|---|---|
| F-001 | Medium | Session-qualified pipe identity plus cancellation-aware retry backoff capped from 250 ms to 30 s; recovery is summarized once. |
| F-002 | Medium | Browser-data clear retains raw-task single-flight ownership through terminal completion after a foreground timeout. |
| F-003 | Medium correctness | Popout construction requires acknowledged Source suppression and rolls back on failure. |
| F-004 | Low | Equivalent DOM failures are suppressed per WebView/operation, with recovery summaries. |
| F-005 | Low | Accent preview replaces resources only when type, value, or identity changes. |

## Measurements

- M-002 yielded one mostly idle four-hour Standard block and no accepted Focused comparator.
- Standard mean attributed process CPU: `0.3713%`; renderer CPU: `0.1428%` on 32 logical processors.
- Post-first-hour private-byte slope: `-0.0709 MiB/min`.
- All 2,815 accepted rows held 10 processes and 3 renderers. Handles moved 8081→7916, threads 447→433, GDI 59→58, and USER 42→42.
- This is evidence against unbounded retention on the measured Standard path. It does not establish Focused incremental cost, callback duration, long tasks, JavaScript heap, DOM-node growth, or frame behavior.

## Constraints to preserve

- One app instance, one Source Window, and at most one Popout Player.
- Auto and Popout sync run at 250 ms with single-flight guards; Source suppression runs at 1 s while Popout owns playback; Focused fallback runs at 1 s only while active.
- Preserve both durable settings checkpoints around Source return.
- Preserve acknowledged Source suppression before Popout construction, generation guards, atomic settings persistence, the bounded 4,096-entry logging queue, and deterministic close/disposal order.
- Treat configured presentation as context, not proof that a Popout or Focused DOM surface was active.

## Unresolved measurements

- M-001: persistent DOM-failure host cost needs explicit fault-injection authority.
- M-003: realized WPF accent-preview cost is optional and unmeasured.
- M-004: Popout lifecycle settling lacks a safe unattended open/close driver.
- M-005: retired on verified machine `snd-desk` on 2026-07-19 because its scheduled PowerShell action opened a visible Windows Terminal at sign-in. Tasks `PiPlay-Passive-Runtime-Logger` and `PiPlay-Passive-Runtime-Watchdog` were removed. Do not reinstall a scheduled shell-based logger.
- M-005 scripts and logs remain under `C:\ProgramData\PiPlayPassiveRuntime\` as historical evidence only. Any future measurement needs fresh authorization and a non-interactive, non-console-host design.
