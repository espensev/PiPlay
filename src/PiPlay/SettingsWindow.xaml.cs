using System.Windows;
using System.Windows.Controls;
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

    public SettingsWindow(bool isBrowserReady)
    {
        InitializeComponent();

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

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void ResetAppStateButton_Click(object sender, RoutedEventArgs e)
    {
        ResetAppStateButton.IsEnabled = false;
        if (Prompt.AskConfirm(this, PrivacyService.ResetConfirmTitle, PrivacyService.ResetConfirmBody,
                PrivacyService.ResetConfirmButton, danger: false))
        {
            RequestedAction = PrivacyAction.ResetAppState;
            DialogResult = true;
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
            DialogResult = true;
        }
        else
        {
            ClearBrowserDataButton.IsEnabled = true;
        }
    }
}
