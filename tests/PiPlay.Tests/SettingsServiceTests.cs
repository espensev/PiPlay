using System.IO;
using PiPlay.Models;
using PiPlay.Services;

namespace PiPlay.Tests;

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
    }

    [Fact]
    public void Save_then_load_roundtrips()
    {
        var svc = new SettingsService(_path);
        var settings = new AppSettings { LastUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ" };
        settings.Player.Topmost = true;
        settings.Profiles.Add(new Profile { Name = "Lo-fi", Url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ" });
        svc.Save(settings);

        Assert.True(File.Exists(_path));
        var loaded = svc.Load();
        Assert.Equal(settings.LastUrl, loaded.LastUrl);
        Assert.True(loaded.Player.Topmost);
        Assert.Single(loaded.Profiles);
        Assert.Equal("Lo-fi", loaded.Profiles[0].Name);
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
            "{\"schemaVersion\":0,\"lastUrl\":\"\",\"player\":{\"idleWindowOpacity\":5.0,\"lastWidth\":10,\"lastHeight\":10}}");
        var svc = new SettingsService(_path);

        var settings = svc.Load();

        Assert.Equal(AppSettings.CurrentSchemaVersion, settings.SchemaVersion);
        Assert.Equal("https://www.youtube.com/", settings.LastUrl);
        Assert.Equal(1.0, settings.Player.IdleWindowOpacity);
        Assert.Equal(960, settings.Player.LastWidth);
        Assert.Equal(540, settings.Player.LastHeight);
    }
}
