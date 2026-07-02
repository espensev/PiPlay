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

When a review finding is accepted into implementation, record its disposition in the matching
dated design spec or follow-up PR. Keep the original review here so future audits can understand
what was checked without mistaking it for today's verdict.

Prune imported reviews that turn out to be for another repository instead of retaining them with repeated disclaimers; those make PiPlay intent less clear and do not improve review quality.

## Retained B25 Review Status

Use the June 26 review pair for current b25 state:

| Artifact | Use |
|---|---|
| [2026-06-26-piplay-b25-address-pass-review.md](2026-06-26-piplay-b25-address-pass-review.md) | Current address-pass status: headless gate green, runtime Stable/WebView2 smoke still required. |
| [2026-06-26-piplay-b25-spec-conformance-review.md](2026-06-26-piplay-b25-spec-conformance-review.md) | Current spec/code conformance review and remaining runtime evidence boundary. |
| [2026-06-25-claude-dependency-audit.md](2026-06-25-claude-dependency-audit.md) | Dated dependency evidence only; refresh before acting on package freshness. |

The older June 25 b25 triage, arbitration, package, and current-work reviews were folded into active docs, this retained pair, or the b25 follow-up plan and then pruned. Re-reading every intermediate verdict makes intent less clear without improving code or review quality.
