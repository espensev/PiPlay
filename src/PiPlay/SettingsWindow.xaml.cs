using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using PiPlay.Services;
using PiPlay.Theme;

namespace PiPlay;

/// <summary>The action the user confirmed in the Settings window, read by MainWindow after close.</summary>
internal enum PrivacyAction { None, ResetAppState, ClearBrowserData }

/// <summary>
/// Themed Settings window (spec 12, Phase 2). Hosts the Privacy section. It confirms an action and
/// records <see cref="RequestedAction"/>, then closes — it performs no app/WebView work itself
/// (MainWindow does, after the modal closes). Visible wording is sourced from
/// <see cref="PrivacyService"/> so the UI and the tested constants cannot drift.
/// </summary>
public partial class SettingsWindow : Window
{
    internal PrivacyAction RequestedAction { get; private set; } = PrivacyAction.None;

    /// <summary>True when any persisted player preference changed (theme, accent, fade delay, or
    /// compact mode), so MainWindow knows to persist and re-apply on close.</summary>
    internal bool AppearanceChanged { get; private set; }

    /// <summary>The selected theme preset id and the single accent hex it/the chips drive (overhaul
    /// Task 10): one accent replaces the separate Pin/Fade color choices.</summary>
    internal string ThemeId { get; private set; }
    internal string AccentColor { get; private set; }

    /// <summary>Corner profile override (review doc §8.1): "theme" follows the preset; the other
    /// styles swap the whole radius + native-corner profile.</summary>
    internal string CornerStyle { get; private set; }
    internal int FadeIdleDelayMs { get; private set; }
    internal bool CompactMode { get; private set; }
    internal double ConstantWindowOpacity { get; private set; }
    internal double IdleWindowOpacity { get; private set; }
    internal bool StripAutoHide { get; private set; }

    /// <summary>Raised on every opacity slider move so MainWindow can live-preview the levels on
    /// the open popout (spec 7.3 / plan Task 3). Args: (constant, idle).</summary>
    internal event Action<double, double>? OpacityPreviewChanged;

    // True from construction until the ctor has seeded the sliders: Slider coerces Value to its
    // Minimum during InitializeComponent, which fires ValueChanged before our values are in.
    private bool _seedingOpacitySliders = true;

    public SettingsWindow(
        bool isBrowserReady,
        string? themeId = ThemeCatalog.DefaultThemeId,
        string? accentColor = ThemeCatalog.DefaultAccentColor,
        int fadeIdleDelayMs = PlayerAppearancePolicy.DefaultFadeIdleDelayMs,
        bool compactMode = false,
        double constantWindowOpacity = WindowOpacityPolicy.Default,
        double idleWindowOpacity = WindowOpacityPolicy.Default,
        bool stripAutoHide = false,
        string? cornerStyle = ThemeCatalog.DefaultCornerStyle)
    {
        InitializeComponent();
        ApplyInitialBounds();
        // The dialog wears the theme/override corner shape itself, and re-applies it on chip
        // clicks — instant feedback for the corner-style row (review doc §8.7).
        SourceInitialized += (_, _) => ApplyOwnCornerMode();

        ThemeId = ThemeCatalog.NormalizeThemeId(themeId);
        AccentColor = ThemeCatalog.NormalizeAccentColor(accentColor);
        CornerStyle = ThemeCatalog.NormalizeCornerStyle(cornerStyle);
        FadeIdleDelayMs = PlayerAppearancePolicy.NormalizeFadeIdleDelayMs(fadeIdleDelayMs);
        CompactMode = compactMode;
        CompactModeToggle.IsChecked = compactMode;
        StripAutoHide = stripAutoHide;
        StripAutoHideToggle.IsChecked = stripAutoHide;

        // A hand-edited sub-floor level (the spec 7.3 explicit unlock) is preserved exactly until
        // the user moves that slider: the DISPLAY clamps to the 45% floor, the stored value doesn't.
        ConstantWindowOpacity = WindowOpacityPolicy.Normalize(constantWindowOpacity);
        IdleWindowOpacity = WindowOpacityPolicy.Normalize(idleWindowOpacity);
        ActiveOpacitySlider.Value = DisplayPercent(ConstantWindowOpacity);
        IdleOpacitySlider.Value = DisplayPercent(IdleWindowOpacity);
        _seedingOpacitySliders = false;
        UpdateOpacityValueTexts();
        ApplyAppearanceSelections();

        ResetDescriptionText.Text = PrivacyService.ResetDescription;
        ResetAppStateButton.Content = PrivacyService.ResetActionLabel;
        ClearDescriptionText.Text = PrivacyService.ClearDescription;
        ClearBrowserDataButton.Content = PrivacyService.ClearActionLabel;

        // Only the Clear action needs a live browser; Reset never does. When it is disabled,
        // explain why (and let the tooltip show on the disabled control).
        ClearBrowserDataButton.IsEnabled = isBrowserReady;
        if (!isBrowserReady)
        {
            ClearBrowserDataButton.ToolTip = PrivacyService.ClearNotReadyHint;
            ToolTipService.SetShowOnDisabled(ClearBrowserDataButton, true);
        }
    }

