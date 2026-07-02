# PiPlay doc clarity pruning audit - 2026-07-02

## Method

This was a subagent-split docs audit. Each doc or major segment was judged by two questions:

1. Would code, function, QA, or review quality degrade if this were gone?
2. Does the text make implementation intent clearer, or would removing/merging it make intent clearer?

## Keep As Authority

Keep the active product and workflow surfaces: `docs/README.md`, `README.md`, `CLAUDE.md`, `docs/AGENTS.md`, `docs/PiPlay_Product_Engineering_Spec.md`, `docs/SPEC_GAPS_AND_OWNERSHIP.md`, `docs/Feature_Workflow.md`, `docs/CHANGELOG.md`, `docs/QA_Checklist.md`, `docs/adr/`, `docs/YouTube_Compliance.md`, `docs/Data_and_Privacy_Map.md`, `tests/README.md`, `docs/evidence/README.md`, and `docs/assets/README.md`.

Keep dated change-pass specs/plans only when they still explain a current code boundary, owner decision, or workflow example that active docs would otherwise lose.

## Removed Or Pruned

- Removed the ignored root review drops. They were uncurated local packets, already covered by retained review/docs surfaces, and made root intent noisier.
- Removed the non-PiPlay imported spec-review artifact because it reviewed TerminalHQ/THQ, not PiPlay. Repeated disclaimers were less clear than pruning the artifact.
- Removed the recursive doc-pruning spec/plan after folding the retention policy into this repository's documentation index.

## Aligned In Place

- `docs/Theme_Preset_Differences.md` now matches current `ThemeCatalog.cs` for default accents, Soft Glass opacity, alpha bytes, and the deduped corner-style vocabulary.
- `docs/PiPlay_Product_Engineering_Spec.md` now uses the current accent palette values.
- `docs/SPEC_GAPS_AND_OWNERSHIP.md` now matches Soft Glass `0.97` / `0.90` defaults.
- `docs/adr/0003-webview2-evergreen.md` now accounts for ADR-0007 Stable portable data roots.
- `src/PiPlay/Services/PrivacyService.cs` no longer points to a deleted privacy-polish spec; it points readers to the durable product spec and QA checklist.

## Review Chain Pruning

The June 25 b25 review chain was over-retained after its findings were implemented or folded forward. Keeping each intermediate verdict made later review work slower and less clear. The retained b25 review status is now the June 26 address-pass and spec-conformance pair, with the dependency audit kept only as dated dependency evidence.

The specific root-review ignore entries were removed from `.gitignore`; future raw root review packets should show up in status so they can be promoted into `docs/reviews/` or deleted deliberately.
