# PiPlay — Spec Conformance Review

- **Date:** 2026-05-31
- **Against:** `docs/PiPlay_Product_Engineering_Spec.md` (Draft 0.4)
- **Scope:** Implementation (`src/PiPlay`) vs every `REQ-*`, `Q-*`, and `UI-CHK-*` gate, by
  requirement area. Produced by a multi-agent fan-out (one reviewer per area) with an
  adversarial verification pass over every finding flagged as a bug.
- **Companion:** `docs/Regression_Test_Suite_Design.md` (the test suite that locks these in).

## Method

8 area reviewers read the relevant spec sections plus the source and existing tests, and
classified each requirement as `met` / `partial` / `gap` / `untested` / `intentional-deviation`
/ `bug`. Every `bug`-flagged finding was then re-examined by an independent skeptic instructed
to refute it. **92 findings** total.

## Summary

| Area | Findings | met | gap | untested | partial | intentional-deviation |
|---|--:|--:|--:|--:|--:|--:|
| Navigation & allowlist | 4 | 2 | 1 | – | – | 1 |
| Popout lifecycle & Return | 9 | 8 | 1 | – | – | – |
| Settings & Profiles | 15 | 12 | – | 2 | 1 | – |
| Window / Placement / DPI | 6 | 3 | 1 | 1 | 1 | – |
| Chrome & Visual identity | 17 | 5 | 4 | 6 | 1 | 1 |
| Fade / Opacity | 19 | 18 | – | – | – | 1 |
| Single-instance & lifecycle | 10 | 9 | 1 | – | – | – |
| Recovery / Errors / Logging | 12 | 10 | 1 | 1 | – | – |
| **Total** | **92** | **67** | **9** | **10** | **3** | **3** |

**Headline:** No confirmed bug in the current source. The implementation conforms to the MVP +
Phase-2 spec. The `gap`/`untested` items are (a) missing automated coverage of already-correct
behavior — most now closed by this suite — or (b) Phase-2/4 features the spec defers.

## Confirmed bugs (await approval before any fix)

**None.** The review's two `isReal=true` verdicts — both in Chrome (REQ-UI-01 "dark-theme
completeness" and "dark tooltips") — are **stale-evidence artifacts**, not current bugs:

- Their evidence cites `docs/Chrome_UI_Issue_Report.md` and the `chrome-current-*.png`
  screenshots, which capture the **pre-fix** state dated 2026-05-30 (there are also
  `chrome-fixed-*.png` "after" captures).
- The current source already implements the dark fixes: `ControlStyles.xaml` defines a full
  `DarkComboBox` `ControlTemplate` with a dark `Popup` (`SurfaceBase`) + `DarkComboBoxItem`, an
  app-wide implicit dark `ToolTip` style (`Placement=Bottom`, `VerticalOffset=4`), and an
  in-template icon-font `TextBlock` to prevent `.notdef` boxes. `docs/CHANGELOG.md` records all
  of these as fixed.
- The adversarial pass **refuted 6 of the 8** Chrome bug flags as stale (REQ-UI-02 icons,
  ComboBox popup, UI-CHK-2/3/4/5). The 2 that stayed "real" did so only on the same stale
  screenshots while explicitly conceding the source is correct.

These are now guarded automatically: **Layer 1** asserts the dark styles/tokens exist and every
`{StaticResource}` resolves; **Layer 3** asserts at runtime that `ProfilesCombo`/`UrlBox` use the
dark styles and the implicit `ToolTip` style is dark. The definitive **pixel** confirmation is
the manual **Layer 4** smoke (`scripts/Test-UiSmoke.ps1`) at fractional DPI.

## Intentional deviations (no action)

- **NAV — Google sign-in allowed on _both_ surfaces.** `NavigationPolicy` ignores the surface
  and allows YouTube + Google auth (incl. regional `accounts.google.<tld>`) on Source *and*
  Player, so a sign-in/consent redirect never dead-ends the player. Documented in the code and
  CHANGELOG; a strict reading of REQ-NAV-02 ("the player never wanders") would block it. Locked
  by `NavigationPolicyTests`.
- **FADE — 2.5 s idle delay** vs the spec's *suggested* 1.2 s (`FadePolicy.IdleDelayMs`). Spec
  §7.1 marks the timing as a suggested default; 2.5 s is the chosen UX. In CHANGELOG.
- **§20 contrast** — declared `TextPrimary`-on-`SurfaceRaised` is 14.98:1 (well above 4.5:1);
  the old "illegible URL" screenshot was the `UseLayoutRounding` clipping, since fixed. Locked
  by the Layer 1 contrast test + Layer 3 render test.

## Gaps & untested items

