using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using PiPlay.Models;
using PiPlay.Services;
using PiPlay.Theme;

namespace PiPlay;

/// <summary>
/// Application entry point. Handles native help before normal startup (REQ-APP-02). Normal
/// launches enforce single-instance ownership (REQ-APP-01) before application UI: a second
/// launch hands its URL to the running instance and exits rather than contending for the shared
/// WebView2 user-data folder. Owns the app-scoped shared WebView2 environment so the Source Window
/// and Popout Player share one session.
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
    private readonly HandoffRequestLedger _handoffLedger = new();
    private readonly DispatcherFaultPolicy _dispatcherFaults = new();
    private bool _shuttingDown;

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
        var request = StartupArgumentPolicy.Parse(e.Args);
        StartupDispatcher.Dispatch(
            request,
            ShowNativeHelp,
            Shutdown,
            launchUrl => StartNormal(e, launchUrl));
    }

    private static void ShowNativeHelp(string helpText)
    {
        MessageBox.Show(
            helpText,
            "PiPlay Help",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void StartNormal(StartupEventArgs e, string? launchUrl)
    {
        Log.Init();
        Log.Info("PiPlay starting.");

        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
        {
            Log.Info("Another instance is already running; handing off and exiting.");
            // Close the handle first: held through the hand-off and its "did not respond" message,
            // it keeps the mutex alive, so a relaunch after the running PiPlay exits would hand off
            // to nobody instead of starting.
            _mutex.Dispose();
            _mutex = null;
            if (!TrySendToExistingInstance(launchUrl))
                ShowHandoffNotAcknowledged();
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
        _shuttingDown = true;
        try { _pipeCts?.Cancel(); } catch { /* ignore */ }
        try { _mutex?.ReleaseMutex(); } catch { /* not owned */ }
        _mutex?.Dispose();
        Log.Info("PiPlay exiting.");
        Log.Shutdown();   // drain the writer; queued entries are lost without this
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Recover cleanly (Q-6) without stacking modals on a repeating fault, and without claiming
        // recovery from one the process cannot survive. DispatcherFaultPolicy owns that decision.
        var decision = _dispatcherFaults.Evaluate(
            e.Exception, _shuttingDown || Dispatcher.HasShutdownStarted);
        Log.Error(
            $"Unhandled UI exception (handled={decision.Handled}, dialog={decision.ShowDialog}, " +
            $"signature={decision.Signature}).",
            e.Exception);

        e.Handled = decision.Handled;
        if (!decision.ShowDialog) return;

        try
        {
            // Modal, so the dispatcher keeps pumping: faults raised behind this dialog re-enter
            // this handler on the same thread and the policy coalesces them.
            MessageBox.Show(
                decision.Handled
                    ? "PiPlay hit an unexpected problem. The details were written to the log and the app will keep running."
                    : "PiPlay hit a problem it cannot recover from. The details were written to the log and the app will close.",
                "PiPlay", MessageBoxButton.OK,
                decision.Handled ? MessageBoxImage.Warning : MessageBoxImage.Error);
        }
        catch (Exception dialogFailure)
        {
            // An out-of-memory fault can take the dialog down with it; never mask the original.
            Log.Error("Failed to show the unhandled UI exception dialog.", dialogFailure);
        }
        finally
        {
            _dispatcherFaults.DialogClosed();
        }
    }

    /// <summary>
    /// Compatibility seam for command-line parsing tests. StartupArgumentPolicy owns help
    /// precedence and the shared supported-YouTube-target boundary.
    /// </summary>
    internal static string? ExtractUrlArg(string[] args) =>
        StartupArgumentPolicy.Parse(args).LaunchUrl;

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
        var outcome = await SingleInstancePipeTransport.ServeOneAsync(
            PipeName,
            (request, ct) => SingleInstanceHandoffPolicy.DispatchAsync(
                request,
                _handoffLedger,
                OnSecondInstance,
                ex => Log.Error("Applying a single-instance hand-off failed on the UI thread.", ex),
                callback => DispatcherHandoffDispatch.Post(Dispatcher, callback),
                ct),
            token);

        if (outcome is null)
            Log.Info("Ignored a malformed single-instance hand-off request.");
        else if (outcome == HandoffOutcome.NotApplied)
            Log.Info("Withdrew a single-instance hand-off the UI thread did not start in time; the sender may retry.");
    }

    /// <summary>True when the running instance acknowledged the hand-off (REQ-APP-01, ADR-0009).</summary>
    private static bool TrySendToExistingInstance(string? url)
    {
        try
        {
            var request = HandoffRequest.Create(url);
            // Off the startup thread: the attempts are async pipe I/O with their own deadlines.
            var outcome = Task.Run(() => SingleInstanceHandoffPolicy.SendAsync(
                request,
                (sameRequest, ct) => SingleInstancePipeTransport.SendOnceAsync(PipeName, sameRequest, ct),
                (attempt, failure) => Log.Info(
                    $"Single-instance hand-off attempt {attempt} of {SingleInstanceHandoffPolicy.MaxAttempts} " +
                    $"was not applied ({failure})."),
                CancellationToken.None)).GetAwaiter().GetResult();
            return outcome == HandoffOutcome.Applied;
        }
        catch (Exception ex)
        {
            Log.Error("Failed to hand off to the existing instance.", ex);
            return false;
        }
    }

    private static void ShowHandoffNotAcknowledged()
    {
        Log.Error("The running instance did not acknowledge the hand-off.");
        MessageBox.Show(
            "PiPlay is already running but did not respond. Try again in a moment.",
            "PiPlay",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private void OnSecondInstance(string? url)
    {
        if (MainWindow is MainWindow main)
            main.ActivateFromSecondInstance(url);
    }
}
