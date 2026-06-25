# P1 — Borderless Window Surface — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax.

**Spec:** `docs/superpowers/specs/2026-06-25-p1-borderless-design.md`

**Goal:** Make PiPlay read borderless — no resting control outlines, no Settings box — on the current opaque-HWND model, with the WebView resize-band reduction held as a conditional follow-up.

**Architecture:** Pure XAML/token + test changes. Flip resting `BorderBrush` setters to `Transparent` (keep thickness so layout is stable and focus triggers still render), zero the Settings dialog border. No `AllowsTransparency`, no composition lift. The WebView resize band (Task 4) is **conditional** — only if the already-black band still reads as a frame on the deployed build.

**Tech Stack:** WPF (.NET 10), xUnit lane-filtered by `[Trait]`.

## Global Constraints

- Build base: `main` @ the v0.6.0 lineage; code on branch `feat/p1-borderless`.
- **No airspace lift:** no `AllowsTransparency=true`, no WebView2 composition/windowless hosting, no literal pixel-zero edges.
- **Preserve focus rings** (REQ-UI-02): keyboard-focus affordances must survive — borders go `Transparent` at rest, NOT thickness-0, so focus triggers still render.
- Keep `BorderSubtle`/`AccentBorder` tokens defined (still used by the ComboBox dropdown popup, Settings separators, focus triggers).
- Test lanes: markup `--filter "FullyQualifiedName~XamlInvariantTests"`, logic `--filter "FullyQualifiedName~BorderlessResizeHitTestPolicyTests"`; full gate `dotnet test PiPlay.sln --configuration Debug --nologo`.
- Commit messages end with:
  ```
  Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01B5EybkqNL3eRSQW9jX3niD
  ```

---

### Task 1: Borderless resting controls

Flip the four visible resting `BorderBrush` setters to `Transparent`. PinToggle is already `Transparent` at rest (no change). Thicknesses stay so layout is stable and the TextBox focus trigger (`AccentBorder`) still renders.

**Files:**
- Modify: `src/PiPlay/Theme/ControlStyles.xaml` (lines 29, 66, 233, 383)
- Test: `tests/PiPlay.Tests/Ui/XamlInvariantTests.cs` (add one `[Fact]`)

**Interfaces:**
- Consumes: nothing.
- Produces: resting `BorderBrush` of `DarkButton`/`AccentButton`/`DarkTextBox`/`DarkComboBox` toggle == `Transparent`; `DarkTextBox` keyboard-focus trigger still sets `AccentBorder`.

- [ ] **Step 1: Write the failing test** — add to `XamlInvariantTests.cs`:

```csharp
[Fact]
public void Resting_control_borders_are_transparent_but_focus_ring_survives()
{
    var styles = XamlTestFiles.Load("Theme/ControlStyles.xaml");

    string RestingBorderBrush(string key)
    {
        var style = styles.Descendants(XamlTestFiles.Pres + "Style")
            .Single(e => (string?)e.Attribute(XamlTestFiles.X + "Key") == key);
        // The style's own (resting) BorderBrush setter — not template-trigger setters.
        return style.Elements(XamlTestFiles.Pres + "Setter")
            .Single(s => (string?)s.Attribute("Property") == "BorderBrush")
            .Attribute("Value")!.Value;
    }

    Assert.Equal("Transparent", RestingBorderBrush("DarkButton"));
    Assert.Equal("Transparent", RestingBorderBrush("AccentButton"));
    Assert.Equal("Transparent", RestingBorderBrush("DarkTextBox"));

    // DarkTextBox keyboard-focus trigger must still paint the accent ring (REQ-UI-02).
    var textBox = styles.Descendants(XamlTestFiles.Pres + "Style")
        .Single(e => (string?)e.Attribute(XamlTestFiles.X + "Key") == "DarkTextBox");
    var focusTrigger = textBox.Descendants(XamlTestFiles.Pres + "Trigger")
        .Single(t => (string?)t.Attribute("Property") == "IsKeyboardFocusWithin");
    Assert.Contains("AccentBorder",
        focusTrigger.Descendants(XamlTestFiles.Pres + "Setter")
            .Single(s => (string?)s.Attribute("Property") == "BorderBrush")
            .Attribute("Value")!.Value);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test PiPlay.sln --configuration Debug --filter "FullyQualifiedName~XamlInvariantTests.Resting_control_borders_are_transparent_but_focus_ring_survives" --nologo`
