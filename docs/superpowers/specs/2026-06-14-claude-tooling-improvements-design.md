# PiPlay Claude tooling improvements — design

Date: 2026-06-14 · Status: proposed · Target: feature branch off `main` (single PR)

## Goals

Tighten four project-local Claude Code tooling surfaces so an agent (or contributor) working this repo
fails less and discovers less by trial-and-error. Each fix is small, evidence-backed, and independent:

- **G1** — the `run-piplay` skill is out of sync with its own scripts (two helpers exist but are
  undocumented, and the skill tells you Settings "is not exercised").
- **G2** — `/smart-test` mis-scopes dotnet (it appends `.cs` paths to `dotnet test`, which filters by
  `--filter`/`[Trait]`, not path); the limitation is named but no correct per-file command is offered.
- **G3** — WCAG contrast ratios for candidate hex pairs are hand-computed repeatedly during theme
  design (base palette *and* the Phase-B derived tokens of CON-1), with no runnable check that reuses
  the canonical formula.
- **G4** — the CI spec-check gate is only discoverable by pushing and opening a PR.

**Done** looks like: the `run-piplay` skill documents every script it ships and over-claims nothing;
`project.toml` carries a copy-pasteable per-file dotnet targeting recipe; a contrast report is runnable
on demand and is provably tied to the real gates; and the spec-check verdict is predictable locally.

**Not changing:** no product/runtime code under `src/PiPlay/**` (the only `tests/` change is one new
test file); no edits to the shared/global `smart-test` skill; no implementation of the Phase-B derived
theme tokens (G3 only *measures* candidate values); the deployed-Stable / never-test-build-output
discipline stands.

**Why one spec covers four items (the gated/non-gated split):** G1 and G2 edit only `.claude/`, which the
CI gate's `^(src|scripts|tests)/` path regex never matches — they need a design spec only by AGENTS.md
convention for tooling work. G3 adds a `tests/` file and G4 adds a `scripts/` file — both *are* gated,
so they need a dated spec to pass CI. This single document does double duty and ships in the same PR,
which is itself the dog-food case for G4.

## Requirements served

Tooling/docs only — no product `REQ-*`/`Q-*`. Motivated by: `docs/AGENTS.md` (design-spec-for-tooling
convention; the run-piplay UI-verification discipline), `.github/workflows/spec-check.yml` (the gate G4
mirrors), and `docs/reviews/2026-06-14-theme-v2-spec-eval.md` CON-1 (the repeated contrast computation
G3 serves).

## Acceptance criteria

- **G1:** `SKILL.md` documents `open-settings.ps1` (incl. that `SETTINGS|INVOKED-WITH-TIMEOUT` is a
  success, since the dialog is modal) and `capture-hwnd.ps1`; the "What this exercises / doesn't" line
  no longer lists opening Settings as un-exercised; the documented capture path **leads with
  `capture.ps1`** and does **not** claim `capture-hwnd.ps1` reliably raises a background window. The
  documented path is verified against a live PiPlay instance before the edit is finalized.
  `open-settings.ps1` is also **hardened**: an `IsEnabled` pre-check plus a typed
  `ElementNotEnabledException` catch make a disabled-button invoke report `SETTINGS|FAIL` instead of
  the success-coded `SETTINGS|INVOKED-WITH-TIMEOUT` — without this guard the doc's "timeout = success"
  rule would mislabel a real failure (the button is briefly disabled while a privacy action is awaiting).
- **G2:** `project.toml`'s KNOWN LIMITATION block contains a per-file recipe mapping a changed source
  file → its test class → a `--filter "FullyQualifiedName~<stem>"` command, with two worked examples
  from real files and a note that `~` is a greedy substring match.
- **G3:** `tests/PiPlay.Tests/ContrastReportTests.cs` exists, calls the existing internal
  `Wcag.ContrastRatio` (no reimplemented luminance math), prints each computed ratio, asserts
  `ratio >= floor`, and includes a pin `[Fact]` reproducing the published `3.43` reference; the full
  Lane A suite stays green; a recipe with the `--logger "console;verbosity=detailed"` flag is in
  `project.toml`.
- **G4:** `scripts/Preflight-SpecGate.ps1` exists, unions committed/staged/unstaged/untracked changes,
  replays the gate with the **corrected** regexes (below), and prints PASS/FAIL matching what CI would
  decide. Running it on this very PR returns **PASS (dated spec present)**.

