# Theme V2 spec + plan — review & evaluation (2026-06-14)

**Reviewer:** Claude (Opus 4.8), multi-agent eval + deterministic verification
**Subjects:**
- `docs/superpowers/specs/2026-06-14-theme-v2-tight-scope-design.md` (canonical implementation spec)
- `docs/superpowers/plans/2026-06-14-theme-v2-tight-scope.md` (10-task implementation plan)
- `docs/superpowers/specs/2026-06-14-theme-differentiation-design.md` (superseded draft — used only as the palette/contrast validation source)
- `docs/Theme_Preset_Differences.md` (current-code reference)

**Status of the work reviewed:** *pending implementation.* So "adherence" here means three things: (a) the spec accurately
describes the current code baseline, (b) the docs are internally consistent, and (c) the planned **test gates will actually
enforce the spec once implemented**. (c) is where the actionable findings cluster.

**Baseline:** code is byte-identical to `main` HEAD (the new docs are the only uncommitted change). The spec's claimed
focused test filter was re-run and returns **97 passed, 0 failed** — its "checked against the local tree, 97 passed" claim is accurate.

---

## Verdict

**The spec and plan are high quality and factually accurate against the code.** The crown-jewel safety claim (WCAG contrast)
is verified **for the base palettes and base accents** — but two Phase-B *derived* accent tokens breach it as specified
(**CON-1**, the one substantive design defect). Every baseline claim and every migration-list value checks out. The remaining
issues are **not in the design — they are in the test gates the plan relies on to enforce the design**; several let a future
regression slip through CI silently. None blocks starting; CON-1 should be resolved before/within Phase B, and each gate fix
folded into its task's verification *before that task is implemented* (TDD: the gate must fail on the un-diverged value first).

---

## 1. Contrast — base palettes + base accents VERIFIED ✅; derived variants NOT all safe ⚠️

Computed deterministically with the exact `tests/PiPlay.Tests/Infrastructure/Wcag.cs` formula against the exact gates in
`ThemeCatalogTests.cs:171-189`, over the **v2 target palettes**:

- The superseded draft's published contrast table **reproduces exactly** (18.84 / 17.99 / 7.50 / 3.43 / 4.22 …). Because the
  draft's palette+radii are byte-identical to v2's, that table validates v2.
- **Every target palette passes all gates.** Floors: TextSecondary/SurfaceBase ≥ 4.5 (min 7.46), white/Danger ≥ 3.0
  (min **3.43**), all six accents glyph ≥ 3.0 on every preset hover (min **steel/Soft-Glass = 3.43**) and dark-text fill ≥ 4.5
  (min **steel = 5.17**).
- **`OnAccent`/`PickReadableForeground`:** all six current chips resolve to dark `#06141A` at ≥ 4.5 (floor steel 5.17). None
  rely on the unsafe "pick the larger of two sub-threshold ratios" fallback. **Caveat:** the algorithm does *not* guarantee
  this for an arbitrary hue (see TG-4) — only the fixed chip set is proven safe.

So the spec's claim that the existing contrast theory covers the new **base** palettes/accents is **true**. But that theory
says nothing about the **derived** accent tokens Phase B introduces — and computing them from the spec's own `ThemeAccentProfile`
mixes shows two of them breach WCAG (see **CON-1**). The headline "contrast is handled" is correct for what ships in Phase A;
it is **not** yet true for Phase B.

## 2. Baseline-vs-code accuracy — ACCURATE ✅

Every "Already in place" / "Remaining gap" claim and every "XAML migration list" *value* was confirmed against code
(values, not line numbers, since the spec marks line numbers as "as of this writing"):

- `AccentButton` foreground hardcoded `#FF06141A`; only `AccentPrimary` + `AccentPrimaryLight` accent tokens applied at
  runtime; `AccentPrimaryLight = Lighten(accent, 0.30)`; nullable behavior-override model + accent-preservation-on-switch both exist.
- All density literals exist verbatim: `DarkButton 12,6`/`1`; `DarkTextBox 32`/`10,0`/`1`; `IconButton`/`PinToggle 32`;
  `ScrollBar 10`; `ComboBoxItem 10,6`; `DarkComboBox 32`/`1`; `ToolTip 8,5`/`1`; `PresetToggle 30`/`10,0`; `SwatchToggle 34`.