### Closed by this test suite
| Item | Area | Now covered by |
|---|---|---|
| REQ-RETURN-01 resume logic untested | Popout/Return | `ReturnPolicyTests` (+ extracted `ReturnPolicy`) |
| `Log.RedactUrl` untested (security-relevant) | Recovery/Logging | `LoggingServiceTests` |
| PerMonitorV2 manifest unverified | Settings, Window/DPI | `XamlInvariantTests.App_manifest_declares_per_monitor_v2_dpi` |
| Off-screen placement clamp untested | Window/DPI | `PlacementMathTests` (+ extracted `PlacementMath`) |
| Dark theme "renders light?" (stale) | Chrome | Layer 1 resource/contrast + Layer 3 runtime style checks |
| URL text clipping at fractional DPI | Chrome | Layer 1 `UseLayoutRounding` invariant + Layer 3 render test |
| ProfileService / nav-scheme / URL-shape edges | several | Layer 2 additions |

### Deferred to Phase 2+ (spec §23 — not MVP)
| Item | Area | Note |
|---|---|---|
| REQ-PROFILE-01 per-field precedence (`FadeEnabled`, `Bounds`) | Settings | Model supports nullable fields; only `Name`/`Url`/`Topmost` used in MVP load path. Profile editing is Phase 2. |
| Profile `Bounds` restored on profile load | Window/DPI | `Profile.Bounds` persists but `ProfilesCombo_SelectionChanged` applies only URL + Topmost. Phase 2. |
| Profile null-field → global fallback | Settings | Same Phase-2 scope. |

### Residual (recommend, not blocking)
| Item | Area | Recommendation |
|---|---|---|
| `PlayerWindow.Core_NewWindowRequested` opens **all** new-window requests externally, unlike `MainWindow` (which checks the allowlist) | Navigation | Minor inconsistency vs the "sign-in never dead-ends the player" intent. In practice login persists via the shared user-data folder. Consider aligning it with `MainWindow` in a Phase-2 polish — **flagged for your decision; no change made.** |
| Unhandled-UI-exception handler untested | Recovery | Integration test (construct `MainWindow`, raise from a handler, assert app survives + logs). Lower value; left for later. |
| Named-mutex / pipe single-instance untested | Single-instance | Process-global + named-pipe; integration-only and flaky to automate. Covered by the manual QA checklist. |
| Chrome UI-CHK-1..6 true-render gates | Chrome | Manual Layer 4 smoke + screenshots at 100/125/150 % (`scripts/Test-UiSmoke.ps1`). |

## Per-area detail

### Navigation & allowlist — 2 met, 1 gap, 1 intentional-deviation
REQ-NAV-01/02 met: both windows consult `NavigationPolicy.IsAllowed` in `NavigationStarting`;
`MainWindow.Core_NewWindowRequested` keeps allowed targets in-window and opens the rest
externally. Allowlist precisely matches YouTube + Google-auth-on-any-TLD and rejects look-alikes.
Deviation + residual gap noted above.

### Popout lifecycle & Return — 8 met, 1 gap (now closed)
REQ-RETURN-01 met exactly: `sourceWasPlayingAtPopout` captured **before** pause
(`MainWindow.xaml.cs:307`); `LastKnownSeconds` nullable with `0` distinct from unknown; guards
against double-popout and single-player; failure path restores + resumes. The only gap was
missing automated coverage — closed by `ReturnPolicyTests` over the extracted `ReturnPolicy`.

### Settings & Profiles — 12 met, 1 partial, 2 untested
Atomic save (temp+flush+`File.Replace`), corruption quarantine (renamed, not lost), sanitize
ranges, schema versioning, duplicate-name overwrite prompt, graceful URL validation — all met
and largely tested. Partial/untested items are Phase-2 profile-field precedence (above).

### Window / Placement / DPI — 3 met, 1 partial, 1 untested, 1 gap
PerMonitorV2 manifest present; Win32 pixel-coordinate placement; never-restore-off-screen clamp
(now unit-tested via `PlacementMath`). Partial/gap = Phase-2 profile bounds; untested =
manual multi-DPI visual gate (Layer 4).

### Chrome & Visual identity — 5 met, 4 gap, 6 untested, 1 partial, 1 intentional-deviation
Color/shape tokens, window layouts, and structure all met. The gap/untested cluster is the
stale-screenshot chrome story (see "Confirmed bugs"). Now guarded by Layer 1 + Layer 3; pixel
gates by Layer 4.

### Fade / Opacity — 18 met, 1 intentional-deviation
Fade decision logic (`FadePolicy`) fully unit-tested; only the chrome strip fades (never the
WebView2 surface); hit-testing dropped only once fully faded (Q-8, no click-through);
persistence wired. Deviation = 2.5 s idle delay.

### Single-instance & lifecycle — 9 met, 1 gap
Named-mutex guard before any UI, named-pipe URL hand-off, one shared `CoreWebView2Environment` —
met. Gap = no automated test (integration-only; manual QA).

### Recovery / Errors / Logging — 10 met, 1 gap (now closed), 1 untested
WebView2-runtime-missing recovery panel, never-throw logging with size rotation, and URL
redaction — met. `RedactUrl` is now tested (`LoggingServiceTests`); the unhandled-exception
handler remains untested (recommended, lower value).
