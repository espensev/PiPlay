# Auto (playback-start auto-popout) — design

## Goals

Implement **`Auto`** (spec §6.1): when enabled, PiPlay automatically starts a Video Popout for a YouTube
**watch** video **when that video is playing** — so a user who just wants to watch doesn't have to click
**Pop out video** every time. `Auto` is **off by default**, must respect the existing single-player
lifecycle (ADR-0005), and must be trivially disable-able. This pass also **resolves the open product
decision** "Auto trigger timing" (`SPEC_GAPS_AND_OWNERSHIP.md`) in favour of **playback-start**.

The manual popout happy path, the return/resume policy (REQ-RETURN-01), and the Default-channel behavior
are **unchanged**: `Auto` is purely an additional, opt-in *trigger* in front of the existing
`StartVideoPopoutAsync`.

## Requirements served

- Spec **§6.1 `Auto`** — automatic Video Popout for supported YouTube watch videos; off by default; same
  single-player lifecycle as manual popout; easy to disable.
- Resolves **`SPEC_GAPS_AND_OWNERSHIP.md` open decision "Auto trigger timing"** → **playback-start**.
- **Q-1** (no duplicate audio) and **ADR-0005** (single player) — *inherited unchanged* because Auto
  calls the existing `StartVideoPopoutAsync`, which already pauses the source and enforces one player.
- **Q-6** (recover cleanly) — detection is best-effort; a DOM/script hiccup degrades to "no auto-pop",
  never a crash, preserving the YouTubeDomBridge contract (spec §12.5).
- **REQ-RETURN-01** — preserved; the return/resume decision (`ReturnPolicy`) is untouched.

## Acceptance criteria

- `AutoPopout` defaults **false**; every existing `settings.json` loads as Auto **off** (additive
  optional bool, no migration).
- **Auto off:** no source-side detector runs; manual popout and all current behavior are byte-for-byte
  unchanged.
- **Auto on:** when a `/watch` video is playing in the Source Window, Auto starts a popout **exactly once
  for that video**, reusing `StartVideoPopoutAsync` (source paused, placeholder shown, one player, no
  duplicate audio). This covers the autoplay case — the video is usually *already playing* when detected,
  so detection is "is playing", **not** a literal pause→play transition.
- **Enabling Auto does not yank the current video:** turning Auto on (or launching with Auto on) while a
  watch video is already playing does **not** pop *that* video; Auto pops the **next** watch video that
  plays. (The currently-playing id is seeded as already-handled.)
- **No re-pop loop:** returning from a popout (which resumes source playback) does **not** re-pop the same
  video; Auto re-pops only when a *different* `/watch` video plays.
- **`/watch`-only:** Auto fires only for `/watch` videos. **Shorts and embeds are excluded** (never pop).
  Playlist autoplay-next *is* allowed to pop each new video (it is a new `/watch` id — consistent with the
  feature).
- Auto never opens a second player while one is open or a popout is in progress, and never fires while
  `Clear browser data` is running.
- Toggling Auto **off** stops auto-popping immediately; toggling it on re-arms (re-seeding the current id).
- The pure `AutoPopoutPolicy` decision is unit-tested (Logic lane); `AutoPopout` default + round-trip are
  tested (Logic lane); the new toolbar toggle constructs without throwing (Wpf lane) and is asserted to
  exist with a tooltip (Markup lane).

## Settled decisions

1. **Trigger = playback (steady-state "is playing"), de-duped per `VideoId` — not a literal edge.** Per
   the product decision, the trigger is "the video is playing", resolving the open "Auto trigger timing"
   item. It is deliberately **not** modelled as a `paused→playing` transition: autoplay is enabled
   (`WebViewEnvironmentService.cs:32`, `--autoplay-policy=no-user-gesture-required`), so a freshly-navigated
   watch video is usually **already playing** by the time the ~250 ms detector first observes it — there is
   no edge to catch. "Fire once per `VideoId` while playing" captures the autoplay case and the
   user-presses-play case identically. Watch-page-navigation and "both" are rejected (navigation fires
   while a user is only skimming, and can pop a not-yet-playing page).

2. **Scope = `/watch` only.** `isWatchVideo` means the canonical page is a **`/watch` video with a
   resolvable `VideoId`** — *not* merely "`YouTubeUrlHelper.TryParse` returned an id". This is load-bearing:
   `/shorts/` resolves to a `VideoId` too, so without the `/watch` restriction, scrolling Shorts with Auto
   on would pop out every short. Shorts and embeds are excluded; playlist autoplay-next is allowed (each
   next item is a new `/watch` id, which is the intended behavior).

