using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using PiPlay.Services;

namespace PiPlay;

/// <summary>
/// Application entry point. Enforces single-instance (REQ-APP-01) BEFORE any UI: a second
/// launch hands its URL to the running instance and exits rather than contending for the
/// shared WebView2 user-data folder. Owns the app-scoped shared WebView2 environment so
/// the Source Window and Popout Player share one session.
/// </summary>
public partial class App : Application
{
    // Per-session single-instance identity (the Local\ mutex namespace is scoped to the Windows logon
    // session), scoped per channel so a Stable copy and the dev app each stay single-instance without
    // colliding (the Default channel keeps the original .v1 names). The guard exists to protect each
    // channel's own WebView2 user-data folder from concurrent access.
    private static string IdentitySuffix =>
        AppChannel.Current == PiPlayChannel.Default ? "v1" : AppChannel.Name;
    private static string MutexName => $@"Local\PiPlay.SingleInstance.{IdentitySuffix}";
    private static string PipeName => $"PiPlay.SingleInstance.{IdentitySuffix}";

    private Mutex? _mutex;
    private CancellationTokenSource? _pipeCts;

    /// <summary>Shared WebView2 environment, created lazily during the Source Window's browser init.</summary>
    public WebViewEnvironmentService WebViewEnvironment { get; } = new();

    public static new App Current => (App)Application.Current;

    protected override void OnStartup(StartupEventArgs e)
    {
        Log.Init();
        Log.Info("PiPlay starting.");

        var launchUrl = ExtractUrlArg(e.Args);

        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
        {
            Log.Info("Another instance is already running; handing off and exiting.");
            TrySendToExistingInstance(launchUrl);
            // Skip base.OnStartup so no window is created; just leave.
            Shutdown(0);
            return;
        }

        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        StartPipeServer();

        var main = new MainWindow();
        MainWindow = main;
        main.Show();

        if (!string.IsNullOrEmpty(launchUrl))
            main.NavigateTo(launchUrl);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { _pipeCts?.Cancel(); } catch { /* ignore */ }
        try { _mutex?.ReleaseMutex(); } catch { /* not owned */ }
        _mutex?.Dispose();
        Log.Info("PiPlay exiting.");
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Recover cleanly (Q-6): log and keep the app alive rather than crashing out.
        Log.Error("Unhandled UI exception.", e.Exception);
        e.Handled = true;
        MessageBox.Show(
            "PiPlay hit an unexpected problem. The details were written to the log and the app will keep running.",
            "PiPlay", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private static string? ExtractUrlArg(string[] args)
    {
        foreach (var a in args)
        {
            if (a.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                a.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                a.StartsWith("youtu", StringComparison.OrdinalIgnoreCase))
            {
                return a;
            }
        }
        return null;
    }

    // --- Single-instance hand-off over a named pipe ---

    private void StartPipeServer()
    {
        _pipeCts = new CancellationTokenSource();
        var token = _pipeCts.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(
                        PipeName, PipeDirection.In, 1,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync(token);
                    using var reader = new StreamReader(server, Encoding.UTF8);
                    var url = await reader.ReadToEndAsync(token);

                    Dispatcher.Invoke(() => OnSecondInstance(url));
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log.Error("Single-instance pipe server error.", ex);
                }
            }
        }, token);
    }

    private static void TrySendToExistingInstance(string? url)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(2000);
            using var writer = new StreamWriter(client, new UTF8Encoding(false));
            writer.Write(url ?? string.Empty);
            writer.Flush();
        }
        catch (Exception ex)
        {
            Log.Error("Failed to hand off to the existing instance.", ex);
        }
    }

    private void OnSecondInstance(string? url)
    {
        if (MainWindow is MainWindow main)
            main.ActivateFromSecondInstance(url);
    }
}
