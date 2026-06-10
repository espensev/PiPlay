# Session worklog — Stage 0 spikes: layered alpha / app-region drag / DWM rounding (2026-06-10)

Saved record of the Stage 0 spike session for the compact overlay controls + whole-window opacity
plan (`docs/superpowers/plans/2026-06-10-popout-overlay-and-opacity.md`, Task 1). All three spikes
**pass**; Tasks 3/5 are unblocked. All spike code was throwaway and is reverted — this worklog,
the evidence PNGs, and the plan tick are the only committed artifacts.

## Request

"continue the p4 ui work" — Phase 4 (chrome fade + whole-window opacity + overlay look), executing
the staged plan from the 2026-06-10 design session. Plan order starts at Stage 0: the product spec
mandates the layered-alpha test before whole-window opacity ships ("must be tested for input,
rendering, and performance"), and Stage 0 evidence must exist in the worklog before Tasks 3/5 start.

## What was run

- Spike build: a `PIPLAY_SPIKE=p4`-gated throwaway `SpikeProbe` (deleted after the runs) attached to
  the popout HWND on `SourceInitialized`, applying S-3 immediately and S-1 alpha stages at
  T+15/40/60 s (60% → 45% → restore 100%), with a 1 s monitor heartbeat; plus a spike-gated
  `IsNonClientRegionSupportEnabled=true` in `InitializePlayerAsync` and a tinted 28 px
  `app-region: drag` strip in `player.html` (S-2).
- Driver: a throwaway PowerShell 5.1 -STA variant of the run-piplay launch-and-capture script
  (settings backup → `compactMode=true`, `topmost=true` → launch with Big Buck Bunny
  `watch?v=aqz-KE-bpKQ` → UIA popout → staged passive screenshots + REAL mouse input tests via
  `SetCursorPos`/`mouse_event` → log scrape → settings restore). Five runs total; runs 1–4 were
  driver/finding iterations, run 5 is the clean evidence run.

## Findings (the part Tasks 3/5 need)

1. **S-1 passes, but plain `SetWindowLongPtr(GWL_EXSTYLE, …|WS_EX_LAYERED)` does NOT work on a WPF
   window.** WPF's `HwndTarget` handles `WM_STYLECHANGING` and strips `WS_EX_LAYERED` from
   `styleNew` whenever the window doesn't use per-pixel opacity (`AllowsTransparency=false`), so the
   bit never lands and `SetLayeredWindowAttributes` fails with `ERROR_INVALID_PARAMETER` (87)
   (run 1: `ok=False err=87 … layered=False`).
2. **WPF also rewrites its cached exstyle wholesale during move/size/topmost operations**, silently
   dropping the bit again even with a message-swallowing guard (run 4 heartbeat: repeated
   `SPIKE S-1 reassert needed: layered=False` clustered exactly around pin toggles, drags, and
   resizes).
3. **The working mechanism (run 5, zero reasserts):** a comctl32 `SetWindowSubclass` proc (installed
   before the WPF wndproc — same mechanism as `BorderlessWindowHelper`) that, while an opacity
   target is active, **forces `WS_EX_LAYERED` into `STYLESTRUCT.styleNew` inside
   `WM_STYLECHANGING` and returns without chaining** (so `HwndTarget` can't edit it back out). The
   bit then never drops, the layered attributes are never discarded, and one
   `SetWindowLongPtr` + `SetLayeredWindowAttributes(LWA_ALPHA)` per opacity change is sufficient.
   This is the design for Task 3's `WindowOpacityApplier`.
4. **Rendering under the layered parent is correct**: video keeps playing, no black/glitched frames
   at 60%/45%, uniform alpha across the native strip + WebView2 child + window chrome (the main
   window's address bar and "Playing in Video Popout" placeholder show *through* the popout in the
   45% shot). Restore to 100% is visually clean with the layered bit still set.
5. **Input is fully preserved at every alpha (Q-8 / §7.5 / ADR-0006)**: real-mouse PinToggle clicks
   land at 60% and 45% (UIA ToggleState On↔Off, `WS_EX_TOPMOST` flips both directions), chrome-strip
   drag moves the window (exact delta 120,60), top-edge resize works at 45% and 100%.
   `WS_EX_TRANSPARENT` was never set (`transparentBit=False` in every log line).
6. **S-2 passes**: `CoreWebView2Settings.IsNonClientRegionSupportEnabled = true` (set after
   `EnsureCoreWebView2Async`, before `Navigate`) + CSS `app-region: drag` in the shell's top-level
   document gives native window drag from the DOM region — verified moving the window (delta
   100,50) *at 45% alpha*. Runtime `149.0.4022.62`, SDK `1.0.3967.48`.
7. **S-3 passes**: `DwmSetWindowAttribute(DWMWA_WINDOW_CORNER_PREFERENCE=DWMWCP_ROUND)` returns
   S_OK; corners are visibly rounded and the WebView2 child clips correctly under the curve
   (corner zoom evidence), alone and combined with S-1 alpha.
8. **Perf sanity**: 5 s CPU samples across PiPlay + all msedgewebview2 processes were flat across
   stages (0.86 / 1.14 / 0.77 CPU-seconds at 100% / 60% / restored-100%) — no measurable layered-
   window cost during playback.
9. **Driver lessons** (for the Task 6 combined smoke): captures must be passive — the
   minimize/restore raise cycle is itself an exstyle-rewrite trigger (and was healed only by the
   heartbeat); input tests must activate the window first (clicks only registered reliably after a
   real activation click); mouse moves over the WebView2 child never reach WPF's `MouseMove`, so
   strip-wake wiggles must target the strip row; NC resize from the bottom/right edges over the
   WebView child is a pre-existing borderless gap unrelated to alpha (top edge works).

## Verification

- Live evidence (run 5, all from one run): `docs/evidence/opacity-spike-s1-alpha-60pct.png`,
  `opacity-spike-s1-alpha-45pct.png`, `opacity-spike-s1-restored-100pct.png`,
  `opacity-spike-s2-appregion-drag-45pct.png`, `opacity-spike-s3-dwm-rounded-100pct.png`,
  `opacity-spike-s3-corner-zoom-br.png`, `opacity-spike-run5-log.txt`.
- Literal log lines (run 5):
  - `SPIKE S-3 applied: DWMWA_WINDOW_CORNER_PREFERENCE=ROUND hr=0x00000000.`
  - `SPIKE S-2 enabled: IsNonClientRegionSupportEnabled=true, runtime=149.0.4022.62.`
  - `SPIKE S-1 stage A (60%) applied: alpha=153 ok=True err=0 exstyle=0xC0108 layered=True transparentBit=False.`
  - `SPIKE S-1 stage B (45%) applied: alpha=115 ok=True err=0 exstyle=0xC0108 layered=True transparentBit=False.`
  - `SPIKE S-1 stage C (restore 100%) applied: alpha=255 ok=True err=0 exstyle=0xC0108 layered=True transparentBit=False.`
  - Zero `reassert needed` lines in run 5 (vs. a dozen in run 4 with the swallow-only guard).
- Driver input lines (run 5): `INPUT|60pct|pinClick|toggle=On->Off|topmost=True->False|clickLanded=True`,
  `INPUT|60pct|stripDrag|moved=True|delta=120,60`, `INPUT|45pct|pinClick|…|clickLanded=True`,
  `INPUT|45pct|appRegionDrag|moved=True|delta=100,50`, `INPUT|45pct|topEdgeResize|…|resized=True`,
  `INPUT|100pct|topEdgeResize|…|resized=True`.
- Local gates after reverting all spike code: `dotnet test PiPlay.sln --configuration Debug` —
  **354/354, 0 skipped**; `.\Build-PiPlay.ps1 -Stage Build -NoVersionBump -NoBuildNumberBump` —
  **0W/0E**.
- **What this spike could not exercise live**: long-session stability under alpha (runs were ~90 s),
  multi-monitor/DPI-change behavior of the layered window, Windows 10 (no-op path for S-3 is
  documented but untested here — this machine is Windows 11), and fade *animation* of alpha
  (stages were hard cuts; production animates via `FadePolicy.FadeDurationMs`).

## Disposition

- S-1/S-2/S-3 all pass → Tasks 3 and 5 are unblocked; the `WebView2CompositionControl` fallback
  tier is NOT needed.
- Production carry-overs for Task 3: the force-bit `WM_STYLECHANGING` subclass (finding 3), opacity
  application as `SetLayeredWindowAttributes(LWA_ALPHA)` on the top-level HWND, never
  `WS_EX_TRANSPARENT`, and the S-2 setting placed after `EnsureCoreWebView2Async` / before
  `Navigate`.
- No production code from the spikes was committed (verified: working tree clean except docs).

## Commits

- `chore(spike): record layered-alpha / app-region / DWM-rounding spike results` — this worklog,
  evidence files, plan tick.
