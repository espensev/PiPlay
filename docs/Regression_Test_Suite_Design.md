# PiPlay — Regression Test Suite Design

- **Status:** Approved (brainstorming) — 2026-05-31
- **Owner:** Espen / SevIQ
- **Scope:** A layered regression-test suite + a spec-conformance review, targeting the
  *UI/markup* regression class (e.g. last night's `UseLayoutRounding` "rounding = 0" text
  clipping) as well as logic regressions, on top of the existing pure-logic xUnit tests.

## 1. Motivation

The current tests (`FadePolicyTests`, `NavigationPolicyTests`, `SettingsServiceTests`,
`YouTubeUrlHelperTests`) are pure-logic xUnit and cannot see anything in XAML/markup or in the
WPF runtime resource graph. The regression that shipped and had to be hot-fixed —
`UseLayoutRounding="True"` clipping the URL/address-bar text to a thin band at fractional DPI
(fixed by setting it `False` on both windows, UI-CHK-5) — is exactly this blind spot: a single
markup property silently flipping.

Several **burned-in invariants** live in markup/theme. Each has a concrete failure mode, and
several have already bitten:

| Invariant | Location | If it flips / breaks | Burned before |
|---|---|---|---|
| `UseLayoutRounding="False"` | both windows | URL text clipped to a band at fractional DPI | ✅ (this fix) |
| `AllowsTransparency="False"` | both windows | WebView2 airspace breaks — black/empty video | hard constraint (ADR-0004) |
| `WindowStyle="None"` + `WindowChrome CornerRadius="0"` | both windows | chrome geometry / native resize regressions | — |
| `SnapsToDevicePixels="True"` | both windows | blurry chrome at fractional DPI | — |
| Every `{StaticResource}` resolves | App + both windows + ControlStyles | runtime `ResourceReferenceKeyNotFoundException` crash | latent |
| Icon-font fallback on glyph controls | `IconButton`/`PinToggle`/inline glyphs | `.notdef` empty boxes | ✅ (REQ-UI-02) |
| Theme contrast ≥ 4.5:1 | `Colors.xaml` tokens | unreadable dark UI | ✅ (UI-CHK-5) |

## 2. Goals / non-goals

**Goals**
- A fast, deterministic layer that re-catches the markup regression class in `dotnet test`,
  with **zero** dependency on WebView2, network, or a visible desktop.
- Expanded logic coverage closing spec gaps (nav allowlist, URL/playlist parsing, settings
  recovery, window placement clamping, REQ-RETURN-01 resume).
- A runtime WPF layer proving every resource resolves and the burned-in DP values hold.
- A manual end-to-end smoke lane (UIA + screenshots at fractional DPI) reusing the existing
  PowerShell UI-Automation approach, run as a release gate (not in `dotnet test`).
- A committed spec-conformance review mapping each requirement to its implementation + test.

**Non-goals**
- Golden-image pixel-equality snapshots as the primary mechanism (too fragile across
  machines/fonts/OS updates). Allowed only as an opt-in aid in the manual lane.
- Testing real YouTube playback behavior in `dotnet test`.
- CI wiring was out of scope for the original suite design. It is now handled by
  `.github/workflows/ci.yml`, which runs Lane A plus the non-mutating build gate on Windows.

## 3. Architecture

Layered pyramid, two run-lanes:

```
Lane A — dotnet test (headless, deterministic, fast)
  Layer 1  XAML markup invariants      [Trait Category=Markup]   parse .xaml as XML, no WPF runtime
  Layer 2  Expanded logic / unit       [Trait Category=Logic]    pure services
  Layer 3  Live WPF on STA             [Trait Category=Wpf]      Application + windows, Xunit.StaFact

Lane B — manual release gate (real desktop + network)
  Layer 4  E2E UIA + screenshot smoke  scripts\Test-UiSmoke.ps1  drives the built exe
```

- Layers 1–3 live in the **existing `PiPlay.Tests`** project, separated by xUnit `[Trait]`
  so a lane can be filtered (`dotnet test --filter Category=Markup`). Decision: one project,
  not a separate `PiPlay.UiTests`, unless Layer 3 proves slow enough to warrant splitting.