- Control-site `CornerRadius` already uses semantic `{DynamicResource Radius*}` tokens (only `WindowChrome="0"` is literal —
  correctly in the do-not-migrate list).
- `Theme_Preset_Differences.md` is still an accurate code-derived mirror of the current catalog.

Two nits (not errors): the applier doesn't refresh the `AccentPrimaryColor` companion token (relevant to Phase B — **BL-09**);
the migration lists are silent on `DangerButton`/chip `BorderThickness` values (completeness for the `BorderThicknessDefault`
decision — **BL-10**).

## 3. Issues to fix — prioritized

Severity reflects "how likely a regression slips through CI / how user-visible the defect." **CON-1 is a real design defect**
(a WCAG breach in the planned derivation); the rest are **test-gate / plan-wording** fixes where the design is sound.

### MAJOR

**CON-1 — two Phase-B derived accent tokens breach WCAG as specified (the one real design defect).**
Computed from the spec's `ThemeAccentProfile` mixes (lines 311-333) with the exact `Wcag.cs` formula, across all six chips ×
all three theme profiles:

| Derived token | Use pairing | Worst ratio | Gate | Result |
|---|---|---:|---:|---|
| `AccentHover` | fill under dark text / glyph on hover | 6.75 / 5.24 | 4.5 / 3.0 | ✅ safe |
| `AccentBorder` | outline on SurfaceBase | 5.73 | 3.0 | ✅ safe |
| **`AccentPressed`** | primary-button fill under dark `#06141A` text | **3.82** (steel, all 3 themes 3.82/3.98/4.14) | 4.5 | ❌ **fails** |
| **`AccentMuted`** | dark-text fill / on-dark glyph | **1.98 / 1.62** | 4.5 / 3.0 | ❌ **fails** |

- `AccentPressed = Mix(primary, Black, 0.12–0.16)` darkens the fill while the foreground stays the *same* dark `OnAccent` that
  was chosen for the lighter primary — so steel (already the dimmest, primary floor 5.17) drops to **3.82** when pressed. Light
  accents (cyan 7.43, amber 8.51) are fine; **steel is the failure**, and steel is the owner's preferred "muted sharp" look.
- `AccentMuted = Mix(primary, SurfaceRaised, 0.40–0.58)` is deliberately pulled toward a dark surface. The spec lists its
  consumers as "restrained sharp buttons, secondary active state" but **never pins the foreground/background it sits in**. Under
  the two readings that wording implies — a button fill with dark text, or a glyph on the hover surface — it fails hard. It is
  only safe if paired with *light* (TextPrimary) foreground on a dark backing, which the spec must state explicitly.
- `AccentSubtle`/`AccentGlow` are alpha washes; effective contrast depends on compositing, not a base ratio — out of scope for a
  simple gate but worth a compositing check where they back text.

**Fix:** in the spec, pin each derived token's exact fg/bg use pairing; adjust the `AccentPressed` handling so `OnAccent` is
re-evaluated against the pressed fill (or cap `PressedBlackMix` for dim accents); confirm `AccentMuted` rides light text. In
Task 3, add a WCAG gate over **every derived token in its pinned pairing, across all six chips × the three profiles** — not just
the base accent. (Verdict: real WCAG breach in the planned derivation, not merely a missing test.)

---

