# PiPlay UI Overhaul Roadmap — Grounded Cross-Validation Report

_2026-06-25 · cross-validation of the static UI review (`PiPlay-v0.6.0-b22-full-review.md`) against live code (`src/PiPlay`), via 8 parallel per-priority verifications + synthesis._

> **⚠️ Correction (P1, found during P1 brainstorming):** the synthesis's claim that P1 can "drop the WebView margin to `0,0,0,0` and resize still works" is **WRONG**. `MainWindow.xaml:156-160` / `PlayerWindow.xaml:95-99` document that the **windowed WebView2 child swallows `WM_NCHITTEST`**, so the top-level window only receives edge mouse where the WebView is *not* — the 10-DIP inset is **structurally required** for resize-over-video. Margin-0 ⇒ resize-over-video breaks. **P1 deliverable is corrected to: shrink + blacken the inset (≈4–6 DIP, pure black so it merges with the letterbox), not zero it.** P1 still needs **no airspace lift** (control/Settings borders removed freely; DWM corners already render seamless per the `corner-topleft.png` capture). The ONLY thing needing the WebView2 windowless/composition lift is *literal pixel-zero edges while keeping resize* — a deferred escalation, earned only if the thin black band still reads as a frame once deployed. §3's P1 bullet and §5's "drop margin to 0" deliverable are superseded by this note.

## 1. Overall verdict

The static review is **highly accurate and trustworthy as a roadmap basis.** Across 8 priorities and ~60 individual claims, every architectural and behavioral claim that was checked holds against the live code; the only defects are **stale line numbers** (the code moved, the claim is still correct) and a handful of forward-looking "can we do X?" probes where the honest answer is "not yet wired, but no architectural blocker." Critically, the review's central engineering thesis — that PiPlay can deliver borderless chrome, profile-driven accent, popout opacity, dock/restore, fit modes, auto-hide chrome, and corner rounding **without** flipping `AllowsTransparency=true` — is **confirmed by the code**. The one place the review (and our planning framing) overstated the cost is the airspace lift: the verification shows even P7 needs **no** lift as currently scoped (see §3).

## 2. Per-priority accuracy table

| Priority | Review-claim accuracy | Current state | Needs airspace lift? | Effort |
|---|---|---|---|---|
| **P1** — Borderless | **Confirmed** — all 10 claims true | Opaque windows; 10-DIP resize zone exists as **both** native hit-test (`BorderlessResizeHitTestPolicy`, pure math) **and** visible WebView margin `10,0,10,10`; resting controls carry `BorderSubtle` hairline (`#FF181F29`); Settings has 1-DIP border. Hit-test is **orthogonal** to the visible margin. | **No** | M |
| **P2** — Active-profile-else-global accent | **Confirmed** — all 4 claims true | `ProfileAccentService.ResolvedAccentColor` returns `NormalizeAccentColor(globalAccent)` and **ignores profile data entirely**; profile color drives only the identity chip. | **No** | M |
| **P3** — Popout opacity / transparency scope | **Mostly confirmed** — 2 stale-line | Layered-window alpha (no `WS_EX_TRANSPARENT`) applied **only** to PlayerWindow; MainWindow never gets opacity. Floor 45%. | **No** | S |
| **P4** — Dock / "bring video back" | **Mostly confirmed** — 1 stale-line; **1 KEY claim FALSE** | Return logic (`ApplyReturnActionAsync`) is **hardwired to `Player_OnClosed`** — requires the popout to **close** to fire. Cannot be invoked mid-session today. | **No** | M |
| **P5** — Video fit modes | **Mostly confirmed** — 2 "false" claims are correct *refutations* | 32-DIP strip + `10,0,10,10` margin; Normal = full YouTube page. **No** fit-mode control. Fit modes are **independent** of the removed Compact embed (CSS/JS injection on the Normal page). | **No** | M |
| **P6** — Auto-hide chrome (Off/Main/Popout/Both) | **Mostly confirmed** — all 9 claims true | Popout auto-hide is **production-ready** (idle timer + activity probe + 16-DIP top-edge reveal + fade-coupled collapse). Main chrome (42+50 fixed rows) has **none**. Collapse is **layout-driven**, not rendering-driven. | **No** | M |
| **P7** — Corner rounding / card | **Confirmed** — 1 stale-line | DWM `SetCornerMode` rounds the outer silhouette **natively without transparency**; soft+round both → `DwmCornerMode.Round`. `Radius*Frame` tokens published but **zero consumers**. | **No** *(see §3)* | S |
| **P8** — Profile→live popout appearance | **Partly stale** — 1 stale-line; **1 "missing" claim FALSE** | Global accent/fade/opacity/corner propagate live to an open popout. Profile selection drives **URL navigation only**. Restore-video plumbing **exists**; UI is the gap. | **No** | M |

