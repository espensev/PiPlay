# Efficiency and customization hardening - design

## Goals

Remove proven avoidable work and lifecycle races from the current PiPlay snapshot while preserving
the accepted global-accent/profile-identity split. Make normal Popout timestamp polling single-flight
and navigation-scoped, avoid unnecessary Auto DOM probes, bound live accent-preview fan-out, cache the
invariant color disc, remove duplicate startup settings work, and make shutdown inert after close.

The pass also restores a visible pressed state for floor-lifted dark accents and ends with a fresh
diagnostic Stable copy at the repo-mandated manual-test path. It does not turn that dirty-tree copy into
release evidence.

## Requirements served

- Q-2 - return state must not be overwritten by stale/out-of-order Popout polling.
- Q-6 - navigation failure and close during asynchronous work recover without spurious follow-up work.
- REQ-UI-01 and spec section 20 - arbitrary valid accents stay visible and interactive.
- REQ-PROFILE-01 and spec section 17 - profile colors remain identity-only; the app accent remains global.
- Spec section 22.4 - avoid app-owned recurring CPU/IPC work beyond the browser/video workload.
- ADR-0007 - manual testing uses only the deployed Stable-channel path.

## Acceptance criteria

- Normal-mode DOM timestamp polling has at most one read in flight, stops at navigation start/close,
  starts only after successful navigation, and discards a result from an older navigation generation.
- Auto does not call the DOM bridge for non-watch URLs, missing IDs, active popout state, or the
  already-handled video; enabling Auto still detects the currently playing new watch video.
- Accent drag previews are coalesced to a bounded cadence, duplicate colors are skipped, and Popout
  preview changes update only Pin/Fade accent brushes rather than opacity, fade timing, or DWM state.
- The hue/saturation disc's frozen bitmap is reused for the same pixel-size/DPI key.
- Production startup reads settings once; one load parses JSON text once while preserving legacy
  no-theme migration and corrupt-file quarantine behavior.
- Closing the Popout suppresses post-await initialization/poll work. Closing the Source Window with an
  open Popout captures player state but skips source restoration and performs one outer settings save.
- Dark floor-lifted accents retain at least 3:1 contrast against `SurfaceHover` and a distinct pressed
  state, while stored global/profile hex values remain byte-for-byte exact.
- The full deterministic tests, release build gate, spec preflight, diff checks, diagnostic publish,
  and diagnostic Stable verifier pass.

## Settled decisions

1. Keep the 250 ms functional cadence but make it single-flight and navigation-generation-aware; this
   preserves timestamp freshness without queued WebView calls.
2. Put the Auto skip test in a pure policy method so tests prove the expensive DOM probe is unnecessary.
3. Coalesce host accent previews at 33 ms (maximum about 30 applies/second); the picker itself remains
   fully responsive and the final accepted/reverted value is applied synchronously.
4. Split `PlayerWindow.ApplyAccent` from full `ApplyAppearance`; accent preview must not touch unrelated
   behavior or native window state.
5. Cache frozen hue-disc bitmaps by physical pixel size and DPI. The disc is invariant with respect to
   selected color, so sharing it changes no visual semantics.
6. Preserve the minimum 3:1 primary correction. When a darker pressed candidate collapses near the
   primary state, use a small lighter fallback instead of raising the primary farther from the stored RGB.
7. Use the already-loaded boot settings only in the production constructor path; keep `new MainWindow()`
   for WPF tests and tooling.
8. Produce an `-AllowDirty` diagnostic Stable copy because unrelated untracked files and uncommitted pass
   changes must be preserved. Release pipeline hardening/signing/versioning is separate work.

## Non-goals / out of scope

- No change to stored accent/profile values, profile identity scope, theme catalog, or YouTube content.
- No compact-player re-enable, timer-cadence redesign, WebView replacement, or telemetry.
- No merge/rebase of the divergent local `main` stack.
- No release version/build bump, stable tag, signing claim, or release-evidence claim.
- No atomic deployment/signing/pipeline-lock overhaul in this application hardening pass.

## Testing approach

- Logic tests: Auto preflight and accent-state contrast/differentiation through the independent WCAG oracle.
- WPF tests: accent-only Popout updates preserve unrelated appearance state; bitmap cache reuses frozen output;
  navigation/poll coordinator and shutdown seams remain inert without live WebView2.
- Settings tests: legacy/no-theme migration, corrupt recovery, and normal round trips continue to pass.
- Full local gate: Debug test suite, Release build pipeline, spec preflight, and diff check.
- Manual-test artifact: diagnostic Stable publish + `Verify-StableDeploy.ps1 -AllowNonReleaseEvidence`.

## Changes by file

| File | Change |
|---|---|
| `src/PiPlay/App.xaml.cs` | Reuse boot-loaded settings for the production Source Window. |
| `src/PiPlay/MainWindow.xaml.cs` | Auto preflight, accent-preview coalescing, accent-only Popout fan-out, and shutdown state. |
| `src/PiPlay/PlayerWindow.xaml.cs` | Navigation-scoped single-flight polling, close guards, and accent-only application. |
| `src/PiPlay/Services/AutoPopoutPolicy.cs` | Pure `NeedsPlayerState` preflight. |
| `src/PiPlay/Services/SettingsService.cs` | Deserialize from one parsed JSON document. |
| `src/PiPlay/Controls/AccentColorPicker.xaml.cs` | Frozen hue-disc cache keyed by pixel size/DPI. |
| `src/PiPlay/Theme/ThemeColors.cs` | Distinct fallback pressed state for floor-lifted accents. |
| `tests/PiPlay.Tests/**` | Focused logic/WPF/settings regressions for the new seams. |
| `docs/CHANGELOG.md` | User-visible responsiveness/reliability summary. |
| `docs/reviews/review-2026-07-14-efficiency-and-customization.md` | Findings-first audit and measured customization impact. |

## Docs & changelog impact

Add an Unreleased entry for Popout polling reliability, lower customization-preview churn, and more
distinct dark-accent pressed feedback. Record diagnostic deployment separately from release evidence.

## Unresolved decisions

- Release-engineering follow-up: atomic Stable swap/rollback, early tag preflight, signing enforcement,
  unique publish handoff/locking, and upstream/branch policy.
