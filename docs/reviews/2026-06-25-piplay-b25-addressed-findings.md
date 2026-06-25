# PiPlay b25 addressed findings - 2026-06-25

## Scope

Address pass for `docs/reviews/2026-06-25-piplay-b25-current-work-review.md`.

## Addressed

- **REQ-RETURN-01 mismatch:** resolved as Option A. Return follows the Popout Player's live paused/playing
  state when known; source-was-playing at launch is fallback-only.
- **Launch-from-paused intent:** `PlayerWindow` now receives source launch intent and only nudges play when
  the source was playing at popout launch.
- **Different-video return replay:** `ReturnAction.Navigate` now queues a pending playback-state replay for
  paused/volume/mute/rate after the Source Window reaches the returned video.
- **Source suppression:** launch now mutes+pauses the source and starts a short reassertion guard while the
  popout owns playback.
- **Final capture authority:** explicit bring-back stops the sync timer before the final DOM read and guards
  in-flight timer writes from overwriting that final capture.
- **Retarget sampling:** popout retarget/allowed navigation resets sampled media state and resumes DOM
  sampling only after successful navigation completion.
- **Shutdown/persistence:** main-window shutdown now skips source-return scripting while still allowing
  popout placement/settings persistence; placement/settings are saved before source-return scripts run.
- **Docs drift:** spec, QA, changelog, `SPEC_GAPS`, `docs/AGENTS.md`, and the follow-up plan now reflect the
  chosen rule and current 4 DIP wording. The malformed arbitration heading was repaired.

## Still Needs Runtime QA

- Real YouTube/WebView2 duplicate-audio smoke through ads, autoplay-next, and SPA re-renders.
- Different-video return replay on a live page, especially whether YouTube accepts immediate replay of
  volume/mute/rate/playback speed after navigation.
- X-close still relies on the latest timer-sampled media state rather than an async close-deferral fresh DOM
  read. The explicit Source Window **Bring video back** path does perform the fresh read.
- Stable publish/verify and deployed Stable manual QA before any RC tag.

## Automated Check Run So Far

- `dotnet test PiPlay.sln --configuration Debug --no-restore --filter "FullyQualifiedName~ReturnPolicyTests|FullyQualifiedName~WpfRuntimeTests"` passed: 116/116.
