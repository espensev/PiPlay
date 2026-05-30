# PiPlay

PiPlay is a Windows desktop utility for watching YouTube in a movable,
resizable **Video Popout** window. The Phase 1 MVP (single Source Window,
the full Video Popout loop, Pin, profiles, atomic settings, dark shell, and
WebView2 runtime recovery) is implemented and verified; further work is
driven by the product and engineering docs.

## Start here

- [Documentation index](docs/README.md)
- [Product & engineering spec](docs/PiPlay_Product_Engineering_Spec.md)
- [Working rules & terminology (AGENTS.md)](docs/AGENTS.md)
- [Spec gaps and ownership notes](docs/SPEC_GAPS_AND_OWNERSHIP.md)
- [Architecture decisions](docs/adr/README.md)
- [Manual QA checklist](docs/QA_Checklist.md)
- [YouTube usage & compliance](docs/YouTube_Compliance.md)
- [Data & privacy map](docs/Data_and_Privacy_Map.md)

## Current stack

- Windows 10/11
- WPF on .NET 10
- Microsoft Edge WebView2 Evergreen Runtime

See [docs/README.md](docs/README.md) for build/run commands and release notes.
──────────────────────────────────────────────────────────────────────
