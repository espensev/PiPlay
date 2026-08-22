## Change

State the user-visible or engineering outcome and its evidence.

## Automated verification

Record the focused checks and:

```powershell
pwsh -NoProfile -File .\scripts\Test-LocalCI.ps1
```

## Unresolved verification

List only release-only live/rendering checks or `None`. Do not request routine manual retesting already covered by automation.