### Corrected file references (use these, not the review's stale numbers)
- **P3** — popout opacity apply: `PlayerWindow.xaml.cs:706-717` (not ~695). Slider floor `Minimum="45"` at `SettingsWindow.xaml:230` & `:246` (not 242/258).
- **P4** — `Player_OnClosed` at `MainWindow.xaml.cs:939`; `ApplyReturnActionAsync` at **977-1006**; placeholder "Show popout" button at `MainWindow.xaml:185-189` (175 is the placeholder `Border`).
- **P7** — `ThemeResourceApplier.ApplyRadii` spans **89-108**; `WindowOpacityApplier.SetCornerMode` at **110-126**.
- **P8** — popout propagation at `MainWindow.xaml.cs:684-689`.

### Substantive corrections
- **P4 KEY claim is FALSE (in caution's favor):** the return path **cannot** be reused for "bring video back" while the popout stays open — `ApplyReturnActionAsync` is only called from `Player_OnClosed`. P4 requires **extracting** that decision+action logic into a standalone handler that reads popout state on-demand.
- **P8 "restore-video-here is missing" is FALSE:** the plumbing (`_popoutSourceVideoId`, `Player_OnClosed`) is present; only the **UI surface** is missing — the same extraction P4 needs (shared dependency).
- **P5's two "false" claims are correct refutations:** no current fit-mode control exists, and fit modes do **not** require the removed Compact embed.

## 3. CRITICAL reconciliation — the airspace / `AllowsTransparency` decision

> **All 8 priorities, as scoped in the review, are achievable WITHOUT setting `AllowsTransparency=true` and WITHOUT any WebView2 composition/airspace lift. Nothing on this roadmap is blocked by the architecture decision — including P7's corner rounding.**

Why each is safe under the current opaque-HWND model:
- **P1** — the 10-DIP resize is delivered by `BorderlessResizeHitTestPolicy` (pure non-client hit-test), **independent** of the visible WebView margin. Drop the margin to `0` and resize still works. The gutter is a *layout choice*, not an airspace artifact.
- **P2 / P8** — `DynamicResource` re-resolution; pure resource/logic changes.
- **P3** — whole-HWND layered alpha, already shipping on the popout; avoids click-through.
- **P4** — behavioral feature on the already-initialized source `CoreWebView2`; no composition.
- **P5** — CSS/JS injection via `ExecuteScriptAsync`.
- **P6** — **layout-driven** (Visibility + Auto row height); the popout already proves it works opaque.
- **P7** — DWM `SetCornerMode` rounds the outer HWND silhouette natively, no per-pixel alpha.

**Where the lift genuinely lives (deferred fork, NOT a present blocker):**
1. **P7 escalated to glass/backdrop-blur** — silhouette rounding ships today; *only* advanced composition effects would force the lift. The reviewed P7 scope does **not**.
2. **Main-window per-pixel transparency** (hypothetical) — WebView2 has no per-pixel alpha in the standard HWND model. No current priority asks for this.

The airspace lift is an architectural fork the owner can **defer indefinitely**, triggered by feature *escalation*, not by anything on the current roadmap.

## 4. Recommended phase order

| Order | Priority | Rationale | Dependency |
|---|---|---|---|
| 1 | **P1 — Borderless** | Highest visible payoff, fully independent, no lift, pure layout+token+test work. | None |
| 2 | **P2 — Profile accent sourcing** | Small logic change to one pure helper + one re-resolve; unblocks the "profile feels live" perception. | None |
| 3 | **P3 — Popout opacity polish** | Smallest effort (S); mechanism already shipping — exposure/tuning. | None |
| 4 | **P4 — Dock / bring-video-back** | Extracts `ApplyReturnActionAsync` into a reusable handler — the **structural prerequisite** for P8's restore UI. | **Blocks P8** |
| 5 | **P8 — Live profile→popout + restore-video UI** | Consumes P4's extracted handler; plumbing exists, only UI + per-profile-override decision remain. | **Depends on P4** |
| 6 | **P6 — Auto-hide main chrome** | Port the proven popout machinery to MainWindow with focus guards. | None (popout = reference impl) |
| 7 | **P5 — Video fit modes** | Settings UI + DOM injection; self-contained, lower urgency. | None |
| 8 | **P7 — Corner rounding consumer** | Silhouette already native via DWM; lowest-effort; defer the glass-escalation (airspace) decision. | None |

**Only hard dependency:** P4 → P8 (shared return-path extraction). P1, P2, P3, P6, P7 are independent/parallelizable.

## 5. Highest-value first branch

**Start with P1 (Borderless).** Highest-visibility change, **no airspace lift**, **no dependencies**, and the enabler is proven in code: `BorderlessResizeHitTestPolicy` provides native edge/corner resize **independently** of the visible WebView margin. Deliverable = drop margin to `0,0,0,0` normal, quiet/condition the `BorderSubtle`+`BorderThicknessDefault` resting-control defaults, zero the Settings outer border.

**Feasibility risk — test churn, NOT architecture:** `XamlInvariantTests` currently **pin the exact things P1 removes** — `WebView_margin` locks `10,0,10,10`, `Grey_border_tokens_are_quieted…` locks the hairline. These must be **rewritten to assert resize _behavior_** (exercise the hit-test policy) instead of the visible margin literal, so the invariant guards _resize works_ rather than _the gutter exists_. Resize itself carries **no** risk; the only real work is making the suite assert the right invariant.
