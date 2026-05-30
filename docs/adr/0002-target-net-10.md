# ADR-0002: Target .NET 10; defer trimming/AOT/single-file

- **Status:** Accepted
- **Date:** 2026-05-30

## Context
PiPlay is a new Windows utility that should sit on a durable long-term runtime. WebView2, WPF/XAML, native loader files, and reflection-heavy UI paths interact badly with aggressive size optimization early on.

## Decision
Target `net10.0-windows` with `Nullable` and `ImplicitUsings` enabled. Set `PublishTrimmed=false` and `PublishSingleFile=false`, and do not use NativeAOT for now. .NET 8 is acceptable only as a brief transitional step that avoids churn.

## Consequences
- Simpler diagnostics and predictable WebView2 native-loader behavior.
- Larger published output; revisit size optimization only after the app is stable.
