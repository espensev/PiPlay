using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PiPlay;

/// <summary>
/// Minimal themed, fully borderless-dark dialogs (text input, confirm, info). Built in code so they
/// match the app's dark identity exactly — no light native title bar (mirrors SettingsWindow).
/// Self-contained, no extra deps.
/// </summary>
internal static class Prompt
{
    private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];
    private static Style Style(string key) => (Style)Application.Current.Resources[key];

    /// <summary>
    /// Build a borderless dark dialog shell (matches SettingsWindow): a 1px border, a thin
    /// draggable title bar with a close button, and a content body the caller fills. Internal so a
    /// WPF test can assert the dark/borderless invariants without showing a modal.
    /// </summary>
    internal static Window BuildShell(Window? owner, string title, out StackPanel body)
    {
        var win = new Window
        {
            Title = title,
            Owner = owner,
            Topmost = owner?.Topmost ?? false,
            Width = 460,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = false,
            ShowInTaskbar = false,
            Background = Brush("AppBackground"),
            UseLayoutRounding = false,
            SnapsToDevicePixels = true,
        };

        var root = new DockPanel();

        var bar = new Grid { Height = 42, Background = Brush("SurfaceBase") };
        bar.MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) win.DragMove(); };
        bar.Children.Add(new TextBlock
        {
            Text = title,
            Margin = new Thickness(16, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("TextPrimary"),
        });
        var close = new Button
        {
            Style = Style("CloseIconButton"),
            Content = "",
            ToolTip = "Close",
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 5, 0),
        };
        // Title-bar close behaves as Cancel: set DialogResult=false (which auto-closes the modal)
        // so ShowDialog() returns false, matching the IsCancel button. (Was win.Close() -> null.)
        close.Click += (_, _) => { win.DialogResult = false; };
        bar.Children.Add(close);
        DockPanel.SetDock(bar, Dock.Top);
        root.Children.Add(bar);

        body = new StackPanel { Margin = new Thickness(20) };
        root.Children.Add(body);

        win.Content = new Border
        {
            BorderBrush = Brush("BorderSubtle"),
            BorderThickness = new Thickness(1),
            Child = root,
        };
        return win;
    }

    /// <summary>Themed text-input dialog (used for naming a profile). Returns null if cancelled.</summary>
    public static string? AskText(Window owner, string title, string message, string initial = "")
    {
        var win = BuildShell(owner, title, out var body);
        body.Children.Add(new TextBlock
        {
            Text = message,
            Foreground = Brush("TextPrimary"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10),
        });

        var box = new TextBox
        {
            Text = initial,
            Style = Style("DarkTextBox"),
            Margin = new Thickness(0, 0, 0, 16),
        };
        body.Children.Add(box);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var ok = new Button { Content = "Save", Style = Style("AccentButton"), MinWidth = 90, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = "Cancel", Style = Style("DarkButton"), MinWidth = 90, IsCancel = true };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        body.Children.Add(buttons);

        string? result = null;
        ok.Click += (_, _) => { result = box.Text; win.DialogResult = true; };
        box.Loaded += (_, _) => { box.Focus(); box.SelectAll(); };

        return win.ShowDialog() == true ? result : null;
    }

    /// <summary>
    /// Themed dark Yes/No confirmation. Returns true only if the user confirms. Default focus is
    /// Cancel so Enter never confirms a destructive action by accident; <paramref name="danger"/>
    /// styles the confirm button as destructive (red). The title-bar close acts as Cancel.
    /// </summary>
    public static bool AskConfirm(Window owner, string title, string message, string confirmText, bool danger = false)
    {
        var win = BuildShell(owner, title, out var body);
        body.Children.Add(new TextBlock
        {
            Text = message,
            Foreground = Brush("TextPrimary"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16),
        });

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var confirm = new Button { Content = confirmText, Style = Style(danger ? "DangerButton" : "AccentButton"), MinWidth = 110, Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = "Cancel", Style = Style("DarkButton"), MinWidth = 90, IsCancel = true, IsDefault = true };
        buttons.Children.Add(confirm);
        buttons.Children.Add(cancel);
        body.Children.Add(buttons);

        var result = false;
        confirm.Click += (_, _) => { result = true; win.DialogResult = true; };

        win.ShowDialog();
        return result;
    }

    /// <summary>Themed dark message dialog with a single OK button (done / not-ready / failed notices).</summary>
    public static void ShowInfo(Window owner, string title, string message)
    {
        var win = BuildShell(owner, title, out var body);
        body.Children.Add(new TextBlock
        {
            Text = message,
            Foreground = Brush("TextPrimary"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16),
        });

        var ok = new Button { Content = "OK", Style = Style("AccentButton"), MinWidth = 90, HorizontalAlignment = HorizontalAlignment.Right, IsDefault = true, IsCancel = true };
        ok.Click += (_, _) => { win.DialogResult = true; };
        body.Children.Add(ok);

        win.ShowDialog();
    }
}
