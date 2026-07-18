# Review Artifacts

This directory is the lane for retained review artifacts and review inputs. Keep it sparse: when a
review's useful facts are folded into active docs or the matching dated change-pass record, prune the
raw artifact instead of keeping another stale report.

Use active docs for current decisions:

- Product behavior: `docs/PiPlay_Product_Engineering_Spec.md`
- Release notes: `docs/CHANGELOG.md`
- Manual release gates: `docs/QA_Checklist.md`
- Durable decisions: `docs/adr/`
- Change-pass contracts: `docs/superpowers/specs/`
- Change-pass plans and worklogs: `docs/superpowers/plans/`, `docs/superpowers/worklog/`

When a review finding is accepted into implementation, record its disposition in the matching dated
design spec or follow-up PR. Retain the original review only when it still carries unique provenance or
an open item; otherwise fold its useful facts forward and prune it.

Prune imported reviews that turn out to be for another repository instead of retaining them with repeated disclaimers; those make PiPlay intent less clear and do not improve review quality.

## Retained artifacts

| Artifact | Use |
|---|---|
| [review-2026-07-15-source-window-return-navigation.md](review-2026-07-15-source-window-return-navigation.md) | Pre-fix Source return/navigation review. Its remediation landed later; current runtime disposition lives in the [interim audit report](../../.audit/deep-audit/piplay-runtime-2026-07-16/REPORT.md). |
| [review-2026-07-15-deep-technical-audit.md](review-2026-07-15-deep-technical-audit.md) | Historical pre-remediation Focused/Popout audit. Do not treat its diagnostic-build verdict as current Stable evidence; use the interim audit report above. |
| [review-2026-07-15-local-ci-runner-audit-map.md](review-2026-07-15-local-ci-runner-audit-map.md) | Executed local-CI review; Notes 1 and 2 were resolved in the same pass, while its remaining notes are explicitly non-blocking/forward-looking. |
| [review-2026-07-14-accent-reach-dial-and-routing.md](review-2026-07-14-accent-reach-dial-and-routing.md) | Pre-fix audit of the reach-default and Settings profile/global routing regressions; disposition lives in the matching follow-up design. |
| [2026-07-14-doc-cleanup-audit.md](2026-07-14-doc-cleanup-audit.md) | Latest full pruning record: what the v0.8.0-b29 pass repaired, pruned, and parked, and why. Read this before pruning again. |
| [2026-07-02-doc-clarity-pruning-audit.md](2026-07-02-doc-clarity-pruning-audit.md) | The pruning **method** (the two questions) and the first pass's decisions. |
| [review-2026-07-14-efficiency-and-customization.md](review-2026-07-14-efficiency-and-customization.md) | The efficiency/customization + release-pipeline findings. All shipped in v0.7.3 (b28). |
| [review-2026-07-11-profile-selector-frame.md](review-2026-07-11-profile-selector-frame.md) | Retained because its **low-severity follow-ups were never individually closed** — it is their only carrier. |
| [review-2026-07-11-profile-identity-rail-and-accent-wash.md](review-2026-07-11-profile-identity-rail-and-accent-wash.md) | The rail/wash address pass. Its contrast contract and open visual-QA gate are now in the product spec and `QA_Checklist.md`. |
| [2026-06-25-claude-dependency-audit.md](2026-06-25-claude-dependency-audit.md) | Dated dependency evidence only; refresh before acting on package freshness. |

### Historical b25 records

The June 26 pair below described **b25** state and was pinned as "current" at the time. It is kept only
as the record of what was checked then; do **not** read it as today's verdict. Its live gate — runtime
Stable/WebView2 smoke — is carried by `SPEC_GAPS_AND_OWNERSHIP.md` and `QA_Checklist.md`.

| Artifact | Use |
|---|---|
| [2026-06-26-piplay-b25-address-pass-review.md](2026-06-26-piplay-b25-address-pass-review.md) | Historical b25 address-pass status. |
| [2026-06-26-piplay-b25-spec-conformance-review.md](2026-06-26-piplay-b25-spec-conformance-review.md) | Historical b25 spec/code conformance. |

The older June 25 b25 triage, arbitration, package, and current-work reviews were folded into active docs and then pruned. Re-reading every intermediate verdict makes intent less clear without improving code or review quality.

> Note: these reviews cite the `run-piplay` skill, and the b25 pair cites design specs that have since
> been pruned. Both are correct as a record of what existed at review time; neither is actionable today.
