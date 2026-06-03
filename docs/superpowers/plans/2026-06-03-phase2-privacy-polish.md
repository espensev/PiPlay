# Phase 2 Privacy Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the four non-blocking review findings on the v0.3.0 privacy feature plus three opted-in polish items, without changing the happy path or the sacred sign-out invariant.

**Architecture:** Behavior-preserving polish over existing code. Wording lives as constants in `PrivacyService`; the Settings window binds to them; `MainWindow` performs the work. The Clear path is made exception-safe and its 30 s bound is named + documented (see the design spec §3) and instrumented so it can be retuned from real data.

**Tech Stack:** WPF on `net10.0-windows`, xUnit (`Xunit.StaFact` for live-WPF), `Microsoft.Web.WebView2`. `Nullable` + `ImplicitUsings` enabled (so `System`, `System.Linq`, `System.Threading.Tasks` resolve without explicit `using`).

**Spec:** `docs/superpowers/specs/2026-06-03-phase2-privacy-polish-design.md`

---

## File Structure

| File | Responsibility | Change |
|---|---|---|
| `src/PiPlay/Services/PrivacyService.cs` | Tested wording constants + clear adapter + the timeout bound | Add `ClearResultTitle`, `ClearTimedOut`, `ClearNotReadyHint`, `ClearTimeout` |
| `src/PiPlay/Prompt.cs` | Themed dialog shells | Title-bar close returns `false` not `null` |
| `src/PiPlay/MainWindow.xaml.cs` | Privacy actions | Restructure `PerformClearBrowserDataAsync`: exception-safe, honest timeout, duration log, named bound, neutral titles |
| `src/PiPlay/SettingsWindow.xaml.cs` | Settings UI | Disabled-Clear tooltip |
| `tests/PiPlay.Tests/PrivacyServiceTests.cs` | Layer 2 wording tests | Neutral-title regression test |
| `tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs` | Layer 3 live-WPF | Tooltip test + `IDisposable` window cleanup |
| `docs/CHANGELOG.md` | Release notes | `[Unreleased]` entries for the user-visible bits |

Baseline: `dotnet test` is currently **141** passing. This plan adds **two** tests → **143**.

---

### Task 1: PrivacyService — wording constants + named timeout (+ regression test)

**Files:**
- Modify: `src/PiPlay/Services/PrivacyService.cs`
- Test: `tests/PiPlay.Tests/PrivacyServiceTests.cs`

- [ ] **Step 1: Write the failing test**

Add this method inside `PrivacyServiceTests` (before the closing brace, after `Clear_uses_the_all_profile_browsing_data_kind`):

```csharp
    [Fact]
    public void Clear_result_titles_are_statements_not_questions()
    {
        // Result/status notices must read as outcomes, not the confirmation question, so a user
        // on a privacy action is never left unsure whether their data was actually cleared.
        foreach (var title in new[] { PrivacyService.ClearResultTitle, PrivacyService.ClearDoneTitle })
        {
            Assert.False(title.TrimEnd().EndsWith("?"),
                $"Clear result/status title should be a statement, not a question: '{title}'");
        }

        // The confirmation prompt, by contrast, is allowed (and expected) to be a question.
        Assert.EndsWith("?", PrivacyService.ClearConfirmTitle);
    }
```

- [ ] **Step 2: Run test to verify it fails (does not compile)**

Run: `dotnet test --filter "FullyQualifiedName~PrivacyServiceTests.Clear_result_titles_are_statements_not_questions"`
Expected: BUILD FAILS — `'PrivacyService' does not contain a definition for 'ClearResultTitle'`.

- [ ] **Step 3: Add the constants + the timeout**

In `src/PiPlay/Services/PrivacyService.cs`, replace this block:

```csharp
    public const string ClearBrowserNotReady = "PiPlay's browser isn't ready yet. Try again in a moment.";
    public const string ClearFailed = "PiPlay couldn't clear the browser data. Please try again.";
```

with:

