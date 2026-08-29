# Working in PiPlay

## Canonical surfaces

- `PiPlay_Product_Engineering_Spec.md` owns product behavior and `Q-*` requirements.
- `DECISIONS.md` owns accepted architecture; supersede an ADR instead of silently contradicting it.
- `Theme_Preset_Differences.md` owns current theme values.
- `YouTube_Compliance.md` owns page-script and platform-safety policy.
- `CHANGELOG.md` contains shipped user-visible notes and is packaged by the release build.

Do not create a design spec, plan, worklog, manual approval, or status document merely to pass a process gate.

## Product language

| Term | Meaning |
|---|---|
| Video Popout | Move current YouTube playback to the floating player. |
| Popout Player | Floating borderless playback window. |
| Source Window | Main PiPlay browser window. |
| Source Placeholder | Near-black surface shown while playback is popped out. |
| Pin | Keep the active surface topmost. |
| Fade | Idle/hover chrome fading; never click-through. |
| Auto | Automatic `/watch` popout; off by default. |

Use **Pop out video**, **Bring video back**, and **Show Popout** in user-facing copy. `MainWindow`, `PlayerWindow`, `Detach`, and `fake PiP` are internal names.

Do not hardcode a user or machine path in documentation.
