# Owner Review Grounding Pass

Date: 2026-06-24

Scope: review the current docs-only working tree against the latest owner review now retained at
`docs/reviews/2026-06-23-owner-appearance-popout-compact-review.md`, plus the current WPF/WebView2
implementation seams for popout activation, compact player terminology, theme corners, opacity, and
profile accent behavior.

## Verdict

No product-code blocker found in this pass. The working docs correctly treat the owner review as
direction, separate it from current code truth, and preserve runtime uncertainty where code inspection
cannot prove the observed behavior.

The only repo hygiene issue found during review was that the new docs originally cited
`PRI-READ/revviews-23.06.2026.md`, an untracked/private review input. That would have left committed
docs pointing at a file outside the repo evidence surface. The owner review has now been copied into
`docs/reviews/2026-06-23-owner-appearance-popout-compact-review.md`, and the new docs cite that tracked
path.

## Findings

### Owner point trace

| Owner review point | Reflected where | Verification |
|---|---|---|
| Themes need stronger perceived differences; current tokens do not make the final window feel different enough. | `docs/Theme_Preset_Differences.md` under "Token Differences vs Perceived Window Impact"; `docs/SPEC_GAPS_AND_OWNERSHIP.md` owner direction 2.1 / 5. | Captured as a real perception gap while preserving the code-backed token inventory. |
| Add **Blackout** and make theme presets visibly distinct. | `docs/SPEC_GAPS_AND_OWNERSHIP.md` owner direction 2.1 / 5; `docs/Theme_Preset_Differences.md` notes Blackout as requested direction, not current code. | Present as future direction, not falsely listed as implemented. |
| Separate global app accent from profile identity color. | `docs/SPEC_GAPS_AND_OWNERSHIP.md` owner direction 2.2 / section 9 and open sub-decision "Accent model reversal"; `docs/PiPlay_Product_Engineering_Spec.md` profile `accentColor` note and open decision list. | Present and correctly flagged as a shipped behavior reversal needing sign-off. |
| Let users pick any accent color; choose readable text automatically; move border strength/opacity out of the readability gate. | `docs/SPEC_GAPS_AND_OWNERSHIP.md` owner direction 2.3 and open sub-decision "Accent gate relax"; `docs/PiPlay_Product_Engineering_Spec.md` open decision list. | Present; current auto-foreground behavior is separated from the current blocking gate. |
| Corner settings must affect the actual popout silhouette, video clipping, border, and shadow. | `docs/PiPlay_Product_Engineering_Spec.md` shape-token caution; `docs/SPEC_GAPS_AND_OWNERSHIP.md` owner direction 2.4 / 7 and open sub-decision "Corner silhouette architecture"; `docs/Theme_Preset_Differences.md` DWM-owned corner limitation. | Present and correctly escalated as an architecture choice because WebView2 airspace blocks the requested large rounded-card effect today. |
| Placeholder should offer direct actions such as Show popout / Restore video here. | `docs/PiPlay_Product_Engineering_Spec.md` Source Placeholder note; `docs/SPEC_GAPS_AND_OWNERSHIP.md` owner direction 3.1 / 8 and open sub-decision "Restore video here". | Present; Show popout is treated as cheaper because activation exists, while Restore video here remains open. |
| Toolbar should restore/focus the existing popout and avoid duplicates. | `docs/SPEC_GAPS_AND_OWNERSHIP.md` "Already present in code" row for 3.2; this review artifact's "Show popout" finding. | Present as mechanism-present but runtime-QA-needed, which matches what code inspection can prove. |
| "Compact mode" in the owner review means a smaller player-first main-window layout, not just the shipped Compact player playback surface. | `docs/PiPlay_Product_Engineering_Spec.md` playback-mode terminology note; `docs/SPEC_GAPS_AND_OWNERSHIP.md` terminology section and main-window mode model open sub-decision. | Present and disambiguated from the existing popout Compact player. |
| Add Browse / Cinema / Compact / Popout UX modes. | `docs/SPEC_GAPS_AND_OWNERSHIP.md` owner direction 4 / 6 and open sub-decision "Main-window mode model"; `docs/PiPlay_Product_Engineering_Spec.md` open decision list. | Present as net-new UX mode work. |
| Reduce default transparency and constrain transparency to a controlled visual effect. | `docs/SPEC_GAPS_AND_OWNERSHIP.md` owner direction P1 and open sub-decision "Transparency band"; `docs/Theme_Preset_Differences.md` behavior defaults and token-impact note. | Present; current defaults are documented as already opaque except Soft Glass. |
| Add border mode/strength, shadow strength, and hover-reveal chrome. | `docs/SPEC_GAPS_AND_OWNERSHIP.md` owner priorities and open sub-decisions for corner silhouette, main-window mode model, and transparency/accent relax. | Present as P2 future direction, not current code. |
| Preserve the implementation intent split: Profiles = content, Appearance = look, Popout state = detachment/focus/restore behavior. | `docs/SPEC_GAPS_AND_OWNERSHIP.md` "Organizing frame"; `docs/PiPlay_Product_Engineering_Spec.md` profile `accentColor` and open decision notes. | Present as the organizing model for follow-up work. |

### P2 - Owner review source needed to be tracked before docs cite it

The owner review was present only under `PRI-READ/`, while the docs changes cited that path from
`docs/SPEC_GAPS_AND_OWNERSHIP.md` and `docs/Theme_Preset_Differences.md`.

Resolution in this pass:

- Added `docs/reviews/2026-06-23-owner-appearance-popout-compact-review.md` as the durable review input.
- Updated the docs citations away from `PRI-READ/...` to the tracked review path.

### No blocker - Popout "Show popout" ground truth is represented accurately

Code inspection confirms the docs' careful wording:

- `MainWindow.xaml.cs` short-circuits `StartVideoPopoutAsync` when `_player` already exists and calls
  `ActivateExistingPlayer`.
- `ActivateExistingPlayer` restores a minimized popout and calls `Activate()`.
- `UpdatePopOutButtonState` flips visible text and automation name between `Pop out video` and
  `Show popout`.

That supports "mechanism present, runtime QA still needed" rather than claiming the owner-reported UX
issue is fixed.

### No blocker - Compact terminology split is necessary

The current app already has a **Compact player** playback-surface mode for the popout shell. The owner
review's **Compact Mode** request is a different main-window layout/mode concept. The docs correctly
separate those axes and should keep doing so; implementing the owner request would be net-new UX mode
work, not a bug fix to `PlaybackModePolicy` alone.

### No blocker - Corner silhouette limitation is correctly escalated as architecture

The current windows host WebView2 with `AllowsTransparency=False` and use DWM corner preferences for
top-level window shape. The docs correctly avoid promising a large rounded card silhouette through the
existing token system. A true curve-following border/shadow/video clip remains an architecture decision,
likely ADR-worthy, because it changes the WebView2 airspace constraints.

### No blocker - Accent/profile reversal is correctly treated as a behavior reversal

The current profile accent behavior is shipped and test-locked: active profile color resolves as an app
accent override. The owner review wants global accent plus profile identity markers. The docs correctly
treat this as a product reversal requiring sign-off, not as a cleanup refactor.

## Validation

- `git diff --check`: passed before this artifact was written.
- Read-only source checks covered `MainWindow.xaml.cs`, `PlayerWindow.xaml`, `MainWindow.xaml`,
  `ThemeCatalog.cs`, `WindowOpacityPolicy.cs`, `ProfileAccentService`, and related tests via `rg`.
- Runtime QA was not performed in this pass.
