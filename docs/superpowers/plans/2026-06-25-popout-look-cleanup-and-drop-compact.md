# Popout Look Cleanup + Drop Embed Compact — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Spec:** `docs/superpowers/specs/2026-06-25-popout-look-cleanup-and-drop-compact-design.md`

**Goal:** Quiet the grey control borders and remove the embed "Compact player" feature, so PiPlay reads cleaner without any window-hosting change.

**Architecture:** Pure token + UI-removal changes. Soften two color tokens in `Colors.xaml` (they feed every control border app-wide). Gate the embed Compact player behind a `false` kill-switch in `PlaybackModePolicy` (code kept dormant, not deleted) and point the popout-creation call site at the gated resolver. Remove the Settings toggle. No transparency, no WebView2 clipping, no shell deletion.

**Tech Stack:** Windows WPF (.NET 10), xUnit (lane-filtered by `[Trait]`), WebView2. Tests run headless via `dotnet test`.

## Global Constraints

- Stacks on commit `9e58734` (filled accent buttons + profile/accent split); working tree otherwise clean except untracked `PRI-READ/`.
- **No architecture lift:** no `AllowsTransparency` change, no WebView2 composition/region-clip, no card radius beyond the OS DWM rounding, no gradient on the window silhouette.
- **Keep, don't delete:** `PlaybackMode.Compact`, `PlaybackModePolicy.ResolveEffectiveMode`, the player shell/IFrame assets, and `Profile.Mode` stay in place but dormant (the shell is wired to the compact timestamp path).
- **Borders quieted, not removed** — keep focus rings (REQ-UI-02 accessible affordances).
- Test lanes: markup `--filter "FullyQualifiedName~XamlInvariantTests"`, logic `--filter "FullyQualifiedName~PlaybackModePolicyTests"`, wpf `--filter "FullyQualifiedName~SettingsWindowAppearanceTests"`; full gate `dotnet test PiPlay.sln --configuration Debug --nologo`.
- Commit style: conventional (`feat(...)`, `fix(...)`, `chore(...)`), and end every commit message with the two trailers:
  ```
  Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01B5EybkqNL3eRSQW9jX3niD
  ```

---

### Task 1: Quiet the grey border tokens

Softens the two color tokens that render every control outline (URL box, Profiles combo, buttons, panels, Settings dialog) via `ControlStyles.xaml`/`SettingsWindow.xaml`, so the UI stops reading as a grey box. Old hard greys: `BorderSubtleColor #FF2B3645`, `BorderStrongColor #FF3E4B5C`.

**Files:**
- Modify: `src/PiPlay/Theme/Colors.xaml:22-23`
- Test: `tests/PiPlay.Tests/Ui/XamlInvariantTests.cs` (add one `[Fact]`)

**Interfaces:**
- Consumes: nothing (leaf token change).
- Produces: token values `BorderSubtleColor = #FF181F29`, `BorderStrongColor = #FF262F3D` (consumed only as `DynamicResource` by existing styles — no signature changes).

- [ ] **Step 1: Write the failing test**

Add to `XamlInvariantTests.cs` (Layer 1 markup; mirrors the existing `Theme/Colors.xaml` literal gates at lines ~634/674):

```csharp
[Fact]
public void Grey_border_tokens_are_quieted_to_a_faint_hairline()
{
    var colors = XamlTestFiles.Load("Theme/Colors.xaml");
    string ColorOf(string key) => colors
        .Descendants(XamlTestFiles.Pres + "Color")
        .Single(e => (string?)e.Attribute(XamlTestFiles.X + "Key") == key)
        .Value.Trim();

    // Softened from the old hard greys (#FF2B3645 / #FF3E4B5C) so control outlines read as a
    // faint hairline on the dark UI instead of a boxed-in grey rectangle (owner review P1/P2).
    Assert.Equal("#FF181F29", ColorOf("BorderSubtleColor"));
    Assert.Equal("#FF262F3D", ColorOf("BorderStrongColor"));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test PiPlay.sln --configuration Debug --filter "FullyQualifiedName~XamlInvariantTests.Grey_border_tokens_are_quieted_to_a_faint_hairline" --nologo`