    /// <summary>
    /// Bounded height (overhaul Task 5; frame model reconciled with the b35c0dd landing): the
    /// fixed launch Height clamps to the primary work area less a margin, so the border never
    /// touches the taskbar and shorter displays get the scroll instead of clipping. The floor
    /// keeps the dialog usable if a misreported work area ever comes back tiny.
    /// </summary>
    private void ApplyInitialBounds()
    {
        MaxHeight = Math.Max(420, SystemParameters.WorkArea.Height - 48);
        Height = Math.Min(Height, MaxHeight);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (AppearanceChanged)
        {
            CompleteDialog();
            return;
        }

        Close();
    }

    private void ThemePreset_Click(object sender, RoutedEventArgs e)
    {
        var previousPreset = ThemeCatalog.PresetFor(ThemeId);
        ThemeId = ThemeCatalog.NormalizeThemeId(((FrameworkElement)sender).Tag as string);
        // An explicit preset selection adopts the preset's defaults (review doc §2.1) — fade
        // delay, top-bar auto-hide, opacity levels, and theme-owned corners. The controls below
        // can then fine-tune each one; manual changes after this click are overrides. The accent
        // follows the §3.3 switch rule (custom accents survive) via the pure catalog helper.
        var preset = ThemeCatalog.PresetFor(ThemeId);
        AccentColor = ThemeCatalog.AccentForThemeSwitch(AccentColor, previousPreset, preset);
        CornerStyle = ThemeCatalog.DefaultCornerStyle;
        FadeIdleDelayMs = ThemeCatalog.FadeDelayMillisecondsForPreset(preset.DefaultFadeDelayPreset);
        StripAutoHide = preset.DefaultStripAutoHide;
        StripAutoHideToggle.IsChecked = StripAutoHide;
        // Adopt the opacity levels directly, then move the sliders: Slider.Value raises no
        // ValueChanged when the DISPLAY percent doesn't move (e.g. a stored 0.917 and a preset
        // 0.92 both display 92), so the stored values must not depend on the event side effect.
        // The explicit preview invoke makes the preset's translucency visible before close.
        ConstantWindowOpacity = WindowOpacityPolicy.Normalize(preset.DefaultActiveWindowOpacity);
        IdleWindowOpacity = WindowOpacityPolicy.Normalize(preset.DefaultIdleWindowOpacity);
        ActiveOpacitySlider.Value = DisplayPercent(ConstantWindowOpacity);
        IdleOpacitySlider.Value = DisplayPercent(IdleWindowOpacity);
        UpdateOpacityValueTexts();
        OpacityPreviewChanged?.Invoke(ConstantWindowOpacity, IdleWindowOpacity);
        AppearanceChanged = true;
        ApplyAppearanceSelections();
        ApplyOwnCornerMode();
    }

    private void AccentChip_Click(object sender, RoutedEventArgs e)
    {
        AccentColor = ThemeCatalog.NormalizeAccentColor(((FrameworkElement)sender).Tag as string);
        AppearanceChanged = true;
        ApplyAppearanceSelections();
    }

    private void CornerStyle_Click(object sender, RoutedEventArgs e)
    {
        CornerStyle = ThemeCatalog.NormalizeCornerStyle(((FrameworkElement)sender).Tag as string);
        AppearanceChanged = true;
        ApplyAppearanceSelections();
        ApplyOwnCornerMode();
    }

