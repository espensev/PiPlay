using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
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
        var preset = ThemeCatalog.PresetFor(theme?.ThemeId);
        ApplyAccentOnly(resources, ThemePreferenceResolver.AccentColor(theme, player), preset);
        ApplyPalette(resources, preset.Palette);
        ApplyRadii(resources, ThemeCatalog.RadiiFor(preset, theme?.CornerStyle));
        ApplyDensity(resources, preset.Density);
        ApplyElevation(resources, preset.Elevation);
        CurrentDwmCorners = ThemeCatalog.DwmCornersFor(preset, theme?.CornerStyle);
    }

    /// <summary>
    /// Derive the accent state set for the resolved (accent x theme) and replace every accent token
    /// AND its companion <c>*Color</c> entry (review BL-09 — the companions used to go stale). Each
    /// consumer references these via <c>DynamicResource</c>, so replacing the entries re-resolves
    /// them. OnAccentPressed carries the CON-1 fix: a dim accent's pressed fill gets a readable
    /// foreground rather than the reused OnAccent.
    /// </summary>
    public static void ApplyAccentOnly(ResourceDictionary resources, string accentColor, ThemePreset preset)
    {
        var set = ThemeColors.DeriveAccentSet(accentColor, preset);
        SetColorPair(resources, "AccentPrimary", set.Primary);
        SetColorPair(resources, "AccentHover", set.Hover);
        SetColorPair(resources, "AccentPressed", set.Pressed);
        SetColorPair(resources, "AccentBorder", set.Border);
        SetColorPair(resources, "AccentShellTint", set.ShellTint);
        SetColorPair(resources, "OnAccent", set.OnAccent);
        SetColorPair(resources, "OnAccentPressed", set.OnAccentPressed);
        // Keep AccentPrimaryLight defined as an alias to AccentHover for one migration pass.
        SetColorPair(resources, "AccentPrimaryLight", set.Hover);
    }

    private static void SetColorPair(ResourceDictionary resources, string key, Color color)
    {
        resources[key] = Frozen(color);
        resources[key + "Color"] = color;   // companion Color token, kept in step
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

    /// <summary>
    /// Replace the density resources from the preset's <see cref="ThemeDensity"/>. Heights/sizes land
    /// as <see cref="double"/>; paddings and the uniform default border land as <see cref="Thickness"/>
    /// (the struct type Padding/BorderThickness DynamicResource consumers expect — a double/string there
    /// hits the .NET 10 DynamicResource type-mismatch crash class). Replaced, not mutated, like the
    /// palette/radius/accent entries above.
    /// </summary>
    private static void ApplyDensity(ResourceDictionary resources, ThemeDensity density)
    {
        resources["DensityControlHeight"] = density.ControlHeight;
        resources["DensityIconButtonSize"] = density.IconButtonSize;
        resources["DensityScrollbarThickness"] = density.ScrollbarThickness;
        resources["DensityButtonPadding"] = density.ButtonPadding;
        resources["DensityInputPadding"] = density.InputPadding;
        resources["DensityMenuItemPadding"] = density.MenuItemPadding;
        resources["DensityPresetChipPadding"] = density.PresetChipPadding;
        resources["DensityToolTipPadding"] = density.ToolTipPadding;
        resources["BorderThicknessDefault"] = density.BorderThicknessDefault;
    }

    /// <summary>
    /// Replace the inner-elevation effects from the preset's <see cref="ThemeElevation"/>. A null
    /// <paramref name="elevation"/> (Sharp Dark) writes a null Effect so popup/panel consumers render
    /// flat — NOT a no-op DropShadowEffect, which would still cost per-frame raster. Minimal and Soft
    /// Glass get frozen <see cref="DropShadowEffect"/>s. Inner-only: these feed popup/menu/panel Effects,
    /// never an outer window (the windows host WebView2 by HWND and stay AllowsTransparency=False).
    /// </summary>
    private static void ApplyElevation(ResourceDictionary resources, ThemeElevation? elevation)
    {
        resources["ElevationPopup"] = elevation is null
            ? null
            : FrozenShadow(elevation.PopupBlurRadius, elevation.PopupShadowDepth, elevation.PopupOpacity);
        resources["ElevationPanel"] = elevation is null
            ? null
            : FrozenShadow(elevation.PanelBlurRadius, elevation.PanelShadowDepth, elevation.PanelOpacity);
    }

    private static DropShadowEffect FrozenShadow(double blurRadius, double shadowDepth, double opacity)
    {
        var effect = new DropShadowEffect
        {
            BlurRadius = blurRadius,
            ShadowDepth = shadowDepth,
            Opacity = opacity,
            Color = Colors.Black,   // neutral inner shadow; Direction stays the WPF default (315°)
        };
        effect.Freeze();   // shareable + immutable across windows, like the brushes above
        return effect;
    }

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();   // shareable + immutable; DynamicResource consumers hold it until the next Apply
        return brush;
    }
}
