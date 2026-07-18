# Rejected Hypotheses

Schema: deep-audit/v1  
Audit slug: `piplay-runtime-2026-07-16`  
Repository/current revision: baseline `8015ba4`; verified remediation `99f9834`; current/released `e16c0f3` on `main`
Source/runtime boundary and authority: see `STATE.md`; M-002/M-005 do not change R-001..R-003
Dirty-state fingerprint/scope/exclusions/environment: see `STATE.md`  
Last updated: 2026-07-18 Europe/Berlin

### R-001 — The second Popout-return settings save is duplicate work

- Discovery/trace: D-005 / T-005.
- Rejection basis: spec section 14 explicitly requires durable settings saves before and after fallible source-return scripting so placement survives a script failure. Pin/Auto settings may also change across the await, and the second save is a retry opportunity because persistence failures are non-fatal.
- Evidence depth: 4; caller, return path, persistence implementation, reentrancy, shutdown, and governing spec inspected.
- Disposition: preserve both checkpoints. Reconsider implementation technique only if a slow-storage measurement proves material UI latency while keeping both durability boundaries.

### R-002 — Shared WebView environment creation is a current concurrent hot path

- Discovery/trace: D-006 / T-006.
- Rejection basis: the service is locally non-single-flight, but current wiring has one cold Source caller; Popout consumes the completed environment after `_browserReady`; Retry follows terminal failure and hides its own surface synchronously. No credible current parallel caller exists.
- Evidence depth: 4; all service callers and UI reachability inspected.
- Disposition: no current product change. Reopen if multiple Source windows/callers or independently reentrant Retry are introduced.

### R-003 — Persistent DOM failure amplifies WebView IPC above healthy cadence

- Discovery/trace: D-001 / T-001.
- Rejection basis: the failure path performs the same bounded 1 Hz + 4 Hz calls as healthy operation, with single-flight guards and one-player cardinality. Failure amplifies exception/log work, not call count.
- Disposition: F-004 addresses deterministic log amplification; M-001 measures whether the fixed failed-call cadence is materially expensive before any polling backoff is considered.
