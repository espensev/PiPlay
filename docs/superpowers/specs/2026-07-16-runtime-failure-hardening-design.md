# Runtime failure hardening — design

## Goals

Close four statically supported runtime-efficiency and lifecycle gaps found by the 2026-07-16 deep audit:

- do not open a Popout unless Source playback suppression was acknowledged;
- retain ownership of a timed-out browser-data clear until the WebView2 task actually completes;
- make single-instance pipe identity match the session boundary and bound persistent retry work; and
- avoid replacing unchanged accent resources during live preview.

Repeated WebView script failures will log once per consecutive failure episode and summarize recovery. Healthy polling, the 1 Hz duplicate-audio safety guard, the 30-second privacy status wait, the single-player model, and all successful-path visuals remain unchanged.

## Requirements served

- Q-1: Source and Popout must not remain audibly active together.
- Q-3 and Q-6: YouTube DOM work stays centralized, best-effort, observable, and non-fatal.
- REQ-APP-01 and spec sections 9.5/12.3: one PiPlay primary per Windows logon session and channel owns its shared WebView2 profile.
- REQ-PRIVACY-02 and spec section 19: browser-data clearing remains explicit, truthful, bounded in the UI, and non-overlapping.
- Spec section 22.4: no unbounded retry/log amplification and no avoidable steady preview invalidation.

## Acceptance criteria

- Source suppression reports a distinct false outcome for no video, a non-true script result, or WebView execution failure. Popout construction does not begin after that outcome, and the existing rollback path restores captured playback state.
- Consecutive WebView query/command exceptions log the first failure only; a later successful call resets the episode and reports how many repeated failures were suppressed. Polling cadence is unchanged.
- A browser-data clear that exceeds the 30-second foreground wait remains the active clear until its actual task succeeds or faults. Another clear cannot start during that lifetime, its late terminal state is observed, and Settings explains the disabled action accurately.
- If Settings is already open when that late terminal state arrives, its Clear action refreshes in place; a timeout-boundary completion is handled as completion rather than misreported as background work.
- Ordinary Source/Popout commands may resume after the foreground timeout; only another destructive Clear stays gated. This preserves the existing anti-wedge behavior while preventing overlapping profile clears.
- Pipe names are stable within a channel/session and differ across sessions. Non-cancellation server failures wait 250 ms with exponential growth capped at 30 seconds, reset after a successful handoff, and do not emit one log entry per retry.
- Reapplying an identical accent set preserves every existing brush/color object. An intensity-only change replaces only the pairs whose derived colors changed; every replacement brush remains frozen.
- Targeted regression tests and the canonical local CI gate pass. No deployed Stable copy or live WebView workload is changed or launched by this pass.

## Settled decisions

1. Preserve the 1 Hz Source suppression guard. It is a Q-1 safety mechanism, not an efficiency defect.
2. Gate logs, not healthy DOM polling. Static evidence proves repetitive error allocation/I/O, but not material IPC/CPU cost; recovery responsiveness remains more important than speculative backoff.
3. Treat the initial suppression result as a launch precondition. A swallowed transport error cannot count as successful transfer of playback ownership.
4. Keep a distinct failed-suppression video latch for Auto. A failed transfer is not recorded as successfully handled, while the 250 ms detector also cannot reopen the same failure prompt until Source moves or a manual retry succeeds.
5. Keep foreground privacy timeout state separate from background operation ownership. Settings and normal app commands recover after 30 seconds, but the destructive operation stays single-flight until terminal completion.
6. Session-qualify the pipe as well as relying on the `Local\` mutex. Cross-session primaries cannot activate one another and must not contend for one machine-wide pipe name.
7. Use cancellation-aware exponential pipe backoff with first-failure and recovery logging. A bounded retry preserves second-launch recovery without a hot exception loop.
8. Compare both the brush color and companion `Color` token before replacement. Wrong-typed or missing resources are repaired; equal, correctly typed resources retain identity.
9. Leave the two Popout-return settings saves intact. Spec section 14 explicitly requires both durable checkpoints.
10. Do not harden shared-environment creation in this pass. The current runtime has no credible concurrent caller; revisit if ownership expands.

## Non-goals / out of scope

- Live WebView2 fault injection, ETW/DevTools profiling, current-source Stable publication, or manual QA.
- Changing Focused DOM observer/listener design without the planned Standard-vs-Focused measurement.
- Claiming or fixing a Popout lifecycle leak without the planned 50-cycle and soak evidence.
- Removing either pre-return or post-return durable settings save.
- Adding multiple Source windows, multiple Popouts, or cross-session activation.
- Adding a per-client named-pipe payload limit/timeout; that residual is tracked separately.

## Testing approach

- Logic tests inject script executors and prove suppression outcomes plus failure-gate reset/count behavior.
- Logic tests drive the browser-clear coordinator with `TaskCompletionSource` instances to prove one in-flight task, late success/fault release, and retry after completion without a real 30-second delay.
- Logic tests inject pipe attempts and delays to prove session identity, monotonic/capped backoff, reset, and cancellation.
- WPF runtime tests use isolated `ResourceDictionary` instances to assert identity preservation, changed-pair counts, values, and frozen brushes. Settings runtime tests assert the background-clear tooltip.
- Run focused filters first, then `scripts/Test-LocalCI.ps1`. Live runtime measurements remain planned, not executed.

## Changes by file

| File | Change |
|---|---|
| `src/PiPlay/Services/YouTubeDomBridge.cs` | Return suppression acknowledgement and coalesce consecutive host-execution errors. |
| `src/PiPlay/Services/ConsecutiveFailureGate.cs` | Small thread-safe first-failure/recovery-count policy. |
| `src/PiPlay/Services/PopoutLaunchPolicy.cs` | Tested fail-closed suppression precondition before player construction. |
| `src/PiPlay/MainWindow.xaml.cs` | Require suppression acknowledgement and retain/observe timed-out clear ownership. |
| `src/PiPlay/Services/BrowserDataClearCoordinator.cs` | Single-flight lifetime for the underlying clear task. |
| `src/PiPlay/Services/PrivacyService.cs` | Truthful already-running copy. |
| `src/PiPlay/SettingsWindow.xaml.cs` | Accept the concrete reason Clear is unavailable. |
| `src/PiPlay/App.xaml.cs` | Session-qualified pipe identity and bounded retry loop. |
| `src/PiPlay/Services/SingleInstancePipePolicy.cs` | Testable pipe naming/backoff/loop policy. |
| `src/PiPlay/Theme/ThemeResourceApplier.cs` | Skip equal, correctly typed accent pair writes. |
| `tests/PiPlay.Tests/*` | Focused logic and WPF regression coverage. |
| `docs/CHANGELOG.md` | User-visible reliability/performance fixes. |
| `.audit/deep-audit/piplay-runtime-2026-07-16/*` | Stable traces, findings, rejections, measurements, and verification evidence. |

## Docs & changelog impact

Add concise Unreleased fixes for fail-closed Popout launch, non-overlapping browser clears, resilient single-instance handoff, and lower live-preview invalidation. The product spec and ADRs do not change; this work enforces their existing boundaries.

## Unresolved decisions

- The material CPU cost of persistent DOM failures, Focused presentation, and realized accent preview remains measurement-bound.
- Repeated Popout resource settling remains measurement-bound and needs a current-source Stable deployment plus explicit profiling authority.
