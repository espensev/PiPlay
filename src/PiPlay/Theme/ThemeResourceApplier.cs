using System.Windows;
using System.Windows.Media;
using PiPlay.Models;

namespace PiPlay.Theme;

/// <summary>
/// Applies the resolved theme (accent + per-preset palette + corner radii) to the app's shared
/// resources. Every theme-controlled consumer references these keys via <c>DynamicResource</c>, so
/// REPLACING the dictionary entries re-resolves every consumer — including controls already
/// realized in open windows. (Compiled BAML freezes the seed brushes, so mutating them in place is
/// a no-op; replacing the dictionary entry is the mechanism that actually works — overhaul Task 9.)
/// Call at <c>App.OnStartup</c> for the persisted theme and again whenever it changes for a live
/// restyle. Native window corners are NOT applied here — windows apply
/// <see cref="ThemePreferenceResolver.DwmCorners"/> to their own HWND.
/// </summary>
public static class ThemeResourceApplier
{
    /// <summary>The lighter hover derivation blends the accent this far toward white.</summary>
    public const double HoverLightenAmount = 0.30;

    /// <summary>
    /// The native corner preference of the last applied theme, for windows built outside the
    /// settings flow (the Prompt dialogs) — they have no ThemeSettings at hand but must wear the
    /// same corner shape as their owners. Updated by every <see cref="Apply"/>.
    /// </summary>
    public static DwmCornerMode CurrentDwmCorners { get; private set; } = DwmCornerMode.Default;

    /// <summary>The palette brush keys this applier owns, in palette order. Shared with tests so
    /// the applied-key set and the catalog palette cannot drift.</summary>
    public static readonly string[] PaletteBrushKeys =
    [
        "AppBackground", "SurfaceBase", "SurfaceRaised", "SurfaceHover",
        "BorderSubtle", "BorderStrong", "TextPrimary", "TextSecondary", "DangerPin",
    ];

    public static void Apply(ResourceDictionary resources, ThemeSettings? theme, PlayerSettings player)
    {
        var accent = ThemeColors.ParseColor(ThemePreferenceResolver.AccentColor(theme, player));
        resources["AccentPrimary"] = Frozen(accent);
        resources["AccentPrimaryLight"] = Frozen(ThemeColors.Lighten(accent, HoverLightenAmount));

        var preset = ThemeCatalog.PresetFor(theme?.ThemeId);
        ApplyPalette(resources, preset.Palette);
        ApplyRadii(resources, ThemeCatalog.RadiiFor(preset, theme?.CornerStyle));
        CurrentDwmCorners = ThemeCatalog.DwmCornersFor(preset, theme?.CornerStyle);
    }

    private static void ApplyPalette(ResourceDictionary resources, ThemePalette palette)
    {
        string[] values =
        [
            palette.AppBackground, palette.SurfaceBase, palette.SurfaceRaised, palette.SurfaceHover,
            palette.BorderSubtle, palette.BorderStrong, palette.TextPrimary, palette.TextSecondary,
            palette.Danger,
        ];
        for (var i = 0; i < PaletteBrushKeys.Length; i++)
        {
            var color = ThemeColors.ParseColor(values[i]);
            resources[PaletteBrushKeys[i]] = Frozen(color);
            // Keep the companion Color token in step for any direct color consumer.
            resources[PaletteBrushKeys[i] + "Color"] = color;
        }
    }

    private static void ApplyRadii(ResourceDictionary resources, ThemeRadii radii)
    {
        resources["RadiusMainWindowFrame"] = new CornerRadius(radii.MainWindowFrame);
        resources["RadiusPopoutFrame"] = new CornerRadius(radii.PopoutFrame);
        // Title bars round only their top corners so they sit flush on the content below.
        resources["RadiusTitleBar"] = new CornerRadius(radii.TitleBar, radii.TitleBar, 0, 0);
        resources["RadiusButton"] = new CornerRadius(radii.Button);
        resources["RadiusIconButton"] = new CornerRadius(radii.IconButton);
        resources["RadiusInput"] = new CornerRadius(radii.Input);
        resources["RadiusPanel"] = new CornerRadius(radii.Panel);
        resources["RadiusPopup"] = new CornerRadius(radii.Popup);
        resources["RadiusThumbnail"] = new CornerRadius(radii.Thumbnail);
        resources["RadiusSwatch"] = new CornerRadius(radii.Swatch);
        resources["RadiusScrollbarThumb"] = new CornerRadius(radii.ScrollbarThumb);
        resources["RadiusToolTip"] = new CornerRadius(radii.ToolTip);
        // Compatibility aliases (review doc §8.4) for one migration pass; kept as separate
        // entries, not resource-to-resource references.
        resources["ControlCornerRadius"] = new CornerRadius(radii.Input);
        resources["ButtonCornerRadius"] = new CornerRadius(radii.Button);
    }

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();   // shareable + immutable; DynamicResource consumers hold it until the next Apply
        return brush;
    }
}
