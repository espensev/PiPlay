# PiPlay b25 — spec-conformance review of the unreleased bring-back stack

**Date:** 2026-06-26
**Scope:** the local unpushed commits `origin/main (5bff7d8)..HEAD (2b553ee)` — i.e. the
`feat(popout): bring video back` (`fbe77c9`) + `fix(popout): stabilize source return playback`
(`2b553ee`) stack and the docs that moved with them. Lens is **does the code conform to the spec**,
which is the interesting question here because the spec was rewritten *in the same commits* as the
code (so "code matches spec" is partly self-fulfilling and the real check is whether the spec changes
were legitimate and complete).

**Complements, does not restate:** `2026-06-26-piplay-b25-address-pass-review.md` (code-residual
pass; #1 muted-source, #2 §13.4 pseudocode, #4 replay-loop all verified FIXED at HEAD).

## Method / evidence

- Read the full code diff (`ReturnPolicy`, `PlayerReturnState`, `YouTubeDomBridge`, `PlayerWindow`,
  `MainWindow`) against the rewritten spec sections (`REQ-RETURN-01`, §12.5, §13, §14, §22.1, §25.1).
- `dotnet test PiPlay.sln -c Debug` → **687/687, exit 0** (re-run this session, HEAD).

## Verdict

**Conformant and internally consistent — but not yet RC.** Every rewritten spec point maps to code I
verified by hand. The green gate is **headless**; the WebView2-dependent core (source suppression,
mute-on-return, navigate-replay) is unproven by tests, so the spec's own §22 runtime rows remain the
real gate. Findings below are minor (ID hygiene, test debt) plus one decision to *confirm awareness*
of, not re-open.

## Code ⇄ spec map (verified first-hand)

| Spec point (rewritten) | Code | OK |
|---|---|---|
| `REQ-RETURN-01` now "follow popout live paused state when known; source-was-playing is fallback" | `ReturnPolicy.Decide(... returnedPaused)` → `returnedPaused ?? sourceWasPlaying` | ✓ |
| "must not auto-nudge if source paused at launch; play must come from user action in popout" | `PlayerWindow(nudgePlayOnInitialPause: _sourceWasPlayingAtPopout)`, gated in `SyncTimer_Tick` | ✓ |
| §14 "different video → navigate there and replay captured state after element ready" | `_pendingReturnReplay` + `Core_NavigationCompleted` + `ReplayPendingReturnStateAsync` (12×250 ms) | ✓ |
| §14 "settings saved before *and* after source-return scripting" | `Player_OnClosed` saves pre- and post-`ApplyReturnActionAsync` | ✓ |
| §12.5 adds `SeekAndPauseAsync`, `ApplyPlaybackSettingsAsync` | both present in `YouTubeDomBridge` | ✓ |
| §13 placeholder action `[Show popout]` → `[Bring video back]` | `PlaceholderBringBackButton_Click` → `BringVideoBackAsync`; PopOut button label flips | ✓ |
| Q-1 source suppression = mute+pause, re-asserted | `SuppressPlaybackAsync` (`v.muted=true; v.pause()`) + 1 Hz `StartSourceSuppressionGuard` | ✓ |

## Findings

### 1. `REQ-RETURN-07` was an orphan ID — minor (ID hygiene) — ✅ FIXED in this commit

The launch-from-paused **behavior** was specced (REQ-RETURN-01 prose: "must not auto-nudge…"), but the
**ID** `REQ-RETURN-07` was not: it was *proposed* in `…-spec-tightening-arbitration.md` §6.7, told to be
"decided" in the meta-review, and cited in the address-pass review (line 46) as the traceability anchor
for the nudge gate — yet `PiPlay_Product_Engineering_Spec.md` defined no REQ-RETURN-07.
**Disposition:** defined REQ-RETURN-07 — tagged the §14 launch-from-paused bullet and added a §25.1
matrix row (the principled companion to REQ-RETURN-01/Option A). The arbitration/meta/address-pass
references now resolve to a real requirement. Wording notes that PiPlay passes intent for the session
(not a one-shot nudge) and does not force-pause a self-autoplaying watch page (runtime-QA residual).

### 2. New return surface is untested scaffolding — minor (test debt, already tracked as Task 8)

Confirmed first-hand (empty grep over `tests/**`):
- `ReturnPausedForTests` / `ReturnVolumeForTests` / `ReturnMutedForTests` / `ReturnPlaybackRateForTests`
  (added to `PlayerWindow`) are referenced by **no** test — dead accessors.
- `SeekAndPauseAsync` and `ApplyPlaybackSettingsAsync` (new, and now part of the §12.5 contract) have
  **no** test references.
The DOM methods are WebView2-coupled, but the *pure* parts are testable today: the
`ReturnAction.Seek → SeekAndPauseAsync` mapping, and `ApplyPlaybackSettingsAsync`'s clamp(0..1) +
invariant-culture formatting + `rate>0` guard. The follow-up plan's Task 8 already names this; flagging
it as a real (small) gap, not a new discovery.

### 3. One user-visible reversal — confirm intent only; do NOT re-open

Option A inverts the *old* REQ-RETURN-01 guarantee in exactly one case: **source paused at launch →
user presses play in the popout → on close the source now resumes** (old rule kept it paused). The
internal record is unambiguous that this is settled — meta-review: "Do not re-open Option A… it's the
owner's settled call"; follow-up plan Task 3c/3i resolved it; the owner authored these commits. Logged
here only so the single visible behavior change is consciously acknowledged before RC. **Not** a
governance gap; not to be re-litigated.

## Gate status (the actual RC blocker — carried, not new)

Per the plan's own guard, no RC tag before deployed-Stable publish/verify + WebView2 smoke. Outstanding
runtime rows the headless suite cannot cover:

- **Double-audio suppression** moved OPEN → "NEEDS STABLE SMOKE". Reassert cadence is **1 s**, so up to
  ~1 s of source audio can leak at an ad / autoplay-next / SPA element-swap before the guard re-mutes —
  smoke must watch for *brief* leaks at transitions, not just "audio stops on popout."
- **Navigate-replay acceptance** on a live page (does YouTube accept an immediate volume/mute/rate +
  play/pause replay right after navigation).
- **X-close fidelity** is still timer-sampled, not a fresh DOM read (deferred plan Task 3d).

A 2-minute `run-piplay` confirmation (pop out → immediately close; repeat from a paused source) covers
the un-mute-on-return path the headless lane can't.

## Recommendation

Land-ready as code; spec is consistent. Before any RC: resolve finding 1 (one edit), optionally close
finding 2's pure-seam tests, and run the §22 runtime/Stable smoke. Finding 3 is acknowledge-only.