## Settled decisions

1. **G2 is a comment/recipe, not a new skill** — the shared `smart-test` skill is read-only/global, and
   a parallel PiPlay dotnet selector would duplicate logic `/qa run <lane>` already owns and drift.
2. **G3 is a test utility + recipe, not a skill** — the need (compute a candidate ratio during design)
   is modest; a `[Theory]` in the existing test assembly reuses `Wcag.cs` with zero new project.
3. **G3 reuses, never reimplements, the formula** — `ContrastReportTests` lives in `namespace
   PiPlay.Tests` and calls the internal `Wcag.ContrastRatio` directly (no `InternalsVisibleTo`); a
   reimplementation could emit a false "WCAG-safe" verdict, which is worse than no tool.
4. **G3 CON-1 derived-token rows ship commented** (not `[InlineData]`, not `[Theory(Skip)]`) — Phase-B
   mixes don't exist in `src` yet; live `[InlineData]` would red CI before Phase B lands the fix.
5. **G4 is a script, not a git hook** — a hook would silently block commits; a preflight a person/agent
   runs is honest and matches the other `scripts/*.ps1` dev tools.
6. **G4 ships no pin-test** — a `tests/` pin would itself trip the gate it predicts, and a literal
   regex-duplication test only proves the script matches itself. The verbatim SOURCE-OF-TRUTH header
   comment naming `spec-check.yml` is the lighter, honest guard.
7. **G4 regexes are corrected from a naive translation** — grep `-E` is case-*sensitive*, so the gated
   and dated-spec checks use PowerShell `-cmatch` (a default `-match` would falsely PASS a `-DESIGN.MD`
   file that CI rejects); the exception check uses `(?mi)^[^\S\r\n]*Spec-Exception:[^\S\r\n]*\S.*$`
   (a naive `\s*\S` lets `\s` consume the CRLF and falsely matches a whitespace-only reason). Both
   corrected forms were re-tested against grep on the full case matrix and agree row-for-row.
8. **G1 leads with `capture.ps1` for the Settings shot** — `capture-hwnd.ps1` raises only via
   `SetForegroundWindow`, which `SKILL.md`'s own Gotchas say is blocked from a background process; it is
   documented as the targeted-by-HWND variant, not as reliable raising.
