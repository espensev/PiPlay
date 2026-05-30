# ADR-0003: Use the Evergreen WebView2 runtime

- **Status:** Accepted
- **Date:** 2026-05-30

## Context
WebView2 ships as Evergreen (shared, auto-updated, OS-provided) or Fixed Version (bundled with the app). YouTube benefits from a current engine, and bundling a fixed runtime adds size and maintenance burden.

## Decision
Use the Evergreen runtime via the `Microsoft.Web.WebView2` package, with a single shared PiPlay user-data folder. Detect a missing runtime and show a friendly install/recovery message. Do not bundle Fixed Version unless offline/kiosk/exact-runtime needs become real requirements.

## Consequences
- Smaller download, always-current engine.
- Adds a runtime dependency the installer and release notes must call out (the runtime-missing path is a required, tested behavior).
