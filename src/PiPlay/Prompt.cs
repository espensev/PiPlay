using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PiPlay;

/// <summary>Minimal themed text-input dialog (used for naming a profile). Self-contained, no extra deps.</summary>
internal static class Prompt
{
    public static string? AskText(Window owner, string title, string message, string initial = "")
    {
        var bg = (Brush)Application.Current.Resources["AppBackground"];
        var fg = (Brush)Application.Current.Resources["TextPrimary"];

        var win = new Window
        {
            Title = title,
            Owner = owner,
            Width = 440,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.ToolWindow,
            Background = bg,
        };

        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock
        {
            Text = message,
            Foreground = fg,
            Margin = new Thickness(0, 0, 0, 8),
        });

        var box = new TextBox
        {
            Text = initial,
            Style = (Style)Application.Current.Resources["DarkTextBox"],
            Margin = new Thickness(0, 0, 0, 14),
        };
        panel.Children.Add(box);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var ok = new Button
        {
            Content = "Save",
            Style = (Style)Application.Current.Resources["AccentButton"],
            Width = 90,
            IsDefault = true,
            Margin = new Thickness(0, 0, 8, 0),
        };
        var cancel = new Button
        {
            Content = "Cancel",
            Style = (Style)Application.Current.Resources["DarkButton"],
            Width = 90,
            IsCancel = true,
        };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);

        win.Content = panel;

        string? result = null;
        ok.Click += (_, _) => { result = box.Text; win.DialogResult = true; };
        box.Loaded += (_, _) => { box.Focus(); box.SelectAll(); };

        return win.ShowDialog() == true ? result : null;
    }

    /// <summary>
    /// Themed dark Yes/No confirmation. Returns true only if the user confirms. Sets the owner and
    /// matches its Topmost so a pinned PiPlay does not occlude it. Default focus is Cancel so Enter
    /// never confirms a destructive action by accident; <paramref name="danger"/> styles the
    /// confirm button as destructive (red).
    /// </summary>
    public static bool AskConfirm(Window owner, string title, string message, string confirmText, bool danger = false)
    {
        var bg = (Brush)Application.Current.Resources["AppBackground"];
        var fg = (Brush)Application.Current.Resources["TextPrimary"];

        var win = new Window
        {
            Title = title,
            Owner = owner,
            Topmost = owner.Topmost,
            Width = 460,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.ToolWindow,
            ShowInTaskbar = false,
            Background = bg,
        };

        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock
        {
            Text = message,
            Foreground = fg,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16),
        });

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var confirm = new Button
        {
            Content = confirmText,
            Style = (Style)Application.Current.Resources[danger ? "DangerButton" : "AccentButton"],
            MinWidth = 110,
            Margin = new Thickness(0, 0, 8, 0),
        };
        var cancel = new Button
        {
            Content = "Cancel",
            Style = (Style)Application.Current.Resources["DarkButton"],
            MinWidth = 90,
            IsCancel = true,
            IsDefault = true,
        };
        buttons.Children.Add(confirm);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);

        win.Content = panel;

        var result = false;
        confirm.Click += (_, _) => { result = true; win.DialogResult = true; };

        win.ShowDialog();
        return result;
    }

    /// <summary>Themed dark message dialog with a single OK button (done / not-ready / failed notices).</summary>
    public static void ShowInfo(Window owner, string title, string message)
    {
        var bg = (Brush)Application.Current.Resources["AppBackground"];
        var fg = (Brush)Application.Current.Resources["TextPrimary"];

        var win = new Window
        {
            Title = title,
            Owner = owner,
            Topmost = owner.Topmost,
            Width = 460,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.ToolWindow,
            ShowInTaskbar = false,
            Background = bg,
        };

        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock
        {
            Text = message,
            Foreground = fg,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16),
        });

        var ok = new Button
        {
            Content = "OK",
            Style = (Style)Application.Current.Resources["AccentButton"],
            MinWidth = 90,
            HorizontalAlignment = HorizontalAlignment.Right,
            IsDefault = true,
            IsCancel = true,
        };
        ok.Click += (_, _) => { win.DialogResult = true; };
        panel.Children.Add(ok);

        win.Content = panel;
        win.ShowDialog();
    }
}
