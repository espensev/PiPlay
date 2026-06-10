using System.IO;
using PiPlay.Models;
using PiPlay.Services;

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
        Assert.Equal("cyan", settings.Player.PinAccent);
        Assert.Equal("cyan", settings.Player.FadeAccent);
        Assert.Equal(2500, settings.Player.FadeIdleDelayMs);
    }

    [Fact]
    public void Save_then_load_roundtrips()
    {
        var svc = new SettingsService(_path);
        var settings = new AppSettings { LastUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ" };
        settings.Player.Topmost = true;
        settings.Player.PinAccent = "green";
        settings.Player.FadeAccent = "amber";
        settings.Player.FadeIdleDelayMs = 4000;
        settings.Profiles.Add(new Profile { Name = "Lo-fi", Url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ" });
        svc.Save(settings);

        Assert.True(File.Exists(_path));
        var loaded = svc.Load();
        Assert.Equal(settings.LastUrl, loaded.LastUrl);
        Assert.True(loaded.Player.Topmost);
        Assert.Equal("green", loaded.Player.PinAccent);
        Assert.Equal("amber", loaded.Player.FadeAccent);
        Assert.Equal(4000, loaded.Player.FadeIdleDelayMs);
        Assert.Single(loaded.Profiles);
        Assert.Equal("Lo-fi", loaded.Profiles[0].Name);
    }

    [Fact]
    public void AutoPopout_defaults_off_and_roundtrips()
    {
        var svc = new SettingsService(_path);
        Assert.False(svc.Load().AutoPopout);   // off by default (and for a missing property)

        svc.Save(new AppSettings { AutoPopout = true });
        Assert.True(svc.Load().AutoPopout);     // survives the atomic round trip
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
    public void Window_opacity_defaults_are_opaque_and_roundtrip()
    {
        var svc = new SettingsService(_path);
        var defaults = svc.Load();
        // Missing properties deserialize to 1.0, so every existing settings.json keeps today's look.
        Assert.Equal(1.0, defaults.Player.IdleWindowOpacity);
        Assert.Equal(1.0, defaults.Player.ConstantWindowOpacity);

        var s = new AppSettings();
        s.Player.IdleWindowOpacity = 0.62;
        s.Player.ConstantWindowOpacity = 0.85;
        svc.Save(s);
        var loaded = svc.Load();
        Assert.Equal(0.62, loaded.Player.IdleWindowOpacity);
        Assert.Equal(0.85, loaded.Player.ConstantWindowOpacity);
    }

    [Fact]
    public void Settings_file_without_opacity_keys_loads_as_opaque()
    {
        // The migration wire shape: every pre-Phase-4 settings.json has a player object but no
        // opacity keys. Deserialization (not just the C# initializer) must yield 1.0 for both.
        File.WriteAllText(_path, "{\"schemaVersion\":2,\"player\":{\"pinAccent\":\"cyan\",\"compactMode\":true}}");
        var svc = new SettingsService(_path);

        var loaded = svc.Load();

        Assert.Equal(1.0, loaded.Player.IdleWindowOpacity);
        Assert.Equal(1.0, loaded.Player.ConstantWindowOpacity);
        Assert.True(loaded.Player.CompactMode);   // proves the player object actually deserialized
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
    public void Profile_mode_is_normalized_on_load()
    {
        // Legacy "embed" -> durable "compact"; unknown -> null (use global); "compact"/"normal" kept;
        // an empty string is treated as unset (null). Proves SettingsService.Sanitize repairs modes.
        File.WriteAllText(_path,
            "{\"profiles\":[" +
            "{\"name\":\"Legacy\",\"url\":\"https://www.youtube.com/watch?v=dQw4w9WgXcQ\",\"mode\":\"embed\"}," +
            "{\"name\":\"Bogus\",\"url\":\"https://www.youtube.com/watch?v=dQw4w9WgXcQ\",\"mode\":\"hologram\"}," +
            "{\"name\":\"Keep\",\"url\":\"https://www.youtube.com/watch?v=dQw4w9WgXcQ\",\"mode\":\"compact\"}," +
            "{\"name\":\"Blank\",\"url\":\"https://www.youtube.com/watch?v=dQw4w9WgXcQ\",\"mode\":\"\"}]}");
        var svc = new SettingsService(_path);

        var loaded = svc.Load();

        Assert.Equal("compact", loaded.Profiles.Single(p => p.Name == "Legacy").Mode);
        Assert.Null(loaded.Profiles.Single(p => p.Name == "Bogus").Mode);
        Assert.Equal("compact", loaded.Profiles.Single(p => p.Name == "Keep").Mode);
        Assert.Null(loaded.Profiles.Single(p => p.Name == "Blank").Mode);
    }

    [Fact]
    public void Compact_mode_global_default_is_off_and_roundtrips()
    {
        var svc = new SettingsService(_path);
        Assert.False(svc.Load().Player.CompactMode);   // off by default (and for a missing property)

        var s = new AppSettings();
        s.Player.CompactMode = true;
        svc.Save(s);
        Assert.True(svc.Load().Player.CompactMode);
    }

    [Fact]
    public void Strip_auto_hide_default_is_off_and_roundtrips()
    {
        var svc = new SettingsService(_path);
        // Off by default AND for a missing property, so every pre-Phase-4 settings.json keeps
        // the always-reserved strip row (spec 7.2 / acceptance criterion 1).
        Assert.False(svc.Load().Player.StripAutoHide);

        var s = new AppSettings();
        s.Player.StripAutoHide = true;
        svc.Save(s);
        Assert.True(svc.Load().Player.StripAutoHide);
    }

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
}
