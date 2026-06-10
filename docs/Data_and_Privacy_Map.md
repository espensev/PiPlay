# PiPlay — Data & privacy map

**Status:** Beta candidate. Lists everything PiPlay writes to disk, where it is stored, and how to clear it.

PiPlay stores everything under one root. The normal (Default channel) app uses:

```
%LOCALAPPDATA%\PiPlay\
```

A **Stable** (portable) channel keeps the same layout **beside the exe** instead,
so its session and settings are self-contained and isolated from the dev profile:

```
<exe folder>\PiPlayData\
```

The `%LOCALAPPDATA%\PiPlay` paths below describe the Default channel; for a Stable copy, read `<exe
folder>\PiPlayData` in their place. (`PIPLAY_DATA_ROOT` overrides the root for tests/CI.) See
[adr/0007-stable-channel-and-portable-data.md](adr/0007-stable-channel-and-portable-data.md).

## What PiPlay writes

| Data | Location | Contents | Lifetime | Reset |
|---|---|---|---|---|
| Settings | `%LOCALAPPDATA%\PiPlay\settings.json` | Schema version, last URL, window placements, player prefs (topmost, fade, opacity, size), profiles. | Persists across runs; rewritten atomically on change. | **Reset app state** deletes/recreates this file. |
| Profiles | Inside `settings.json` (`profiles[]`) | Named launch targets: name, URL, mode, topmost/fade overrides, bounds, and monitor identity. | Persists until edited / removed. | Remove via UI, or **Reset app state**. |
| Logs | `%LOCALAPPDATA%\PiPlay\logs\piplay.log` | App lifecycle, WebView init, navigation failures, popout/return events, settings/runtime errors. | Local only; should be size-bounded (no unbounded growth). | Delete the file or the `logs\` folder. |
| WebView2 browser data | `%LOCALAPPDATA%\PiPlay\WebView2UserData\` | Standard browser profile: YouTube cookies, login/session, cache, permissions. Owned by WebView2, shared by both windows. | Persists like a browser profile; cache can grow. | **Clear browser data** confirmed action through WebView2 profile clearing, or delete the folder while PiPlay is closed. |

## Privacy classification
- **Sensitive (browser session):** `WebView2UserData\` holds YouTube login / cookies — treat as private browser data. It is never copied into `settings.json` and never logged.
- **Local config:** `settings.json` and profiles hold no credentials.
- **Diagnostics:** logs are local-only and must never contain cookies, authorization headers, or full credential URLs (spec section 18).

## What PiPlay does not collect
- No network telemetry, analytics, or crash upload — nothing leaves the machine.
- No credential capture or storage by PiPlay itself (the WebView2 profile holds session state, exactly as a browser would).
- User-entered search text is not logged unless explicitly needed and sanitized.

## Reset / uninstall
- **Reset app state:** clears `settings.json`, including profiles, global preferences, and window placement. It does **not** delete `WebView2UserData\`; the user stays logged into YouTube.
- **Clear browser data:** separate confirmed action that clears the shared WebView2 profile's browser data (`ClearBrowsingDataAsync(AllProfile)`) and logs the user out of YouTube. PiPlay keeps the Source Window WebView alive, closes any Popout Player first, then reloads YouTube after a successful clear.
- **Clean uninstall:** remove the whole `%LOCALAPPDATA%\PiPlay\` folder after uninstalling the app.
- **Logs:** keep logs through reset by default unless the user explicitly chooses to clear logs or delete the `logs\` folder.
