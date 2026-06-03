# PiPlay — Phase 2 privacy polish design

**Date:** 2026-06-03
**Status:** Approved (design); spec under review
**Requirements:** REQ-PRIVACY-02 (Clear browser data), REQ-UI-01 (themed chrome), Q-6 (recover cleanly from every error)
**Spec refs:** §19 (Security and privacy), §12 (Settings window), `docs/superpowers/specs/2026-05-31-privacy-actions-design.md`, `docs/Data_and_Privacy_Map.md`
**Origin:** post-merge review of PR #1 (`espensev/PiPlay#1`, merged → `main` as v0.3.0) — four sub-threshold review findings plus three opted-in polish items.

---

## 1. Goals

A small, **behavior-preserving** polish pass over the Phase-2 privacy feature that shipped in
v0.3.0. It closes the non-blocking findings the merge review surfaced and tightens the
*Clear browser data* UX. No new feature, no change to the happy path, and **no change to the
sacred sign-out invariant** (only the explicit, confirmed *Clear browser data* signs the user out).

The pass sets out to:

1. **Tell the user the truth on every Clear outcome.** Result and error notices must read as
   statements, not as the confirmation question. Today the not-ready and failed notices reuse the
   `"Clear browser data?"` confirmation title, which leaves the user unsure whether data was
   cleared on a privacy-critical action.
2. **Never silently swallow a Clear failure.** The fire-and-forget `_ = PerformClearBrowserDataAsync()`
   can throw on the pre-`try` readiness re-check (`Browser.CoreWebView2` access during a WebView2
   teardown/recovery); that exception is currently unobserved and leaves the gear enabled with no
   feedback. Every path must surface an outcome and restore UI state (Q-6).
3. **Stop lying about a timeout.** The 30 s `WaitAsync` bound cancels only the *wait*; on timeout
   the underlying clear may still complete. Showing `"couldn't clear"` is misleading — the user may
   in fact be signed out moments later. Say so honestly instead.
4. **Make dialog dismissal consistent.** `Prompt.BuildShell`'s title-bar close calls `Close()`
   without setting `DialogResult`, so `ShowDialog()` returns `null` where the `IsCancel` button
   returns `false`. Align them so future callers can rely on `false`.
5. **Explain why Clear is disabled.** When the browser is still starting, the disabled
   *Clear browser data* button gives no reason; add a tooltip.
6. **Lock the wording and tidy the test layer.** Pin the neutral-title rule with a regression test,
   and stop the Layer-3 WPF tests leaking constructed windows onto `Application.Windows`.

### 1.1 Non-goals

- No change to the sacred invariant, the `AllProfile` clear scope, or the confirmation flow.
- Not re-architecting the fire-and-forget into a fully awaited command — wrapping the body so no
  path is unobserved is sufficient and lower-risk.
- Not eliminating the timeout-vs-background-clear state question beyond honest messaging
  (see §3, accepted residual).

---

## 2. Changes

Behavior-preserving on the happy path. Each row notes the originating review finding and its
adversarial confidence score (≥ items were the four review follow-ups; the rest are opted-in polish).

| # | Change | File(s) | Origin |
|---|---|---|---|
| 1 | Neutral result/error titles — add `ClearResultTitle = "Clear browser data"` (no `?`); use it for the not-ready, failed, and timeout notices. Confirmation keeps `ClearConfirmTitle`; success keeps `ClearDoneTitle`. | `Services/PrivacyService.cs`, `MainWindow.xaml.cs` (clear notices) | Review finding (75) |
| 2 | Exception-safe Clear — wrap the **whole** `PerformClearBrowserDataAsync` body (incl. the readiness re-check) in one `try`; `finally` resets `_privacyActionInProgress` / `_clearingBrowserData` / `SettingsButton.IsEnabled`. No path is unobserved. | `MainWindow.xaml.cs` | Review finding (68) |
| 3 | Honest timeout — dedicated `catch (TimeoutException)` shows a new `ClearTimedOut` message ("…taking longer than expected; it will finish in the background and you may be signed out…"); other exceptions → `ClearFailed`. The 30 s bound is a named constant `ClearTimeout` (derived in §3). | `MainWindow.xaml.cs`, `Services/PrivacyService.cs` | Review finding (72) |
| 4 | Title-bar close → `win.DialogResult = false` (auto-closes the modal), so `ShowDialog()` returns `false` not `null`, matching `IsCancel`. | `Prompt.cs` | Review finding (72) |
| 5 | Disabled-Clear tooltip — when `!isBrowserReady`, set `ClearBrowserDataButton.ToolTip = PrivacyService.ClearNotReadyHint` and `ToolTipService.ShowOnDisabled = true`. | `SettingsWindow.xaml.cs`, `Services/PrivacyService.cs` | Opted-in polish |
| 6 | Regression test — assert Clear's result/done titles are not interrogative and the confirm title is. | `tests/PiPlay.Tests/PrivacyServiceTests.cs` | Opted-in polish |
| 7 | Close Layer-3 windows — make `WpfRuntimeTests` `IDisposable`; close open `Application.Current.Windows` on the STA thread per test (guarded). | `tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs` | Review finding (72) |
| 8 | Instrument the clear — time it with a `Stopwatch`; log the measured duration on success (local-only) so the §3 bound can be retuned from real data instead of estimate. | `MainWindow.xaml.cs` | Resource-lean / data |

