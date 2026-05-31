using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace PiPlay.Tests;

/// <summary>
/// One long-lived STA thread hosting the single WPF <see cref="Application"/> + a running
/// <see cref="Dispatcher"/>, shared across all Layer 3 tests. Each test body is marshaled onto
/// this thread via <see cref="Invoke"/>.
///
/// Notes:
/// - A WPF Application is a process-wide singleton with thread affinity; per-test STA threads
///   would access it cross-thread and throw, so one owning thread is used.
/// - Startup runs in <see cref="Start"/> (a static-field initializer), NOT in a static ctor that
///   blocks: the STA thread communicates back via captured LOCALS only. Touching a static field
///   of this type from the STA thread while the initializer is mid-run would deadlock on the
///   CLR type-initialization lock.
/// - App/resource init is queued onto the dispatcher and runs only once the pump is going.
/// - <see cref="Application.ResourceAssembly"/> is set in <see cref="TestDataRoot"/> so the
///   windows' short-form pack URIs resolve from the PiPlay assembly. Windows are constructed,
///   never shown, so WebView2 (Loaded) and the network are never touched.
/// </summary>
internal static class StaTestThread
{
    private static readonly Dispatcher _dispatcher = Start();

    private static Dispatcher Start()
    {
        Dispatcher? dispatcher = null;
        Exception? error = null;
        using var ready = new ManualResetEventSlim();

        var thread = new Thread(() =>
        {
            // IMPORTANT: assign only captured LOCALS here, never StaTestThread statics.
            var d = Dispatcher.CurrentDispatcher;
            dispatcher = d;

            d.InvokeAsync(() =>
            {
                try
                {
                    if (Application.Current is null)
                    {
                        // Plain Application + the app's merged dictionaries (avoids PiPlay.App's
                        // InitializeComponent), so window-level {StaticResource} lookups resolve.
                        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                        foreach (var rd in new[] { "Theme/Colors.xaml", "Theme/ControlStyles.xaml" })
                            app.Resources.MergedDictionaries.Add(new ResourceDictionary
                            {
                                Source = new Uri($"/PiPlay;component/{rd}", UriKind.Relative),
                            });
                    }
                }
                catch (Exception ex)
                {
                    error = ex;
                }
                finally
                {
                    ready.Set();
                }
            });

            Dispatcher.Run();
        })
        {
            IsBackground = true,
            Name = "PiPlay-WPF-Test-STA",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        if (!ready.Wait(TimeSpan.FromSeconds(30)))
            throw new TimeoutException("WPF test STA thread did not initialize within 30 s.");
        if (error is not null)
            throw new InvalidOperationException("Failed to start the WPF test Application.", error);
        return dispatcher!;
    }

    /// <summary>Run <paramref name="action"/> on the shared STA/UI thread; exceptions rethrow to the caller.</summary>
    public static void Invoke(Action action) => _dispatcher.Invoke(action);
}
