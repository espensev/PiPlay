using System.Windows;
using System.Windows.Media;
using PiPlay.Models;
using PiPlay.Services;

namespace PiPlay.Theme;

/// <summary>
/// Applies the resolved theme onto the application resource tokens at startup, BEFORE any
/// window is constructed (UI overhaul Task 9). Window XAML is StaticResource-only, so token
/// values must be final before the first window parses. WPF FREEZES Freezable values served
/// from Application-owned dictionaries, so mutating the parsed brushes in place is impossible —
/// instead each token ENTRY is replaced in EVERY dictionary that defines it: App.xaml merges
/// Colors.xaml twice (directly, and nested inside ControlStyles.xaml), and a deferred style
/// resolves StaticResource against its own dictionary scope, so a root-only replacement would
/// leave later-inflated styles on stale values. Replacement runs before any style or window
/// inflates, so every consumer resolves the themed value. Live re-theming of already-inflated
/// styles remains the deferred StaticResource-to-DynamicResource migration (Task 10+).
/// Never throws: theming must not be able to break startup.
/// </summary>
public static class ThemeResourceApplier
{
    public static void Apply(ResourceDictionary resources, AppSettings settings)
    {
        try
        {
            var palette = AccentPalette.Derive(
                ThemePreferenceResolver.AccentColor(settings.Theme, settings.Player));

            SetColorToken(resources, "Theme.Accent", palette.Accent);
            SetColorToken(resources, "Theme.AccentHover", palette.Hover);
            SetColorToken(resources, "Theme.AccentPressed", palette.Pressed);
            SetColorToken(resources, "Theme.AccentDim", palette.Dim);
            SetColorToken(resources, "Theme.AccentBorder", palette.Border);
            SetColorToken(resources, "Theme.AccentForeground", palette.Foreground);

            SetToken(resources, "Theme.ActiveWindowOpacity",
                ThemePreferenceResolver.ActiveWindowOpacity(settings.Theme, settings.Player));
            SetToken(resources, "Theme.IdleWindowOpacity",
                ThemePreferenceResolver.IdleWindowOpacity(settings.Theme, settings.Player));
            SetToken(resources, "Theme.FadeIdleDelayMs",
                ThemePreferenceResolver.FadeIdleDelayMs(settings.Theme, settings.Player));

            Log.Info($"Theme applied: {ThemeCatalog.NormalizeThemeId(settings.Theme?.ThemeId)} accent {palette.Accent}.");
        }
        catch (Exception ex)
        {
            Log.Error("Failed to apply theme resources; keeping XAML defaults.", ex);
        }
    }

    private static void SetColorToken(ResourceDictionary resources, string baseKey, Color color)
    {
        // Freeze deliberately: app-owned dictionaries freeze served Freezables anyway, and a
        // frozen brush is cheaper for the renderer.
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        SetToken(resources, baseKey + "Color", color);
        SetToken(resources, baseKey, brush);
    }

    private static void SetToken(ResourceDictionary resources, string key, object value)
    {
        // A dictionary that never defined the token (defensive) still gets a root entry.
        if (!ReplaceEverywhere(resources, key, value))
            resources[key] = value;
    }

    /// <summary>
    /// Replaces <paramref name="key"/> in every (nested merged) dictionary defining it, so a
    /// deferred style that resolves StaticResource against its own dictionary scope inflates
    /// the themed value rather than the stale XAML default.
    /// </summary>
    private static bool ReplaceEverywhere(ResourceDictionary dict, string key, object value)
    {
        var found = false;
        foreach (var merged in dict.MergedDictionaries)
            found |= ReplaceEverywhere(merged, key, value);
        if (dict.Contains(key))
        {
            dict[key] = value;
            found = true;
        }
        return found;
    }
}
