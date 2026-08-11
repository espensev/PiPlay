# Data and privacy map

PiPlay has no telemetry, analytics, crash upload, or credential collection.

## Data roots

```text
Default: %LOCALAPPDATA%\PiPlay\
Stable:  <exe folder>\PiPlayData\
Tests:   PIPLAY_DATA_ROOT overrides either root
```

Stable data is isolated from Default and preserved across staged redeploys. See ADR-0007 in `DECISIONS.md`.

## Files

| Data | Relative location | Contents and lifetime | Clear |
|---|---|---|---|
| Settings/profiles | `settings.json` | Schema 4: last URL, Auto, Source/Popout placement and Pin, Fade/opacity, theme/accent/corners, presentation, active profile, profile URL/overrides/bounds. Atomic and persistent; corrupt files are quarantined as `settings.json.corrupt.YYYYMMDD-HHMMSS.json` and pruned after 30 days. | **Reset app state** or delete while closed. |
| Logs | `logs\piplay.log` | Local redacted lifecycle/failure diagnostics. Queue 4,096; batches 64 KiB; retained failed batch 512 KiB; rotate near 1,000,000 bytes with one backup. | Delete log/folder. Reset keeps logs. |
| Browser profile | `WebView2UserData\` | WebView2 cookies, YouTube/Google login, cache, permissions. Shared by Source/Popout inside one channel. Sensitive private browser data; never copied to settings/logs. | **Clear browser data** or delete while closed. |

## Actions

- **Reset app state (REQ-PRIVACY-01):** clears settings, profiles, preferences, and placement. It does not touch `WebView2UserData\`; YouTube remains signed in.
- **Clear browser data (REQ-PRIVACY-02):** separate confirmed action using `ClearBrowsingDataAsync(AllProfile)`. It closes Popout, keeps the Source WebView alive, clears the shared profile, then reloads YouTube and signs the user out. The UI timeout is `30 seconds`; after a timeout, clear remains single-flight until the underlying task succeeds or fails.
- **Clean uninstall:** close PiPlay, uninstall binaries, then remove the whole data root. Stable data is contained under its deployed folder; the app remains framework-dependent.

## Restrictions

- Never log cookies, authorization headers, full credential-bearing URLs, command lines containing secrets, or unsanitized search text.
- PiPlay settings/profiles contain no YouTube credentials. Treat `WebView2UserData\` like any browser profile.
- Browser and app resets remain separate in UI wording, confirmation, and implementation.
