# Working in PiPlay

## Authority and change records

- `PiPlay_Product_Engineering_Spec.md` owns product behavior and requirement IDs.
- `DECISIONS.md` owns architecture decisions. Add or supersede a stable decision ID instead of silently contradicting one.
- `SPEC_GAPS_AND_OWNERSHIP.md` owns unresolved work and code boundaries.
- Non-trivial code changes require `docs/superpowers/specs/YYYY-MM-DD-<topic>-design.md` with goals, requirements, decisions, non-goals, files, tests, and unresolved questions. Start from `docs/superpowers/templates/feature-design-template.md`.
- Multi-step work requires a dated plan from `docs/superpowers/templates/plan-template.md` while work remains. Delete completed plans and status/worklog prose after durable facts move into canonical docs.
- The `Require design spec` check applies to PRs changing `src/`, `scripts/`, or `tests/`. A deliberate exception is one PR-body line: `Spec-Exception: <reason>`.
- Reference the served `Q-*`/`REQ-*` IDs in PRs and tests.

## Product language

| Term | Meaning |
|---|---|
| PiPlay | The app/product. |
| Video Popout | Moving current YouTube playback to the floating player. |
| Popout Player | Floating borderless playback window. |
| Source Window | Main PiPlay browser window. |
| Source Placeholder | Accent-derived near-black letterbox area shown while playback is popped out. |
| Pin | Keep the active surface always-on-top. |
| Fade | Idle/hover fading; never click-through. |
| Auto | Automatic `/watch` popout; off by default. |

`MainWindow`, `PlayerWindow`, `Detach`, and `fake PiP` are internal-only names.

## Non-negotiable constraints

- Q-1: no duplicate audio. Q-2: preserve video/timestamp/window context on return. Q-3: DOM injection is isolated and best-effort. Q-4: use WebView2 Evergreen. Q-5: do not interfere with ads, monetization, credentials, DRM, regions, age gates, or required controls. Q-6: recover from failures. Q-7: native-quality window/DPI behavior. Q-8: every visible player remains interactable.
- Windows-only WPF on `net10.0-windows`; `Nullable` and `ImplicitUsings` enabled. No trimming, NativeAOT, or single-file publish.
- One Popout Player. No click-through, transparent WebView, video downloading, ad blocking, required global hotkeys, cross-platform build, or credential/network telemetry.
- All WebView JavaScript belongs in `YouTubeDomBridge`. Page-to-host protocols are exact-schema, versioned, nonce/document-token checked, and source checked; never accept pointer coordinates, arbitrary URLs, commands, or filesystem access.
- Save settings atomically: temp file, writer/stream flush, then same-volume `File.Move(..., overwrite: true)` or `File.Replace`. Never use `File.Copy` over live settings.
- Per-monitor DPI is `PerMonitorV2` in `src\PiPlay\app.manifest`. Source minimum is 760 x 480 DIP; Normal Popout 320 x 180; dormant Compact 480 x 270.
- Logs are local and bounded. Never log cookies, authorization headers, credential URLs, or unsanitized search text.

## WPF traps already settled

- Explicitly dark-template every popup-bearing control, including dropdown/menu item containers and tooltips.
- Render icon glyphs through an element whose template/local value sets `Segoe Fluent Icons` with `Segoe MDL2 Assets` fallback. An implicit `TextBlock` style can otherwise replace the font or active color.
- Keep `UseLayoutRounding="False"` at Window level. At fractional DPI, `True` clipped the editable URL line box; mixing rounding between a parent and hosted text made it worse.
- Keep `AllowsTransparency=False`; WebView2 is an HWND child. Effective `Round` Popouts use the DPI-scaled native region defined by ADR-0008 in `DECISIONS.md`.

## Verify and release

```powershell
pwsh -NoProfile -File .\scripts\Test-LocalCI.ps1
```

- Release candidates also run `QA_Checklist.md` against the deployed Stable copy at `E:\Dev_test_implemenations\PiPlay\PiPlay.exe`, never repo output.
- Commit `VERSION`, `BUILD_NUMBER`, and `docs/CHANGELOG.md`, then run `.\scripts\Publish-Stable.ps1` from a clean tree and `.\scripts\Verify-StableDeploy.ps1` before manual testing.
- Provenance is the exact-source commit, `stable-vX.Y.Z-bN`, and verifier output. Signing is optional through `-SignScript <path>` and is not a release gate. `-AllowVersionBump` and `-AllowDirty` produce diagnostic-only evidence.
- Public pull requests stay on hosted `windows-latest`. `PIPLAY_WINDOWS_RUNNER` is for trusted `main`/manual runs only; leave it unset until a dedicated disposable PiPlay runner exists. There is no automatic hosted failover once a self-hosted label is selected.

Parallel agents must read this file and the product spec, use the terminology above, and write only inside assigned ownership.