- Layer 4 is a `pwsh` harness in `scripts/`, documented as manual; it is **not** referenced
  by the solution's test run.
- The spec review is produced by a multi-agent fan-out (one agent per spec area) and
  synthesized into `docs/Spec_Conformance_Review.md`; each testable gap becomes a test.

New test dependency: `Xunit.StaFact` (provides `[StaFact]`/`[StaTheory]` for STA WPF tests).

## 4. Layers in detail

### Layer 1 — XAML markup invariants (`tests/PiPlay.Tests/Ui/XamlInvariantTests.cs`)

Parses the `.xaml` files as XML via `System.Xml.Linq` (no WPF runtime; robust and instant).
A single source-of-truth `XamlInvariants` registry drives the assertions so adding a guard is
one line. Asserts, per window (`MainWindow.xaml`, `PlayerWindow.xaml`):

- Window attributes: `AllowsTransparency=False`, `UseLayoutRounding=False`, `WindowStyle=None`,
  `SnapsToDevicePixels=True`.
- `WindowChrome`: present, `CornerRadius=0`, `GlassFrameThickness=0`,
  `UseAeroCaptionButtons=False`, expected `ResizeBorderThickness`, and the per-window
  `CaptionHeight` (MainWindow `42`, PlayerWindow `0`).
- Required `x:Name` controls present (the set the code-behind resolves via generated fields /
  `FindName`): MainWindow — `Browser, UrlBox, ProfilesCombo, PinToggle, PinnedHint,
  PopOutButton, BackButton, ReloadButton, HomeButton, SaveProfileButton, MinimizeButton,
  MaximizeButton, CloseButton, SourcePlaceholder, RuntimeErrorPanel, RuntimeErrorText`;
  PlayerWindow — `ChromeStrip, FadeToggle, PinToggle, CloseButton, Player`.
- Glyph controls carry the icon-font (`Segoe Fluent Icons, Segoe MDL2 Assets`) either via the
  `IconButton`/`PinToggle`/`CloseIconButton` style or inline `FontFamily` — no inline glyph
  `TextBlock` may omit it (guards the `.notdef` box regression).
- Tooltips present on the spec-required controls (caption buttons, nav, URL box, Pin, Pop out,
  profiles).

Plus theme-file checks (`Colors.xaml`, `ControlStyles.xaml`, `App.xaml`):

- **Resource integrity:** collect every `{StaticResource Key}` referenced across `App.xaml`,
  both windows, and `ControlStyles.xaml`; assert each key is defined in `Colors.xaml` or
  `ControlStyles.xaml`. (Catches a renamed/deleted token before it crashes at runtime.)
- **Contrast:** parse the hex tokens and assert WCAG 2.x ratios from the *specified* colors:
  `TextPrimary`-on-`SurfaceRaised` (URL box, UI-CHK-5), `TextPrimary`-on-`AppBackground`,
  `TextPrimary`-on-`SurfaceBase`, `TextSecondary`-on-`SurfaceBase` ≥ 4.5:1; and the
  `AccentButton` foreground (`#FF06141A`) on `AccentCyan` ≥ 4.5:1.

### Layer 2 — Expanded logic / unit tests (extend existing files)

Close spec-coverage gaps in the pure services:

- `YouTubeUrlHelper`: `watch?v=`, `youtu.be/…`, `watch?v=X&list=PL…` (keep video + playlist),
  `playlist?list=PL…`, `list=RD…`/radio/mix fallback to single video, timestamp (`t=`)
  parsing, `BuildWatchUrl` with/without seconds, malformed/non-YouTube input.
- `NavigationPolicy`: YouTube allowed on both surfaces; Google sign-in (incl. regional
  `accounts.google.no`) allowed on **Source only**, blocked on **Player**; arbitrary site →
  external on both. (Locks in the regional-sign-in fix.)
- `SettingsService`: round-trip; missing file → defaults; corrupt JSON → bad file renamed
  (not lost) + defaults; atomic save (temp + rename) leaves no partial file.
- `WindowPlacementService`: clamp an off-screen / removed-monitor placement back onto a
  visible monitor; capture/restore round-trip. (Seam if it reads live screen geometry.)