| ID | Issue | Fix |
|---|---|---|
| **TG-1** | Plan Task 2's anti-collapse gates cover radii/DWM/opacity/accent but **not** the two behavior axes v2 newly diverges: fade-delay (Sharp=normal, Minimal=long, Soft Glass=short) and strip-auto-hide (Soft Glass=true). Today all presets share `normal`/`false` (`ThemeCatalog.cs`). A regression collapsing them back passes Task 2 *and* all 97 tests. | Add per-preset **exact-literal** assertions for `DefaultFadeDelayPreset` and `DefaultStripAutoHide`. ⚠️ Fade is **non-monotonic** (short/normal/long = 1500/2500/4000 → Sharp/Minimal/SoftGlass = 2500/4000/1500) — do **not** use an ordering inequality; assert exact values or pairwise-distinctness. |
| **TG-2** | Plan Task 1's "exact value gates" are unspecified. The only existing exact gate (`Colors_xaml_seeds_match_the_sharp_dark_preset`) derives expected values **from the catalog** — a catalog↔seed *consistency* check, not a spec gate. Modeled the same way, changing catalog+test together stays green and enforces nothing against the spec's target tables. | Specify that the new gate pins **hardcoded literals from the spec target tables** (e.g. `Assert.Equal("#050609", sharp.Palette.AppBackground)`), independent of the catalog. Keep the catalog↔seed consistency check separately. Covers Minimal/Soft Glass too (today only Sharp's exact values are gated, via the seed test). |
| **TG-3** | Plan Task 5 verifies only "sane density ranges." That passes even if all three presets collapse to the identical "Safe fallback" column — defeating "Sharp compact / Soft Glass airy." | Assert **exact** per-preset density values. ⚠️ A `<=` ordering gate is **not** sufficient (non-strict `<=` passes when all equal — the same weakness Task 2 was created to fix), and strict `<` is wrong because spec values legitimately tie on several axes (ScrollbarThickness 8/10/10, BorderThickness 1/1/1, PresetChipPadding-Y 0/0/0). Use exact literals + a distinctness check on the diverging axes. This is the spec's own acceptance bar (line 71: "density values, and identity deltas"). |
| **TG-4** | Spec promises "any wheel-selected hue produces readable primary buttons," but `PickReadableForeground`'s final branch can return a foreground < 4.5:1, and every planned `OnAccent` gate iterates only the fixed 6 chips. A Phase E hue wheel could ship an unreadable primary button with nothing failing. | Add to Task 9 a concrete **hue-sweep** invariant (`OnAccent ≥ 4.5` across a dense hue sweep) **or** constrain the wheel emitter to a pre-validated accessible lane. (Not urgent — Phase E is last — but it gates the wheel's safety.) |
| **FEAS-01** | The 150%-DPI URL-clipping test the spec leans on to bound the *dense end* (Sharp ControlHeight 30 < today's 32) is **illusory** *(confirmed by direct read of `WpfRuntimeTests.cs:1485-1512`)*: the host `Border` is fixed `Width=320, Height=32` and the child `TextBox` (no Height/alignment) is measured+arranged into that 32px box, so `MinHeight`/`DensityControlHeight` never binds — lowering it to 30 renders the identical 32px field and the `inkedRows>=8` assertion is invariant to the value it claims to gate. | Before Task 6 relies on it, make the test host **size-to-content** (or test a real 30-DIP field) so `DensityControlHeight` actually drives the arranged height. Otherwise the dense-end clipping risk (the plan's "highest risk") ships unguarded. |

### MINOR

| ID | Issue | Fix |
|---|---|---|
| **FEAS-08** | The existing "no hardcoded values" ban is **CornerRadius-only**; it cannot catch a residual literal left at a migrated Padding/Height/BorderThickness site, and the DynamicResource sweep passes *vacuously* for a site that kept its literal. (This is the correct, scoped version of a concern that was raised more broadly and refuted — see §5.) | Add a Task 6 assertion keyed to the **named migration-list styles** (by `x:Key` + `Setter Property`) that those Setters reference the `Density*`/`BorderThicknessDefault` DynamicResource keys. |
| **TG-8** | No runtime test resolves a density/border/elevation token on an **already-realized** control (the spec's wording, stronger than Task 5's "keys are replaced"). Existing `WpfRuntimeTests` only do this for palette/radius/accent. | Add a realized-consumer assertion (e.g. `DarkButton.Padding` re-resolves from `DensityButtonPadding`, mirroring the existing `bd.CornerRadius` check) and `ElevationPopup` null-for-Sharp vs non-null-for-Soft-Glass. |
| **F1** | The persisted-shape JSON example pairs `"themeId": "sharp-dark"` with `"accentColor": "#4A8FAB"` (steel), while the documented install default is cyan `#00D4FF` (spec table + `ThemeCatalog.DefaultAccentColor`). An implementer could seed the wrong default. | Change the example to `#00D4FF`, or annotate it as the "Sharp Dark + Steel" variant. |
| **TG-10** | Spec asks for "display names are stable" but no test asserts the `DisplayName` strings (only IDs). | Pin each `DisplayName` ("Sharp Dark"/"Minimal"/"Soft Glass") or mark intentionally ungated. |
| **BL-09** | The applier replaces `AccentPrimary`/`AccentPrimaryLight` brushes but not their companion `*Color` tokens (palette tokens *do* get companions). | When Phase B adds `OnAccent`/`AccentHover`/etc., also refresh `AccentPrimaryColor` so direct Color consumers stay in step. |

