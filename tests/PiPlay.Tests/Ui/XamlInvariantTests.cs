using System.Text.RegularExpressions;
using System.Xml.Linq;
using PiPlay.Services;
using PiPlay.Theme;

namespace PiPlay.Tests;

/// <summary>
/// Layer 1 — markup invariants. Parses the source .xaml as XML (no WPF runtime) and asserts the
/// burned-in properties that, if silently flipped, break the app — most notably the
/// <c>UseLayoutRounding="False"</c> guard that re-catches the "rounding = 0" URL-text clipping.
/// </summary>
[Trait(TestCategories.Key, TestCategories.Markup)]
public class XamlInvariantTests
{
    // Segoe Fluent / MDL2 glyphs live in the Unicode Private Use Area (U+E000 and up).
    private const int PuaStart = 0xE000;

    private static XElement Window(string file) => XamlTestFiles.Load(file).Root!;
    private static string? Attr(XElement e, string name) => e.Attribute(name)?.Value;

    // --- Window layout / airspace / chrome ---

    [Theory]
    [InlineData("MainWindow.xaml")]
    [InlineData("PlayerWindow.xaml")]
    [InlineData("SettingsWindow.xaml")]
    public void Window_layout_and_airspace_invariants_hold(string file)
    {
        var w = Window(file);

        // The "rounding = 0" regression: layout rounding MUST be off on every window (UI-CHK-5).
        Assert.Equal("False", Attr(w, "UseLayoutRounding"));
        // WebView2 airspace hard constraint (ADR-0004): a transparent window breaks the HwndHost.
        Assert.Equal("False", Attr(w, "AllowsTransparency"));
        // Custom chrome + crisp scaling.
        Assert.Equal("None", Attr(w, "WindowStyle"));
        Assert.Equal("True", Attr(w, "SnapsToDevicePixels"));
    }

    [Theory]
    [InlineData("MainWindow.xaml", "42")]
    [InlineData("PlayerWindow.xaml", "0")]
    public void WindowChrome_invariants_hold(string file, string expectedCaptionHeight)
    {
        var chrome = Window(file).Descendants(XamlTestFiles.Pres + "WindowChrome").Single();

        Assert.Equal("0", chrome.Attribute("CornerRadius")?.Value);
        Assert.Equal("0", chrome.Attribute("GlassFrameThickness")?.Value);
        Assert.Equal("False", chrome.Attribute("UseAeroCaptionButtons")?.Value);
        Assert.Equal(BorderlessResizeHitTestPolicy.ResizeBorderDip.ToString(),
            chrome.Attribute("ResizeBorderThickness")?.Value);
        Assert.Equal(expectedCaptionHeight, chrome.Attribute("CaptionHeight")?.Value);
    }

    // --- WebView2 resize inset band (REQ-WINDOW-02, overhaul Task 1) ---

    [Theory]
    [InlineData("MainWindow.xaml", "Browser")]
    [InlineData("PlayerWindow.xaml", "Player")]
    public void WebView_margin_gives_the_window_the_resize_band(string file, string name)
    {
        var webview = XamlTestFiles.Load(file).Descendants()
            .Single(e => e.Attribute(XamlTestFiles.X + "Name")?.Value == name);

        // The WebView2 child HWND swallows WM_NCHITTEST, so left/right/bottom resize only works
        // where the top-level window owns the pixels: the band must equal the policy's resize DIP.
        var d = BorderlessResizeHitTestPolicy.ResizeBorderDip;
        var margins = webview.Descendants(XamlTestFiles.Pres + "Setter")
            .Where(s => s.Attribute("Property")?.Value == "Margin")
            .Select(s => s.Attribute("Value")?.Value)
            .ToList();
        Assert.Contains($"{d},0,{d},{d}", margins);   // normal state: band on left/right/bottom
        Assert.Contains("0", margins);                // maximized: full-bleed (zones inert there)

        // A direct Margin attribute would silently override the style setters.
        Assert.Null(webview.Attribute("Margin"));

        var trigger = webview.Descendants(XamlTestFiles.Pres + "DataTrigger").Single();
        Assert.Equal("Maximized", trigger.Attribute("Value")?.Value);
        Assert.Contains("WindowState", trigger.Attribute("Binding")?.Value ?? "");
    }

    // --- Required named controls (code-behind FindName / generated fields depend on these) ---

