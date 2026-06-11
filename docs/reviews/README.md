# Review Artifacts

This directory stores completed review artifacts and review inputs. These files are evidence and
provenance, not the current source of truth for product behavior.

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