Expected: FAIL — actual is `#FF2B3645` / `#FF3E4B5C`.

- [ ] **Step 3: Apply the token change**

In `src/PiPlay/Theme/Colors.xaml`, lines 22-23:

```xml
  <Color x:Key="BorderSubtleColor">#FF181F29</Color>
  <Color x:Key="BorderStrongColor">#FF262F3D</Color>
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test PiPlay.sln --configuration Debug --filter "FullyQualifiedName~XamlInvariantTests" --nologo`
Expected: PASS (new fact + all existing markup invariants).

- [ ] **Step 5: Record the contrast for the record**

`ContrastReportTests` is a *floor* tool (asserts ratio ≥ floor), so it does not fit a deliberately-quiet border (we want a low ratio). Instead, note the chosen contrast in the Task 4 changelog line: compute `Wcag.ContrastRatio("#FF181F29", "<AppBackground hex from Colors.xaml>")` mentally/with the existing report run and record it (expected ≈ 1.1–1.3:1 — present but quiet). No test row added.

- [ ] **Step 6: Commit**

```bash
git add src/PiPlay/Theme/Colors.xaml tests/PiPlay.Tests/Ui/XamlInvariantTests.cs
git commit  # message: "feat(theme): quiet the grey control-border tokens" + trailers
```

---

### Task 2: Remove the embed Compact toggle from Settings

Removes the user-facing "Compact player" control and its handler. Keeps the `compactMode` constructor param + `CompactMode` pass-through property so the stored value still round-trips (the popout path ignores it after Task 3).

**Files:**
- Modify: `src/PiPlay/SettingsWindow.xaml:193-205` (remove the Playback header, hint, toggle; keep one separator)
- Modify: `src/PiPlay/SettingsWindow.xaml.cs:102` (drop `CompactModeToggle.IsChecked = compactMode;`) and `:240-244` (remove `CompactModeToggle_Click`)
- Test: `tests/PiPlay.Tests/Ui/SettingsWindowAppearanceTests.cs` (add one `[Fact]`)

**Interfaces:**
- Consumes: nothing.
- Produces: `SettingsWindow` no longer contains named elements `CompactModeToggle` or `CompactModeHintText`. The `SettingsWindow.CompactMode` property and `compactMode` ctor param remain (silent pass-through for persistence).

- [ ] **Step 1: Write the failing test**

Add to `SettingsWindowAppearanceTests.cs` (Wpf lane; mirrors the existing STA `FindName` tests):

```csharp
[Fact]
public void SettingsWindow_no_longer_offers_the_embed_compact_toggle() => StaTestThread.Invoke(() =>
{
    var w = new SettingsWindow(isBrowserReady: true);
    Assert.Null(w.FindName("CompactModeToggle"));
    Assert.Null(w.FindName("CompactModeHintText"));
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test PiPlay.sln --configuration Debug --filter "FullyQualifiedName~SettingsWindowAppearanceTests.SettingsWindow_no_longer_offers_the_embed_compact_toggle" --nologo`
Expected: FAIL — both elements still resolve (not null).

- [ ] **Step 3: Remove the toggle markup**

In `src/PiPlay/SettingsWindow.xaml`, delete the Playback header, the hint text, and the toggle (current lines 192-203), leaving the separator at line 190 and the next separator/section intact. After the edit, the separator that was at line 205 is removed too (it bracketed the now-gone section); the block between the line-190 separator and the next section (Advanced) is gone:

```xml
        <!-- (removed) Playback section: embed Compact player dropped 2026-06 — embed-disabled
             videos break it for near-zero visible gain. New popouts are always Normal. -->
```

- [ ] **Step 4: Remove the code wiring**

In `src/PiPlay/SettingsWindow.xaml.cs`:
- Delete line 102 `CompactModeToggle.IsChecked = compactMode;` (keep line 101 `CompactMode = compactMode;`).
- Delete the handler (lines 240-244):

```csharp
    private void CompactModeToggle_Click(object sender, RoutedEventArgs e)
    {
        CompactMode = CompactModeToggle.IsChecked == true;
        AppearanceChanged = true;
    }
```

(Keep the `CompactMode` property and the `compactMode` ctor param — the settings-save caller reads `SettingsWindow.CompactMode` to persist; leaving it a silent pass-through keeps the stored value intact.)

