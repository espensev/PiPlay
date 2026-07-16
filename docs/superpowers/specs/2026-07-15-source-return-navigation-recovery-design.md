# Source return and navigation recovery - design

## Goals

Make the Source Window recover predictably from every Video Popout return and remain usable at its
supported minimum size. Preserve the single-player playback/return pipeline, Focused overlay, and
independent Source/Popout Pin preferences while fixing the live failures found in Stable v0.11.0-b34:
sub-minimum Source restore, return into a minimized Source, hidden Source navigation, transition
re-entry, and dense Source controls.

This pass applies the Focused overlay's hierarchy to the Source without creating a new main-window
layout mode or layering WPF controls over the WebView2 child HWND.

## Requirements served

- Q-2: return keeps the user's video and window context.
- Q-6: every Popout exit has a visible recovery path and a guarded transition state.
- Q-7 / REQ-WINDOW-01: native sizing, DPI, restore, focus, and z-order are intentional.
- REQ-UI-01 / REQ-UI-02: new popup/actions remain dark, keyboard reachable, and truthfully named.
- Spec sections 5.5, 6.3, 13.3-13.5, 14, and 16.4.
- ADR-0005: one Popout Player; Show Popout activates the existing player and never creates another.

## Acceptance criteria

- A saved Source placement can never restore below `MinWidth` / `MinHeight`, including at fractional DPI.
- Closing or bringing back a Popout restores and activates a minimized Source without changing its
  persisted Pin preference; app shutdown does not steal focus.
- A Source Pin is temporarily suspended while the Popout owns playback so it cannot cover an unpinned
  player, then restored on return. Source and Popout preferences remain distinct.
- The transfer action has explicit Ready, Open, and Returning states. Manual and Auto launch are gated
  until same-video return scripting or different-video replay completes or times out.
- While the Tier-1 Source Placeholder hides the WebView, Source navigation/profile commands are
  disabled. Auto remains available to turn off; Show Popout and Bring video back remain direct.
- Show Popout focuses/restores the existing player without capturing state or closing it.
- Profile Save/Edit/Delete move behind one dark keyboard-accessible actions menu. Below the compact
  width threshold, only the transfer action's visible text collapses; tooltip and UIA name remain.
- Source and native Popout Pin tooltip/UIA copy changes between Pin and Unpin with actual state.

## Settled decisions

1. Enforce the existing 480-DIP Source floor before adding chrome auto-hide. The live screenshot is an
   invalid 321-DIP restore, so a new layout mode would conceal the correctness defect.
2. Keep the 42 + 50 DIP Source chrome and use adaptive disclosure only for width. This is a bounded
   toolbar correction, not the unresolved Browse/Cinema/Compact mode model.
3. Keep Bring video back as the primary transfer command and add a distinct Show Popout recovery
   command. Focus and destructive transfer must not share a handler.
4. Preserve separate Pin preferences, but suspend actual Source topmost while a Popout is active. The
   inactive placeholder must not cover the playback surface; the saved Source preference resumes on
   return and is not rewritten.
5. Disable hidden Source navigation rather than supporting concurrent invisible browsing. Tier-1 hides
   the whole WebView, so navigation results cannot be inspected and would invalidate return assumptions.
6. Use a real WPF `ContextMenu` / `MenuItem` profile menu with explicit dark styles. Native menu keyboard
   behavior and automation are preferable to a hand-rolled popup.
7. Hold Returning through pending different-video replay, not merely through `NavigateInternal`, because
   replay is where timestamp, paused state, volume, mute, and playback rate actually settle.

## Non-goals / out of scope

- A persisted Source layout mode, Source chrome auto-hide, Cinema mode, or WPF-over-WebView overlay.
- Merging Source and Popout Pin settings or removing the Popout's direct Pin action.
- Multiple Popout Players, compact embed playback, or changes to YouTube playback/ads.
- Release version/build stamping or Stable promotion in this implementation pass.

## Testing approach

- Logic: native min-track DPI conversion and preservation of stricter native values.
- Markup: dark profile menu, required named controls, accessible action names, stable toolbar rows.
- WPF: sub-minimum Source normalization, hidden-navigation availability, return busy state, minimized
  Source activation with Pin preservation, Show/Bring state, compact-width disclosure, dynamic Pin copy.
- Existing return/presentation/DOM protocol suites guard playback and Focused behavior.
- Deterministic local CI and build gate run before handoff. No live actions are sent to the owner's
  running Stable process; deployed manual QA requires a later sanctioned Stable publish.

## Changes by file

| File | Change |
|---|---|
| `src/PiPlay/Services/BorderlessWindowHelper.cs` | Preserve per-window DPI-scaled minimum track size in `WM_GETMINMAXINFO`. |
| `src/PiPlay/MainWindow.xaml(.cs)` | Normalize restore, coordinate return/Pin/navigation state, add Show Popout, responsive transfer action, and profile menu. |
| `src/PiPlay/PlayerWindow.xaml.cs` | Keep native Pin name/tooltip synchronized with Topmost. |
| `src/PiPlay/Theme/ControlStyles.xaml` | Add dark ContextMenu/MenuItem templates. |
| `tests/PiPlay.Tests/**` | Add sizing, lifecycle, markup, responsive, and Pin regressions. |
| `docs/PiPlay_Product_Engineering_Spec.md` | Make visible Source return and hidden-navigation behavior normative. |
| `docs/adr/0005-single-player.md` | Record Show Popout as the non-creating activate-existing command. |
| `docs/QA_Checklist.md`, `docs/CHANGELOG.md` | Add runtime acceptance and user-visible fix notes. |

## Docs & changelog impact

Update the product spec, ADR-0005 clarification, QA checklist, Source UI priority status, changelog,
dated plan, and session worklog. No new architecture ADR is required: the change restores ADR-0005's
activate-existing path and stays within the existing native WPF/WebView2 architecture.

## Unresolved decisions

- None for this pass. Main-window auto-hide/layout modes remain separately open.