3. **Anti-loop + de-dup = "last handled `VideoId`".** Auto fires only when the current `VideoId` differs
   from the **last handled** id. Starting any popout for a video (manual *or* auto) records that id as
   handled; on enable, the currently-playing id is **seeded** as handled. Therefore: the return path's own
   resume-playback (`Player_OnClosed`, `MainWindow.xaml.cs:589-607`), an in-source pause/resume of the same
   video, and the just-enabled current video are all **no-ops** — the single biggest integration risk
   (discovery gotcha #1) is closed structurally, with **no timing/debounce constant required**. A
   *different* `/watch` video playing re-pops normally; navigating back to an earlier video later also
   re-pops (its id is no longer the most-recently-handled one).

4. **Reuse `StartVideoPopoutAsync` unchanged; pre-validate before calling.** Auto invokes the existing
   parameterless `StartVideoPopoutAsync` (`MainWindow.xaml.cs:490`) so it inherits the single-player guards
   (`493-494`), the source pause + placeholder, and the return wiring for free. Because that method raises
   **modal dialogs** on an unresolved/failed target (`511`, `541`) — wrong for a silent trigger — Auto only
   calls it after confirming a `/watch` `VideoId` and that no player is active. `StartVideoPopoutAsync`
   records the handled id (so manual and auto share one place), making a separate `Player_OnClosed`
   recording unnecessary.

5. **Detection = source-side poll, not a new push channel.** A `DispatcherTimer` (~250 ms) on the Source
   Window mirrors `PlayerWindow._syncTimer` (`PlayerWindow.xaml.cs:66-67,139-154`), reading playback state
   + the current id via `YouTubeDomBridge` (best-effort), and consulting `AutoPopoutPolicy`. Chosen over
   `AddScriptToExecuteOnDocumentCreated` + `CoreWebView2.WebMessageReceived` (a **net-new host↔JS message
   channel** the app has never used, which would have to survive YouTube's SPA `<video>` re-renders)
   because the poll **reuses already-exercised code, adds no new messaging surface, and keeps the
   best-effort DOM contract**. The push channel is recorded as a deferred upgrade (Non-goals).

6. **Setting home = top-level `AppSettings.AutoPopout` (bool, default false).** Auto is an app-level
   *trigger* decision made *before* any popout exists, so it does **not** belong in `PlayerSettings`
   (which holds the popout window's restored UI state). It rides the existing atomic Save/Load path with
   **no `SchemaVersion` bump and no `Sanitize` entry** (a missing/false bool already deserializes to off).

7. **UI = a new toolbar `AutoToggle`, mirroring `PinToggle`.** A `ToggleButton` in the MainWindow toolbar
   (`MainWindow.xaml:63-111`) with `Click → ApplyAuto(...) → _settingsService.Save(_settings)`, applied
   once in the constructor next to `ApplyTopmost(...)` and re-synced in `ApplyResetState`. It is now
   **shown enabled** (its phase is being implemented). Persistence copies **Pin's** path (direct
   `ToggleButton → Save`), *not* Fade's (which persists via `PlayerReturnState`).

8. **Testable decision extracted to a pure `AutoPopoutPolicy`.** Mirroring `ReturnPolicy`/`FadePolicy`, a
   static `AutoPopoutPolicy.Decide(autoEnabled, isPlaying, isWatchVideo, currentVideoId, lastHandledVideoId,
   popoutActive) → AutoPopDecision { Skip, Pop }` holds the `/watch`-only, de-dup, and single-player branch
   logic so it is unit-tested without WebView2. The 250 ms timer, the DOM reads, and the seed/record of
   `lastHandledVideoId` stay in `MainWindow` (manual/Lane-B territory).

## Non-goals / out of scope

- The **push/event detector** (`AddScriptToExecuteOnDocumentCreated` + `WebMessageReceived`) — deferred;
  the poll is sufficient for Phase 2.
- **Auto-popping Shorts or embeds** — excluded by the `/watch`-only scope.
- **Per-profile Auto** — global toggle only. (`Profile` already has the nullable-override pattern if this
  is ever wanted.)
- **Watch-page-navigation** or **"both"** triggers — explicitly not chosen.
- Any change to the **return/resume policy** (`ReturnPolicy`) or the Default-channel behavior.

## Testing approach

- **Logic lane (Layer 2)** — new `AutoPopoutPolicyTests` with `[Theory]`/`[InlineData]` truth-table rows
  (mirrors `ReturnPolicyTests`): off ⇒ Skip; not playing ⇒ Skip; non-`/watch` (Shorts/embed/no id) ⇒ Skip;
  player active ⇒ Skip; current id == last handled (return-resume, in-source resume, just-enabled current)
  ⇒ Skip; enabled + playing + `/watch` + new id + no player ⇒ Pop. Plus `SettingsServiceTests`:
  `AutoPopout` default-false and Save/Load round-trip.
- **Markup lane (Layer 1)** — assert the new named `AutoToggle` exists and carries a tooltip
  (`XamlInvariantTests`, same shape as the profile Edit/Delete button assertions).
- **Wpf lane (Layer 3)** — `WpfRuntimeTests` constructs `MainWindow` without throwing; the toggle's
  checked-state reflects the loaded setting.
- **Manual smoke (Lane B)** — *not* unit-testable (WebView2-bound): verify a real `/watch` video auto-pops
  once while playing, returning does not loop, a *second* video re-pops, a Short does **not** pop, Auto-off
  stops it, and rapid double-trigger still yields one player. New Phase-2 `Auto` row in
  `docs/QA_Checklist.md`.

## Changes by file

| File | Change |
|---|---|
| `src/PiPlay/Models/AppSettings.cs` | Add `public bool AutoPopout { get; set; }` (top-level, default `false`). |
| `src/PiPlay/Services/AutoPopoutPolicy.cs` | **New** pure seam: `Decide(autoEnabled, isPlaying, isWatchVideo, currentVideoId, lastHandledVideoId, popoutActive) → AutoPopDecision`. |
| `src/PiPlay/Services/YouTubeDomBridge.cs` | *(if needed)* a combined best-effort read returning `{ paused, videoId }` in one `ExecuteScriptAsync`, so the detector resolves "is playing + which `/watch` id" in a single DOM call; otherwise reuse `ReadPlayerStateAsync` + `ReadCanonicalUrlAsync`. |
| `src/PiPlay/MainWindow.xaml` | Add a toolbar `ColumnDefinition` + `AutoToggle` `ToggleButton` (mirrors `PinToggle`). |
| `src/PiPlay/MainWindow.xaml.cs` | `AutoToggle_Click`/`ApplyAuto` (Pin-style); apply in ctor + `ApplyResetState`; seed `lastHandledVideoId` on enable; a source-side `DispatcherTimer` (with an `_autoTickInProgress` reentrancy guard) that reads playback + id, consults `AutoPopoutPolicy`, and calls `StartVideoPopoutAsync`; record the handled id inside `StartVideoPopoutAsync`; start/stop the timer with `_browserReady`, the Auto flag, popout state, `_clearingBrowserData`, and `MainWindow_Closing`. |
| `tests/PiPlay.Tests/AutoPopoutPolicyTests.cs` | **New** Logic-lane truth-table for the decision seam. |
| `tests/PiPlay.Tests/SettingsServiceTests.cs` | Add `AutoPopout` default-false + Save/Load round-trip cases. |
| `tests/PiPlay.Tests/Ui/XamlInvariantTests.cs` | Assert `AutoToggle` exists with a tooltip. |
| `tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs` | Assert the toggle reflects the loaded `AutoPopout` setting. |

## Docs & changelog impact

- `docs/CHANGELOG.md` — `[Unreleased]` Phase 2 entry: "**Auto** — opt-in auto-popout when a watch video
  plays, off by default" (user-visible).
- `docs/SPEC_GAPS_AND_OWNERSHIP.md` — move the "Auto trigger timing" row from open decisions to
  "Resolved" (decided: playback-start, `/watch`-only, once-per-video).
- `docs/PiPlay_Product_Engineering_Spec.md` §6.1 — replace "Trigger timing remains an open decision" with
  the decided behavior (plays → once per `/watch` id; no re-pop on return; Shorts excluded).
- `docs/QA_Checklist.md` — new Phase-2 `Auto` row(s).
- A dated **plan** at `docs/superpowers/plans/2026-06-06-auto-popout.md`.

## Unresolved decisions

- **Poll interval** (250 ms proposed, matching `_syncTimer`) — tunable; correctness does **not** depend on
  it (the id-based de-dup needs no timing constant).
- **Combined vs. two DOM reads:** whether to add a single `{paused, videoId}` bridge read or reuse the two
  existing reads per tick — a perf/readability call settled during implementation, not a behavior change.
- **SPA teardown signal:** `CoreWebView2.SourceChanged` *likely* fires on YouTube's in-SPA `watch→watch`
  navigation but is unconfirmed. The design does **not** depend on it (the id is read at poll time), but if
  we later want eager detector teardown on navigation, confirm its SPA behavior first.