- [ ] **Step 5: Run tests to verify pass**

Run: `dotnet test PiPlay.sln --configuration Debug --filter "FullyQualifiedName~SettingsWindowAppearanceTests" --nologo`
Expected: PASS (new fact + existing appearance tests). If any existing test set `compactMode:` and asserted the toggle, update it to drop the toggle assertion.

- [ ] **Step 6: Commit**

```bash
git add src/PiPlay/SettingsWindow.xaml src/PiPlay/SettingsWindow.xaml.cs tests/PiPlay.Tests/Ui/SettingsWindowAppearanceTests.cs
git commit  # message: "feat(settings): remove the embed Compact player toggle" + trailers
```

---

### Task 3: Disable embed Compact for new popouts (kill-switch)

New popouts always launch Normal (the full watch page — never breaks on embed-disabled videos). The compact resolver stays intact and tested; a `false` master switch overrides it at the popout call site.

**Files:**
- Modify: `src/PiPlay/Services/PlaybackModePolicy.cs` (add `CompactPlayerEnabled` + `ResolveEffectivePopoutMode`)
- Modify: `src/PiPlay/MainWindow.xaml.cs:818-819` (call the gated resolver)
- Test: `tests/PiPlay.Tests/PlaybackModePolicyTests.cs` (add one `[Fact]`)

**Interfaces:**
- Consumes: existing `PlaybackModePolicy.ResolveEffectiveMode(string?, bool)` and `PlaybackMode.Normal`.
- Produces:
  - `public const bool PlaybackModePolicy.CompactPlayerEnabled = false;`
  - `public static PlaybackMode PlaybackModePolicy.ResolveEffectivePopoutMode(string? profileMode, bool globalCompact)` — returns `Normal` when disabled, else delegates to `ResolveEffectiveMode`.

- [ ] **Step 1: Write the failing test**

Add to `PlaybackModePolicyTests.cs` (Logic lane):

```csharp
[Fact]
public void Embed_compact_player_is_disabled_so_new_popouts_resolve_to_normal()
{
    Assert.False(PlaybackModePolicy.CompactPlayerEnabled);
    // The kill switch overrides BOTH a global compact default and a per-profile compact override.
    Assert.Equal(PlaybackMode.Normal, PlaybackModePolicy.ResolveEffectivePopoutMode(null, globalCompact: true));
    Assert.Equal(PlaybackMode.Normal, PlaybackModePolicy.ResolveEffectivePopoutMode("compact", globalCompact: true));
    Assert.Equal(PlaybackMode.Normal, PlaybackModePolicy.ResolveEffectivePopoutMode("compact", globalCompact: false));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test PiPlay.sln --configuration Debug --filter "FullyQualifiedName~PlaybackModePolicyTests.Embed_compact_player_is_disabled_so_new_popouts_resolve_to_normal" --nologo`
Expected: FAIL — `CompactPlayerEnabled` / `ResolveEffectivePopoutMode` do not exist (compile error).

- [ ] **Step 3: Add the kill-switch to the policy**

In `src/PiPlay/Services/PlaybackModePolicy.cs`, add near `ResolveEffectiveMode`:

```csharp
    /// <summary>
    /// Master switch for the embed Compact player. Dropped 2026-06: the embedded IFrame breaks on
    /// embed-disabled videos for near-zero visible gain (owner review §4). False = new popouts are
    /// always Normal. The Compact path (ResolveEffectiveMode, the shell/IFrame, Profile.Mode) is kept
    /// DORMANT behind this flag rather than deleted, because the shell is the compact timestamp source.
    /// </summary>
    public const bool CompactPlayerEnabled = false;

    /// <summary>
    /// Effective popout mode honoring <see cref="CompactPlayerEnabled"/>: Normal whenever Compact is
    /// disabled, otherwise the profile/global resolution. The popout-creation path calls THIS, not
    /// <see cref="ResolveEffectiveMode"/> directly.
    /// </summary>
    public static PlaybackMode ResolveEffectivePopoutMode(string? profileMode, bool globalCompact) =>
        CompactPlayerEnabled ? ResolveEffectiveMode(profileMode, globalCompact) : PlaybackMode.Normal;
```

