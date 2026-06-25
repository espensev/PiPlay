# Review — Codex P4 "Bring video back" (commits `fbe77c9` + `4841842`)

**Date:** 2026-06-25
**Reviewer:** Claude (Opus 4.8), adversarial multi-agent review (5 dimensions × refute-by-default verify)
**Scope:** `fbe77c9` "feat(popout): bring video back from source window" (18 files, +1369) and
`4841842` "docs: record b25 follow-up stabilization".
**Method:** every claim re-derived from source; build/tests run; the verdict-gating finding
independently re-confirmed against the spec by the synthesizer.

> **Reconciled to HEAD `a4fe91c`.** While this review ran, a *parallel* package self-review committed
> two docs-only commits — `011e8ee` (found the cross-video Navigate gap → now **Task 3b**; fixed the
> SPEC_GAPS 3.2 "working tree" wording) and `a4fe91c` (recorded a clean automated QA pass: build +
> 683/683 + Release gate). Neither changed code, so every code-level finding below still holds against
> HEAD. **The parallel self-review did NOT find the decision-gating item below** — that is the
> material delta this review adds.

## Verdict

**The code is well-built — no crash, no injection, no data-loss, no lifecycle defect — but the feature
is NOT "done," and one decision-gating item must be resolved before these commits are pushed or
RC-tagged.**

> Git reality: this work is already on **local `main`**, now **6 commits ahead of `origin/main` and
> unpushed** (the P4 code in `fbe77c9` plus four docs commits). So the live decision is not "merge" —
> it is **push now vs. resolve the gating item first.** Owner directive 2026-06-25: **decide later,
> do not push** — the gating decision must land before push, and certainly before any RC tag.

- ✅ Build green, **683/683** tests pass.
- ✅ Bring-back lifecycle is re-entrancy-safe; the button label flip is correct (synchronous
  `Close()` nulls `_player` before the `finally`).
- ✅ No script-injection vector in `ApplyPlaybackSettingsAsync` (JSON-numeric/bool only, clamped,
  `ToString("R", InvariantCulture)`).
- ✅ Does **not** worsen the known double-audio bug; `SeekAndPauseAsync` mildly improves the
  paused-return case.
- ✅ Docs are honest about transparency ("not video-safe / where YouTube permits"); the new QA item
  is added **unchecked**; runtime-gated visual QA was correctly left as a **deferred** gate (no false
  completion).
- 🟠 **One decision-gating finding** (below).
- 🟡 A cluster of low/nit test-coverage and doc-consistency gaps, all cheaply closeable.

Statistics: 27 findings raised, **26 confirmed, 1 refuted**. Exactly one is decision-gating; the rest
are low/nit/none.

---

## 🟠 Decision-gating: resume now keys off the popout's live paused state — silently changing the existing close-via-X path and contradicting normative REQ-RETURN-01

**This is the only finding that blocks an RC sign-off, and it is a product/spec decision, not a code
bug.** It was surfaced independently by two dimensions (return-correctness + docs-honesty) and
re-confirmed by the synthesizer against the spec.

**What changed.** `ReturnPolicy.Decide` now computes
`shouldResume = returnedPaused.HasValue ? !returnedPaused.Value : sourceWasPlaying`
(`ReturnPolicy.cs:32`), and `ApplyReturnActionAsync` passes `state.Paused`
(`MainWindow.xaml.cs:1015-1016`). Because `ApplyReturnActionAsync` is the **single** return handler for
**both** the new bring-back close **and** the user closing the popout via its X button
(`Player_OnClosed` → `ApplyReturnActionAsync`, wired at `MainWindow.xaml.cs:849/969/988`), the resume
semantics of the *existing* close-via-X path changed even though P4 only targeted bring-back. The
popout's paused/volume/mute/rate are captured continuously each DOM-sync tick
(`PlayerWindow.xaml.cs:481/498-505`), so an X-close carries the popout's last-known state.

**Why it gates.** The shipped behavior now contradicts the normative source-of-truth, which `fbe77c9`
left unreconciled (its spec edits were only terminology, the placeholder note, and the bridge method
list). The strongest leg is **categorical, not frequency-dependent**:

- spec **line 933** (the model directive): "`sourceWasPlayingAtPopout` is captured before PiPlay pauses
  the source; **do not infer it later from close-time state.**" The new code now infers resume from
  close-time popout state on **every** non-Navigate close — an outright violation of the stated model,
  regardless of how often it changes the outcome. *(verified unchanged)*
- `REQ-RETURN-01` (spec **line 932**): "resume … only if the source was playing when Video Popout
  started. If … paused when popped out … remain paused." *(verified unchanged)*
- recommended model (lines 937-939), requirements matrix (line 1439), `SPEC_GAPS` resolved-decision
  (line 107) — all still source-anchored.
- **RC-acceptance QA row 1233**: "Close paused source → Source returns at timestamp and **stays
  paused** (`REQ-RETURN-01`)." This is where a *user* would see the divergence. *(verified unchanged)*

**Concrete divergence (Case C).** Pop out while the source is **paused** (`sourceWasPlaying=false`);
the popout plays (autoplay, or the play-once nudge at `PlayerWindow.xaml.cs:475-479` sets
`Paused=false`); user closes via X. **Old code:** `Seek` → source stays paused (matches spec + QA row
1233). **New code:** `returnedPaused=false` → `SeekAndPlay` → **source resumes** → fails QA row 1233
and violates line 932/933.