Expected: FAIL — `DarkButton` resting BorderBrush is `{DynamicResource BorderSubtle}`, not `Transparent`.

- [ ] **Step 3: Flip the four resting setters in `ControlStyles.xaml`**

- Line 29 (`DarkButton`): `<Setter Property="BorderBrush" Value="{DynamicResource BorderSubtle}" />` → `<Setter Property="BorderBrush" Value="Transparent" />`
- Line 66 (`AccentButton`): `<Setter Property="BorderBrush" Value="{DynamicResource AccentBorder}" />` → `<Setter Property="BorderBrush" Value="Transparent" />`
- Line 233 (`DarkTextBox`): `<Setter Property="BorderBrush" Value="{DynamicResource BorderSubtle}" />` → `<Setter Property="BorderBrush" Value="Transparent" />`
- Line 383 (`DarkComboBox` toggle template `Border`): change only its `BorderBrush="{DynamicResource BorderSubtle}"` → `BorderBrush="Transparent"` (leave its `BorderThickness` and the dropdown popup `Border` at ~420-421 untouched — the transient popup keeps its subtle edge).

Do NOT change any `BorderThickness`. Do NOT touch `PinToggle` (already `Transparent` at rest). Do NOT touch the line-254 focus trigger.

- [ ] **Step 4: Run tests to verify pass**

Run: `dotnet test PiPlay.sln --configuration Debug --filter "FullyQualifiedName~XamlInvariantTests" --nologo`
Expected: PASS (new fact + all existing markup invariants; `Grey_border_tokens_are_quieted…` still passes — the tokens remain defined).

- [ ] **Step 5: Commit**

```bash
git add src/PiPlay/Theme/ControlStyles.xaml tests/PiPlay.Tests/Ui/XamlInvariantTests.cs
git commit  # "feat(theme): make resting control borders transparent (P1 borderless)" + trailers
```

---

### Task 2: Settings dialog — no outer border

**Files:**
- Modify: `src/PiPlay/SettingsWindow.xaml:83`
- Test: `tests/PiPlay.Tests/Ui/XamlInvariantTests.cs` (add one `[Fact]`)