- [ ] **Step 4: Point the call site at the gated resolver**

In `src/PiPlay/MainWindow.xaml.cs`, lines 818-819, change `ResolveEffectiveMode` to `ResolveEffectivePopoutMode`:

```csharp
            var mode = PlaybackModePolicy.ResolveEffectivePopoutMode(
                ResolveActiveProfileMode(target.VideoId), _settings.Player.CompactMode);
```

- [ ] **Step 5: Run tests to verify pass**

Run: `dotnet test PiPlay.sln --configuration Debug --filter "FullyQualifiedName~PlaybackModePolicyTests" --nologo`
Expected: PASS (new fact + the existing `ResolveEffectiveMode` theories, which are unchanged because the pure resolver is untouched).

- [ ] **Step 6: Commit**

```bash
git add src/PiPlay/Services/PlaybackModePolicy.cs src/PiPlay/MainWindow.xaml.cs tests/PiPlay.Tests/PlaybackModePolicyTests.cs
git commit  # message: "feat(playback): always-Normal popouts; gate embed Compact off" + trailers
```

---

### Task 4: Changelog + full gate

**Files:**
- Modify: `docs/CHANGELOG.md` (Unreleased section)

- [ ] **Step 1: Add the changelog entry**

Under `## [Unreleased]` in `docs/CHANGELOG.md`:

```markdown
- Quieted the grey control-border tokens (`BorderSubtle`/`BorderStrong`) to a faint hairline so the
  UI no longer reads as a boxed-in browser window (owner review P1/P2).
- Removed the embed "Compact player" setting; new popouts always use the full watch page (the embed
  player broke on embed-disabled videos for near-zero visible gain). The compact code path is kept
  dormant behind `PlaybackModePolicy.CompactPlayerEnabled = false`.
```

- [ ] **Step 2: Run the full Lane A gate**

Run: `dotnet test PiPlay.sln --configuration Debug --nologo`
Expected: PASS, 0 failed (≈685 tests: 682 prior + 3 new facts).

- [ ] **Step 3: Whitespace check**

Run: `git diff --check`
Expected: clean (CRLF normalization warnings are fine).

- [ ] **Step 4: Commit**

```bash
git add docs/CHANGELOG.md
git commit  # message: "docs(changelog): record border quiet + compact removal" + trailers
```

- [ ] **Step 5: Hand off for deploy + visual judging**

Stop here for the owner. Manual/visual QA is owner-gated (deploy via `Publish-Stable.ps1`, then judge on `E:\...\PiPlay.exe`). Report: build green, what changed, and that the big-card escalation (larger radius / gradient edge) was deliberately deferred.

---

### Task 5 (OPTIONAL — defer): subtle popout card edge

Only do this if, after Tasks 1–4 are deployed, the popout still reads flat. Adds a quiet 1px inner border (or faint top→bottom gradient ring) just inside the popout's DWM-rounded edge — no transparency. Scope when/if requested; not part of the core deliverable.

---

## Self-Review

**1. Spec coverage:**
- Part A (quiet borders) → Task 1. ✓
- Part B (drop embed Compact: UI + always-Normal + keep plumbing dormant) → Task 2 (UI) + Task 3 (kill-switch, code kept). ✓
- Part C (optional card edge) → Task 5 (deferred, matches spec's "ship A+B first"). ✓
- Part D (opacity confirm, no product change) → no task needed; covered by the spec note + Task 4 hand-off. ✓
- Testing strategy (markup/logic/wpf + full gate) → Tasks 1-4. ✓
- Deviation: spec said "validate with `ContrastReportTests`"; that file is floor-only, so Task 1 Step 5 records the ratio in the changelog instead and pins the literals via markup. Noted.

**2. Placeholder scan:** No TBD/TODO; every code step shows full code; commands have expected output. ✓

**3. Type consistency:** `CompactPlayerEnabled` (const) and `ResolveEffectivePopoutMode(string?, bool)` are defined in Task 3 and used identically in its test and the MainWindow call site. `CompactMode`/`compactMode` pass-through in Task 2 matches the existing `SettingsWindow` members. ✓
