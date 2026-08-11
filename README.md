# PiPlay

PiPlay is a Windows desktop utility that plays YouTube in a movable, resizable native **Popout Player**. It supports return-to-source playback, Pin, Auto, Fade, profiles, Standard/Focused presentation, playlists and mixes, privacy controls, and live theme/accent changes.

## Use

Requirements: Windows 10/11 x64, Microsoft Edge WebView2 Evergreen Runtime, and the .NET 10 desktop runtime unless the app is published self-contained.

1. Launch PiPlay and open a YouTube video or playlist.
2. Select **Pop out video**.
3. Move, resize, Pin, Fade, or change the Popout presentation.
4. Select **Bring video back** or close the player to return playback to the Source Window. **Show Popout** only restores/focuses the existing player.

PiPlay intentionally supports one Popout Player. It does not download media, bypass YouTube restrictions, remove ads, make WebView2 transparent, or allow click-through.

## Develop

```powershell
dotnet build PiPlay.sln -c Debug
dotnet run --project src\PiPlay\PiPlay.csproj

# Inspect or run the same deterministic gate as CI.
pwsh -NoProfile -File .\scripts\Test-LocalCI.ps1 -Plan
pwsh -NoProfile -File .\scripts\Test-LocalCI.ps1
```

The executable DOM tests require Node 24 but no `npm install`, browser, network, or visible desktop. See [tests/README.md](tests/README.md) for filters and test boundaries.

## Publish and test

Repo output is for automated development only. All manual/release-candidate testing uses the deployed Stable copy at `E:\Dev_test_implemenations\PiPlay\PiPlay.exe`.

```powershell
# Exact-source release path: commit VERSION, BUILD_NUMBER, and docs/CHANGELOG.md first.
.\scripts\Publish-Stable.ps1
.\scripts\Verify-StableDeploy.ps1

# Manual UI Automation/screenshot smoke against the verified Stable copy.
pwsh -File .\scripts\Test-UiSmoke.ps1 -ExePath E:\Dev_test_implemenations\PiPlay\PiPlay.exe
```

`Publish-Stable.ps1` refuses dirty release evidence, deploys by staged swap while preserving `PiPlayData`, and creates `stable-vX.Y.Z-bN` on the exact source commit. `-AllowVersionBump` and `-AllowDirty` are diagnostic-only escape hatches; their output is not release evidence. Signing is optional through `-SignScript <path>` and must happen before manifest hashes are written.

Other pipeline entry points:

```powershell
.\Build-PiPlay.ps1 -Stage Build -NoVersionBump
.\Build-PiPlay.ps1 -Stage Release -Version patch
.\scripts\Test-PublishMetadata.ps1
```

Release output is under `bin\publish\<label>` with `latest`, `archive`, `build-info.json`, `BUILDINFO.json`, and `VERSION_TABLE.json`. `VERSION` is the semantic version; `BUILD_NUMBER` is the monotonic publish counter.

## Documentation

- [Product and engineering requirements](docs/PiPlay_Product_Engineering_Spec.md)
- [Decisions in force](docs/DECISIONS.md)
- [Open issues and ownership](docs/SPEC_GAPS_AND_OWNERSHIP.md)
- [Contributor workflow](docs/Feature_Workflow.md)
- [Manual release checklist](docs/QA_Checklist.md)
- [Theme values](docs/Theme_Preset_Differences.md)
- [Data and privacy](docs/Data_and_Privacy_Map.md)
- [YouTube compliance](docs/YouTube_Compliance.md)
