# Working in PiPlay

## Canonical surfaces

- `PiPlay_Product_Engineering_Spec.md` owns product behavior and `Q-*`/`REQ-*` requirements.
- `DECISIONS.md` owns accepted architecture; supersede an ADR instead of silently contradicting it.
- `Theme_Preset_Differences.md`, `Data_and_Privacy_Map.md`, and `YouTube_Compliance.md` own their named detail.
- `SPEC_GAPS_AND_OWNERSHIP.md` contains verified open work only. Git history contains shipped history.
- `CHANGELOG.md` contains shipped user-visible notes and is packaged by the release build.

Do not create a design spec, plan, worklog, manual approval, or status document merely to pass a process gate. Put durable facts in the canonical surface they belong to and let source, tests, and release scripts verify them.

## Product language

| Term | Meaning |
|---|---|
| Video Popout | Move current YouTube playback to the floating player. |
| Popout Player | Floating borderless playback window. |
| Source Window | Main PiPlay browser window. |
| Source Placeholder | Near-black surface shown while playback is popped out. |
| Pin | Keep the active surface topmost. |
| Fade | Idle/hover chrome fading; never click-through. |
| Auto | Automatic `/watch` popout; off by default. |

Use **Pop out video**, **Bring video back**, and **Show Popout** in user-facing copy. `MainWindow`, `PlayerWindow`, `Detach`, and `fake PiP` are internal names.

## Boundaries that code and tests must preserve

- Windows WPF on `net10.0-windows`; no trimming, NativeAOT, or single-file publish (`src/PiPlay/PiPlay.csproj`).
- One Popout Player. No media downloading, ad/monetization changes, restriction bypass, click-through, transparent WebView, credential inspection, or telemetry.
- All YouTube JavaScript belongs in `YouTubeDomBridge`. Host protocols use exact schemas, versions, nonces/document tokens, trusted sources, and closed action sets.
- Settings writes are atomic: flush the temporary file, then same-volume `File.Move(..., overwrite: true)` or `File.Replace` (`SettingsService`).
- `PerMonitorV2` is declared in `src/PiPlay/app.manifest`; source and Popout minimums are defined by `PlaybackModePolicy`.
- Logs are local and bounded; never log cookies, authorization headers, credential URLs, secrets, or unsanitized search text (`LoggingService`).

## Verification

The automated gate is:

```powershell
pwsh -NoProfile -File .\scripts\Test-LocalCI.ps1
```

It owns restore, tests, temporary test data, Node enforcement, and the non-mutating Release build. Stable deployment and manifest verification use `PIPLAY_STABLE_ROOT`; manual testing is only the final end-user check against that verified copy. Do not hardcode a user or machine path in documentation.
