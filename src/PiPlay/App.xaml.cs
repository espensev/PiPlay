using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using PiPlay.Models;
using PiPlay.Services;
using PiPlay.Theme;

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
    // colliding (the Default mutex keeps the original .v1 identity). The pipe adds the numeric session
    // id because named pipes use a machine-wide namespace while cross-session windows cannot activate
    // one another. This keeps the rendezvous boundary aligned with the existing primary-election
    // boundary; each elected primary still protects its channel's WebView2 user-data ownership.
    private static string IdentitySuffix =>
        AppChannel.Current == PiPlayChannel.Default ? "v1" : AppChannel.Name;
    private static string MutexName => $@"Local\PiPlay.SingleInstance.{IdentitySuffix}";
    private static readonly int SessionId = GetCurrentSessionId();
    private static string PipeName => SingleInstancePipePolicy.BuildPipeName(
        IdentitySuffix, SessionId);

    private Mutex? _mutex;
    private CancellationTokenSource? _pipeCts;

    private static int GetCurrentSessionId()
    {
        using var process = Process.GetCurrentProcess();
        return process.SessionId;
    }

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

        // Load once, then use the same sanitized object both for the resources parsed by the first
        // window and for MainWindow itself. A failure here must never block startup.
        AppSettings? bootSettings = null;
        try
        {
            bootSettings = new SettingsService().Load();
            ThemeResourceApplier.Apply(Resources, bootSettings.Theme, bootSettings.Player);
        }
        catch (Exception ex)
        {
            Log.Error("Failed to apply theme resources at startup; using defaults.", ex);
        }

        var main = bootSettings is null ? new MainWindow() : new MainWindow(bootSettings);
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
        Log.Shutdown();   // drain the writer; queued entries are lost without this
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

        _ = Task.Run(() => SingleInstancePipePolicy.RunAsync(
            attemptAsync: ServeOnePipeConnectionAsync,
            delayAsync: Task.Delay,
            onFirstFailure: ex => Log.Error(
                "Single-instance pipe server error; retries are delayed and repeats suppressed until recovery.",
                ex),
            onRecovery: failures => Log.Info(
                $"Single-instance pipe server recovered after {failures} failed attempt(s)."),
            token), token);
    }

    private async Task ServeOnePipeConnectionAsync(CancellationToken token)
    {
        using var server = new NamedPipeServerStream(
            PipeName, PipeDirection.In, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

        await server.WaitForConnectionAsync(token);
        using var reader = new StreamReader(server, Encoding.UTF8);
        var url = await reader.ReadToEndAsync(token);

        Dispatcher.Invoke(() => OnSecondInstance(url));
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
