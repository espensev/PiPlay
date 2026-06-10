# PiPlay

PiPlay is a Windows desktop utility for watching YouTube in a movable,
resizable **Video Popout** window. The beta includes the complete popout
loop, Pin, Auto popout, profile management, privacy controls, a dark native
shell, and WebView2 runtime recovery.

## Start here

- [Documentation index](docs/README.md)
- [Product & engineering spec](docs/PiPlay_Product_Engineering_Spec.md)
- [Feature workflow](docs/Feature_Workflow.md)
- [Architecture decisions](docs/adr/README.md)
- [Manual QA checklist](docs/QA_Checklist.md)
- [YouTube usage & compliance](docs/YouTube_Compliance.md)
- [Data & privacy map](docs/Data_and_Privacy_Map.md)

## Current stack

- Windows 10/11
- WPF on .NET 10
- Microsoft Edge WebView2 Evergreen Runtime

See [docs/README.md](docs/README.md) for build/run commands and release notes. A
differentiable **Stable** copy (isolated data folder, single-instance identity,
and "PiPlay — Stable" title) can be built and deployed with
`scripts\Publish-Stable.ps1`; see the [Feature workflow](docs/Feature_Workflow.md).
