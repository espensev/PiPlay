# Working in PiPlay

## Authority

- `PiPlay_Product_Engineering_Spec.md` is the sole product and architecture contract. Preserve its stable Q/REQ/ADR IDs when code still cites them.
- Actual behavior comes from current code, tests, configuration, script help, and reproducible output. Existing prose is never evidence by itself.
- `README.md` owns build, verification, data, and release commands. `CHANGELOG.md` contains only current unreleased user-visible changes.

## Work

- Preserve unrelated dirty files and active processes. Do not publish, deploy, stop a running Stable copy, tag, or perform interactive UI work unless the user asks.
- Derive repository paths from `$PSScriptRoot` or the active checkout. Use `$env:LOCALAPPDATA`, `PIPLAY_DATA_ROOT`, and `PIPLAY_STABLE_ROOT`; never add a user-specific or deployment drive path to source or docs.
- Put behavioral changes behind deterministic tests. Run focused tests during development and `pwsh -NoProfile -File .\scripts\Test-LocalCI.ps1` at the meaningful completion boundary.
- Do not require a design file, temporary plan, requirement-ID ceremony, repeated human review, or manual test for routine changes. Keep transient coordination outside the maintained document set.
- Reserve human testing for one end-of-cycle GitHub desk-candidate acceptance pass, limited to **Unresolved verification** in the product contract; never require it per change.

## Documentation

Retain a sentence only when it has distinct operational or product value and direct evidence. Cite nearby with a repository path plus symbol/test/config key, or a current official URL. Remove speculation, status narrative, duplicate tables, copied citations, machine paths, and commands not exercised by implementation or help.

After documentation changes, run:

```powershell
pwsh -NoProfile -File .\scripts\Test-Documentation.ps1
```

The full local gate also runs this check. It rejects conflict markers, broken local Markdown links, broken source references to repository documents, literal drive-root paths in Markdown, and literal user-profile paths in maintained source.

For YouTube page code, preserve Q-1/Q-3/Q-5/Q-8, current-document source/token checks, the closed native action set, and ad fail-closed behavior. For settings, keep atomic same-volume replacement. For release work, keep exact-source provenance, staged rollback, and diagnostic evidence distinct from release evidence; details and sources are canonical in the product contract.
