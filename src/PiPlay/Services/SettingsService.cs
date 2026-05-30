using System.IO;
using System.Text.Json;
using PiPlay.Models;

namespace PiPlay.Services;

/// <summary>
/// Loads/saves <see cref="AppSettings"/> with atomic writes and corruption recovery
/// (spec 12.6, 26.4). Never loses settings to a partial write; a corrupt file is
/// quarantined and defaults are used.
/// </summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly string _path;

    public SettingsService(string? path = null) => _path = path ?? AppPaths.SettingsFile;

    public AppSettings Load()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            CleanupOldCorruptFiles();

            if (!File.Exists(_path))
            {
                Log.Info("Settings file not found; starting with defaults.");
                return Sanitize(new AppSettings());
            }

            var json = File.ReadAllText(_path);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, Options);
            if (settings is null)
            {
                Log.Warn("Settings deserialized to null; quarantining and using defaults.");
                Quarantine();
                return Sanitize(new AppSettings());
            }

            return Sanitize(settings);
        }
        catch (Exception ex)
        {
            Log.Error("Failed to load settings; quarantining and using defaults.", ex);
            Quarantine();
            return Sanitize(new AppSettings());
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Sanitize(settings);
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);

            // Atomic save (spec 26.4): write a temp file, flush it durably to disk, then
            // swap it in with an atomic same-volume rename. DO NOT use File.Copy or a
            // direct overwrite - a crash mid-write would leave settings.json half-written.
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
        catch (Exception ex)
        {
            Log.Error("Failed to save settings.", ex);
        }
    }

    private void Quarantine()
    {
        try
        {
            if (File.Exists(_path))
            {
                var dest = $"{_path}.corrupt.{DateTime.Now:yyyyMMdd-HHmmss}.json";
                File.Move(_path, dest);
                Log.Warn($"Quarantined corrupt settings to {Path.GetFileName(dest)}.");
            }
        }
        catch (Exception ex)
        {
            Log.Error("Failed to quarantine corrupt settings.", ex);
        }
    }

    private void CleanupOldCorruptFiles()
    {
        try
        {
            var dir = Path.GetDirectoryName(_path)!;
            foreach (var f in Directory.EnumerateFiles(dir, "*.corrupt.*.json"))
            {
                if (File.GetLastWriteTime(f) < DateTime.Now.AddDays(-30))
                    File.Delete(f);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }

    /// <summary>Repair nulls and out-of-range values so the rest of the app can trust the model.</summary>
    private static AppSettings Sanitize(AppSettings s)
    {
        s.MainWindow ??= new WindowSettings();
        s.Player ??= new PlayerSettings();
        s.Profiles ??= new List<Profile>();

        if (string.IsNullOrWhiteSpace(s.LastUrl)) s.LastUrl = "https://www.youtube.com/";
        if (s.Player.IdleWindowOpacity is < 0.1 or > 1.0) s.Player.IdleWindowOpacity = 1.0;
        if (s.Player.LastWidth < 320) s.Player.LastWidth = 960;
        if (s.Player.LastHeight < 180) s.Player.LastHeight = 540;
        if (s.SchemaVersion <= 0) s.SchemaVersion = AppSettings.CurrentSchemaVersion;

        s.Profiles.RemoveAll(p => p is null || string.IsNullOrWhiteSpace(p.Name));
        return s;
    }
}
