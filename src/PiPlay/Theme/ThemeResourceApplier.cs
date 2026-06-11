using System.Windows;
using System.Windows.Media;
using PiPlay.Models;

namespace PiPlay.Theme;

/// <summary>
/// Applies the resolved theme accent to the app's shared accent resources (overhaul Task 9). The
/// accent consumers (AccentButton fill/hover, URL caret/selection/focus, the Pin toggle default, the
/// Settings preset chips) reference <c>AccentPrimary</c>/<c>AccentPrimaryLight</c> via
/// <c>DynamicResource</c>, so REPLACING those entries re-resolves every consumer — including controls
/// already realized in open windows. (Compiled BAML freezes the seed brushes, so mutating them in
/// place is a no-op; replacing the dictionary entry is the mechanism that actually works.) Call at
/// <c>App.OnStartup</c> for the persisted accent and again whenever the accent changes for a live
/// recolor.
/// </summary>
public static class ThemeResourceApplier
{
    /// <summary>The lighter hover derivation blends the accent this far toward white.</summary>
    public const double HoverLightenAmount = 0.30;

    public static void Apply(ResourceDictionary resources, ThemeSettings? theme, PlayerSettings player)
    {
        var accent = ThemeColors.ParseColor(ThemePreferenceResolver.AccentColor(theme, player));
        resources["AccentPrimary"] = Frozen(accent);
        resources["AccentPrimaryLight"] = Frozen(ThemeColors.Lighten(accent, HoverLightenAmount));
    }

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();   // shareable + immutable; DynamicResource consumers hold it until the next Apply
        return brush;
    }
}
