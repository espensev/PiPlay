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
