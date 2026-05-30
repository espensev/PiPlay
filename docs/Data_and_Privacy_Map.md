# PiPlay — Data & privacy map

**Status:** Draft 0.4. Consolidates spec sections 12.6, 17, 18, and 19. Lists everything PiPlay writes to disk, where, and how to clear it.

PiPlay stores everything under one root:

```
%LOCALAPPDATA%\PiPlay\
```

## What PiPlay writes

| Data | Location | Contents | Lifetime | Reset |
|---|---|---|---|---|
| Settings | `%LOCALAPPDATA%\PiPlay\settings.json` | Schema version, last URL, window placements, player prefs (topmost, fade, opacity, size), profiles. | Persists across runs; rewritten atomically on change. | **Reset app state** deletes/recreates this file. |
| Profiles | Inside `settings.json` (`profiles[]`) | Named launch targets: name, URL, mode, topmost/fade overrides, bounds, and monitor identity. | Persists until edited / removed. | Remove via UI, or **Reset app state**. |
| Logs | `%LOCALAPPDATA%\PiPlay\logs\piplay.log` | App lifecycle, WebView init, navigation failures, popout/return events, settings/runtime errors. | Local only; should be size-bounded (no unbounded growth). | Delete the file or the `logs\` folder. |
| WebView2 browser data | `%LOCALAPPDATA%\PiPlay\WebView2UserData\` | Standard browser profile: YouTube cookies, login/session, cache, permissions. Owned by WebView2, shared by both windows. | Persists like a browser profile; cache can grow. | **Clear browser data** confirmed action, or delete the folder while PiPlay is closed. |

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
- **Clear browser data:** separate confirmed action that clears `WebView2UserData\` and logs the user out of YouTube. Because WebView2 may hold files open, PiPlay should close/recreate WebViews or ask for restart before deleting the folder.
- **Clean uninstall:** remove the whole `%LOCALAPPDATA%\PiPlay\` folder after uninstalling the app.
- **Logs:** keep logs through reset by default unless the user explicitly chooses to clear logs or delete the `logs\` folder.