```csharp
    public const string ClearBrowserNotReady = "PiPlay's browser isn't ready yet. Try again in a moment.";
    public const string ClearFailed = "PiPlay couldn't clear the browser data. Please try again.";

    // Neutral title for Clear result/status notices (not the confirmation question) — see
    // PrivacyServiceTests.Clear_result_titles_are_statements_not_questions.
    public const string ClearResultTitle = "Clear browser data";
    public const string ClearTimedOut =
        "Clearing browser data is taking longer than expected. It will finish in the background, " +
        "and you may be signed out of YouTube.";
    public const string ClearNotReadyHint = "Available once the browser has finished loading.";

    /// <summary>
    /// Hang-guard bound for the Clear operation (NOT a progress wait). 30 s is about 2x the ~15 s
    /// adverse worst-case clear (slow HDD, sub-GB profile; the cleared volume is disk cache + site
    /// storage, and Chromium caps the HTTP cache near 256-320 MB). On an SSD the clear finishes in
    /// about 1-2 s. Chosen high enough not to false-flag a slow-but-succeeding clear, and short of
    /// 60 s. Retune only from logged real durations (MainWindow logs the measured ms).
    /// See docs/superpowers/specs/2026-06-03-phase2-privacy-polish-design.md section 3.
    /// </summary>
    public static readonly TimeSpan ClearTimeout = TimeSpan.FromSeconds(30);
```

> NOTE: `ClearTimeout` is `static readonly`, **not** `const` — `TimeSpan` is not a compile-time constant type. `TimeSpan` resolves via ImplicitUsings (`System`); no extra `using` needed.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~PrivacyServiceTests.Clear_result_titles_are_statements_not_questions"`
Expected: PASS (1 test).

- [ ] **Step 5: Commit**

```bash
git add src/PiPlay/Services/PrivacyService.cs tests/PiPlay.Tests/PrivacyServiceTests.cs
git commit -m "feat(privacy): neutral Clear result titles + named ClearTimeout (REQ-PRIVACY-02)"
```

---

### Task 2: Prompt — title-bar close returns `false`, not `null`

**Files:**
- Modify: `src/PiPlay/Prompt.cs`

> No headless unit test: the difference (`DialogResult` = `false` vs `null`) only manifests under a real modal `ShowDialog()`, which the live-WPF suite deliberately never starts. The existing `Prompt_dialogs_are_borderless_dark` construction test still covers shell structure; correctness here is confirmed by build + manual check.

- [ ] **Step 1: Make the change**

In `src/PiPlay/Prompt.cs` (inside `BuildShell`), replace:

```csharp
        close.Click += (_, _) => win.Close();
```

with:

```csharp
        // Title-bar close behaves as Cancel: set DialogResult=false (which auto-closes the modal)
        // so ShowDialog() returns false, matching the IsCancel button. (Was win.Close() -> null.)
        close.Click += (_, _) => { win.DialogResult = false; };
```

- [ ] **Step 2: Build and run the existing dialog test**

Run: `dotnet test --filter "FullyQualifiedName~WpfRuntimeTests.Prompt_dialogs_are_borderless_dark"`
Expected: PASS (the test constructs the shell and never clicks close, so it is unaffected; this just proves the change compiles and the shell is intact).

- [ ] **Step 3: Commit**

```bash
git add src/PiPlay/Prompt.cs
git commit -m "fix(ui): themed dialog title-bar close returns DialogResult=false (matches IsCancel)"
```

---

### Task 3: MainWindow — exception-safe Clear, honest timeout, duration log

**Files:**
- Modify: `src/PiPlay/MainWindow.xaml.cs` (replace `PerformClearBrowserDataAsync`, currently lines 353–396)

> No new headless unit test: the method is private and needs a live `CoreWebView2`, which the live-WPF suite avoids. The existing `Clear_is_not_ready_on_a_window_without_a_browser` (checks `CanClearBrowserData`) and `Reset_clears_dirty_ui_without_a_live_browser` stay green. Runtime behavior is confirmed by build + full suite + the manual smoke (`scripts/Test-UiSmoke.ps1` / QA checklist §6). `using System.Diagnostics;` (for `Stopwatch`) is already present at the top of the file.

- [ ] **Step 1: Replace the method**

Replace the entire `PerformClearBrowserDataAsync` method:

```csharp
    private async Task PerformClearBrowserDataAsync()
    {
        if (_privacyActionInProgress) return;

        // Re-check readiness at execution time (the cached enabled state can be stale).
        var core = Browser.CoreWebView2;
        if (!CanClearBrowserData || core is null)
        {
            Prompt.ShowInfo(this, PrivacyService.ClearConfirmTitle, PrivacyService.ClearBrowserNotReady);
            return;
        }

        _privacyActionInProgress = true;
        _clearingBrowserData = true;
        SettingsButton.IsEnabled = false;   // no second Settings window mid-await
        try
        {
            // Single shared profile: closing the popout avoids it showing a logged-out surface.
            // _clearingBrowserData makes the popout's return handler skip driving source playback.
            if (_player is not null) { try { _player.Close(); } catch { /* ignore */ } }

            // Bound the wait so a hung clear can never wedge the gear/privacy actions for the
            // rest of the session; a timeout falls through to the catch and re-enables the UI.
            await PrivacyService.ClearBrowserDataAsync(core).WaitAsync(TimeSpan.FromSeconds(30));

            // Reflect the signed-out state; a nav hiccup must not mask a successful clear.
            try { NavigateInternal("https://www.youtube.com/"); }
            catch (Exception navEx) { Log.Error("Post-clear navigation failed.", navEx); }

            Log.Info("Browser data cleared (user signed out).");
            Prompt.ShowInfo(this, PrivacyService.ClearDoneTitle, PrivacyService.ClearDoneBody);
        }
        catch (Exception ex)
        {
            Log.Error("Clear browser data failed.", ex);
            Prompt.ShowInfo(this, PrivacyService.ClearConfirmTitle, PrivacyService.ClearFailed);
        }
        finally
        {
            _privacyActionInProgress = false;
            _clearingBrowserData = false;
            SettingsButton.IsEnabled = true;
        }
    }
```

with:

```csharp
    private async Task PerformClearBrowserDataAsync()
    {
        if (_privacyActionInProgress) return;

        try
        {
            // Re-check readiness at execution time (the cached enabled state can be stale). This
            // lives INSIDE the try so a throw here (e.g. CoreWebView2 access during a WebView2
            // teardown) can never escape this fire-and-forget task unobserved.
            var core = Browser.CoreWebView2;
            if (!CanClearBrowserData || core is null)
            {
                Prompt.ShowInfo(this, PrivacyService.ClearResultTitle, PrivacyService.ClearBrowserNotReady);
                return;
            }

            _privacyActionInProgress = true;
            _clearingBrowserData = true;
            SettingsButton.IsEnabled = false;   // no second Settings window mid-await

            // Single shared profile: closing the popout avoids it showing a logged-out surface.
            // _clearingBrowserData makes the popout's return handler skip driving source playback.
            if (_player is not null) { try { _player.Close(); } catch { /* ignore */ } }

            // Bound the wait (PrivacyService.ClearTimeout) so a hung clear can never wedge the
            // gear/privacy actions for the rest of the session. The clear runs on the SOURCE core,
            // which we keep alive, so its completion handler always fires (a closed WebView would
            // release it un-invoked). Time it so the bound can be retuned from real durations.
            var sw = Stopwatch.StartNew();
            await PrivacyService.ClearBrowserDataAsync(core).WaitAsync(PrivacyService.ClearTimeout);
            sw.Stop();

            // Reflect the signed-out state; a nav hiccup must not mask a successful clear.
            try { NavigateInternal("https://www.youtube.com/"); }
            catch (Exception navEx) { Log.Error("Post-clear navigation failed.", navEx); }

            Log.Info($"Browser data cleared in {sw.ElapsedMilliseconds} ms (user signed out).");
            Prompt.ShowInfo(this, PrivacyService.ClearDoneTitle, PrivacyService.ClearDoneBody);
        }
        catch (TimeoutException)
        {
            // The wait elapsed, not the clear: ClearBrowsingDataAsync may still finish in the
            // background. Tell the truth instead of reporting a failure that may not be one.
            Log.Warn("Clear browser data exceeded the timeout; it may still complete in the background.");
            Prompt.ShowInfo(this, PrivacyService.ClearResultTitle, PrivacyService.ClearTimedOut);
        }
        catch (Exception ex)
        {
            Log.Error("Clear browser data failed.", ex);
            Prompt.ShowInfo(this, PrivacyService.ClearResultTitle, PrivacyService.ClearFailed);
        }
        finally
        {
            _privacyActionInProgress = false;
            _clearingBrowserData = false;
            SettingsButton.IsEnabled = true;
        }
    }
```

- [ ] **Step 2: Run the Clear-related WPF tests to verify no regression**