> Note: the related "user pauses the popout, source stays paused on X-close" case is *new permitted*
> behavior, not a contradiction (REQ-RETURN-01's "only if" is necessary, not sufficient). And matching
> the popout's live state on return is arguably **better UX** — which is exactly why this is a decision,
> not a revert.

**Required action (owner decision):**
- **Option A — bless the new behavior** (popout-state-wins): update REQ-RETURN-01 (§14 line 932), line
  933, the recommended model (937-939), requirements matrix (line 1439), `SPEC_GAPS` (line 107), and
  QA row 1233 to state "the popout's live paused state takes precedence when known; source-was-playing
  is the fallback only when unknown (e.g. compact mode)."
- **Option B — keep the spec** (source-anchored): scope `state.Paused` to the **bring-back path only**
  so close-via-X retains the old `sourceWasPlaying` semantics.

Either way the code change is small; the decision is the gate.

---

## 🟡 Low / nit — cheaply closeable, none block merge

### Test coverage (the action→DOM half of P4 is unverified)
- **`ReturnAction.Seek` now maps to `SeekAndPauseAsync` (was `SeekAsync`) and the new
  `ApplyPlaybackSettingsAsync` call have no automated coverage** — the only two `ApplyReturnActionAsync`
  tests run with `core == null`, so both branches no-op. The decision layer (`ReturnPolicy.Decide`) is
  well-covered; only the WebView2-bound dispatch is not. Mitigated by the new manual QA row. *(low,
  runtime-tier)*
- **Four new `ForTests` accessors (`ReturnPaused/Volume/Muted/PlaybackRateForTests`) are unused dead
  scaffolding**, and `ApplyReturnPlaybackState`'s field-copy has zero coverage. Closeable with a
  one-line `ApplyReturnPlaybackStateForTests(PlayerState)` seam + a round-trip assert. *(low)*
- **`YouTubeDomBridge` JS-gen is untested** — the `Math.Clamp(0,1)`, the `rate>0` guard, and crucially
  `ToString("R", InvariantCulture)` (a comma-locale regression would emit invalid JS `v.volume = 0,5`
  with nothing to catch it). Extract a pure `BuildPlaybackSettingsScript(...)` + a `ParsePlayerState`
  and test typical/exponential/NaN/locale cases. *(low)*
- **The production 5-arg `Decide` overload's `returnedPaused` forwarding is tested only on the Navigate
  short-circuit**, never the same/unknown-id fallback at `ReturnPolicy.cs:57` — add one `InlineData`
  row (e.g. same id, `returnedPaused:true` ⇒ `Seek`). *(low)*
- **No test pins that `PopOutButton`/placeholder route to `BringVideoBackAsync`**, and `PopOutButton`'s
  `Click` attr is unpinned (asymmetric with the placeholder). Add a `XamlInvariant` Click-attr pin.
  The bulk of the test diff is renamed-string re-pinning, not new-behavior coverage. *(low)*
- Paused-override `Theory` omits the `0`-timestamp boundary the sibling REQ-RETURN-01 Theory
  deliberately covers — add `[InlineData(0, true, true, Seek)]`. *(nit)*

### Docs consistency
- **Stale file:line refs in the open double-audio bug row** (`SPEC_GAPS:9` cites
  `YouTubeDomBridge.cs:65-66` / `MainWindow.xaml.cs:826` — both shifted to `:84-85` / `:828-831` by
  this commit). Update or cite symbols only. *(low)*
- **`AGENTS.md:28` glossary still says "whole-window opacity"** — missed by the rename sweep. *(nit)*
- ~~**`SPEC_GAPS` 3.2 row says "Implemented in working tree"**~~ — **resolved by `011e8ee`** (now
  "Partially implemented in `fbe77c9`"). *(nit, closed)*

### Behavior
- **Navigate (cross-video) return skips `ApplyPlaybackSettingsAsync`**, so `playbackRate` is lost on a
  different-video return (volume/mute survive via YouTube localStorage). **Already tracked as Task 3b**
  by the parallel self-review (`011e8ee`); the fix is a post-navigation playback-state replay. *(nit,
  tracked)*
- Dead 4-arg `ReturnPolicy.Decide` overload (only a test caller remains). *(nit)*
- Pre-existing race: `BringVideoBackAsync`'s `finally` clears `_popoutInProgress` before the async
  return settles — same window already exists on close-via-X; **not materially worsened**. *(nit)*
- Latent: bring-back from a (currently dormant) Compact popout would not restore volume/mute/rate —
  the early-return gate is correct (cross-origin embed iframe), just record the limitation. *(nit)*

---

## ✅ Verified sound
Bring-back lifecycle re-entrancy/ordering; button-label flip; no double-popout/null-deref/instant
re-pop; no script-injection vector; no double-audio regression.

## ❌ Refuted (1)
"Source returns silent because the popout's captured `muted` carries back from an autoplay
auto-mute." **Refuted at the code level:** the shared `CoreWebView2Environment` launches with
`--autoplay-policy=no-user-gesture-required` (`WebViewEnvironmentService.cs:50-70`), so the popout is
never auto-muted; any mute carried back is user-chosen — the intended P4 behavior.

## Verification tiers (repo Rule #1)
- **Code-verified** (this review): all of the above.
- **Runtime-QA-tier — still owed on the deployed Stable copy:** whether YouTube actually honors the
  direct `v.volume`/`v.muted`/`v.playbackRate` writes; whether play/pause + state truly preserve on
  bring-back; the Case-C close-via-X behavior; and the new "X-close now propagates popout
  volume/mute/rate to the source" UX. The QA row added in this commit exercises only the **button**,
  not a plain X-close — add an X-close QA line.