**New `PrivacyService` members:** strings `ClearResultTitle`, `ClearTimedOut`, `ClearNotReadyHint`; and `ClearTimeout = TimeSpan.FromSeconds(30)` — the hang-guard bound derived in §3.

---

## 3. Timeout bound (30 s) — rationale

The 30 s bound on `ClearBrowsingDataAsync` is a **hang guard, not a progress wait**: it exists so a
wedged clear can never dead-end the gear/privacy actions for the rest of the session — not to pace a
normal clear. It is derived, not guessed.

**What gets cleared.** `AllProfile` is inclusive of disk cache, all site storage (IndexedDB,
CacheStorage, Local/Session/WebSQL, Service Workers), cookies, download/browsing history, autofill,
and settings. Cookies/settings/autofill/history are kilobytes; the cleared **volume is dominated by
the HTTP disk cache + Service-Worker/CacheStorage + IndexedDB** for `youtube.com`.

**Volume ceiling.** WebView2 imposes no global cap on the user-data folder, but Chromium sizes the
HTTP disk cache from a disk-space heuristic with a default on the order of **~256–320 MB** (tunable
via the `--disk-cache-size` switch). Single-origin site storage adds tens to low-hundreds of MB. A
realistic **worst case for a YouTube-only profile is sub-GB**, and typically < 300 MB.

**Time math.** The clear is I/O-bound — truncate/remove the block-file cache backend and clear the
LevelDB-backed storage, *not* a walk over millions of tiny files:
- **SSD / NVMe (the 2026 norm):** a few hundred MB → **< 1–2 s**; even ~1 GB in a few seconds.
- **Adverse case (slow 5400-rpm HDD, ~GB, AV scanning the deletes):** single-digit seconds with a
  pathological tail to **~10–15 s**.

So the worst-case **successful** completion is ≈ **≤ 15 s**. 30 s places the bound at **~2× that
adverse worst case** — high enough that a slow-but-succeeding clear is never false-flagged as
"didn't finish" (mis-reporting a privacy action is the worse failure), low enough to un-wedge
promptly. We deliberately do **not** stretch to 60 s (diminishing safety, longer dead-time); per the
resource-lean we sit at the short end of "safe," and the only thing held during the wait is one
awaited continuation plus the disabled gear — negligible CPU/memory. The expectation when retuning
from data (change 8) is that we *tighten* toward ~15–20 s, never grow it.

**Correctness note (API contract).** Per the WebView2 docs, if the WebView is *closed* before the
clear completes the completion handler "will be released, but not invoked." Our clear runs on the
**source** `Browser.CoreWebView2`, which we keep alive — only the popout is closed — so the handler
always fires. The 30 s path is therefore reached only on a genuine stall, never on our own teardown.

---

## 4. Accepted residual

On a 30 s timeout the `finally` still clears `_clearingBrowserData` — the UI **must** un-wedge so
the gear/privacy actions are not dead for the rest of the session. The review noted that this could
let a late popout-return drive playback against a session still being wiped; in practice the popout
is already closed at the *start* of the clear, so there is no popout left to return when the timeout
fires. The honest `ClearTimedOut` message (change 3) covers the user-facing half. Documented here so
it is a known, deliberate trade-off rather than an oversight.

---

## 5. Testing

- **Unit (Layer 2):** the new neutral-title assertions (change 6) extend `PrivacyServiceTests`.
- **Live-WPF (Layer 3):** the existing `Clear_is_not_ready_*` / construction tests must stay green
  after the `IDisposable` cleanup (change 7); cleanup is verified by the full suite passing, not by
  a new assertion on `Application.Windows.Count` (which would couple tests to each other).
- **No new flake surface:** the suite remains serial (`AssemblyConfig.cs`); changes 2–4 are on the
  Clear path, exercised by existing `CanClearBrowserData` gating tests.
- Gate: `dotnet test` green (currently 141; +1 for the new title test).

---

## 6. Documentation

- `CHANGELOG.md` `[Unreleased]` gets entries for the user-visible bits (truthful Clear notices,
  disabled-Clear tooltip) under **Fixed/Changed** — landed with the code, per AGENTS.md.
- This design spec + the new **Design docs (per change pass)** convention added to `AGENTS.md`
  (the `docs/superpowers/specs|plans` pattern existed by practice but was uncodified).
- **The `ClearTimeout` constant carries an XML-doc** that states the value (30 s), summarizes §3
  (hang-guard ≈ 2× the ≤ 15 s adverse worst-case clear), and links back to this spec — so the
  rationale lives next to the number and survives even if this doc is missed. **Decision: 30 s,
  settled.** Revisit only from logged real durations (change 8), tightening toward ~15–20 s.
- The implementation plan will live at `docs/superpowers/plans/2026-06-03-phase2-privacy-polish.md`.