    [Theory]
    [MemberData(nameof(RequiredNames))]
    public void Required_named_controls_exist(string file, string[] names)
    {
        var present = XamlTestFiles.Load(file).Descendants()
            .Select(e => e.Attribute(XamlTestFiles.X + "Name")?.Value)
            .Where(n => n is not null)
            .ToHashSet();

        foreach (var name in names)
            Assert.Contains(name, present!);
    }

    public static IEnumerable<object[]> RequiredNames() => new[]
    {
        new object[] { "MainWindow.xaml", new[]
        {
            "Browser", "UrlBox", "ProfilesCombo", "PinToggle", "AutoToggle", "TitleText", "PinnedHint", "PopOutButton",
            "PopOutButtonIcon", "PopOutButtonText",
            "BackButton", "ReloadButton", "HomeButton", "SaveProfileButton",
            "EditProfileButton", "DeleteProfileButton",
            "SettingsButton", "MinimizeButton", "MaximizeButton", "CloseButton",
            "SourcePlaceholder", "PlaceholderNoteText", "RuntimeErrorPanel", "RuntimeErrorText",
        }},
        new object[] { "PlayerWindow.xaml", new[]
        {
            "ChromeStrip", "FadeToggle", "PinToggle", "ExpandButton", "CloseButton", "Player",
            "ErrorBar", "ErrorText", "FallbackButton", "ErrorDismissButton",
        }},
        new object[] { "SettingsWindow.xaml", new[]
        {
            "SettingsScroll",
            "PrivacySectionHeader", "AppearanceSectionHeader", "PlaybackSectionHeader", "AdvancedSectionHeader",
            "ResetAppStateButton", "ResetDescriptionText",
            "ClearBrowserDataButton", "ClearDescriptionText", "CloseButton",
            "ThemeSharpDarkPreset", "ThemeMinimalPreset", "ThemeSoftGlassPreset",
            "AccentChipCyan", "AccentChipSteelBlue", "AccentChipViolet", "AccentChipGreen", "AccentChipAmber",
            "FadeDelayShortPreset", "FadeDelayNormalPreset", "FadeDelayLongPreset",
            "ActiveOpacitySlider", "ActiveOpacityValueText", "IdleOpacitySlider", "IdleOpacityValueText",
            "StripAutoHideToggle",
            "CompactModeToggle", "CompactModeHintText",
        }},
    };

    // --- Settings is scrollable and sectioned (overhaul Task 5) ---

    [Fact]
    public void Settings_sections_live_inside_the_scroll_viewer_in_order()
    {
        var doc = XamlTestFiles.Load("SettingsWindow.xaml");
        var scroll = doc.Descendants(XamlTestFiles.Pres + "ScrollViewer")
            .Single(e => e.Attribute(XamlTestFiles.X + "Name")?.Value == "SettingsScroll");

        // Every section header scrolls; the order is the spec's Privacy/Appearance/Playback/Advanced.
        var headers = scroll.Descendants()
            .Select(e => e.Attribute(XamlTestFiles.X + "Name")?.Value)
            .Where(n => n is not null && n.EndsWith("SectionHeader"))
            .ToArray();
        Assert.Equal(new[]
        {
            "PrivacySectionHeader", "AppearanceSectionHeader", "PlaybackSectionHeader", "AdvancedSectionHeader",
        }, headers);

        // The title bar must NOT scroll away (CloseButton stays reachable at any content height).
        Assert.DoesNotContain(scroll.Descendants(),
            e => e.Attribute(XamlTestFiles.X + "Name")?.Value == "CloseButton");
    }

    [Fact]
    public void Settings_window_uses_the_fixed_height_frame()
    {
        // Frame model reconciled with the parallel main landing (b35c0dd): a fixed launch Height
        // (clamped to the work area in code) instead of SizeToContent, so the dialog cannot grow
        // with future sections; the scroll viewer never scrolls sideways.
        var doc = XamlTestFiles.Load("SettingsWindow.xaml");
        var root = doc.Root!;
        Assert.Null(root.Attribute("SizeToContent"));
        Assert.NotNull(root.Attribute("Height"));
        Assert.NotNull(root.Attribute("MinHeight"));

        var scroll = doc.Descendants(XamlTestFiles.Pres + "ScrollViewer")
            .Single(e => e.Attribute(XamlTestFiles.X + "Name")?.Value == "SettingsScroll");
        Assert.Equal("Disabled", scroll.Attribute("HorizontalScrollBarVisibility")?.Value);
    }