- `ProfileService`: `ValidateUrl`, `Exists`, `Save`/overwrite semantics.
- **REQ-RETURN-01** resume decision (see seam 2): `seek` vs `seek+play` vs `play` for the
  matrix of `wasPlaying ∈ {true,false}` × `LastKnownSeconds ∈ {null, 0, n}` — `0` is valid.

### Layer 3 — Live WPF on STA (`tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs`, `Xunit.StaFact`)

A shared STA fixture boots an `Application` with the real merged resource dictionaries (or
instantiates `PiPlay.App`). Then:

- Constructing `MainWindow` and `PlayerWindow` **does not throw** — the runtime counterpart to
  Layer 1's static resource check: proves every `{StaticResource}` resolves and every template
  compiles. (WebView2 stays uninitialized because it's created in `Loaded`, which we never
  fire — windows are constructed, never `Show()`n.)
- Resolved DependencyProperty values hold: `UseLayoutRounding == false`,
  `WindowChrome.GetWindowChrome(w).CornerRadius == 0`, `AllowsTransparency == false`,
  `WindowStyle == None`.
- `FindName` returns non-null controls of the expected types (e.g. `UrlBox` is a `TextBox`
  styled `DarkTextBox`, `PopOutButton` a `Button`).
- Representative styled controls (`DarkTextBox`, `IconButton`) get their template applied
  (`Template != null`, `PART_ContentHost` found) and resolved brushes equal the theme tokens.
- **DPI characterization render test:** build a `DarkTextBox` with sample text in a host under
  `UseLayoutRounding=True` vs `False`, `Measure`/`Arrange`, render via `RenderTargetBitmap` at
  144 DPI (150 %), and assert the inked text rows are not collapsed to a band when `False`
  (documents *why* rounding must be off; the affirmative guard for the original bug).

### Layer 4 — E2E UIA + screenshot smoke (`scripts/Test-UiSmoke.ps1`, manual)

Launches the built exe, waits for the Source Window, asserts the key named
`AutomationElement`s exist (`Pop out video` button, URL box, caption buttons), captures
screenshots at 100/125/150 % DPI into `docs/evidence/`, and optionally pixel-checks the URL
band height (no clip). Documented as a release gate alongside `QA_Checklist.md` §8; not part
of `dotnet test`.

## 5. Test-isolation seams (the only app-code changes; pre-approved)

1. **`AppPaths` data-root override** — honor a `PIPLAY_DATA_ROOT` environment variable, else
   the current `%LOCALAPPDATA%\PiPlay`. ~3 lines, non-behavioral in production. Lets Layers 2
   and 3 run isolated from the real user profile.
2. **Extract `ReturnPolicy`** — a pure decision function (mirrors `FadePolicy`) for the
   return-resume choice currently inline in `MainWindow.Player_OnClosed`, making REQ-RETURN-01
   unit-testable without WebView2. `MainWindow` calls into it.

Any **other** app-code change implied by a review finding follows the agreed protocol: write a
failing test that proves the bug, record it in the review, and wait for approval before fixing.

## 6. Deliverables

- `docs/Spec_Conformance_Review.md` — requirement-by-requirement (REQ-*, Q-*, UI-CHK-*) status,
  evidence, and gaps.
- Layers 1–3 in `PiPlay.Tests` (trait-separated); `Xunit.StaFact` added.
- `scripts/Test-UiSmoke.ps1` (Layer 4) + a short run-doc (`tests/README.md`).
- The two seams above.
- Failing tests for any confirmed gaps, paused for approval before fixes.

## 7. Risks / open questions

- **STA fixture cost:** if Layer 3 noticeably slows `dotnet test`, split it into a separate
  `PiPlay.UiTests` project filtered out of the default fast run.
- **`RenderTargetBitmap` determinism:** font hinting differs across machines; the DPI test
  asserts *structural* properties (inked-row coverage / no band collapse), never exact pixels.
- **`WindowPlacementService` seam:** if it binds to live `System.Windows.SystemParameters` /
  screen geometry, extract the clamp math into a pure function taking monitor rects.
