# Runtime audit coverage

- Audit: `piplay-runtime-2026-07-16` (`deep-audit/v1`).
- Source: baseline `8015ba4`; remediation `99f9834`; released Stable `e16c0f3` (`v0.12.0 b35`).
- Last updated: 2026-07-19 Europe/Berlin.
- Scope: Windows 11, WPF `net10.0-windows`, WebView2 Evergreen, one Source Window, and at most one Popout Player.
- Exact environment, authority, and dirty-state fingerprint are in [STATE.md](STATE.md).

| Runtime area | Depth | Result | Remaining gap |
|---|---:|---|---|
| Startup and single-instance handoff | 4 | F-001 fixed and verified | Live failure not induced |
| Shared WebView environment | 4 | Current ownership is bounded | Reopen if ownership expands |
| Source initialization/navigation | 4 | Mapped and guarded | Optional live fault test |
| Auto detector | 3 | 250 ms, cheap preflight, single-flight | Live cadence cost unmeasured |
| Popout launch and Source suppression | 4 | F-003/F-004 fixed and verified | M-001 requires fault authority |
| Popout state sync | 4 | 250 ms and single-flight | Live failure cost unmeasured |
| Compact shell | 2 | Feature-gated/dormant | Reachability not fully traced |
| Focused DOM surface | 4 | Static lifecycle covered; Standard process baseline measured | No active Focused comparator or Focused callback/heap/node/frame attribution |
| Appearance preview | 4 | F-005 fixed and verified | M-003 optional |
| Native window state | 4 | UI-thread ownership and cleanup traced | Repeated mixed-DPI lifecycle unmeasured |
| Source return and persistence | 4 | Required checkpoints verified | Live failure injection absent |
| Privacy clear | 4 | F-002 fixed and verified | WebView2 internal disk scheduling is unmeasured |
| Logging and shutdown | 4 | Queue, rotation, cancellation, and drain bounded | Disk-failure behavior not profiled |
| Long-session process trends | 3 | One Standard block plateaued | M-005 retired; no passive follow-up |

No audit evidence supports a super-linear reachable path or a confirmed leak. Remaining runtime claims require the specific measurements listed in [REPORT.md](REPORT.md), not inference from configuration or host telemetry.
