# Phase 2 Privacy Actions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add two Phase-2 privacy actions — **Reset app state** (REQ-PRIVACY-01) and **Clear browser data** (REQ-PRIVACY-02) — as separate, confirmed actions in a new themed Settings window, with layered regression tests.

**Architecture:** Pure decision-service + thin WPF/WebView adapter (matches `FadePolicy`/`ReturnPolicy`/`ProfileService`). A new `PrivacyService` owns all user-facing wording as `const` (so it is test-pinned) plus the `ClearBrowsingDataKinds` choice. `SettingsService.Reset()` atomically rewrites `settings.json` with defaults and touches nothing else. A new `SettingsWindow` confirms an action, records a `PrivacyAction` result, and closes; `MainWindow` performs the work after the modal closes, hardened against re-entrancy / stale readiness / async failure / modal-owner issues.

**Tech Stack:** C# / .NET 10 WPF, WebView2 (`Microsoft.Web.WebView2` 1.0.3967.48), xUnit (lanes via `[Trait("Category", …)]`: `Logic` / `Markup` / `Wpf`), Layer-4 PowerShell UIA smoke.

**Sacred invariant:** YouTube login persists across runs by default. The session lives in `%LOCALAPPDATA%\PiPlay\WebView2UserData\`, never in `settings.json`. **The only code path that signs the user out is the explicit, confirmed _Clear browser data_ action.** Reset never does. Tasks 2–3 encode this as tests.

**Spec:** `docs/superpowers/specs/2026-05-31-privacy-actions-design.md`

---

## File structure

| File | Responsibility |
|---|---|
| `src/PiPlay/Services/PrivacyService.cs` | **New.** Wording `const`s, `ClearKinds`, `ClearBrowserDataAsync` adapter. |
| `src/PiPlay/Services/SettingsService.cs` | Add atomic `Reset()`; extract shared `AtomicWrite`. |
| `src/PiPlay/Theme/ControlStyles.xaml` | Add `DangerButton` destructive style. |
| `src/PiPlay/Prompt.cs` | Add `AskConfirm` + `ShowInfo` themed dialogs. |
| `src/PiPlay/SettingsWindow.xaml` / `.cs` | **New.** Themed Settings window, Privacy section, `PrivacyAction` result. |
| `src/PiPlay/MainWindow.xaml` | Add `SettingsButton` gear to caption strip. |
| `src/PiPlay/MainWindow.xaml.cs` | Gear handler, `ApplyResetState`, `PerformClearBrowserDataAsync`, `_settings` non-readonly, `PendingUrlForTests`. |
| `tests/PiPlay.Tests/PrivacyServiceTests.cs` | **New.** Wording + `ClearKinds` (Logic). |
| `tests/PiPlay.Tests/SettingsServiceTests.cs` | Add reset / preservation tests (Logic). |
| `tests/PiPlay.Tests/Ui/XamlInvariantTests.cs` | Add `SettingsButton` + `SettingsWindow.xaml` wiring + `DangerButton` (Markup). |
| `tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs` | Add Settings-window + reset-without-browser tests (Wpf). |
| `scripts/Test-UiSmoke.ps1`, `docs/QA_Checklist.md` | Phase-2 privacy smoke (Layer 4). |
| `docs/CHANGELOG.md`, `VERSION`, `BUILD_NUMBER` | Release notes + version bump. |

**Lane commands:** `dotnet test --filter Category=Logic` · `Category=Markup` · `Category=Wpf` · `dotnet test` (all). Run from repo root `D:\Development\DesktopApps\PiPlay`.

---

## Task 1: PrivacyService (wording constants + clear adapter)

**Files:**
- Create: `src/PiPlay/Services/PrivacyService.cs`
- Test: `tests/PiPlay.Tests/PrivacyServiceTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/PiPlay.Tests/PrivacyServiceTests.cs`:

```csharp
using Microsoft.Web.WebView2.Core;
using PiPlay.Services;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class PrivacyServiceTests
{
    private static bool Has(string s, string sub) =>
        s.Contains(sub, StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void Reset_wording_says_the_user_stays_signed_in()
    {
        foreach (var s in new[]
                 {
                     PrivacyService.ResetDescription,
                     PrivacyService.ResetConfirmBody,
                     PrivacyService.ResetDoneBody,
                 })
        {
            Assert.True(Has(s, "signed in"), $"Reset wording should reassure login is kept: '{s}'");
            Assert.False(Has(s, "sign out") || Has(s, "signed out"),
                $"Reset wording must NOT imply logout: '{s}'");
        }
    }

    [Fact]
    public void Clear_wording_says_the_user_is_signed_out()
    {
        foreach (var s in new[]
                 {
                     PrivacyService.ClearDescription,
                     PrivacyService.ClearConfirmBody,
                     PrivacyService.ClearDoneBody,
                 })
        {
            Assert.True(Has(s, "sign") && Has(s, "out"),
                $"Clear wording should state the user is signed out: '{s}'");
        }
    }

    [Fact]
    public void Reset_and_clear_are_worded_distinctly()
    {
        // REQ-PRIVACY-02: the two actions must be worded separately.
        Assert.NotEqual(PrivacyService.ResetActionLabel, PrivacyService.ClearActionLabel);
        Assert.NotEqual(PrivacyService.ResetDescription, PrivacyService.ClearDescription);
        Assert.NotEqual(PrivacyService.ResetConfirmBody, PrivacyService.ClearConfirmBody);
    }

    [Fact]
    public void Clear_uses_the_all_profile_browsing_data_kind()
    {
        // AllProfile clears cookies + cache + site storage, which logs the user out.
        Assert.Equal(CoreWebView2BrowsingDataKinds.AllProfile, PrivacyService.ClearKinds);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test --filter Category=Logic`
Expected: FAIL — build error, `PrivacyService` does not exist.

- [ ] **Step 3: Write the implementation**

Create `src/PiPlay/Services/PrivacyService.cs`:

```csharp
using Microsoft.Web.WebView2.Core;

namespace PiPlay.Services;

/// <summary>
/// Privacy actions (spec 19, Phase 2). Owns the user-facing wording for both actions as
/// constants so "worded separately" (REQ-PRIVACY-02) and the login-kept / signed-out promises
/// are regression-testable, plus the browsing-data scope and a thin clear adapter. The Settings
/// window binds its visible text to these constants; MainWindow performs the work.
/// </summary>
public static class PrivacyService
{
    /// <summary>Clearing AllProfile (cookies + cache + site storage) signs the user out of YouTube.</summary>
    public const CoreWebView2BrowsingDataKinds ClearKinds = CoreWebView2BrowsingDataKinds.AllProfile;

    // --- Reset app state (REQ-PRIVACY-01) — keeps the YouTube session ---
    public const string ResetActionLabel = "Reset app state";
    public const string ResetDescription =
        "Clears PiPlay's settings, saved profiles, and window placement. You'll stay signed in to YouTube.";
    public const string ResetConfirmTitle = "Reset app state?";
    public const string ResetConfirmBody =
        "This clears PiPlay's settings, saved profiles, and window placement.\n\nYou'll stay signed in to YouTube.";
    public const string ResetConfirmButton = "Reset app state";
    public const string ResetDoneTitle = "App state reset";
    public const string ResetDoneBody = "PiPlay's settings were reset. You're still signed in to YouTube.";

    // --- Clear browser data (REQ-PRIVACY-02) — separate, confirmed, signs the user out ---
    public const string ClearActionLabel = "Clear browser data";
    public const string ClearDescription =
        "Signs you out of YouTube and clears PiPlay's browsing data — cookies, cache, and site data. " +
        "You'll need to sign in again.";
    public const string ClearConfirmTitle = "Clear browser data?";
    public const string ClearConfirmBody =
        "This signs you out of YouTube and clears PiPlay's browsing data — cookies, cache, and site data." +
        "\n\nYou'll need to sign in again next time.";
    public const string ClearConfirmButton = "Clear browser data";
    public const string ClearDoneTitle = "Browser data cleared";
    public const string ClearDoneBody = "Browser data cleared. You've been signed out of YouTube.";
    public const string ClearBrowserNotReady = "PiPlay's browser isn't ready yet. Try again in a moment.";
    public const string ClearFailed = "PiPlay couldn't clear the browser data. Please try again.";

    /// <summary>Clear the shared WebView2 profile's browsing data (signs the user out). Needs a live core.</summary>
    public static Task ClearBrowserDataAsync(CoreWebView2 core) =>
        core.Profile.ClearBrowsingDataAsync(ClearKinds);
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test --filter Category=Logic`
Expected: PASS (all `PrivacyServiceTests` green; existing Logic tests still green).

- [ ] **Step 5: Commit**

```bash
git add src/PiPlay/Services/PrivacyService.cs tests/PiPlay.Tests/PrivacyServiceTests.cs
git commit -m "$(cat <<'EOF'
feat(privacy): add PrivacyService wording constants + clear adapter

Tested const strings for both actions (login-kept vs signed-out, worded
separately per REQ-PRIVACY-02) and ClearKinds=AllProfile.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: SettingsService.Reset() — atomic, touches only settings.json

**Files:**
- Modify: `src/PiPlay/Services/SettingsService.cs`
- Test: `tests/PiPlay.Tests/SettingsServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

Append these to `tests/PiPlay.Tests/SettingsServiceTests.cs` (inside the existing `SettingsServiceTests` class, before the closing brace). They use the existing `_dir` / `_path` fields — `_dir` mirrors the real layout where `settings.json`, `logs/`, and `WebView2UserData/` are siblings under one root.

```csharp
    [Fact]
    public void Reset_recreates_the_file_with_defaults()
    {
        var svc = new SettingsService(_path);
        var populated = new AppSettings { LastUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ" };
        populated.MainWindow.Topmost = true;
        populated.Profiles.Add(new Profile { Name = "Lo-fi", Url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ" });
        svc.Save(populated);

        var fresh = svc.Reset();

        // Returned object is defaults.
        Assert.Equal("https://www.youtube.com/", fresh.LastUrl);
        Assert.Empty(fresh.Profiles);
        Assert.False(fresh.MainWindow.Topmost);

        // On-disk file is defaults too (atomic rewrite, never absent).
        Assert.True(File.Exists(_path));
        var reloaded = svc.Load();
        Assert.Empty(reloaded.Profiles);
        Assert.Equal("https://www.youtube.com/", reloaded.LastUrl);
    }

    [Fact]
    public void Reset_preserves_sibling_logs_and_webview_user_data()
    {
        // The sacred invariant: reset never touches the browser session (login) or logs.
        var logsDir = Path.Combine(_dir, "logs");
        var webViewDir = Path.Combine(_dir, "WebView2UserData");
        Directory.CreateDirectory(logsDir);
        Directory.CreateDirectory(webViewDir);
        var logFile = Path.Combine(logsDir, "piplay.log");
        var sessionFile = Path.Combine(webViewDir, "session-cookie");
        File.WriteAllText(logFile, "log line");
        File.WriteAllText(sessionFile, "logged-in");

        new SettingsService(_path).Reset();

        Assert.True(File.Exists(logFile), "Reset must not delete logs.");
        Assert.True(File.Exists(sessionFile), "Reset must not delete the WebView2 session (login).");
        Assert.Equal("logged-in", File.ReadAllText(sessionFile));
    }

    [Fact]
    public void Reset_does_not_create_a_webview_user_data_dir()
    {
        var webViewDir = Path.Combine(_dir, "WebView2UserData");
        Assert.False(Directory.Exists(webViewDir));

        new SettingsService(_path).Reset();

        Assert.False(Directory.Exists(webViewDir), "Reset must never create/recreate the WebView2 folder.");
    }

    [Fact]
    public void Reset_removes_stale_corrupt_quarantine_files()
    {
        var corrupt = Path.Combine(_dir, "settings.json.corrupt.20200101-000000.json");
        File.WriteAllText(corrupt, "{}");

        new SettingsService(_path).Reset();

        Assert.False(File.Exists(corrupt), "Reset should clean up stale quarantines for a clean slate.");
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --filter Category=Logic`
Expected: FAIL — build error, `SettingsService` has no `Reset` method.

- [ ] **Step 3: Write the implementation**

In `src/PiPlay/Services/SettingsService.cs`, replace the body of `Save` and add `AtomicWrite` + `Reset`. Change the existing `Save` method to:

```csharp
    public void Save(AppSettings settings)
    {
        try
        {
            Sanitize(settings);
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            AtomicWrite(settings);
        }
        catch (Exception ex)
        {
            Log.Error("Failed to save settings.", ex);
        }
    }

    /// <summary>
    /// Reset app state (REQ-PRIVACY-01): atomically replace settings.json with defaults and drop
    /// stale corrupt-quarantine files. Touches ONLY the settings-file path — never the WebView2
    /// user-data folder or logs — so the user stays signed in to YouTube. Returns the defaults.
    /// </summary>
    public AppSettings Reset()
    {
        var fresh = Sanitize(new AppSettings());
        try
        {
            var dir = Path.GetDirectoryName(_path)!;
            Directory.CreateDirectory(dir);
            AtomicWrite(fresh);

            foreach (var f in Directory.EnumerateFiles(dir, "*.corrupt.*.json"))
            {
                try { File.Delete(f); } catch { /* best-effort cleanup */ }
            }
            Log.Info("App state reset to defaults (WebView2 session preserved).");
        }
        catch (Exception ex)
        {
            Log.Error("Failed to reset app state.", ex);
        }
        return fresh;
    }

    /// <summary>
    /// Atomic write (spec 26.4): temp file, durable flush, atomic same-volume swap. Shared by
    /// <see cref="Save"/> and <see cref="Reset"/>. The live file is always either the previous
    /// content or the new content — never absent or half-written.
    /// </summary>
    private void AtomicWrite(AppSettings settings)
    {
        var tmp = _path + ".tmp";
        using (var stream = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, settings, Options);
            stream.Flush(flushToDisk: true);
        }

        if (File.Exists(_path))
            File.Replace(tmp, _path, destinationBackupFileName: null);
        else
            File.Move(tmp, _path);
    }
```

Note: the original inline atomic-write block inside `Save` (the `var tmp = _path + ".tmp";` … `File.Move(tmp, _path);` lines) is now removed because `AtomicWrite` holds it. `Sanitize` already returns the same instance it is given, so `Sanitize(new AppSettings())` returns a sanitized defaults object.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --filter Category=Logic`
Expected: PASS (new reset tests + existing `Save_then_load_roundtrips`, `Corrupt_file_is_quarantined…`, `Sanitize_repairs…` all green — the refactor preserves `Save` behavior).

- [ ] **Step 5: Commit**

```bash
git add src/PiPlay/Services/SettingsService.cs tests/PiPlay.Tests/SettingsServiceTests.cs
git commit -m "$(cat <<'EOF'
feat(privacy): atomic SettingsService.Reset() that preserves the session

Reset atomically rewrites settings.json with defaults (shared AtomicWrite)
and drops stale quarantines. It touches only the settings path — never the
WebView2 user-data folder or logs — so login persists. Tests encode the
invariant: reset preserves a sibling WebView2 session file and never creates
the WebView2 folder.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: DangerButton destructive style

**Files:**
- Modify: `src/PiPlay/Theme/ControlStyles.xaml`
- Test: `tests/PiPlay.Tests/Ui/XamlInvariantTests.cs`

- [ ] **Step 1: Write the failing test**

Add this `[Fact]` to `XamlInvariantTests` (e.g. after `Accent_button_text_is_readable_on_accent_fill`):

```csharp
    [Fact]
    public void DangerButton_style_is_defined()
    {
        var keys = XamlTestFiles.Load("Theme/ControlStyles.xaml").Descendants()
            .Select(e => e.Attribute(XamlTestFiles.X + "Key")?.Value)
            .Where(k => k is not null);
        Assert.Contains("DangerButton", keys);
    }
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test --filter Category=Markup`
Expected: FAIL — `Assert.Contains` cannot find `"DangerButton"`.

- [ ] **Step 3: Write the implementation**

In `src/PiPlay/Theme/ControlStyles.xaml`, add this style immediately after the `AccentButton` style (after its closing `</Style>`, before the `IconButton` comment). It reuses the existing `DangerPin` color token; hover dims via opacity to avoid introducing a new color token:

```xml
  <!-- Destructive action button (e.g. the Clear browser data confirm): red fill, white text. -->
  <Style TargetType="Button" x:Key="DangerButton" BasedOn="{StaticResource DarkButton}">
    <Setter Property="Background" Value="{StaticResource DangerPin}" />
    <Setter Property="Foreground" Value="White" />
    <Setter Property="BorderThickness" Value="0" />
    <Setter Property="FontWeight" Value="SemiBold" />
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="Button">
          <Border x:Name="bd" Background="{TemplateBinding Background}" CornerRadius="10">
            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"
                              Margin="{TemplateBinding Padding}" />
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="bd" Property="Opacity" Value="0.88" />
            </Trigger>
            <Trigger Property="IsEnabled" Value="False">
              <Setter Property="Opacity" Value="0.4" />
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test --filter Category=Markup`
Expected: PASS (`DangerButton_style_is_defined` green; `Every_StaticResource_reference_is_defined` still green — `DangerPin` is already a defined token).

- [ ] **Step 5: Commit**

```bash
git add src/PiPlay/Theme/ControlStyles.xaml tests/PiPlay.Tests/Ui/XamlInvariantTests.cs
git commit -m "$(cat <<'EOF'
feat(theme): add DangerButton destructive style

Red (DangerPin) fill, white text — used by the Clear browser data confirm.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 4: Prompt.AskConfirm + Prompt.ShowInfo (themed dialogs)

**Files:**
- Modify: `src/PiPlay/Prompt.cs`

These are modal `ShowDialog` dialogs — not unit-testable without showing a window. They are build-verified here and exercised by the Layer-4 manual smoke (Task 8). No automated test in this task.

- [ ] **Step 1: Add the two methods**

In `src/PiPlay/Prompt.cs`, add these two methods inside the `Prompt` class (after `AskText`):

```csharp
    /// <summary>
    /// Themed dark Yes/No confirmation. Returns true only if the user confirms. Sets the owner and
    /// matches its Topmost so a pinned PiPlay does not occlude it. Default focus is Cancel so Enter
    /// never confirms a destructive action by accident; <paramref name="danger"/> styles the
    /// confirm button as destructive (red).
    /// </summary>
    public static bool AskConfirm(Window owner, string title, string message, string confirmText, bool danger = false)
    {
        var bg = (Brush)Application.Current.Resources["AppBackground"];
        var fg = (Brush)Application.Current.Resources["TextPrimary"];

        var win = new Window
        {
            Title = title,
            Owner = owner,
            Topmost = owner.Topmost,
            Width = 460,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.ToolWindow,
            ShowInTaskbar = false,
            Background = bg,
        };

        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock
        {
            Text = message,
            Foreground = fg,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16),
        });

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var confirm = new Button
        {
            Content = confirmText,
            Style = (Style)Application.Current.Resources[danger ? "DangerButton" : "AccentButton"],
            MinWidth = 110,
            Margin = new Thickness(0, 0, 8, 0),
        };
        var cancel = new Button
        {
            Content = "Cancel",
            Style = (Style)Application.Current.Resources["DarkButton"],
            MinWidth = 90,
            IsCancel = true,
            IsDefault = true,
        };
        buttons.Children.Add(confirm);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);

        win.Content = panel;

        var result = false;
        confirm.Click += (_, _) => { result = true; win.DialogResult = true; };

        win.ShowDialog();
        return result;
    }

    /// <summary>Themed dark message dialog with a single OK button (done / not-ready / failed notices).</summary>
    public static void ShowInfo(Window owner, string title, string message)
    {
        var bg = (Brush)Application.Current.Resources["AppBackground"];
        var fg = (Brush)Application.Current.Resources["TextPrimary"];

        var win = new Window
        {
            Title = title,
            Owner = owner,
            Topmost = owner.Topmost,
            Width = 460,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.ToolWindow,
            ShowInTaskbar = false,
            Background = bg,
        };

        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock
        {
            Text = message,
            Foreground = fg,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16),
        });

        var ok = new Button
        {
            Content = "OK",
            Style = (Style)Application.Current.Resources["AccentButton"],
            MinWidth = 90,
            HorizontalAlignment = HorizontalAlignment.Right,
            IsDefault = true,
            IsCancel = true,
        };
        ok.Click += (_, _) => { win.DialogResult = true; };
        panel.Children.Add(ok);

        win.Content = panel;
        win.ShowDialog();
    }
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build src/PiPlay/PiPlay.csproj`
Expected: Build succeeded (the existing `using System.Windows; using System.Windows.Controls; using System.Windows.Media;` cover `TextWrapping`, `Orientation`, `Brush`).

- [ ] **Step 3: Commit**

```bash
git add src/PiPlay/Prompt.cs
git commit -m "$(cat <<'EOF'
feat(privacy): add themed AskConfirm + ShowInfo dialogs

Dark Yes/No confirm (Cancel is default focus; danger styles the confirm red)
and a dark OK notice, both owner-aware and topmost-matched.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 5: SettingsWindow (XAML + code-behind) with Markup + Wpf tests

**Files:**
- Create: `src/PiPlay/SettingsWindow.xaml`, `src/PiPlay/SettingsWindow.xaml.cs`
- Test: `tests/PiPlay.Tests/Ui/XamlInvariantTests.cs`, `tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs`

- [ ] **Step 1: Write the failing tests (Markup)**

In `XamlInvariantTests`:

(a) Add a `SettingsWindow.xaml` entry to the `RequiredNames` member data. Replace the `RequiredNames()` method's array with this (adds the third entry; keeps the existing two unchanged):

```csharp
    public static IEnumerable<object[]> RequiredNames() => new[]
    {
        new object[] { "MainWindow.xaml", new[]
        {
            "Browser", "UrlBox", "ProfilesCombo", "PinToggle", "PinnedHint", "PopOutButton",
            "BackButton", "ReloadButton", "HomeButton", "SaveProfileButton",
            "MinimizeButton", "MaximizeButton", "CloseButton",
            "SourcePlaceholder", "RuntimeErrorPanel", "RuntimeErrorText",
        }},
        new object[] { "PlayerWindow.xaml", new[]
        {
            "ChromeStrip", "FadeToggle", "PinToggle", "CloseButton", "Player",
        }},
        new object[] { "SettingsWindow.xaml", new[]
        {
            "ResetAppStateButton", "ResetDescriptionText",
            "ClearBrowserDataButton", "ClearDescriptionText", "CloseButton",
        }},
    };
```

(b) Add `"SettingsWindow.xaml"` to the `files` array in `Every_StaticResource_reference_is_defined`:

```csharp
        var files = new[]
        {
            "App.xaml", "MainWindow.xaml", "PlayerWindow.xaml", "SettingsWindow.xaml",
            "Theme/ControlStyles.xaml", "Theme/Colors.xaml",
        };
```

(c) Add `"SettingsWindow.xaml"` to the `Glyph_controls_use_the_icon_font` theory:

```csharp
    [Theory]
    [InlineData("MainWindow.xaml")]
    [InlineData("PlayerWindow.xaml")]
    [InlineData("SettingsWindow.xaml")]
    public void Glyph_controls_use_the_icon_font(string file)
```

(d) Add a new fact that the dialog is not transparent (it hosts no WebView2):

```csharp
    [Fact]
    public void SettingsWindow_is_not_transparent()
    {
        var w = XamlTestFiles.Load("SettingsWindow.xaml").Root!;
        Assert.NotEqual("True", w.Attribute("AllowsTransparency")?.Value);
    }
```

- [ ] **Step 2: Write the failing tests (Wpf)**

In `tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs`, add these facts (the `using PiPlay;` import is already present; `Button`/`TextBlock` come from `System.Windows.Controls` which is already imported):

```csharp
    [Fact]
    public void SettingsWindow_constructs_without_throwing() => StaTestThread.Invoke(() =>
    {
        var ex = Record.Exception(() => new SettingsWindow(isBrowserReady: true));
        Assert.Null(ex);
    });

    [Fact]
    public void SettingsWindow_shows_the_tested_privacy_wording() => StaTestThread.Invoke(() =>
    {
        var w = new SettingsWindow(isBrowserReady: true);
        Assert.Equal(PiPlay.Services.PrivacyService.ResetDescription,
            ((TextBlock)w.FindName("ResetDescriptionText")!).Text);
        Assert.Equal(PiPlay.Services.PrivacyService.ClearDescription,
            ((TextBlock)w.FindName("ClearDescriptionText")!).Text);
        Assert.Equal(PiPlay.Services.PrivacyService.ResetActionLabel,
            (string)((Button)w.FindName("ResetAppStateButton")!).Content);
        Assert.Equal(PiPlay.Services.PrivacyService.ClearActionLabel,
            (string)((Button)w.FindName("ClearBrowserDataButton")!).Content);
    });

    [Fact]
    public void SettingsWindow_disables_only_clear_when_browser_not_ready() => StaTestThread.Invoke(() =>
    {
        var notReady = new SettingsWindow(isBrowserReady: false);
        Assert.True(((Button)notReady.FindName("ResetAppStateButton")!).IsEnabled);
        Assert.False(((Button)notReady.FindName("ClearBrowserDataButton")!).IsEnabled);

        var ready = new SettingsWindow(isBrowserReady: true);
        Assert.True(((Button)ready.FindName("ResetAppStateButton")!).IsEnabled);
        Assert.True(((Button)ready.FindName("ClearBrowserDataButton")!).IsEnabled);
    });
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test --filter Category=Markup` then `dotnet test --filter Category=Wpf`
Expected: FAIL — `SettingsWindow.xaml` not found / `SettingsWindow` type does not exist.

- [ ] **Step 4: Create the XAML**

Create `src/PiPlay/SettingsWindow.xaml`:

```xml
<Window x:Class="PiPlay.SettingsWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="PiPlay settings"
        Width="480" SizeToContent="Height"
        WindowStyle="None"
        ResizeMode="NoResize"
        AllowsTransparency="False"
        WindowStartupLocation="CenterOwner"
        ShowInTaskbar="False"
        Background="{StaticResource AppBackground}"
        UseLayoutRounding="False"
        SnapsToDevicePixels="True">

  <Border BorderBrush="{StaticResource BorderSubtle}" BorderThickness="1">
    <StackPanel>

      <!-- Title bar (drag to move; close button) -->
      <Grid Background="{StaticResource SurfaceBase}" Height="42"
            MouseLeftButtonDown="TitleBar_MouseLeftButtonDown">
        <TextBlock Text="Settings" Margin="16,0" VerticalAlignment="Center"
                   FontSize="14" FontWeight="SemiBold" Foreground="{StaticResource TextPrimary}" />
        <Button x:Name="CloseButton" Style="{StaticResource CloseIconButton}"
                Content="&#xE8BB;" ToolTip="Close"
                HorizontalAlignment="Right" Margin="0,0,5,0" Click="CloseButton_Click" />
      </Grid>

      <!-- Privacy section -->
      <StackPanel Margin="20">
        <TextBlock Text="Privacy" FontSize="16" FontWeight="SemiBold"
                   Foreground="{StaticResource TextPrimary}" Margin="0,0,0,2" />

        <TextBlock x:Name="ResetDescriptionText" TextWrapping="Wrap" FontSize="12"
                   Foreground="{StaticResource TextSecondary}" Margin="0,10,0,8" />
        <Button x:Name="ResetAppStateButton" Style="{StaticResource DarkButton}"
                HorizontalAlignment="Left" Click="ResetAppStateButton_Click" />

        <Border Height="1" Background="{StaticResource BorderSubtle}" Margin="0,18" />

        <TextBlock x:Name="ClearDescriptionText" TextWrapping="Wrap" FontSize="12"
                   Foreground="{StaticResource TextSecondary}" Margin="0,0,0,8" />
        <Button x:Name="ClearBrowserDataButton" Style="{StaticResource DarkButton}"
                HorizontalAlignment="Left" Click="ClearBrowserDataButton_Click" />
      </StackPanel>

    </StackPanel>
  </Border>
</Window>
```

- [ ] **Step 5: Create the code-behind**

Create `src/PiPlay/SettingsWindow.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Input;
using PiPlay.Services;

namespace PiPlay;

/// <summary>The action the user confirmed in the Settings window, read by MainWindow after close.</summary>
internal enum PrivacyAction { None, ResetAppState, ClearBrowserData }

/// <summary>
/// Themed Settings window (spec 12, Phase 2). Hosts the Privacy section. It confirms an action and
/// records <see cref="RequestedAction"/>, then closes — it performs no app/WebView work itself
/// (MainWindow does, after the modal closes). Visible wording is sourced from
/// <see cref="PrivacyService"/> so the UI and the tested constants cannot drift.
/// </summary>
public partial class SettingsWindow : Window
{
    internal PrivacyAction RequestedAction { get; private set; } = PrivacyAction.None;

    public SettingsWindow(bool isBrowserReady)
    {
        InitializeComponent();

        ResetDescriptionText.Text = PrivacyService.ResetDescription;
        ResetAppStateButton.Content = PrivacyService.ResetActionLabel;
        ClearDescriptionText.Text = PrivacyService.ClearDescription;
        ClearBrowserDataButton.Content = PrivacyService.ClearActionLabel;

        // Only the Clear action needs a live browser; Reset never does.
        ClearBrowserDataButton.IsEnabled = isBrowserReady;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void ResetAppStateButton_Click(object sender, RoutedEventArgs e)
    {
        ResetAppStateButton.IsEnabled = false;
        if (Prompt.AskConfirm(this, PrivacyService.ResetConfirmTitle, PrivacyService.ResetConfirmBody,
                PrivacyService.ResetConfirmButton, danger: false))
        {
            RequestedAction = PrivacyAction.ResetAppState;
            DialogResult = true;
        }
        else
        {
            ResetAppStateButton.IsEnabled = true;
        }
    }

    private void ClearBrowserDataButton_Click(object sender, RoutedEventArgs e)
    {
        ClearBrowserDataButton.IsEnabled = false;
        if (Prompt.AskConfirm(this, PrivacyService.ClearConfirmTitle, PrivacyService.ClearConfirmBody,
                PrivacyService.ClearConfirmButton, danger: true))
        {
            RequestedAction = PrivacyAction.ClearBrowserData;
            DialogResult = true;
        }
        else
        {
            ClearBrowserDataButton.IsEnabled = true;
        }
    }
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test --filter Category=Markup` then `dotnet test --filter Category=Wpf`
Expected: PASS — `SettingsWindow` markup + Wpf facts green; existing window facts still green.

- [ ] **Step 7: Commit**

```bash
git add src/PiPlay/SettingsWindow.xaml src/PiPlay/SettingsWindow.xaml.cs tests/PiPlay.Tests/Ui/XamlInvariantTests.cs tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs
git commit -m "$(cat <<'EOF'
feat(privacy): themed Settings window with Privacy section

Result-based (PrivacyAction) modal: confirms an action, records the result,
closes — no async work across the modal. Visible text sourced from the tested
PrivacyService constants; only the Clear button is gated on browser readiness.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 6: MainWindow gear + hardened reset/clear wiring with tests

**Files:**
- Modify: `src/PiPlay/MainWindow.xaml`, `src/PiPlay/MainWindow.xaml.cs`
- Test: `tests/PiPlay.Tests/Ui/XamlInvariantTests.cs`, `tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs`

- [ ] **Step 1: Write the failing tests (Markup)**

(a) In `XamlInvariantTests.RequiredNames()`, add `"SettingsButton"` to the `MainWindow.xaml` name array (first entry), so it reads:

```csharp
        new object[] { "MainWindow.xaml", new[]
        {
            "Browser", "UrlBox", "ProfilesCombo", "PinToggle", "PinnedHint", "PopOutButton",
            "BackButton", "ReloadButton", "HomeButton", "SaveProfileButton",
            "SettingsButton", "MinimizeButton", "MaximizeButton", "CloseButton",
            "SourcePlaceholder", "RuntimeErrorPanel", "RuntimeErrorText",
        }},
```

(b) In `Caption_and_toolbar_controls_have_tooltips`, add `"SettingsButton"` to the names array:

```csharp
        foreach (var name in new[]
        {
            "SettingsButton", "MinimizeButton", "MaximizeButton", "CloseButton", "BackButton",
            "ReloadButton", "HomeButton", "UrlBox", "ProfilesCombo", "SaveProfileButton", "PinToggle",
        })
```

- [ ] **Step 2: Write the failing tests (Wpf)**

In `WpfRuntimeTests.cs`, add `using Microsoft.Web.WebView2.Wpf;` to the top imports, then add:

```csharp
    [Fact]
    public void MainWindow_exposes_settings_button() => StaTestThread.Invoke(() =>
    {
        var w = new MainWindow();
        Assert.IsType<Button>(w.FindName("SettingsButton"));
    });

    [Fact]
    public void Reset_applies_to_ui_without_a_live_browser() => StaTestThread.Invoke(() =>
    {
        var w = new MainWindow();

        // CoreWebView2 is null because the window is never shown (Loaded never runs).
        var ex = Record.Exception(() => w.ApplyResetState());

        Assert.Null(ex);
        Assert.Empty(((ComboBox)w.FindName("ProfilesCombo")!).Items);
        Assert.False(((ToggleButton)w.FindName("PinToggle")!).IsChecked);
        Assert.Null(w.PendingUrlForTests);                                   // reset queued no navigation
        Assert.Null(((WebView2)w.FindName("Browser")!).Source);             // browser source untouched
    });
```

Note: `ComboBox` and `ToggleButton` are in `System.Windows.Controls` / `System.Windows.Controls.Primitives`. `System.Windows.Controls` is already imported; add `using System.Windows.Controls.Primitives;` for `ToggleButton`.

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test --filter Category=Markup` then `dotnet test --filter Category=Wpf`
Expected: FAIL — `SettingsButton` not present in markup; `ApplyResetState` / `PendingUrlForTests` do not exist.

- [ ] **Step 4: Add the gear button to the XAML**

In `src/PiPlay/MainWindow.xaml`, add `SettingsButton` as the first child of the caption-button `StackPanel` (the one with `Grid.Column="1"` and `WindowChrome.IsHitTestVisibleInChrome="True"`), before `MinimizeButton`:

```xml
      <StackPanel Grid.Column="1" Orientation="Horizontal" WindowChrome.IsHitTestVisibleInChrome="True">
        <Button x:Name="SettingsButton" Style="{StaticResource IconButton}" Click="SettingsButton_Click"
                Content="&#xE713;" ToolTip="Settings" />
        <Button x:Name="MinimizeButton" Style="{StaticResource IconButton}" Click="MinimizeButton_Click"
                Content="&#xE921;" ToolTip="Minimize" />
        <Button x:Name="MaximizeButton" Style="{StaticResource IconButton}" Click="MaximizeButton_Click"
                Content="&#xE922;" ToolTip="Maximize" />
        <Button x:Name="CloseButton" Style="{StaticResource CloseIconButton}" Click="CloseButton_Click"
                Content="&#xE8BB;" ToolTip="Close" />
      </StackPanel>
```

- [ ] **Step 5: Wire the code-behind**

In `src/PiPlay/MainWindow.xaml.cs`:

(a) Change the field declaration so reset can reassign it:

```csharp
    private readonly SettingsService _settingsService = new();
    private AppSettings _settings;
```

(b) Add a re-entrancy guard field alongside the other Video-Popout state fields (after `private bool _sourceWasPlayingAtPopout;`):

```csharp
    private bool _privacyActionInProgress;
```

(c) Add the gear handler + the reset/clear methods + the test accessor. Place them after the Profiles region (after `SaveProfileButton_Click`), before the Video Popout region:

```csharp
    // --- Privacy actions: Settings window (spec 19, Phase 2, REQ-PRIVACY-01/02) ---

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_privacyActionInProgress) return;

        var dialog = new SettingsWindow(isBrowserReady: _browserReady && Browser.CoreWebView2 is not null)
        {
            Owner = this,
            Topmost = Topmost,
        };

        if (dialog.ShowDialog() != true) return;

        switch (dialog.RequestedAction)
        {
            case PrivacyAction.ResetAppState:
                PerformResetAppState();
                break;
            case PrivacyAction.ClearBrowserData:
                _ = PerformClearBrowserDataAsync();
                break;
        }
    }

    private void PerformResetAppState()
    {
        if (_privacyActionInProgress) return;
        _privacyActionInProgress = true;
        try
        {
            ApplyResetState();
            Prompt.ShowInfo(this, PrivacyService.ResetDoneTitle, PrivacyService.ResetDoneBody);
        }
        finally
        {
            _privacyActionInProgress = false;
        }
    }

    /// <summary>
    /// Apply a reset to the live UI: defaults to settings.json, empty profiles combo, pin off.
    /// References no Browser/WebView2 member and queues no navigation, so the user stays signed in.
    /// Internal so a headless WPF test can prove it runs with a null CoreWebView2.
    /// </summary>
    internal void ApplyResetState()
    {
        _settings = _settingsService.Reset();
        ApplyTopmost(false);
        LoadProfilesIntoCombo();
    }

    private async Task PerformClearBrowserDataAsync()
    {
        if (_privacyActionInProgress) return;

        // Re-check readiness at execution time (the cached enabled state can be stale).
        var core = Browser.CoreWebView2;
        if (!_browserReady || core is null)
        {
            Prompt.ShowInfo(this, PrivacyService.ClearConfirmTitle, PrivacyService.ClearBrowserNotReady);
            return;
        }

        _privacyActionInProgress = true;
        SettingsButton.IsEnabled = false;   // no second Settings window mid-await
        try
        {
            // Single shared profile: closing the popout avoids it showing a logged-out surface.
            if (_player is not null) { try { _player.Close(); } catch { /* ignore */ } }

            await PrivacyService.ClearBrowserDataAsync(core);

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
            SettingsButton.IsEnabled = true;
        }
    }

    /// <summary>Test-only: the navigation queued while the browser was not ready (null = none).</summary>
    internal string? PendingUrlForTests => _pendingUrl;
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test --filter Category=Markup` then `dotnet test --filter Category=Wpf`
Expected: PASS — `MainWindow_exposes_settings_button`, `Reset_applies_to_ui_without_a_live_browser`, and the markup name/tooltip facts all green; the glyph test still green (gear uses `IconButton` + a PUA glyph).

- [ ] **Step 7: Commit**

```bash
git add src/PiPlay/MainWindow.xaml src/PiPlay/MainWindow.xaml.cs tests/PiPlay.Tests/Ui/XamlInvariantTests.cs tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs
git commit -m "$(cat <<'EOF'
feat(privacy): wire Settings gear + hardened reset/clear in MainWindow

Gear opens the modal Settings window; result dispatched after close. Reset
applies defaults to the live UI with no browser dependency (tested headless,
queues no navigation). Clear re-checks readiness, closes the popout, clears the
shared profile, reloads home, and reports result — guarded against re-entrancy,
stale readiness, and async failure.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 7: Full suite green

**Files:** none (verification).

- [ ] **Step 1: Run the entire suite**

Run: `dotnet test`
Expected: PASS — all lanes (`Logic` + `Markup` + `Wpf`). Note the count (was 119 before this work; it grows by the tests added here — roughly 11 new). Record the exact pass count from the output.

- [ ] **Step 2: If anything fails, fix before continuing.** Do not proceed to docs/version with a red suite.

---

## Task 8: Layer-4 smoke + QA checklist

**Files:**
- Modify: `scripts/Test-UiSmoke.ps1`, `docs/QA_Checklist.md`

- [ ] **Step 1: Add the gear presence check to the smoke**

In `scripts/Test-UiSmoke.ps1`, add a line to the `Assert-Element` block (after the `ProfilesCombo` assert, around line 55):

```powershell
    Assert-Element 'SettingsButton' 'Settings gear button'
```

- [ ] **Step 2: Turn the QA Phase-2 privacy rows into concrete steps**

In `docs/QA_Checklist.md`, replace the two Phase-2 lines in section 6 (currently lines 61–62) with:

```markdown
- [ ] Phase 2: Open Settings (gear) → **Reset app state** → confirm. Settings/profiles/placement clear, but the YouTube tab is **still signed in** (no re-login, no 2FA). **(REQ-PRIVACY-01 — not in MVP)**
- [ ] Phase 2: Settings → **Clear browser data** is a separate, red-confirmed action; after confirming, reload youtube.com and verify you are **signed out**. **(REQ-PRIVACY-02 — not in MVP)**
- [ ] Phase 2: The two actions are clearly worded as distinct; the Clear confirm warns about signing out; Cancel (not the destructive button) has default focus.
- [ ] Phase 2: With the WebView2 runtime missing (recovery panel showing), the Clear browser data button is disabled or reports "browser isn't ready"; Reset still works.
```

- [ ] **Step 3: Commit**

```bash
git add scripts/Test-UiSmoke.ps1 docs/QA_Checklist.md
git commit -m "$(cat <<'EOF'
test(e2e): Phase-2 privacy smoke + QA checklist steps

UIA presence check for the Settings gear; concrete QA steps for Reset (stays
signed in) and Clear browser data (signs out, separate confirmed action).

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 9: CHANGELOG + version bump

**Files:**
- Modify: `docs/CHANGELOG.md`, `VERSION`, `BUILD_NUMBER`

- [ ] **Step 1: Update the CHANGELOG**

In `docs/CHANGELOG.md`, under `## [Unreleased]`, replace the `### Planned — Phase 2 (remaining)` line about privacy with a new shipped subsection (place it after the existing `### Added — Phase 2 (convenience)` block):

```markdown
### Added — Phase 2 (privacy)
- **Reset app state** (REQ-PRIVACY-01) and **Clear browser data** (REQ-PRIVACY-02) as separate,
  confirmed actions in a new themed **Settings** window (gear in the Source Window title bar).
  Reset atomically rewrites `settings.json` to defaults (settings, profiles, placement) and
  **keeps the YouTube session** — you stay signed in. Clear browser data is a separate, red-confirmed
  action that clears the shared WebView2 profile (`ClearBrowsingDataAsync(AllProfile)`) and signs you
  out. The only code path that logs you out is this explicit action — enforced by a regression test.
  Wording lives in `Services/PrivacyService.cs` and the UI binds to it so the visible text and the
  tested copy cannot drift. The flow is hardened against double-clicks, stale browser readiness,
  failed clears, and modal-owner issues (result-based, work runs after the modal closes).
```

And update the remaining-Phase-2 planned line to drop privacy:

```markdown
### Planned — Phase 2 (remaining)
- `Auto` off by default, profile edit/validation, release publish profiles, and Phase 2 QA coverage.
```

- [ ] **Step 2: Bump the version**

Set `VERSION` file contents to:

```
0.3.0
```

Set `BUILD_NUMBER` file contents to:

```
7
```

- [ ] **Step 3: Commit**

```bash
git add docs/CHANGELOG.md VERSION BUILD_NUMBER
git commit -m "$(cat <<'EOF'
docs(changelog): Phase 2 privacy actions; bump 0.2.1 -> 0.3.0

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Self-review (completed during planning)

**Spec coverage:** REQ-PRIVACY-01 → Tasks 2, 6; REQ-PRIVACY-02 → Tasks 1, 5, 6; "worded separately" → Task 1; sacred login invariant → Tasks 2 (test), 6 (no-browser reset); change 1 (visible == tested) → Tasks 1 + 5; change 2 (atomic / no WebView2 recreate-navigate) → Tasks 2 + 6; change 3 (hardened flow) → Tasks 5 + 6; entry point gear→Settings → Tasks 5 + 6; testing layers → Tasks 1–8; docs/version → Tasks 8–9. No gaps.

**Placeholder scan:** none — every code/test step has complete content.

**Type consistency:** `PrivacyService.{ResetActionLabel, ResetDescription, ResetConfirmTitle/Body/Button, ResetDoneTitle/Body, ClearActionLabel, ClearDescription, ClearConfirmTitle/Body/Button, ClearDoneTitle/Body, ClearBrowserNotReady, ClearFailed, ClearKinds, ClearBrowserDataAsync}`; `SettingsService.Reset()`/`AtomicWrite`; `DangerButton`; `Prompt.AskConfirm`/`Prompt.ShowInfo`; `SettingsWindow(bool isBrowserReady)` + named controls `ResetAppStateButton`/`ResetDescriptionText`/`ClearBrowserDataButton`/`ClearDescriptionText`/`CloseButton` + `RequestedAction`/`PrivacyAction`; `MainWindow.{SettingsButton, SettingsButton_Click, PerformResetAppState, ApplyResetState, PerformClearBrowserDataAsync, PendingUrlForTests, _privacyActionInProgress, _settings (now non-readonly)}` — names are consistent across every task that references them.
