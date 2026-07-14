# Efficiency and customization hardening - implementation plan

**Spec:** `docs/superpowers/specs/2026-07-14-efficiency-and-customization-hardening-design.md`

**Goal:** Remove the proven recurring IPC, duplicate startup/render work, and accent-preview fan-out
without changing stored customization semantics, then deploy a verified diagnostic Stable copy.

**Result:** Complete. All 707 tests, the non-mutating Release build, spec gate, and diff gate pass.
A verified diagnostics-only Stable copy is deployed for manual testing.

## Tasks

- [x] **Task 1 - Harden recurring WebView work and shutdown.**
  - Add Auto preflight, navigation-scoped single-flight Popout polling, and close/shutdown guards.
  - Verify with `dotnet test PiPlay.sln --configuration Debug --filter
    "FullyQualifiedName~AutoPopoutPolicyTests|FullyQualifiedName~WpfRuntimeTests" --nologo`.
  - Commit: `fix(runtime): bound WebView polling and shutdown work (Q-2 Q-6)`

- [x] **Task 2 - Bound customization work and restore pressed feedback.**
  - Coalesce accent previews, split accent-only Popout application, cache hue-disc bitmaps, and add the
    dark-accent pressed fallback without changing stored RGB values.
  - Verify with `dotnet test PiPlay.sln --configuration Debug --filter
    "FullyQualifiedName~ThemeColorsTests|FullyQualifiedName~AccentColorPickerTests|FullyQualifiedName~WpfRuntimeTests" --nologo`.
  - Commit: `perf(ui): bound accent preview and picker work (REQ-UI-01)`

- [x] **Task 3 - Remove duplicate startup settings work.**
  - Reuse boot settings in the production Source Window and deserialize from one parsed JSON document.
  - Verify with `dotnet test PiPlay.sln --configuration Debug --filter
    "FullyQualifiedName~SettingsServiceTests|FullyQualifiedName~WpfRuntimeTests" --nologo`.
  - Commit: `perf(startup): reuse parsed settings snapshot (Q-6)`

- [x] **Task 4 - Run the full gate and deploy the fresh diagnostic copy.**
  - Run the full Debug suite, non-mutating Release build, spec preflight, diff checks, diagnostic Stable
    publish, and diagnostics-only Stable verification. Preserve the existing `PiPlayData` folder and
    unrelated untracked files.
  - Verify with `dotnet test PiPlay.sln --configuration Debug --nologo`,
    `.\Build-PiPlay.ps1 -Stage Build -NoVersionBump -NoBuildNumberBump`,
    `.\scripts\Preflight-SpecGate.ps1`, `git diff --check`,
    `.\scripts\Publish-Stable.ps1 -AllowDirty -SkipTests`, and
    `.\scripts\Verify-StableDeploy.ps1 -AllowNonReleaseEvidence`.
  - Commit: `docs(qa): record efficiency hardening verification`

## Self-review

- Requirements -> tasks: Q-2/Q-6 and section 22.4 -> Task 1/3; REQ-UI-01 and REQ-PROFILE-01 ->
  Task 2; ADR-0007 -> Task 4.
- Ownership: DOM scripts stay centralized in `YouTubeDomBridge`; theme resources stay in
  `ThemeResourceApplier`; raw settings/profile colors stay unchanged; release engineering is deferred.
- Risk: async navigation generation and modal preview finalization are the concentrated risks; logic and
  WPF seams cover them before the full gate.
- Verified: 707/707 tests; zero-warning Release build; spec/diff gates pass; all 21 deployed
  artifacts re-hash clean under diagnostic Stable label `20260714-023107-v0.7.2-b25-stable`.