    [Theory]
    [InlineData("CompactModeToggle", "PlaybackSectionHeader", "AdvancedSectionHeader")]   // Playback owns compact
    [InlineData("FadeDelayShortPreset", "AdvancedSectionHeader", null)]                   // Advanced owns fade delay
    [InlineData("ActiveOpacitySlider", "AdvancedSectionHeader", null)]                    // Advanced owns opacity
    [InlineData("StripAutoHideToggle", "AdvancedSectionHeader", null)]                    // Advanced owns auto-hide
    public void Settings_controls_sit_under_their_section(string control, string after, string? before)
    {
        // Document order stands in for section membership: the sections share one StackPanel, so
        // "after its own header (and before the next)" is the structural invariant.
        var ordered = XamlTestFiles.Load("SettingsWindow.xaml").Descendants()
            .Select(e => e.Attribute(XamlTestFiles.X + "Name")?.Value)
            .Where(n => n is not null)
            .ToList();

        Assert.True(ordered.IndexOf(control) > ordered.IndexOf(after),
            $"{control} must follow {after}.");
        if (before is not null)
        {
            Assert.True(ordered.IndexOf(control) < ordered.IndexOf(before),
                $"{control} must precede {before}.");
        }
    }

    // --- Glyph icon-font fallback (REQ-UI-02: no .notdef boxes) + tooltips (UI-CHK-4) ---

    private static readonly HashSet<string> IconFontStyles = new()
    {
        "{StaticResource IconButton}", "{StaticResource CloseIconButton}", "{StaticResource PinToggle}",
    };

    [Theory]
    [InlineData("MainWindow.xaml")]
    [InlineData("PlayerWindow.xaml")]
    [InlineData("SettingsWindow.xaml")]
    public void Glyph_controls_use_the_icon_font(string file)
    {
        var doc = XamlTestFiles.Load(file);

        // Any TextBlock whose Text starts with a PUA glyph must declare the icon font inline.
        foreach (var tb in doc.Descendants(XamlTestFiles.Pres + "TextBlock"))
        {
            var text = tb.Attribute("Text")?.Value;
            if (string.IsNullOrEmpty(text) || text[0] < PuaStart) continue;
            var font = tb.Attribute("FontFamily")?.Value;
            Assert.True(font is not null && font.Contains("Segoe Fluent Icons"),
                $"Glyph TextBlock '{text}' in {file} is missing the icon FontFamily.");
        }

        // Any Button/ToggleButton carrying a glyph Content must use an icon-font style.
        foreach (var btn in doc.Descendants().Where(e =>
                     e.Name == XamlTestFiles.Pres + "Button" || e.Name == XamlTestFiles.Pres + "ToggleButton"))
        {
            var content = btn.Attribute("Content")?.Value;
            if (string.IsNullOrEmpty(content) || content[0] < PuaStart) continue;
            var style = btn.Attribute("Style")?.Value;
            Assert.True(style is not null && IconFontStyles.Contains(style),
                $"Glyph button '{content}' in {file} must use an icon-font style (was '{style}').");
        }
    }

    // --- Accessible names for icon-only / templated controls (REQ-UI-02, overhaul Task 7) ---

    [Theory]
    [MemberData(nameof(RequiredAccessibleNames))]
    public void Icon_controls_have_accessible_names(string file, string[] names)
    {
        var byName = XamlTestFiles.Load(file).Descendants()
            .Where(e => e.Attribute(XamlTestFiles.X + "Name") is not null)
            .GroupBy(e => e.Attribute(XamlTestFiles.X + "Name")!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var name in names)
        {
            Assert.False(string.IsNullOrWhiteSpace(byName[name].Attribute("AutomationProperties.Name")?.Value),
                $"{name} in {file} is missing AutomationProperties.Name (REQ-UI-02).");
        }
    }

    public static IEnumerable<object[]> RequiredAccessibleNames() => new[]
    {
        // MaximizeButton's name is deliberately state-neutral ("Maximize or restore"): its glyph
        // flips in code on StateChanged. PopOutButton's static name is its launch-state action;
        // the Task 6 show-popout state change must update it in the same code path as the label.
        // PlayerWindow's ExpandButton follows the MaximizeButton precedent (state-neutral name,
        // glyph/tooltip flip in code).
        new object[] { "MainWindow.xaml", new[]
        {
            "SettingsButton", "MinimizeButton", "MaximizeButton", "CloseButton",
            "BackButton", "ReloadButton", "HomeButton", "UrlBox", "ProfilesCombo",
            "SaveProfileButton", "EditProfileButton", "DeleteProfileButton",
            "PinToggle", "AutoToggle", "PopOutButton",
        }},
        new object[] { "PlayerWindow.xaml", new[] { "FadeToggle", "PinToggle", "ExpandButton", "CloseButton" } },
        new object[] { "SettingsWindow.xaml", new[] { "CloseButton" } },
    };