**Interfaces:**
- Consumes: nothing.
- Produces: the Settings root `Border` has `BorderThickness="0"`.

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void Settings_dialog_has_no_outer_border()
{
    var root = XamlTestFiles.Load("SettingsWindow.xaml").Root!;
    // The outermost Border (the dialog frame) must not draw a stroke.
    var outerBorder = root.Descendants(XamlTestFiles.Pres + "Border")
        .First(b => b.Attribute("BorderThickness") is not null);
    Assert.Equal("0", outerBorder.Attribute("BorderThickness")!.Value);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test PiPlay.sln --configuration Debug --filter "FullyQualifiedName~XamlInvariantTests.Settings_dialog_has_no_outer_border" --nologo`
Expected: FAIL — outer border `BorderThickness="1"`.

- [ ] **Step 3: Zero the border**

`src/PiPlay/SettingsWindow.xaml:83`: change the root `<Border BorderBrush="{DynamicResource BorderSubtle}" BorderThickness="1">` to `BorderThickness="0"` (the `BorderBrush` may stay; thickness 0 makes it moot).

- [ ] **Step 4: Run tests to verify pass**

Run: `dotnet test PiPlay.sln --configuration Debug --filter "FullyQualifiedName~XamlInvariantTests|FullyQualifiedName~SettingsWindowAppearanceTests" --nologo`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/PiPlay/SettingsWindow.xaml tests/PiPlay.Tests/Ui/XamlInvariantTests.cs
git commit  # "feat(settings): remove the dialog outer border (P1 borderless)" + trailers
```

---

### Task 3: Changelog + full gate + deploy-for-look

**Files:**
- Modify: `docs/CHANGELOG.md` (Unreleased)

- [ ] **Step 1: Add the changelog entry** under `## [Unreleased]`:

```markdown
### Changed
- **Borderless controls (P1):** resting control outlines (toolbar buttons, URL box, profile combo)
  and the Settings dialog frame are now transparent — the UI reads as a clean surface instead of a
  grid of grey boxes. Keyboard-focus rings are preserved.
```

- [ ] **Step 2: Run the full gate**

Run: `dotnet test PiPlay.sln --configuration Debug --nologo`
Expected: PASS, 0 failed (≈686 with the two new facts).

- [ ] **Step 3: Whitespace check**

Run: `git diff --check`
Expected: clean (CRLF warnings are fine).

- [ ] **Step 4: Commit**

```bash
git add docs/CHANGELOG.md
git commit  # "docs(changelog): record P1 borderless controls" + trailers
```

- [ ] **Step 5: Hand off for deploy + look**

Stop for the owner. After merge, deploy via `Publish-Stable.ps1` and **look**: confirm no grey control outlines, no Settings box. **Then decide Task 4:** does the (already-black) WebView resize band still read as a frame? If yes → do Task 4. If no → P1 is done.

---

### Task 4: (CONDITIONAL) Shrink the WebView resize band to 4 DIP

**Do this ONLY if, after Tasks 1–3 are deployed, the black WebView resize band still reads as a frame.** It carries policy-test recalibration that Tasks 1–3 do not.

**Files:**
- Modify: `src/PiPlay/Services/BorderlessResizeHitTestPolicy.cs:9` (`ResizeBorderDip` 10→4)
- Modify: `src/PiPlay/MainWindow.xaml` (WindowChrome `ResizeBorderThickness` 10→4; `Browser` WebView style margin `10,0,10,10`→`4,0,4,4`)
- Modify: `src/PiPlay/PlayerWindow.xaml` (WindowChrome `ResizeBorderThickness` 10→4; `Player` WebView style margin `10,0,10,10`→`4,0,4,4`)
- Modify: `tests/PiPlay.Tests/BorderlessResizeHitTestPolicyTests.cs` (recalibrate coordinates to the 4-DIP band)

**Interfaces:**
- Consumes: nothing.
- Produces: `BorderlessResizeHitTestPolicy.ResizeBorderDip == 4`. (The markup tests `WindowChrome_invariants_hold` and `WebView_margin_gives_the_window_the_resize_band` are already policy-relative — they assert `== ResizeBorderDip` — so they pass once the XAML matches the constant. Do NOT rewrite them; just keep the XAML consistent.)

- [ ] **Step 1: Recalibrate the policy tests (RED first)** — in `BorderlessResizeHitTestPolicyTests.cs`, the band-dependent coordinates move from the 10-DIP calibration to 4 DIP (Width=960, Height=540, CornerLength=32 unchanged). Replace these `[InlineData]` rows:
  - `Edges_return_cardinal_resize_results`: `(5,100,HTLEFT)`→`(2,100,HTLEFT)`; `(955,100,HTRIGHT)`→`(958,100,HTRIGHT)`; `(100,5,HTTOP)`→`(100,2,HTTOP)`; `(100,535,HTBOTTOM)`→`(100,538,HTBOTTOM)`.
  - `Corners_extend_along_the_edge_band`: `(20,5,…TOPLEFT)`→`(20,2,…)`; `(5,20,…TOPLEFT)`→`(2,20,…)`; `(940,5,…TOPRIGHT)`→`(940,2,…)`; `(955,20,…TOPRIGHT)`→`(958,20,…)`; `(20,535,…BOTTOMLEFT)`→`(20,538,…)`; `(5,520,…BOTTOMLEFT)`→`(2,520,…)`; `(940,535,…BOTTOMRIGHT)`→`(940,538,…)`; `(955,520,…BOTTOMRIGHT)`→`(958,520,…)`.
  - `Corner_length_boundary_is_explicit`: `(32,5,…TOPLEFT)`→`(32,2,…)`; `(33,5,…TOP)`→`(33,2,…)`; `(5,32,…TOPLEFT)`→`(2,32,…)`; `(5,33,…LEFT)`→`(2,33,…)`; `(928,5,…TOPRIGHT)`→`(928,2,…)`; `(927,5,…TOP)`→`(927,2,…)`.
  - `Right_and_bottom_resize_zones_start_at_the_configured_border`: `(950,100,HTRIGHT)`→`(956,100,HTRIGHT)`; `(100,530,HTBOTTOM)`→`(100,536,HTBOTTOM)`.
  - `Tiny_windows_clamp_border_and_corner_lengths_to_half_the_window`: change `width:18,height:18,x:4,y:4` → `width:6,height:6,x:2,y:2` (so half-window `3 < ResizeBorderDip 4` still triggers the clamp).
  - `Interior_and_outer_border_boundary_points_are_client_area`, `Out_of_window_points_are_ignored`, `Maximized_windows…`, `Non_resizable_windows…`: unchanged (their points stay interior/out-of-bounds/short-circuited under a 4-DIP band).

  Run: `dotnet test PiPlay.sln --configuration Debug --filter "FullyQualifiedName~BorderlessResizeHitTestPolicyTests" --nologo`
  Expected: FAIL (the recalibrated points assume `ResizeBorderDip=4`, still 10).

- [ ] **Step 2: Change the constant** — `BorderlessResizeHitTestPolicy.cs:9`: `public const double ResizeBorderDip = 10;` → `= 4;`. Leave `CornerLengthDip = 32`.

  Run the same logic filter — Expected: PASS.

- [ ] **Step 3: Make the XAML match the constant** — in BOTH `MainWindow.xaml` and `PlayerWindow.xaml`: set `WindowChrome ResizeBorderThickness="10"` → `"4"`, and the WebView style normal-state `Margin` setter `10,0,10,10` → `4,0,4,4` (leave the maximized `0` trigger). The band grids are already `Background="Black"`.

- [ ] **Step 4: Full gate** — `dotnet test PiPlay.sln --configuration Debug --nologo`. Expected: 0 failed. The relative markup tests (`WindowChrome_invariants_hold`, `WebView_margin_gives_the_window_the_resize_band`) pass because XAML now equals `ResizeBorderDip` (4).

- [ ] **Step 5: Commit**

```bash
git add src/PiPlay/Services/BorderlessResizeHitTestPolicy.cs src/PiPlay/MainWindow.xaml src/PiPlay/PlayerWindow.xaml tests/PiPlay.Tests/BorderlessResizeHitTestPolicyTests.cs
git commit  # "feat(window): shrink the resize band to 4 DIP (P1 borderless)" + trailers
```

---

## Self-Review

**1. Spec coverage:**
- A (resting control borders off) → Task 1. ✓
- B (Settings border off) → Task 2. ✓
- C (shrink+blacken band) → Task 4 (conditional, per cheapest-disproof). ✓
- D (keep DWM corners) → no task (no change). ✓
- E (tests rewritten as invariants) → Tasks 1/2 add borderless asserts; Task 4 recalibrates the policy tests. The two pinned markup tests are already policy-relative, so they need no rewrite — noted in Task 4 Interfaces. ✓
- Non-goals (no lift) honored — no `AllowsTransparency`/composition touched. ✓

**2. Placeholder scan:** No TBD/TODO; every step has exact lines, code, commands, expected output. ✓

**3. Type/consistency:** `Transparent` used uniformly; `ResizeBorderDip` is the single source the markup tests key off; the recalibrated coordinates are computed for border=4/corner=32/960×540 and listed explicitly. ✓