Run: `dotnet test --filter "FullyQualifiedName~WpfRuntimeTests"`
Expected: PASS (all live-WPF tests, including `Clear_is_not_ready_on_a_window_without_a_browser` and `Reset_clears_dirty_ui_without_a_live_browser`).

- [ ] **Step 3: Commit**

```bash
git add src/PiPlay/MainWindow.xaml.cs
git commit -m "harden(privacy): exception-safe Clear with honest timeout + duration log (Q-6, REQ-PRIVACY-02)"
```

---

### Task 4: SettingsWindow — explain why Clear is disabled

**Files:**
- Test: `tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs`
- Modify: `src/PiPlay/SettingsWindow.xaml.cs`

- [ ] **Step 1: Write the failing test**

Add this method to `WpfRuntimeTests` (after `SettingsWindow_disables_only_clear_when_browser_not_ready`):

```csharp
    [Fact]
    public void SettingsWindow_explains_why_clear_is_disabled() => StaTestThread.Invoke(() =>
    {
        var notReady = new SettingsWindow(isBrowserReady: false);
        var clear = (Button)notReady.FindName("ClearBrowserDataButton")!;
        Assert.Equal(PiPlay.Services.PrivacyService.ClearNotReadyHint, (string)clear.ToolTip);
        Assert.True(ToolTipService.GetShowOnDisabled(clear));   // tip shows on the disabled button

        // When the browser is ready the button is enabled and carries no explanatory tooltip.
        var ready = new SettingsWindow(isBrowserReady: true);
        Assert.Null(((Button)ready.FindName("ClearBrowserDataButton")!).ToolTip);
    });
```

> `ToolTipService` and `Button` resolve from the existing `using System.Windows.Controls;` at the top of `WpfRuntimeTests.cs`.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~WpfRuntimeTests.SettingsWindow_explains_why_clear_is_disabled"`
Expected: FAIL — `Assert.Equal` gets `null` (no tooltip set) / `GetShowOnDisabled` is `false`.

- [ ] **Step 3: Implement the tooltip**

In `src/PiPlay/SettingsWindow.xaml.cs`, add to the `using` block (after `using System.Windows.Input;`):

```csharp
using System.Windows.Controls;
```

Then in the constructor replace:

```csharp
        // Only the Clear action needs a live browser; Reset never does.
        ClearBrowserDataButton.IsEnabled = isBrowserReady;
```

with:

```csharp
        // Only the Clear action needs a live browser; Reset never does. When it is disabled,
        // explain why (and let the tooltip show on the disabled control).
        ClearBrowserDataButton.IsEnabled = isBrowserReady;
        if (!isBrowserReady)
        {
            ClearBrowserDataButton.ToolTip = PrivacyService.ClearNotReadyHint;
            ToolTipService.SetShowOnDisabled(ClearBrowserDataButton, true);
        }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~WpfRuntimeTests.SettingsWindow_explains_why_clear_is_disabled"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/PiPlay/SettingsWindow.xaml.cs tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs
git commit -m "feat(privacy): tooltip explaining the disabled Clear button (REQ-UI-01)"
```

---

### Task 5: WpfRuntimeTests — close constructed windows per test

**Files:**
- Modify: `tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs`

> The "test" for this hygiene change is the full suite staying green. Each `Close()` is guarded: a never-shown `MainWindow` whose `OnClosing` throws is swallowed (best-effort), so cleanup never fails a test. `IDisposable`, `Cast`/`ToArray` resolve via ImplicitUsings (`System`, `System.Linq`).

- [ ] **Step 1: Make the class disposable**

Change the class declaration:

```csharp
public class WpfRuntimeTests
```

to:

```csharp
public class WpfRuntimeTests : IDisposable
```

- [ ] **Step 2: Add the cleanup method**

Add this at the end of the class (after `CountInkedRows`, before the closing brace):

```csharp
    // Layer 3 constructs real windows (never shown). Close any that remain on the shared STA
    // thread after each test so they don't accumulate on Application.Windows for the whole run.
    public void Dispose() => StaTestThread.Invoke(() =>
    {
        foreach (var w in Application.Current.Windows.Cast<Window>().ToArray())
        {
            try { w.Close(); } catch { /* a never-shown window may resist closing; ignore */ }
        }
    });
```