### NIT
- **BL-10 / F3:** decide explicitly whether `DangerButton`/chip `BorderThickness` and `SwatchToggle` size are in scope for
  `BorderThicknessDefault`/`DensitySwatchSize` so the migration list is exhaustive. `DensitySwatchSize` is referenced in the
  migration list but absent from the add-list (hedged "defer or add if needed"; Task 6 omits SwatchToggle) — consistent, no v2 action.

## 4. Feasibility confirmations ✅

- **Inner elevation is feasible.** The ComboBox dropdown is a real `<Popup AllowsTransparency="True">` (separate HWND) —
  a `DropShadowEffect` there works but **clips if flush**; apply `ElevationPopup` to the *inner* `DropDownBorder` with inset
  room. The main/popout windows stay `AllowsTransparency=False` and host WebView2 by HWND, so video airspace (ADR-0006) is
  protected and no outer-window glow is attempted. (FEAS-04)
- **`BorderThicknessDefault` as a uniform `Thickness` resource is correct and necessary** — a `double`/`string` would hit the
  .NET 10 DynamicResource type-mismatch crash class; `CornerRadius` already proves a struct works as a replaced DynamicResource. (FEAS-05)
- **Sharp Dark's `null` elevation is valid** (see §5 — the concern that it was "contradictory" was refuted): `{x:Null}` is a
  legal resource value and `UIElement.Effect` defaults to null. Just ensure the applier writes the null/unset case rather than
  a concrete no-op effect (a no-op `DropShadowEffect` would add per-frame raster cost the spec deliberately avoids).

## 5. Findings considered and REFUTED (transparency)

The adversarial pass killed four candidate findings — recorded so they aren't re-raised:

- **Density-site ban "unscoped"** — refuted: Plan Task 6 explicitly says "at migrated sites" + an allowlist and denylist. The
  *real* residual is the narrower FEAS-08 above.
- **Additive-safety "doesn't cover applied Sharp density"** — refuted: it conflates an *unmigrated* site (renders identical by
  its retained literal — airtight) with *applied* density (the intended Phase-C change). The safety claim is correctly scoped.
- **Sharp `null` effect "contradictory" + .NET 10 crash** — refuted: null Effect is valid; the .NET 10 crash is a *type
  mismatch*, not a null value.
- **`AccentButton` pressed-state "missing from spec"** — refuted: the migration table is the *density* pass list; the accent
  pass governs pressed-state via Phase B scope (line 468) and Changes-by-file (line 579). Spec and plan already agree.

## 6. Owner decision to confirm (not blocking)

The spec names exactly one product fork: **Sharp Dark default accent = cyan `#00D4FF` (recommended) vs steel `#4A8FAB`.**
The spec recommends keeping cyan and treating steel as the "Sharp Dark + Steel" accent variant; code already defaults to cyan.
No action needed unless you want steel as the install default — in which case change only the sharp preset default accent +
tests (do **not** add a fourth theme ID). Resolving F1's JSON example would also remove the ambiguity.

## 7. Adherence enforcement — at a glance

**Already enforce the spec (no work):** hardcoded-`CornerRadius` ban (incl. Setter form) · Sharp-Dark seed↔catalog lockstep ·
WCAG contrast at both catalog and markup-seed layers · Settings chips↔catalog match · per-scope DynamicResource reachability ·
replace-not-mutate for palette/radius/accent on realized controls.

**Must be ADDED to actually enforce v2** (the §3 list): **derived-accent WCAG gate over all six chips × profiles (CON-1)** ·
per-preset fade/strip gates (TG-1) · spec-literal value gates (TG-2) · exact density gates (TG-3) · a real dense-end clipping
gate (FEAS-01) · positive density-Setter DynamicResource assertion (FEAS-08) · realized-consumer re-resolution for the new
tokens (TG-8) · hue-sweep OnAccent gate before the wheel (TG-4).

**Design change (not just a gate):** CON-1 — adjust the `AccentPressed` foreground handling / `PressedBlackMix` for dim accents,
and pin `AccentMuted`'s use pairing, so the steel chip's pressed state and the muted token are WCAG-safe before Phase B ships.

---

*Method: deterministic contrast computation + test-filter re-run, then a 4-dimension multi-agent review (baseline-vs-code,
test-gate adequacy, WPF feasibility, doc/spec/plan consistency) with adversarial verification of every material finding
against the source tree. 17 agents. Code citations verified against the worktree (= main HEAD).*
