using System.Windows;

namespace PiPlay.Tests;

/// <summary>
/// Boots a single WPF <see cref="Application"/> for the test process and ensures the PiPlay
/// assembly's resources (App.xaml merged dictionaries, the app icon) resolve. Crucially sets
/// <see cref="Application.ResourceAssembly"/> to the PiPlay assembly: the windows use
/// short-form pack URIs (<c>pack://application:,,,/Assets/piplay.ico</c>,
/// <c>{StaticResource ...}</c>) that otherwise resolve against the test host and fail. Windows
/// are constructed but never shown, so WebView2 (created in Loaded) and the network are never
/// touched.
/// </summary>
public sealed class WpfAppFixture
{
    public WpfAppFixture()
    {
        if (Application.Current is not null) return;

        // Must be set before any pack:// resource is resolved.
        Application.ResourceAssembly = typeof(PiPlay.MainWindow).Assembly;

        var app = new PiPlay.App();
        app.InitializeComponent(); // loads Theme/Colors.xaml + Theme/ControlStyles.xaml into App.Resources
    }
}

/// <summary>Single shared WPF app across all STA UI tests (one Application per process).</summary>
[CollectionDefinition(WpfCollection.Name)]
public sealed class WpfCollection : ICollectionFixture<WpfAppFixture>
{
    public const string Name = "WPF";
}