    [Fact]
    public void Caption_and_toolbar_controls_have_tooltips()
    {
        var byName = XamlTestFiles.Load("MainWindow.xaml").Descendants()
            .Where(e => e.Attribute(XamlTestFiles.X + "Name") is not null)
            .ToDictionary(e => e.Attribute(XamlTestFiles.X + "Name")!.Value);

        foreach (var name in new[]
        {
            "SettingsButton", "MinimizeButton", "MaximizeButton", "CloseButton", "BackButton",
            "ReloadButton", "HomeButton", "UrlBox", "ProfilesCombo", "SaveProfileButton",
            "EditProfileButton", "DeleteProfileButton", "PinToggle", "AutoToggle",
        })
        {
            Assert.False(string.IsNullOrWhiteSpace(byName[name].Attribute("ToolTip")?.Value),
                $"{name} is missing a ToolTip (UI-CHK-4).");
        }
    }

    // --- Strip auto-hide layout (spec 7.2, Phase 4 Task 4) ---

    [Fact]
    public void Player_strip_row_is_auto_sized_so_a_collapsed_strip_returns_its_height()
    {
        var doc = XamlTestFiles.Load("PlayerWindow.xaml");

        // The collapse works by Visibility on the strip element; a fixed RowDefinition would keep
        // a dead 32-DIP band above the video. The row must be Auto and the strip carries the height.
        var firstRow = doc.Descendants(XamlTestFiles.Pres + "RowDefinition").First();
        Assert.Equal("Auto", firstRow.Attribute("Height")?.Value);

        var strip = doc.Descendants()
            .Single(e => e.Attribute(XamlTestFiles.X + "Name")?.Value == "ChromeStrip");
        Assert.Equal("32", strip.Attribute("Height")?.Value);
    }

    // --- Opacity sliders pin the policy floor (spec 7.3, Phase 4) ---

    [Theory]
    [InlineData("ActiveOpacitySlider")]
    [InlineData("IdleOpacitySlider")]
    public void Opacity_slider_minimum_matches_the_policy_ui_floor(string name)
    {
        var slider = XamlTestFiles.Load("SettingsWindow.xaml").Descendants()
            .Single(e => e.Attribute(XamlTestFiles.X + "Name")?.Value == name);

        Assert.Equal((WindowOpacityPolicy.UiFloor * 100).ToString(), slider.Attribute("Minimum")?.Value);
        Assert.Equal((WindowOpacityPolicy.Max * 100).ToString(), slider.Attribute("Maximum")?.Value);
    }

    // --- Compact error bar (spec 10.3 / Q-6, Stage 4) ---

