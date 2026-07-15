# Session worklog — Focused Popout and rounded-region stabilization (2026-07-15)

Saved record of the owner-directed Popout feature pass, deep audit, remediation, and release-candidate
packaging work.

## Request

> Continue the owner-approved media-first Popout direction: optional Opera-style Focused controls,
> drag from passive picture pixels, easier native resize acquisition, and a real rounded Soft Glass
> silhouette. After seeing the diagnostics build on the desktop, the owner asked whether anything was
> left, said it looked "pretty cool," and then said "go."

## What was reviewed

- Repo rules, product/engineering spec, YouTube compliance boundary, QA checklist, ownership gaps,
  theme/preset contract, ADRs, and the July 14 Popout interaction worklog.
- Source and Popout XAML, settings/profile persistence, WebView2 initialization/navigation/teardown,
  generated YouTube scripts, host-message protocols, borderless resize, native window regions, and
  existing Logic/Markup/WPF regression seams.
- A 48-path audit-start tree and the 50-path remediated implementation tree, including lifecycle,
  performance, native-resource, security-boundary, compliance, and maintainability review. The final
  54-path package adds this worklog plus the CI/executable-DOM harness evidence lane.
- The sanctioned Stable diagnostics copy and its manifest at
  `E:\Dev_test_implemenations\PiPlay\PiPlay.exe`.

## Decisions

- Keep Standard as the default presentation and make Focused a new-popout global/profile choice;
  do not revive Compact or replace the real signed-in YouTube watch page.
- Keep `object-fit: contain`, preserve native YouTube/ad controls, and fail closed during active ads.
- Treat passive-picture drag as a thresholded, trusted top-document capability with exact-schema,
  nonce-bearing, current-document messages. Preserve the 44 DIP native handle as recovery.
- Keep the standard WebView2 HWND and `AllowsTransparency=False`; use a DPI-scaled top-level region
  only for Soft Glass / explicit Round, clearing it for maximized and snap-like layouts.
- Land Focused, rounded-region, and audit-remediation work as one consolidated feature commit because
  their core files and tests overlap. Use a separate version/build stamp commit.
- Treat every `-AllowDirty` deploy as exploratory only. The feature release candidate is
  `v0.11.0` / build `34`, published only from a clean exact-source commit.

## Implementation

- New: Focused presentation policy; passive-drag and Focused host bridges/protocols; rounded-region
  policy/applier; ADR-0008; focused/rounded design and plan records; deep audit; logic/WPF regressions;
  and a dependency-free executable-DOM harness wired into CI.
- Edited: Source/Popout/Settings surfaces, profile/settings models and services, theme catalog,
  centralized YouTube DOM behavior, native resize/window helpers, living specs, compliance, QA,
  changelog, ownership map, and test index.
- Remediation: navigation-scoped document tokens, trusted-event gates, ad fail-closed behavior,
  local-before-await bridge ownership, latest-wins appearance IPC, bounded active-only page updates,
  top-document-only drag installation, native move-loop truth, and reduced DWM/region churn.

## Verification

- Full post-remediation Debug suite: **937 passed, 0 failed**, including eight executable fake-DOM
  scenarios driven through the real generated YouTube scripts.
- RID-specific Release build: **0 warnings, 0 errors**.
- Targeted lifecycle/UI slice: **210 passed**; targeted surface/region/presentation slice:
  **104 passed**.
- All six generated JavaScript programs and the dependency-free Node harness passed syntax checks;
  spec gate and `git diff --check` passed; no debug-marker or literal-secret matches remained.
- Diagnostics Stable label: `20260715-080621-v0.10.1-b33-stable`; all 21 deployed artifacts re-hashed
  clean, but the manifest correctly records `releaseEvidence=false` and `sourceDirty=true`.
- Exploratory live check: the Source Placeholder rendered without WebView bleed, Focused controls were
  exposed through accessibility, and the guaranteed native top handle moved the Popout. The first
  picture-drag probe landed on the intentionally excluded centered Play/Pause control; passive-picture
  drag remains a clean-candidate manual row rather than a claimed pass.
- Outstanding deployed release evidence: real ad states, real-WebView2 synthetic/current-document
  action exercise, Standard/Focused navigation and return, double-audio confirmation, 100/125/150%
  and mixed-DPI rounded-window inspection, 50 open/close cycles, and the 30–60 minute settling soak.

## Disposition

- Branch: `main`, based on exact released baseline `stable-v0.10.1-b33` / `804b693`.
- Packaging: consolidated implementation/docs/tests commit, followed by a `v0.11.0` / build `34`
  release-stamp commit and clean Stable publish.
- Remote push is a separate owner-confirmed action after local release verification.

## Commits

- Consolidated Focused Popout, rounded-region, and stabilization commit: this record ships with it;
  use Git history for the immutable id.
- Release stamp: follows after the integrated source passes the finalized verification loop.
