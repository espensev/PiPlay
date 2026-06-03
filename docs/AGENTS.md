# AGENTS.md — Working in the PiPlay repo

Orientation for AI agents (Claude Code, etc.) and new contributors. Read this and the spec before changing code or docs.

## Source of truth
- The product & engineering spec is authoritative: `PiPlay_Product_Engineering_Spec.md` (this folder).
- Architecture decisions and their rationale live in `adr/`. If you change a decision, add or supersede an ADR — do not just change code silently.

## Design docs (per change pass)
Before writing code for any non-trivial change, record the **goals and approach** of that pass in a dated design spec at `docs/superpowers/specs/YYYY-MM-DD-<topic>-design.md`:
- Open with a `## Goals` section, then the requirement IDs it serves (`Q-n`, `REQ-*`), the settled decisions, and the changes by file.
- Multi-step work also gets an implementation plan at `docs/superpowers/plans/YYYY-MM-DD-<topic>.md`.
- These are **subordinate** to the product spec (still authoritative) and the ADRs (which own architecture decisions); they capture what one pass set out to do and why. Link the spec from the PR.
- Established examples: `superpowers/specs/2026-05-31-privacy-actions-design.md`, `superpowers/plans/2026-05-31-*.md`.

## Terminology (do not drift)
Use these names everywhere — UI, code, comments, tests, commits, issues:

| Term | Meaning |
|---|---|
| PiPlay | The app/product. |
| Video Popout | The feature that moves the current YouTube video into a floating player. |
| Popout Player | The floating, borderless playback window. |
| Source Window | The main PiPlay browser window. |
| Source Placeholder | The black area shown while the video is popped out. |
| Pin | Keep the active surface always-on-top. |
| Fade | Hover/idle fading of controls (and optional whole-window opacity). |
| Auto | Auto-pop-out of supported watch videos; off by default. |

`Detach`, `PlayerWindow`, and `MainWindow` are **internal-only** names (the code already uses `MainWindow`/`PlayerWindow`). Never surface them in user-facing text.

## Quality bar (spec section 3)
Quality outranks scope: no duplicate audio after popout (Q-1); no lost video/timestamp/window context on return (Q-2); DOM injection stays isolated and best-effort (Q-3); use Evergreen WebView2 (Q-4); no invasive YouTube behavior (Q-5); recover cleanly from every error (Q-6); native-quality window behavior including DPI (Q-7); a visible player stays interactable — no click-through (Q-8).

## Hard non-goals (do not implement)
- Downloading videos; blocking ads or touching monetization; bypassing DRM/region/age gates.
- Click-through / mouse pass-through windows; making the WebView itself transparent.
- Global hotkeys as *required* functionality.
- Multiple simultaneous Popout Players — single-player by design for now (see `adr/0005-single-player.md`).
- Cross-platform builds.

## Conventions
- WPF on `net10.0-windows`; `Nullable` and `ImplicitUsings` enabled. No trimming / NativeAOT / single-file yet (`adr/0002-target-net-10.md`).
- Settings are written atomically (temp file + flush + `File.Move`/`File.Replace`), never `File.Copy`. Never lose settings on a partial write.
- Per-monitor DPI (PerMonitorV2) is declared in `src\PiPlay\app.manifest`.
- No network telemetry. Logs are local only and must never contain cookies, auth headers, or credential URLs.
- Reference requirement IDs (`Q-n`, `REQ-*`) in PRs and tests. Update `CHANGELOG.md` for user-visible changes.
- Sign release binaries with the SevIQ code-signing certificate before sharing.

## UI implementation notes (REQ-UI-01 / REQ-UI-02)

The spec keeps visual identity general; these are the WPF specifics that satisfy it. They exist because chrome drift to light/default theming is the most common regression here.

- **Dark everything that pops up.** WPF's default `ComboBox` dropdown, `ContextMenu`, `Menu`, and `ToolTip` are light-themed. Each needs an explicit dark `ControlTemplate`/style covering the popup and item containers (e.g. `ComboBoxItem`), not just the closed control. Define an app-level dark `ToolTip` style so every tooltip inherits it, and place tooltips so they don't occlude their control (esp. caption buttons).
- **Icon glyphs must resolve on the element that renders them.** Setting the icon font via a style `Setter` on a button is not enough: an implicit `TextBlock` style (e.g. one that sets `FontFamily`) clobbers the auto-generated content `TextBlock` and the glyph falls back to `.notdef` boxes — while an inline `TextBlock` that sets the font locally renders fine. Render glyph content through a `TextBlock` inside the control template with the icon `FontFamily` and `Foreground` set in-template (template-property precedence beats implicit styles), or host the glyph in an element carrying the font directly. Use one icon family (`Segoe Fluent Icons` with `Segoe MDL2 Assets` fallback).
- **Don't let an implicit `TextBlock` style leak into control templates.** A keyless `<Style TargetType="TextBlock">` that sets `Foreground`/`FontFamily` applies to content presenters too and can override active/hover colors (e.g. Pin-active cyan, close-hover white). Set per-control colors in-template via `TemplateBinding`, or scope the base text style with a key.
- **`UseLayoutRounding="True"` on a Window can clip editable text at fractional DPI.** It was set on both windows and, at non-integer scales (e.g. 133/177%), rounded the `TextBox`'s internal `TextBoxView` line box off the device grid so the URL text rendered as a thin clipped band (and trying to compensate inside the textbox template — padding, `VerticalContentAlignment`, `Display` formatting, per-control `UseLayoutRounding=False` — did **not** fix it; mixing rounding settings between parent and child made it worse). The fix was to set `UseLayoutRounding="False"` at the window level. Keep it off unless you have a specific crisp-edge need, and never mix rounding settings across a parent/child that hosts text.
- Verify with fresh screenshots against the section 22.2 Chrome acceptance checks before calling a build a release candidate. Capture at a fractional DPI (e.g. 150%) — integer-scale captures hide the rounding/clipping class of bug.

## Multi-agent workflow
When work is split across parallel agents, each agent reads this file and the spec first, writes only within its assigned area, and treats the terminology table above as the shared vocabulary.

> If this file is moved to the repo root, prefix the doc paths above with `docs/`.