    [Fact]
    public void Player_error_bar_is_collapsed_by_default_with_accessible_actions()
    {
        var byName = XamlTestFiles.Load("PlayerWindow.xaml").Descendants()
            .Where(e => e.Attribute(XamlTestFiles.X + "Name") is not null)
            .ToDictionary(e => e.Attribute(XamlTestFiles.X + "Name")!.Value);

        // The bar must never show before an actual compact error.
        Assert.Equal("Collapsed", byName["ErrorBar"].Attribute("Visibility")?.Value);

        // Both actions need a tooltip (UI-CHK-4) and an automation name (accessibility).
        foreach (var name in new[] { "FallbackButton", "ErrorDismissButton" })
        {
            Assert.False(string.IsNullOrWhiteSpace(byName[name].Attribute("ToolTip")?.Value),
                $"{name} is missing a ToolTip (UI-CHK-4).");
            Assert.False(string.IsNullOrWhiteSpace(
                    byName[name].Attribute(XamlTestFiles.Pres + "AutomationProperties.Name")?.Value ??
                    byName[name].Attribute("AutomationProperties.Name")?.Value),
                $"{name} is missing an AutomationProperties.Name.");
        }

        // User-facing wording stays in the settled vocabulary: "normal page", never internal names.
        var fallbackText = byName["FallbackButton"].Attribute("Content")?.Value ?? "";
        Assert.Contains("normal page", fallbackText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PlayerWindow", fallbackText);
        Assert.DoesNotContain("embed", fallbackText, StringComparison.OrdinalIgnoreCase);
    }

    // --- Resource integrity: every {StaticResource} reference resolves to a defined key ---

    [Fact]
    public void Every_StaticResource_reference_is_defined()
    {
        var files = new[]
        {
            "App.xaml", "MainWindow.xaml", "PlayerWindow.xaml", "SettingsWindow.xaml",
            "Theme/ControlStyles.xaml", "Theme/Colors.xaml",
        };

        var defined = new HashSet<string>();
        var referenced = new HashSet<string>();
        var rx = new Regex(@"\{StaticResource\s+([^}]+)\}", RegexOptions.Compiled);

        foreach (var f in files)
        {
            foreach (var el in XamlTestFiles.Load(f).Descendants())
            {
                if (el.Attribute(XamlTestFiles.X + "Key")?.Value is { } key) defined.Add(key.Trim());
                foreach (var a in el.Attributes())
                    foreach (Match m in rx.Matches(a.Value))
                        referenced.Add(m.Groups[1].Value.Trim());
            }
        }

        var missing = referenced.Where(r => !defined.Contains(r)).OrderBy(x => x).ToArray();
        Assert.True(missing.Length == 0, "Undefined StaticResource keys: " + string.Join(", ", missing));
    }

    // --- Theme contrast (WCAG) computed from the actual Colors.xaml tokens ---

    private static Dictionary<string, string> ColorTokens()
    {
        return XamlTestFiles.Load("Theme/Colors.xaml")
            .Descendants(XamlTestFiles.Pres + "Color")
            .Where(e => e.Attribute(XamlTestFiles.X + "Key") is not null)
            .ToDictionary(
                e => e.Attribute(XamlTestFiles.X + "Key")!.Value,
                e => e.Value.Trim());
    }

    [Theory]
    [InlineData("TextPrimaryColor", "SurfaceRaisedColor", 4.5)]   // URL box (UI-CHK-5)
    [InlineData("TextPrimaryColor", "AppBackgroundColor", 4.5)]
    [InlineData("TextPrimaryColor", "SurfaceBaseColor", 4.5)]
    [InlineData("TextSecondaryColor", "SurfaceBaseColor", 4.5)]   // secondary text / empty state
    public void Theme_contrast_meets_minimum(string fg, string bg, double min)
    {
        var t = ColorTokens();
        var ratio = Wcag.ContrastRatio(t[fg], t[bg]);
        Assert.True(ratio >= min, $"{fg} on {bg} = {ratio:F2}:1, below {min}:1.");
    }

    [Fact]
    public void Accent_button_text_is_readable_on_accent_fill()
    {
        // AccentButton: foreground #FF06141A literal on the AccentPrimary fill (ControlStyles.xaml).
        // This is the DEFAULT (cyan) token; user accents are gated by the contrast theory below.
        var ratio = Wcag.ContrastRatio("#FF06141A", ColorTokens()["AccentPrimaryColor"]);
        Assert.True(ratio >= 4.5, $"Accent button text contrast = {ratio:F2}:1.");
    }

    [Fact]
    public void Accent_primary_token_defaults_to_the_sharp_dark_cyan()
    {
        // Overhaul Task 9: the theme accent token defaults to the existing cyan as a markup fallback
        // before ThemeResourceApplier runs. AccentCyan stays defined as a compatibility alias.
        var t = ColorTokens();
        Assert.Equal(t["AccentCyanColor"], t["AccentPrimaryColor"]);
        Assert.Equal(t["AccentCyanLightColor"], t["AccentPrimaryLightColor"]);
    }

    [Theory]
    [InlineData("AccentCyanColor")]
    [InlineData("AccentVioletColor")]
    [InlineData("AccentGreenColor")]
    [InlineData("AccentAmberColor")]
    public void Customization_accents_are_readable_as_active_glyphs_on_hover_surface(string accent)
    {
        var t = ColorTokens();
        var ratio = Wcag.ContrastRatio(t[accent], t["SurfaceHoverColor"]);
        Assert.True(ratio >= 3.0, $"{accent} on SurfaceHoverColor = {ratio:F2}:1, below 3:1.");
    }

    [Fact]
    public void Theme_accent_palette_is_readable()
    {
        // Every OFFERED accent chip (ThemeCatalog) must read as an on-dark glyph (>=3:1 on the hover
        // surface) AND carry the dark AccentButton text (>=4.5:1) — the gate behind the Task 10
        // single-accent palette and the Task 9 startup recolor of the primary button.
        var hover = ColorTokens()["SurfaceHoverColor"];
        foreach (var option in ThemeCatalog.AccentOptions)
        {
            var glyph = Wcag.ContrastRatio(option.HexColor, hover);
            Assert.True(glyph >= 3.0, $"Accent {option.Key} ({option.HexColor}) on hover surface = {glyph:F2}:1.");

            var text = Wcag.ContrastRatio("#FF06141A", option.HexColor);
            Assert.True(text >= 4.5, $"Dark button text on accent {option.Key} ({option.HexColor}) = {text:F2}:1.");
        }
    }

    [Fact]
    public void Settings_theme_and_accent_controls_match_the_catalog()
    {
        var controls = XamlTestFiles.Load("SettingsWindow.xaml").Descendants()
            .Where(e => e.Attribute(XamlTestFiles.X + "Name") is not null)
            .ToList();

        string? Tag(string name) => controls
            .First(e => e.Attribute(XamlTestFiles.X + "Name")!.Value == name).Attribute("Tag")?.Value;
        IEnumerable<string> NamesWhere(Func<string, bool> pred) => controls
            .Select(e => e.Attribute(XamlTestFiles.X + "Name")!.Value).Where(pred);

        // Preset chip Tags are the catalog preset ids; accent chip Tags are the catalog hex colors.
        // Order-independent so the hand-written markup and the catalog cannot drift apart.
        var presetTags = NamesWhere(n => n.StartsWith("Theme") && n.EndsWith("Preset")).Select(Tag).ToHashSet();
        Assert.Equal(ThemeCatalog.Presets.Select(p => p.Id).ToHashSet(), presetTags!);

        var accentTags = NamesWhere(n => n.StartsWith("AccentChip")).Select(Tag).ToHashSet();
        Assert.Equal(ThemeCatalog.AccentOptions.Select(o => o.HexColor).ToHashSet(), accentTags!);
    }

    [Fact]
    public void Settings_appearance_controls_have_tooltips_and_accessible_names()
    {
        var byName = XamlTestFiles.Load("SettingsWindow.xaml").Descendants()
            .Where(e => e.Attribute(XamlTestFiles.X + "Name") is not null)
            .GroupBy(e => e.Attribute(XamlTestFiles.X + "Name")!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var name in new[]
        {
            "ThemeSharpDarkPreset", "ThemeMinimalPreset", "ThemeSoftGlassPreset",
            "AccentChipCyan", "AccentChipSteelBlue", "AccentChipViolet", "AccentChipGreen", "AccentChipAmber",
            "FadeDelayShortPreset", "FadeDelayNormalPreset", "FadeDelayLongPreset",
            "ActiveOpacitySlider", "IdleOpacitySlider",
            "StripAutoHideToggle",
            "CompactModeToggle",
        })
        {
            Assert.False(string.IsNullOrWhiteSpace(byName[name].Attribute("ToolTip")?.Value),
                $"{name} is missing a ToolTip.");
            Assert.False(string.IsNullOrWhiteSpace(byName[name].Attribute("AutomationProperties.Name")?.Value),
                $"{name} is missing AutomationProperties.Name.");
        }
    }

    [Fact]
    public void SettingsWindow_is_not_transparent()
    {
        // The Settings dialog hosts no WebView2, but stays opaque for visual consistency.
        var w = XamlTestFiles.Load("SettingsWindow.xaml").Root!;
        Assert.NotEqual("True", w.Attribute("AllowsTransparency")?.Value);
    }

    [Fact]
    public void DangerButton_style_is_defined()
    {
        var keys = XamlTestFiles.Load("Theme/ControlStyles.xaml").Descendants()
            .Select(e => e.Attribute(XamlTestFiles.X + "Key")?.Value)
            .Where(k => k is not null);
        Assert.Contains("DangerButton", keys);
    }

    // --- App manifest declares per-monitor-v2 DPI awareness (REQ-WINDOW-01, Q-7) ---

    [Fact]
    public void App_manifest_declares_per_monitor_v2_dpi()
    {
        var dpiAwareness = XamlTestFiles.Load("app.manifest")
            .Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "dpiAwareness");

        Assert.NotNull(dpiAwareness);
        Assert.Contains("PerMonitorV2", dpiAwareness!.Value);
    }
}
