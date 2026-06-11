using System.Windows;
using System.Windows.Media;
using PiPlay.Models;

namespace PiPlay.Theme;

/// <summary>
/// Applies the resolved theme accent to the app's shared resource brushes at startup (overhaul
/// Task 9). The window XAML uses <c>StaticResource</c> throughout, which freezes the lookup at parse
/// time — but a <see cref="SolidColorBrush"/> resource is a single shared, MUTABLE instance, so
/// rewriting its <see cref="SolidColorBrush.Color"/> reaches every consumer that already resolved it
/// (the app-level styles) AND every window parsed afterward. Call this BEFORE constructing the first
/// window. Application this pass is startup/next-window only; live theme switching of open windows
/// would need a <c>StaticResource</c>→<c>DynamicResource</c> migration (deferred — design §"Unresolved").
/// </summary>
public static class ThemeResourceApplier
{
    /// <summary>The lighter hover derivation blends the accent this far toward white.</summary>
    public const double HoverLightenAmount = 0.30;

    public static void Apply(ResourceDictionary resources, ThemeSettings? theme, PlayerSettings player)
    {
        var accentHex = ThemePreferenceResolver.AccentColor(theme, player);
        var accent = ThemeColors.ParseColor(accentHex);
        var accentLight = ThemeColors.Lighten(accent, HoverLightenAmount);

        SetBrushColor(resources, "AccentPrimary", accent);
        SetBrushColor(resources, "AccentPrimaryLight", accentLight);
    }

    // Mutate the shared brush in place; leave the (parse-time) Color tokens alone — they only seeded
    // the brushes. If the key is missing or not a mutable SolidColorBrush, skip silently: the default
    // (cyan) resource still renders, so a resource rename can never crash startup.
    private static void SetBrushColor(ResourceDictionary resources, string key, Color color)
    {
        if (resources[key] is SolidColorBrush { IsFrozen: false } brush)
            brush.Color = color;
    }
}
