# Codex opinion - b25 return-rule gate

Date: 2026-06-25

Base reviewed: local `main` at `a4fe91c`, with the pending docs-only review packet still
uncommitted.

## Short Opinion

Do not push or RC-tag the current local stack until the return-rule decision is resolved in docs and
tests.

My recommendation is **Option A: bless popout-state-wins**.

Reason: once the video is in the Popout Player, that window is the active playback surface. A user who
pauses, resumes, changes volume, mutes, or changes speed in the popout expects the Source Window to
receive that current session state when the video comes back. Treating close-via-X differently from the
new **Bring video back** action would preserve older wording, but it creates two return meanings for the
same single-player lifecycle.

The current implementation direction is therefore defensible. The problem is not that the code is
obviously wrong; the problem is that the normative product spec still says the opposite.

## What I Would Do

1. Keep `fbe77c9`'s general direction: captured popout state should win when known.
2. Update REQ-RETURN-01, the requirements matrix, SPEC_GAPS, and QA rows so they state the new rule:
   popout live paused state wins when known; source-was-playing is the fallback only when popout state
   is unknown.
3. Add explicit QA coverage for both return paths:
   - **Bring video back** button.
   - Popout close button / X.
4. Fix Task 3b before calling P4 complete: returned-video navigation still needs a post-navigation
   replay for paused/volume/mute/playback-rate.
5. Do not reopen broad P1/P2/P5/P7 work in this packet. Keep those as separate gated follow-up.

## Risk If We Choose Option A

There is one visible behavior change: if a source was paused at popout time, the popout starts playing,
and the user closes the popout, the Source Window will resume playing. That is different from the old
REQ-RETURN-01 wording.

I think that is acceptable if the product model is "return the live popout session." It is not
acceptable if the product model is "closing the popout restores the source's original playback intent."
The docs must choose one model.

## Why I Do Not Prefer Option B

Option B would scope live paused-state preservation only to the explicit **Bring video back** command
and keep close-via-X source-anchored. That is safer relative to old text, but it makes the close button
semantically special in a way users are unlikely to understand. It also means two ways of returning the
same popout can produce different playback outcomes.

If owner intent is strict "X means restore original source state," then Option B is the correct product
choice. But absent that explicit owner call, I would align all return paths to the live popout state.

## Current Package Readiness

The current local package has already passed the deterministic automated gate at `a4fe91c`:

- `dotnet clean PiPlay.sln --configuration Debug --nologo`
- `dotnet test PiPlay.sln --configuration Debug --nologo` - 683/683 passed
- `./Build-PiPlay.ps1 -Stage Build -NoVersionBump -NoBuildNumberBump`
- `git diff --check`

That proves the package is build/test clean. It does not resolve the product-rule conflict above and
does not replace deployed Stable manual smoke.

## Push Recommendation

Hold the push. Commit the pending review packet only after the zip has been reviewed, then resolve the
return-rule decision, rerun QA, and push once the spec/code/test story is internally consistent.

