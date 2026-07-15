# Architecture Decision Records

Short, dated records of significant decisions: the context, the decision, and its consequences. They keep the *why* durable so settled choices are not relitigated.

**Status values:** Proposed · Accepted · Superseded by ADR-NNNN · Deprecated

**To add one:** copy the template below into `NNNN-short-title.md`, increment the number, and never reuse a number.

| ADR | Title | Status |
|---|---|---|
| [0001](0001-app-shell-wpf.md) | Use WPF for the app shell | Accepted |
| [0002](0002-target-net-10.md) | Target .NET 10; defer trimming/AOT/single-file | Accepted |
| [0003](0003-webview2-evergreen.md) | Use the Evergreen WebView2 runtime | Accepted |
| [0004](0004-native-fake-pip.md) | Native "fake-PiP" architecture | Accepted |
| [0005](0005-single-player.md) | Single Popout Player for now | Accepted |
| [0006](0006-no-click-through.md) | No click-through / pass-through transparency | Accepted |
| [0007](0007-stable-channel-and-portable-data.md) | Stable channel + portable data for deployed copies | Accepted |
| [0008](0008-popout-rounded-window-region.md) | Use a custom window region for large round Popout corners | Accepted |

## Template

~~~md
# ADR-NNNN: <title>

- **Status:** Proposed | Accepted | Superseded by ADR-NNNN
- **Date:** YYYY-MM-DD

## Context
<the forces at play; what makes this a real decision>

## Decision
<what we are doing>

## Consequences
<trade-offs; what this enables and what it closes off>
~~~
