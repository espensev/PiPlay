## Summary

<!-- What changes and why, in 2-4 sentences. -->

## Design spec

<!--
Link the dated spec under docs/superpowers/specs/, e.g.
docs/superpowers/specs/2026-06-06-<topic>-design.md

No spec because the change is trivial/urgent? The "Require design spec" check honors an explicit
override — uncomment and fill in the next line (it must be its own line in this description):
-->
<!-- Spec-Exception: <why this change needs no design spec> -->

## Requirements served

<!-- Requirement / quality IDs, e.g. REQ-UI-01, REQ-PROFILE-01, Q-6. "None (tooling/docs)" is fine. -->

## Acceptance criteria

<!-- Observable conditions that make this PR "done". -->
- [ ]

## Verification

<!-- Commands run and their results. Mark anything not run, and anything only verifiable live. -->

```powershell
dotnet test PiPlay.sln --configuration Debug
.\Build-PiPlay.ps1 -Stage Build -NoVersionBump -NoBuildNumberBump
```

## Docs / changelog impact

- [ ] `docs/CHANGELOG.md` updated (user-visible change) — or N/A
- [ ] Relevant `docs/` / ADRs updated — or N/A

## Manual QA evidence

<!-- For release-candidate or visual work: evidence paths under docs/evidence/. Otherwise "N/A". -->