    /// <summary>The dialog's own native corner shape follows the pending theme/override selection.</summary>
    private void ApplyOwnCornerMode()
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;   // SourceInitialized applies the initial state
        WindowOpacityApplier.SetCornerMode(hwnd,
            ThemeCatalog.DwmCornersFor(ThemeCatalog.PresetFor(ThemeId), CornerStyle));
    }

    private void FadeDelay_Click(object sender, RoutedEventArgs e)
    {
        var tag = ((FrameworkElement)sender).Tag as string;
        if (int.TryParse(tag, out var delay))
        {
            FadeIdleDelayMs = PlayerAppearancePolicy.NormalizeFadeIdleDelayMs(delay);
            AppearanceChanged = true;
        }
        ApplyAppearanceSelections();
    }

    private void CompactModeToggle_Click(object sender, RoutedEventArgs e)
    {
        CompactMode = CompactModeToggle.IsChecked == true;
        AppearanceChanged = true;
    }

    private void StripAutoHideToggle_Click(object sender, RoutedEventArgs e)
    {
        StripAutoHide = StripAutoHideToggle.IsChecked == true;
        AppearanceChanged = true;
    }

    private static double DisplayPercent(double level) =>
        Math.Round(Math.Max(level, WindowOpacityPolicy.UiFloor) * 100.0);

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_seedingOpacitySliders) return;
        var slider = (Slider)sender;
        var level = Math.Round(slider.Value) / 100.0;
        if (ReferenceEquals(slider, ActiveOpacitySlider)) ConstantWindowOpacity = level;
        else IdleWindowOpacity = level;
        AppearanceChanged = true;
        UpdateOpacityValueTexts();
        OpacityPreviewChanged?.Invoke(ConstantWindowOpacity, IdleWindowOpacity);
    }

    private void UpdateOpacityValueTexts()
    {
        ActiveOpacityValueText.Text = $"{Math.Round(ActiveOpacitySlider.Value)}%";
        IdleOpacityValueText.Text = $"{Math.Round(IdleOpacitySlider.Value)}%";
    }

    private void ResetAppStateButton_Click(object sender, RoutedEventArgs e)
    {
        ResetAppStateButton.IsEnabled = false;
        if (Prompt.AskConfirm(this, PrivacyService.ResetConfirmTitle, PrivacyService.ResetConfirmBody,
                PrivacyService.ResetConfirmButton, danger: false))
        {
            RequestedAction = PrivacyAction.ResetAppState;
            CompleteDialog();
        }
        else
        {
            ResetAppStateButton.IsEnabled = true;
        }
    }

    private void ClearBrowserDataButton_Click(object sender, RoutedEventArgs e)
    {
        ClearBrowserDataButton.IsEnabled = false;
        if (Prompt.AskConfirm(this, PrivacyService.ClearConfirmTitle, PrivacyService.ClearConfirmBody,
                PrivacyService.ClearConfirmButton, danger: true))
        {
            RequestedAction = PrivacyAction.ClearBrowserData;
            CompleteDialog();
        }
        else
        {
            ClearBrowserDataButton.IsEnabled = true;
        }
    }

    private void ApplyAppearanceSelections()
    {
        SelectByTag(ThemeId, ThemeSharpDarkPreset, ThemeMinimalPreset, ThemeSoftGlassPreset);
        SelectByTag(AccentColor, AccentChipCyan, AccentChipSteelBlue, AccentChipSteel, AccentChipViolet,
            AccentChipGreen, AccentChipAmber);
        SelectByTag(CornerStyle, CornerStyleThemeChip, CornerStyleSquareChip, CornerStyleSmallChip,
            CornerStyleSoftChip, CornerStyleRoundChip);
        SelectDelay(FadeIdleDelayMs, FadeDelayShortPreset, FadeDelayNormalPreset, FadeDelayLongPreset);
    }

    private static void SelectByTag(string selected, params ToggleButton[] buttons)
    {
        foreach (var button in buttons)
        {
            button.IsChecked = string.Equals(button.Tag as string, selected, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void SelectDelay(int selected, params ToggleButton[] buttons)
    {
        foreach (var button in buttons)
        {
            var matches = int.TryParse(button.Tag as string, out var delay) && delay == selected;
            button.IsChecked = matches;
        }
    }

    private void CompleteDialog()
    {
        try
        {
            DialogResult = true;
        }
        catch (InvalidOperationException)
        {
            Close();
        }
    }
}
