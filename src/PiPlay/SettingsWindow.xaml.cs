using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using PiPlay.Services;

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

    /// <summary>True when any persisted player preference changed (accents, fade delay, or compact
    /// mode), so MainWindow knows to persist and re-apply on close.</summary>
    internal bool AppearanceChanged { get; private set; }
    internal string PinAccent { get; private set; }
    internal string FadeAccent { get; private set; }
    internal int FadeIdleDelayMs { get; private set; }
    internal bool CompactMode { get; private set; }

    public SettingsWindow(
        bool isBrowserReady,
        string? pinAccent = PlayerAppearancePolicy.DefaultAccent,
        string? fadeAccent = PlayerAppearancePolicy.DefaultAccent,
        int fadeIdleDelayMs = PlayerAppearancePolicy.DefaultFadeIdleDelayMs,
        bool compactMode = false)
    {
        InitializeComponent();

        PinAccent = PlayerAppearancePolicy.NormalizeAccent(pinAccent);
        FadeAccent = PlayerAppearancePolicy.NormalizeAccent(fadeAccent);
        FadeIdleDelayMs = PlayerAppearancePolicy.NormalizeFadeIdleDelayMs(fadeIdleDelayMs);
        CompactMode = compactMode;
        CompactModeToggle.IsChecked = compactMode;
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

    private void PinAccent_Click(object sender, RoutedEventArgs e)
    {
        PinAccent = PlayerAppearancePolicy.NormalizeAccent(((FrameworkElement)sender).Tag as string);
        AppearanceChanged = true;
        ApplyAppearanceSelections();
    }

    private void FadeAccent_Click(object sender, RoutedEventArgs e)
    {
        FadeAccent = PlayerAppearancePolicy.NormalizeAccent(((FrameworkElement)sender).Tag as string);
        AppearanceChanged = true;
        ApplyAppearanceSelections();
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
        SelectAccent(PinAccent, PinAccentCyanSwatch, PinAccentVioletSwatch, PinAccentGreenSwatch, PinAccentAmberSwatch);
        SelectAccent(FadeAccent, FadeAccentCyanSwatch, FadeAccentVioletSwatch, FadeAccentGreenSwatch, FadeAccentAmberSwatch);
        SelectDelay(FadeIdleDelayMs, FadeDelayShortPreset, FadeDelayNormalPreset, FadeDelayLongPreset);
    }

    private static void SelectAccent(string selected, params ToggleButton[] buttons)
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