- [ ] **Step 3: Run the full live-WPF lane to verify still green**

Run: `dotnet test --filter Category=Wpf`
Expected: PASS (all live-WPF tests; the per-test cleanup runs after each without failing any).

- [ ] **Step 4: Commit**

```bash
git add tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs
git commit -m "test(wpf): close constructed Layer-3 windows per test so they don't accumulate"
```

---

### Task 6: CHANGELOG + full verification + PR

**Files:**
- Modify: `docs/CHANGELOG.md`

- [ ] **Step 1: Add the user-visible changes to the changelog**

In `docs/CHANGELOG.md`, under `## [Unreleased]` → `### Fixed`, replace this (the last bullet in that section):

```markdown
- `Build-PiPlay.ps1` Release stage no longer exits non-zero on success when no old publish
  folders are pruned.
```

with:

```markdown
- `Build-PiPlay.ps1` Release stage no longer exits non-zero on success when no old publish
  folders are pruned.
- **Clear browser data** now reports outcomes truthfully (REQ-PRIVACY-02, Q-6): result and
  not-ready notices read as statements rather than the "Clear browser data?" question; a clear
  that exceeds its ~30 s safety timeout says it will finish in the background instead of claiming
  it failed; and any unexpected error is surfaced instead of being silently swallowed.
- The Settings **Clear browser data** button now explains via a tooltip why it is disabled while
  the browser is still loading.
- Themed dialogs treat the title-bar close as Cancel (consistent dismissal).
```

- [ ] **Step 2: Run the complete suite**

Run: `dotnet test`
Expected: `Passed! - Failed: 0, Passed: 143` (141 baseline + the title test + the tooltip test).

- [ ] **Step 3: Commit the changelog**

```bash
git add docs/CHANGELOG.md
git commit -m "docs(changelog): Phase 2 privacy polish (truthful Clear, disabled-Clear tooltip)"
```

- [ ] **Step 4: Push and open the PR**

```bash
git push -u origin feature/phase2-polish
gh pr create --base main --head feature/phase2-polish \
  --title "Phase 2 privacy polish: truthful Clear, exception-safety, dialog + test fixes" \
  --body "Follow-up to PR #1. Implements docs/superpowers/specs/2026-06-03-phase2-privacy-polish-design.md: neutral/honest Clear dialog wording, exception-safe Clear with a documented 30s hang-guard (ClearTimeout) + duration logging, consistent title-bar dismissal, a disabled-Clear tooltip, a neutral-title regression test, and Layer-3 window cleanup. No happy-path or sign-out-invariant change. 143/143 tests pass."
```

Expected: PR URL printed.

- [ ] **Step 5: Verify the PR**

Run: `gh pr view --json number,state,baseRefName,headRefName`
Expected: state `OPEN`, base `main`, head `feature/phase2-polish`.

---

## Self-Review

**Spec coverage** (design spec §2 changes 1–8):
- Change 1 (neutral titles) → Task 1 (constants) + Task 3 (usage at the not-ready/failed/timeout notices). ✓
- Change 2 (exception-safe Clear) → Task 3. ✓
- Change 3 (honest timeout + `ClearTimeout`) → Task 1 (constant + message) + Task 3 (`catch (TimeoutException)`). ✓
- Change 4 (title-bar close → false) → Task 2. ✓
- Change 5 (disabled-Clear tooltip) → Task 4. ✓
- Change 6 (neutral-title regression test) → Task 1. ✓
- Change 7 (close Layer-3 windows) → Task 5. ✓
- Change 8 (instrument clear duration) → Task 3 (`Stopwatch` + `Log.Info(... ms ...)`). ✓
- §3 timeout XML-doc requirement → Task 1 (`ClearTimeout` doc comment). ✓
- §6 CHANGELOG → Task 6. ✓

**Placeholder scan:** none — every code/command step shows the literal content.

**Type/name consistency:** `ClearResultTitle`, `ClearTimedOut`, `ClearNotReadyHint` (string consts) and `ClearTimeout` (`static readonly TimeSpan`) are defined in Task 1 and used in Tasks 3/4. `PerformClearBrowserDataAsync` signature unchanged. `ToolTipService.SetShowOnDisabled` / `GetShowOnDisabled` paired correctly across Task 4 impl/test. Test count 141 → 143 consistent across Tasks 1, 4, 6.
