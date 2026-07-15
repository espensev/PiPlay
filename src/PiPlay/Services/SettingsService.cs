using System.IO;
using System.Text.Json;
using PiPlay.Models;
using PiPlay.Theme;

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
            using var document = JsonDocument.Parse(json);
            var seedThemeFromLegacy = !HasThemeBlock(document.RootElement);
            var settings = document.RootElement.Deserialize<AppSettings>(Options);
            if (settings is null)
            {
                Log.Warn("Settings deserialized to null; quarantining and using defaults.");
                Quarantine();
                return Sanitize(new AppSettings());
            }

            return Sanitize(settings, seedThemeFromLegacy);
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

            // Drop any orphaned temp left by a crashed AtomicWrite (harmless, but a clean slate).
            var tmp = _path + ".tmp";
            if (File.Exists(tmp)) { try { File.Delete(tmp); } catch { /* best-effort cleanup */ } }

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
    /// <see cref="Save"/> and <see cref="Reset"/>. DO NOT use File.Copy or a direct overwrite — a
    /// crash mid-write would leave settings.json half-written. The live file is always either the
    /// previous content or the new content — never absent or partial.
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
    private static AppSettings Sanitize(AppSettings s, bool seedThemeFromLegacy = false)
    {
        s.MainWindow ??= new WindowSettings();
        s.Player ??= new PlayerSettings();
        s.Theme ??= new ThemeSettings();
        s.Profiles ??= new List<Profile>();

        if (string.IsNullOrWhiteSpace(s.LastUrl)) s.LastUrl = "https://www.youtube.com/";
        if (string.IsNullOrWhiteSpace(s.ActiveProfileName)) s.ActiveProfileName = null;
        s.Player.PinAccent = PlayerAppearancePolicy.NormalizeAccent(s.Player.PinAccent);
        s.Player.FadeAccent = PlayerAppearancePolicy.NormalizeAccent(s.Player.FadeAccent);
        s.Player.FadeIdleDelayMs = PlayerAppearancePolicy.NormalizeFadeIdleDelayMs(s.Player.FadeIdleDelayMs);
        s.Player.IdleWindowOpacity = WindowOpacityPolicy.Normalize(s.Player.IdleWindowOpacity);
        s.Player.ConstantWindowOpacity = WindowOpacityPolicy.Normalize(s.Player.ConstantWindowOpacity);
        if (seedThemeFromLegacy)
            s.Theme = ThemeSettings.FromLegacy(s.Player);
        s.Theme.ThemeId = ThemeCatalog.NormalizeThemeId(s.Theme.ThemeId);
        s.Theme.AccentColor = ThemeCatalog.NormalizeAccentColor(s.Theme.AccentColor);
        s.Theme.FadeDelayPreset = ThemeCatalog.NormalizeFadeDelayPreset(s.Theme.FadeDelayPreset);
        s.Theme.CornerStyle = ThemeCatalog.NormalizeCornerStyle(s.Theme.CornerStyle);
        s.Theme.AccentIntensity = ThemeCatalog.NormalizeAccentIntensity(s.Theme.AccentIntensity);
        s.Theme.ActiveWindowOpacity = NormalizeOptionalOpacity(s.Theme.ActiveWindowOpacity);
        s.Theme.IdleWindowOpacity = NormalizeOptionalOpacity(s.Theme.IdleWindowOpacity);
        // Schema ≤2 semantic switch (end-pass review F2): those files' null theme behavior
        // values meant "use the legacy Player fields", so backfill them as explicit overrides
        // once — under schema 3 a null means "use the preset default", and a configured look
        // must never change across the upgrade. Runs after the opacity normalization so an
        // invalid old override also lands on the user's previous effective value.
        if (s.SchemaVersion < 3)
        {
            s.Theme.StripAutoHide ??= s.Player.StripAutoHide;
            s.Theme.ActiveWindowOpacity ??= s.Player.ConstantWindowOpacity;
            s.Theme.IdleWindowOpacity ??= s.Player.IdleWindowOpacity;
        }
        if (s.Player.LastWidth < 320) s.Player.LastWidth = 960;
        if (s.Player.LastHeight < 180) s.Player.LastHeight = 540;
        if (s.SchemaVersion < AppSettings.CurrentSchemaVersion) s.SchemaVersion = AppSettings.CurrentSchemaVersion;

        s.Profiles.RemoveAll(p => p is null || string.IsNullOrWhiteSpace(p.Name));
        // Repair the per-profile playback mode to the durable vocabulary (null/normal/compact),
        // folding the legacy "embed" alias to "compact" and unknown values to null (Phase 3).
        foreach (var p in s.Profiles)
        {
            p.Mode = PlaybackModePolicy.NormalizeProfileMode(p.Mode);
            p.Presentation = PopoutPresentationPolicy.NormalizeProfilePresentation(p.Presentation);
            p.AccentColor = ProfileService.NormalizeAccentForStorage(p.AccentColor);
        }
        ProfileAccentService.ReconcileActiveProfile(s);
        return s;
    }

    private static bool HasThemeBlock(JsonElement root)
    {
        return root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("theme", out var theme)
            && theme.ValueKind != JsonValueKind.Null;
    }

    private static double? NormalizeOptionalOpacity(double? value) =>
        WindowOpacityPolicy.NormalizeOptional(value);   // one repair rule with the Settings-apply writer
}