9. **dotnet targeting idiom rule (threads G2↔G3):** use the bare type *stem* (`~Foo`) when mapping a
   source file to its test family (matches the repo's existing specs); use the *full class name*
   (`~ContrastReportTests`) when targeting one specific class on demand.

## Non-goals / out of scope

- No edits to the shared global `smart-test` `SKILL.md` (or any `~/.claude/skills/` asset).
- No Phase-B derived-token implementation in `src` (G3 measures candidates only).
- No `git fetch`/`origin` diffing in the preflight (stays offline/read-only); no mechanical
  regex-sync enforcement between the preflight and `spec-check.yml`.
- No `VERSION`/`BUILD_NUMBER` bump, publish, deploy, or tag (none of this is runtime behavior).
- G1 does not make Pin, Fade, return-to-source, profiles, or the controls *inside* Settings scripted —
  those stay manual.

## Testing approach

- **G1 (manual, live):** verify `launch-and-capture.ps1 -NoPopout` → `open-settings.ps1` → capture
  against a running PiPlay; confirm the PNG shows the Settings dialog (not the main window, not an
  `OCCLUDED` frame) before finalizing the prose. This is the run-piplay change-verification loop on the
  Debug build, not a release/QA pass.
- **G2 (none / by inspection):** comment-only; spot-check that the worked example command runs and scopes.
- **G3 (logic lane):** the new file runs in `Category=Logic` as part of the full Lane A suite; confirm
  `dotnet test PiPlay.sln --configuration Debug` stays green, and that
  `Math.Round(Wcag.ContrastRatio("#FFFFFF","#E45D75"), 2) == 3.43` on first run (banker's-rounding
  edge) so the pin is trustworthy.
- **G4 (dog-food):** run `pwsh -File scripts\Preflight-SpecGate.ps1` on this branch; expected verdict
  **PASS (dated design spec present)** — the spec file this very document becomes. Also confirm a FAIL
  path by temporarily pointing it at a gated change with no spec (or trust the grep-parity matrix
  already verified in the review).
- **Whole bundle:** `dotnet test PiPlay.sln --configuration Debug` and
  `.\Build-PiPlay.ps1 -Stage Build -NoVersionBump -NoBuildNumberBump` both green before the PR.

## Changes by file

| File | Change |
|---|---|
| `.claude/skills/run-piplay/SKILL.md` | **G1.** Insert a "## 4. Drive Settings (optional)" step documenting `open-settings.ps1` (modal-timeout = success) and `capture-hwnd.ps1` (targeted variant; capture *leads* with `capture.ps1`); correct the "What this exercises / doesn't" line to move opening Settings to scriptable while keeping Pin/Fade/return/profiles/in-Settings-controls manual. Live-verified before finalizing. |
| `.claude/skills/run-piplay/scripts/open-settings.ps1` | **G1 (harden).** Add an `IsEnabled` pre-check and a typed `ElementNotEnabledException` catch so invoking a disabled `SettingsButton` (privacy-action window) reports `SETTINGS|FAIL`, not a false success-coded `SETTINGS|INVOKED-WITH-TIMEOUT`. Keeps the SKILL.md "timeout = success" contract honest. (Not a gated path — `.claude/…/scripts/` ≠ top-level `scripts/`.) |
| `.claude/skills/project.toml` | **G2 + G3** (two distinct edits to one file). G2: expand the `[commands]` KNOWN LIMITATION comment with the per-file `--filter "FullyQualifiedName~<stem>"` recipe + worked examples + greedy-stem note. G3: add a recipe comment under `[qa.lanes]` for the on-demand contrast report (with the mandatory `--logger` flag). |
| `tests/PiPlay.Tests/ContrastReportTests.cs` | **G3 (already shipped in Theme-V2 PR1 as CON-1 forward-prep — NOT in this PR's diff).** `[Theory]` contrast report (Category=Logic) calling `Wcag.ContrastRatio`, printing ratios via `ITestOutputHelper`, asserting `>= floor`; `[Fact]` pin reproducing `3.43` and re-checking the catalog's dark-text-on-steel pair; CON-1 rows shipped commented. This PR carries only G3's `project.toml` recipe half. |
| `scripts/Preflight-SpecGate.ps1` | **G4, new.** Replays `spec-check.yml` locally over committed/staged/unstaged/untracked changes with the corrected regexes; SOURCE-OF-TRUTH header naming the workflow; `-BaseBranch`/`-PrBody`/`-PrBodyFile`/`-Quiet`; mirrors the gate's decision order and fail-closed intent. |
| `docs/superpowers/specs/2026-06-14-claude-tooling-improvements-design.md` | This spec (satisfies the gate for G3/G4 and the convention for G1/G2). |

## Docs & changelog impact

Tooling-only; **no `docs/CHANGELOG.md` entry** (changelog is for user-visible app changes — none here)
and **no version/build bump**. No ADR (no architecture decision changes). Optionally, a one-line pointer
to `Preflight-SpecGate.ps1` could be added to `docs/Feature_Workflow.md` step 5 — deferred unless the
owner wants the preflight surfaced there (see Unresolved).

## Unresolved decisions

These are implementation preconditions and cheap owner calls, not design unknowns:

- **Precondition (G1):** live-verify the `open-settings.ps1` → capture path and decide whether the
  documented capture leads with `capture.ps1` (all windows, passive) or `capture-hwnd.ps1 -Hwnd`; do
  not claim `capture-hwnd.ps1` reliably raises.
- **Precondition (G4):** ship the **corrected** regexes verbatim (decision 7); do not regress to
  `-match`/`\s*`.
- **Precondition (G3):** confirm `3.43` on first run; keep CON-1 rows commented.
- **Owner call (a):** is G1's Settings step a numbered `## 4.` step or a Gotchas sub-note? (Drafted as
  a numbered step to match `## 1/2/3`.)
- **Owner call (b):** does G3's report ride `Category=Logic` (in the default gate, cheap, filterable —
  current choice) or get its own excluded Category? 
- **Owner call (c):** add the `Preflight-SpecGate.ps1` pointer to `Feature_Workflow.md` step 5, or leave
  it discoverable in `scripts/`?
