using System.IO;
using System.Text.Json;
using PiPlay.Models;
using PiPlay.Services;
using PiPlay.Theme;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class SettingsServiceTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public SettingsServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "PiPlayTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "settings.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    [Fact]
    public void Load_returns_defaults_when_file_missing()
    {
        var svc = new SettingsService(_path);
        var settings = svc.Load();

        Assert.Equal(AppSettings.CurrentSchemaVersion, settings.SchemaVersion);
        Assert.NotNull(settings.MainWindow);
        Assert.NotNull(settings.Player);
        Assert.Equal("https://www.youtube.com/", settings.LastUrl);
        Assert.False(settings.AutoPopout);
        Assert.Null(settings.ActiveProfileName);
        Assert.Equal("cyan", settings.Player.PinAccent);
        Assert.Equal("cyan", settings.Player.FadeAccent);
        Assert.Equal(2500, settings.Player.FadeIdleDelayMs);
        Assert.Equal(1.0, settings.Player.IdleWindowOpacity);
        Assert.Equal(1.0, settings.Player.ConstantWindowOpacity);
        Assert.False(settings.Player.CompactMode);
        Assert.False(settings.Player.FocusedPresentation);
        Assert.False(settings.Player.StripAutoHide);
        Assert.Equal(ThemeCatalog.DefaultThemeId, settings.Theme.ThemeId);
        Assert.Equal(ThemeCatalog.DefaultAccentColor, settings.Theme.AccentColor);
        Assert.Equal(ThemeCatalog.DefaultFadeDelayPreset, settings.Theme.FadeDelayPreset);
        Assert.Null(settings.Theme.StripAutoHide);
        Assert.Null(settings.Theme.ActiveWindowOpacity);
        Assert.Null(settings.Theme.IdleWindowOpacity);
        Assert.Equal(ThemeCatalog.DefaultCornerStyle, settings.Theme.CornerStyle);
    }

    [Fact]
    public void Save_then_load_roundtrips()
    {
        var svc = new SettingsService(_path);
        var settings = new AppSettings
        {
            LastUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
            AutoPopout = true,
            ActiveProfileName = "Lo-fi",
        };
        settings.Player.Topmost = true;
        settings.Player.PinAccent = "green";
        settings.Player.FadeAccent = "amber";
        settings.Player.FadeIdleDelayMs = 4000;
        settings.Player.IdleWindowOpacity = 0.62;
        settings.Player.ConstantWindowOpacity = 0.85;
        settings.Player.CompactMode = true;
        settings.Player.FocusedPresentation = true;
        settings.Player.StripAutoHide = true;
        settings.Profiles.Add(new Profile
        {
            Name = "Lo-fi",
            Url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
            AccentColor = "#A78BFA",
        });
        svc.Save(settings);

        Assert.True(File.Exists(_path));
        var loaded = svc.Load();
        Assert.Equal(settings.LastUrl, loaded.LastUrl);
        Assert.True(loaded.AutoPopout);
        Assert.Equal("Lo-fi", loaded.ActiveProfileName);
        Assert.True(loaded.Player.Topmost);
        Assert.Equal("green", loaded.Player.PinAccent);
        Assert.Equal("amber", loaded.Player.FadeAccent);
        Assert.Equal(4000, loaded.Player.FadeIdleDelayMs);
        Assert.Equal(0.62, loaded.Player.IdleWindowOpacity);
        Assert.Equal(0.85, loaded.Player.ConstantWindowOpacity);
        Assert.True(loaded.Player.CompactMode);
        Assert.True(loaded.Player.FocusedPresentation);
        Assert.True(loaded.Player.StripAutoHide);
        Assert.Single(loaded.Profiles);
        Assert.Equal("Lo-fi", loaded.Profiles[0].Name);
        Assert.Equal("#A78BFA", loaded.Profiles[0].AccentColor);
    }

    [Fact]
    public void Theme_settings_roundtrip_without_dropping_legacy_player_fields()
    {
        var svc = new SettingsService(_path);
        var settings = new AppSettings();
        settings.Player.PinAccent = "green";
        settings.Player.FadeAccent = "amber";
        settings.Player.FadeIdleDelayMs = 4000;
        settings.Theme = new ThemeSettings
        {
            ThemeId = "soft-glass",
            AccentColor = "#a78bfa",
            FadeDelayPreset = "short",
            StripAutoHide = true,
            ActiveWindowOpacity = 0.82,
            IdleWindowOpacity = 0.5,
            CornerStyle = "ROUND",
        };

        svc.Save(settings);
        var loaded = svc.Load();

        Assert.Equal("green", loaded.Player.PinAccent);
        Assert.Equal("amber", loaded.Player.FadeAccent);
        Assert.Equal(4000, loaded.Player.FadeIdleDelayMs);
        Assert.Equal("soft-glass", loaded.Theme.ThemeId);
        Assert.Equal("#A78BFA", loaded.Theme.AccentColor);
        Assert.Equal("short", loaded.Theme.FadeDelayPreset);
        Assert.True(loaded.Theme.StripAutoHide);
        Assert.Equal(0.82, loaded.Theme.ActiveWindowOpacity);
        Assert.Equal(0.5, loaded.Theme.IdleWindowOpacity);
        Assert.Equal("round", loaded.Theme.CornerStyle);   // normalized on save
    }

    /// <summary>
    /// A hand-edited settings.json cannot hand the theme layer an out-of-range accent dial. The
    /// derivation clamps too, so this is defence in depth — it keeps the REPAIRED value in the file
    /// rather than silently re-clamping a junk value on every read.
    /// </summary>
    [Fact]
    public void Load_repairs_an_out_of_range_accent_intensity()
    {
        var svc = new SettingsService(_path);
        var settings = new AppSettings();
        settings.Theme.AccentIntensity = 4000;

        svc.Save(settings);

        Assert.Equal(100, svc.Load().Theme.AccentIntensity);

        settings.Theme.AccentIntensity = -1;
        svc.Save(settings);

        Assert.Equal(0, svc.Load().Theme.AccentIntensity);
    }

    [Fact]
    public void Legacy_settings_without_theme_seed_theme_from_player_preferences()
    {
        File.WriteAllText(_path,
            "{\"schemaVersion\":2,\"player\":{\"pinAccent\":\"green\",\"fadeAccent\":\"amber\",\"fadeIdleDelayMs\":4000," +
            "\"stripAutoHide\":true,\"constantWindowOpacity\":0.82,\"idleWindowOpacity\":0.44}}");
        var svc = new SettingsService(_path);

        var loaded = svc.Load();

        Assert.Equal("#2DB57F", loaded.Theme.AccentColor);   // legacy "green" -> the deepened green
        Assert.Equal("long", loaded.Theme.FadeDelayPreset);
        // The legacy behavior values become EXPLICIT theme overrides at seed time (end-pass
        // review F2): the resolver's preset-default fallback must never change a migrated
        // user's configured look.
        Assert.True(loaded.Theme.StripAutoHide);
        Assert.Equal(0.82, loaded.Theme.ActiveWindowOpacity);
        Assert.Equal(0.44, loaded.Theme.IdleWindowOpacity);
        Assert.Equal(4000, ThemePreferenceResolver.FadeIdleDelayMs(loaded.Theme, loaded.Player));
        Assert.True(ThemePreferenceResolver.StripAutoHide(loaded.Theme, loaded.Player));
        Assert.Equal(0.82, ThemePreferenceResolver.ActiveWindowOpacity(loaded.Theme, loaded.Player));
        Assert.Equal(0.44, ThemePreferenceResolver.IdleWindowOpacity(loaded.Theme, loaded.Player));

        svc.Save(loaded);
        using var doc = JsonDocument.Parse(File.ReadAllText(_path));
        Assert.True(doc.RootElement.TryGetProperty("theme", out _));
    }

    [Fact]
    public void Theme_settings_override_legacy_fields_when_present()
    {
        File.WriteAllText(_path,
            "{\"player\":{\"pinAccent\":\"amber\",\"fadeIdleDelayMs\":4000,\"stripAutoHide\":true," +
            "\"constantWindowOpacity\":0.9,\"idleWindowOpacity\":0.7}," +
            "\"theme\":{\"themeId\":\"minimal\",\"accentColor\":\"4d7ea8\",\"fadeDelayPreset\":\"short\"," +
            "\"stripAutoHide\":false,\"activeWindowOpacity\":0.6,\"idleWindowOpacity\":0.4}}");
        var svc = new SettingsService(_path);

        var loaded = svc.Load();

        Assert.Equal("#4D7EA8", ThemePreferenceResolver.AccentColor(loaded.Theme, loaded.Player));
        Assert.Equal(1500, ThemePreferenceResolver.FadeIdleDelayMs(loaded.Theme, loaded.Player));
        Assert.False(ThemePreferenceResolver.StripAutoHide(loaded.Theme, loaded.Player));
        Assert.Equal(0.6, ThemePreferenceResolver.ActiveWindowOpacity(loaded.Theme, loaded.Player));
        Assert.Equal(0.4, ThemePreferenceResolver.IdleWindowOpacity(loaded.Theme, loaded.Player));
    }

    [Fact]
    public void PascalCase_Theme_block_is_preserved_without_seeding_from_legacy_player()
    {
        File.WriteAllText(_path,
            "{\"schemaVersion\":3,\"Player\":{\"PinAccent\":\"amber\",\"FadeIdleDelayMs\":4000}," +
            "\"Theme\":{\"ThemeId\":\"soft-glass\",\"AccentColor\":\"#A78BFA\",\"FadeDelayPreset\":\"short\"}}");
        var svc = new SettingsService(_path);

        var loaded = svc.Load();

        Assert.Equal("soft-glass", loaded.Theme.ThemeId);
        Assert.Equal("#A78BFA", loaded.Theme.AccentColor);
        Assert.Equal("short", loaded.Theme.FadeDelayPreset);
    }

    [Fact]
    public void Invalid_theme_values_fall_back_to_safe_defaults()
    {
        File.WriteAllText(_path,
            "{\"theme\":{\"themeId\":\"hologram\",\"accentColor\":\"not-a-color\",\"fadeDelayPreset\":\"forever\"," +
            "\"cornerStyle\":\"dodecagon\",\"activeWindowOpacity\":5.0,\"idleWindowOpacity\":-1.0}}");
        var svc = new SettingsService(_path);

        var loaded = svc.Load();

        Assert.Equal(ThemeCatalog.DefaultThemeId, loaded.Theme.ThemeId);
        Assert.Equal(ThemeCatalog.DefaultAccentColor, loaded.Theme.AccentColor);
        Assert.Equal(ThemeCatalog.DefaultFadeDelayPreset, loaded.Theme.FadeDelayPreset);
        Assert.Equal(ThemeCatalog.DefaultCornerStyle, loaded.Theme.CornerStyle);
        // A schema-LESS hand-made file deserializes at the current schema (the C# initializer),
        // so the invalid opacities null out and resolve to preset defaults — only files that
        // explicitly say schemaVersion ≤ 2 (every real pre-upgrade PiPlay file does) get the
        // legacy backfill below.
        Assert.Null(loaded.Theme.ActiveWindowOpacity);
        Assert.Null(loaded.Theme.IdleWindowOpacity);
    }

    [Fact]
    public void Schema2_theme_nulls_backfill_from_legacy_player_values()
    {
        // A PR #18-era (schema 2) file: theme block exists but its null behavior values meant
        // "use the Player fields". The schema-3 upgrade must pin the OLD effective look as
        // explicit overrides — never let preset defaults change a configured window.
        File.WriteAllText(_path,
            "{\"schemaVersion\":2,\"player\":{\"constantWindowOpacity\":0.8,\"idleWindowOpacity\":0.7," +
            "\"stripAutoHide\":false},\"theme\":{\"themeId\":\"soft-glass\",\"accentColor\":\"#A78BFA\"}}");
        var svc = new SettingsService(_path);

        var loaded = svc.Load();

        Assert.Equal(0.8, loaded.Theme.ActiveWindowOpacity);
        Assert.Equal(0.7, loaded.Theme.IdleWindowOpacity);
        Assert.False(loaded.Theme.StripAutoHide);
        Assert.Equal(0.8, ThemePreferenceResolver.ActiveWindowOpacity(loaded.Theme, loaded.Player));
        Assert.False(ThemePreferenceResolver.StripAutoHide(loaded.Theme, loaded.Player));
        Assert.Equal(AppSettings.CurrentSchemaVersion, loaded.SchemaVersion);
    }

    [Fact]
    public void Schema3_theme_nulls_resolve_to_preset_defaults()
    {
        // Same content at schema 3: nulls are deliberate "follow the preset" values — a
        // hand-edited soft-glass file is translucent without manual slider work (review F2).
        File.WriteAllText(_path,
            "{\"schemaVersion\":3,\"player\":{\"constantWindowOpacity\":0.8,\"idleWindowOpacity\":0.7," +
            "\"stripAutoHide\":false},\"theme\":{\"themeId\":\"soft-glass\",\"accentColor\":\"#A78BFA\"}}");
        var svc = new SettingsService(_path);

        var loaded = svc.Load();

        Assert.Null(loaded.Theme.ActiveWindowOpacity);
        Assert.Null(loaded.Theme.IdleWindowOpacity);
        Assert.Null(loaded.Theme.StripAutoHide);
        var preset = ThemeCatalog.PresetFor("soft-glass");
        Assert.Equal(preset.DefaultActiveWindowOpacity, ThemePreferenceResolver.ActiveWindowOpacity(loaded.Theme, loaded.Player));
        Assert.Equal(preset.DefaultIdleWindowOpacity, ThemePreferenceResolver.IdleWindowOpacity(loaded.Theme, loaded.Player));
        Assert.True(ThemePreferenceResolver.StripAutoHide(loaded.Theme, loaded.Player));
    }

    [Fact]
    public void Unknown_settings_json_is_preserved_across_save()
    {
        File.WriteAllText(_path,
            "{\"schemaVersion\":2,\"futureRoot\":{\"enabled\":true},\"player\":{\"pinAccent\":\"cyan\",\"futurePlayer\":42}," +
            "\"theme\":{\"themeId\":\"sharp-dark\",\"accentColor\":\"#2D6F8F\",\"futureTheme\":\"kept\"}}");
        var svc = new SettingsService(_path);

        var loaded = svc.Load();
        svc.Save(loaded);

        using var doc = JsonDocument.Parse(File.ReadAllText(_path));
        Assert.True(doc.RootElement.TryGetProperty("futureRoot", out var root));
        Assert.True(root.GetProperty("enabled").GetBoolean());
        Assert.Equal(42, doc.RootElement.GetProperty("player").GetProperty("futurePlayer").GetInt32());
        Assert.Equal("kept", doc.RootElement.GetProperty("theme").GetProperty("futureTheme").GetString());
    }

    [Fact]
    public void Corrupt_file_is_quarantined_and_defaults_returned()
    {
        File.WriteAllText(_path, "{ this is not valid json ]]]");
        var svc = new SettingsService(_path);

        var settings = svc.Load();

        Assert.Equal(AppSettings.CurrentSchemaVersion, settings.SchemaVersion);
        // The bad file was renamed aside, not left in place to break the next load.
        var quarantined = Directory.GetFiles(_dir, "*.corrupt.*.json");
        Assert.Single(quarantined);
    }

    [Fact]
    public void Sanitize_repairs_out_of_range_values()
    {
        File.WriteAllText(_path,
            "{\"schemaVersion\":0,\"lastUrl\":\"\",\"player\":{\"pinAccent\":\"hotpink\",\"fadeAccent\":\"\",\"fadeIdleDelayMs\":777,\"idleWindowOpacity\":5.0,\"constantWindowOpacity\":-0.5,\"lastWidth\":10,\"lastHeight\":10}}");
        var svc = new SettingsService(_path);

        var settings = svc.Load();

        Assert.Equal(AppSettings.CurrentSchemaVersion, settings.SchemaVersion);
        Assert.Equal("https://www.youtube.com/", settings.LastUrl);
        Assert.Equal("cyan", settings.Player.PinAccent);
        Assert.Equal("cyan", settings.Player.FadeAccent);
        Assert.Equal(2500, settings.Player.FadeIdleDelayMs);
        Assert.Equal(1.0, settings.Player.IdleWindowOpacity);
        Assert.Equal(1.0, settings.Player.ConstantWindowOpacity);
        Assert.Equal(960, settings.Player.LastWidth);
        Assert.Equal(540, settings.Player.LastHeight);
    }

    [Fact]
    public void Hand_edited_opacity_below_the_ui_floor_is_honored()
    {
        // Spec 7.3 explicit unlock: the Settings sliders stop at 0.45, but a hand-edited
        // settings.json may go down to the 0.10 file floor and Sanitize must NOT repair it.
        File.WriteAllText(_path,
            "{\"player\":{\"idleWindowOpacity\":0.25,\"constantWindowOpacity\":0.10}}");
        var svc = new SettingsService(_path);

        var loaded = svc.Load();

        Assert.Equal(0.25, loaded.Player.IdleWindowOpacity);
        Assert.Equal(0.10, loaded.Player.ConstantWindowOpacity);
    }

    [Fact]
    public void Profiles_and_active_profile_are_sanitized_on_load()
    {
        File.WriteAllText(_path,
            "{\"activeProfileName\":\"Ghost\",\"profiles\":[" +
            "{\"name\":\"   \",\"url\":\"https://www.youtube.com/watch?v=dQw4w9WgXcQ\"}," +
            "{\"name\":\"Legacy\",\"url\":\"https://www.youtube.com/watch?v=dQw4w9WgXcQ\",\"mode\":\"embed\",\"presentation\":\" STANDARD \",\"accentColor\":\"not-a-color\"}," +
            "{\"name\":\"Bogus\",\"url\":\"https://www.youtube.com/watch?v=dQw4w9WgXcQ\",\"mode\":\"hologram\",\"presentation\":\"compact\"}," +
            "{\"name\":\"Keep\",\"url\":\"https://www.youtube.com/watch?v=dQw4w9WgXcQ\",\"mode\":\"compact\",\"presentation\":\"Focused\",\"accentColor\":\"#787878\"}," +
            "{\"name\":\"Blank\",\"url\":\"https://www.youtube.com/watch?v=dQw4w9WgXcQ\",\"mode\":\"\",\"presentation\":\"\"}]}");
        var svc = new SettingsService(_path);

        var loaded = svc.Load();

        Assert.Null(loaded.ActiveProfileName);
        Assert.Equal(4, loaded.Profiles.Count);
        Assert.DoesNotContain(loaded.Profiles, p => string.IsNullOrWhiteSpace(p.Name));

        var legacy = loaded.Profiles.Single(p => p.Name == "Legacy");
        Assert.Equal("compact", legacy.Mode);
        Assert.Equal("standard", legacy.Presentation);
        Assert.Null(legacy.AccentColor);

        var bogus = loaded.Profiles.Single(p => p.Name == "Bogus");
        Assert.Null(bogus.Mode);
        Assert.Null(bogus.Presentation);

        var keep = loaded.Profiles.Single(p => p.Name == "Keep");
        Assert.Equal("compact", keep.Mode);
        Assert.Equal("focused", keep.Presentation);
        Assert.Equal("#787878", keep.AccentColor);

        var blank = loaded.Profiles.Single(p => p.Name == "Blank");
        Assert.Null(blank.Mode);
        Assert.Null(blank.Presentation);
    }

    [Fact]
    public void Reset_recreates_defaults_removes_quarantines_and_preserves_logs_and_webview_data()
    {
        var svc = new SettingsService(_path);
        var populated = new AppSettings { LastUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ" };
        populated.MainWindow.Topmost = true;
        populated.Profiles.Add(new Profile { Name = "Lo-fi", Url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ" });
        svc.Save(populated);

        // Stale corrupt quarantine file to be cleaned up.
        var corrupt = Path.Combine(_dir, "settings.json.corrupt.20200101-000000.json");
        File.WriteAllText(corrupt, "{}");

        // The sacred invariant: reset never touches the browser session (login) or logs.
        var logsDir = Path.Combine(_dir, "logs");
        var webViewDir = Path.Combine(_dir, "WebView2UserData");
        Directory.CreateDirectory(logsDir);
        Directory.CreateDirectory(webViewDir);
        var logFile = Path.Combine(logsDir, "piplay.log");
        var sessionFile = Path.Combine(webViewDir, "session-cookie");
        File.WriteAllText(logFile, "log line");
        File.WriteAllText(sessionFile, "logged-in");

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

        // Quarantine cleaned up, sibling logs and WebView2 session preserved.
        Assert.False(File.Exists(corrupt), "Reset should clean up stale quarantines for a clean slate.");
        Assert.True(File.Exists(logFile), "Reset must not delete logs.");
        Assert.True(File.Exists(sessionFile), "Reset must not delete the WebView2 session (login).");
        Assert.Equal("logged-in", File.ReadAllText(sessionFile));
    }
}
