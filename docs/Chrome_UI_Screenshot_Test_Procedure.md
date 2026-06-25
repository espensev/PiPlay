# Chrome UI Screenshot Test Procedure

Use this for the manual visual gate in `QA_Checklist.md` section 8. The old 2026-05-30
one-off findings and screenshots were folded into the product spec, regression tests, and this
procedure; raw review evidence has been pruned.

## Test Target

Run against the deployed Stable copy only:

```powershell
.\scripts\Verify-StableDeploy.ps1
.\scripts\Test-UiSmoke.ps1 -ExePath 'E:\Dev_test_implemenations\PiPlay\PiPlay.exe'
```

Do not use repo build output for human/manual QA. Record the Stable identity printed by
`Verify-StableDeploy.ps1` in the evidence note.

## Capture Scope

Capture only build-specific evidence that is worth retaining:

- Source Window base chrome after launch/navigation.
- Back/Reload/Home/URL/Profiles/Pin/Auto/Pop out controls with visible glyphs and readable text.
- Tooltip on a toolbar or caption button.
- Profiles dropdown open.
- Settings Appearance surface.
- Popout Player chrome, including Pin/Fade/Close controls.
- Any failing or questionable state.

Use build-specific names under `docs/evidence/`, for example
`v0.6.0-b22-main-chrome.png`, not generic `chrome-current-*` names that go stale.

## Pass Criteria

- All popups and tooltips are dark themed.
- Icon glyphs render; no `.notdef` boxes.
- URL/search text is readable and not clipped at fractional DPI.
- Caption/tool buttons keep hover/active/disabled colors.
- Text fits inside buttons and controls.
- The Popout Player remains interactable; no click-through behavior.
- Evidence notes clearly distinguish pass, fail, blocked, and not-run rows.

## Procedure

1. Verify Stable deploy identity and record version/build/commit.
2. Launch PiPlay from the deployed Stable path.
3. Capture the base Source Window state.
4. Exercise toolbar tooltips and the Profiles dropdown.
5. Open Settings and capture Appearance.
6. Open a YouTube watch page where allowed, start Video Popout, and capture the Popout Player.
7. If a state needs user credentials or account-specific content, mark it blocked instead of faking it.
8. Write a short evidence note under `docs/evidence/` when the screenshots are release evidence.

## Evidence Note Template

```text
Build:
Date:
Tester:
Stable verification:

| Area | Result | Evidence | Notes |
|---|---|---|---|
| Source Window chrome | pass/fail/blocked | docs/evidence/...png | |
| Tooltips/dropdowns | pass/fail/blocked | docs/evidence/...png | |
| Settings Appearance | pass/fail/blocked | docs/evidence/...png | |
| Popout Player chrome | pass/fail/blocked | docs/evidence/...png | |
```
