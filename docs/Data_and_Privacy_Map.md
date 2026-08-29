# Data and privacy map

Primary sources: `AppPaths`, `AppChannel`, `SettingsService`, `LoggingService`, `PrivacyService`, and `DeploySwap.ps1`; regression coverage is in the corresponding `*Tests` files.

PiPlay has no telemetry, analytics, crash upload, or credential collection in the current implementation. It stores data locally:

| Root | Resolution |
|---|---|
| Default | `%LOCALAPPDATA%\PiPlay` |
| Stable | `<exeDir>\PiPlayData` |
| Tests/diagnostics | `PIPLAY_DATA_ROOT` overrides both |

Staged Stable deployment leaves `PiPlayData` in place. `PIPLAY_CHANNEL` is a separate test/diagnostic channel override; it does not replace `PIPLAY_DATA_ROOT`.

| Data | Location | Contents / retention |
|---|---|---|
| App state | `settings.json` | Schema `4`: URL, playback/appearance settings, profiles, active profile, and placement. Writes are flushed to a temporary file and atomically replaced. Corrupt files are quarantined with a timestamp and old quarantines are removed after 30 days. |
| Diagnostics | `logs\piplay.log` and one `.1` backup | Local redacted lifecycle/failure logs. Queue `4096` entries, batches `64 KiB`, failed-batch retention `512 KiB`, rotation near `1,000,000` bytes. |
| Browser profile | `WebView2UserData\` | WebView2 cookies, cache, permissions, and YouTube/Google session data shared by Source and Popout. Treat it as private browser data. |

**Reset app state** replaces app settings with defaults, removes stale settings quarantines, and does not touch browser data or logs; the YouTube session remains. **Clear browser data** is separate and confirmed: it closes the Popout, calls `ClearBrowsingDataAsync(AllProfile)`, and keeps the operation single-flight through its `30 s` UI timeout. The underlying browser clear determines when the session is actually gone. (`PrivacyService`, `MainWindow.xaml.cs`, `PrivacyServiceTests`.)

Never log cookies, authorization headers, credential-bearing URLs, secret-containing command lines, or unsanitized search text. Settings/profiles contain no YouTube credentials by design; browser credentials remain in `WebView2UserData\`.
