namespace PiPlay.Services;

internal enum StartupAction
{
    Launch,
    ShowHelp,
}

internal readonly record struct StartupRequest(
    StartupAction Action,
    string? LaunchUrl);

/// <summary>
/// Pure command-line boundary for application startup. It recognizes executable-owned help and
/// selects at most one launch URL without touching WPF, logging, settings, IPC, or WebView2.
/// </summary>
internal static class StartupArgumentPolicy
{
    internal static string HelpText { get; } =
        """
        PiPlay

        Usage:
          PiPlay.exe [URL]
          PiPlay.exe --help
          PiPlay.exe -h
          PiPlay.exe /?

        Options:
          --help, -h, /?  Show this help and exit.
        """;

    internal static StartupRequest Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        foreach (var argument in args)
        {
            if (argument is "--help" or "-h" or "/?")
                return new StartupRequest(StartupAction.ShowHelp, null);
        }

        foreach (var argument in args)
        {
            if (TryResolveLaunchCandidate(argument, out var launchUrl))
                return new StartupRequest(StartupAction.Launch, launchUrl);
        }

        return new StartupRequest(StartupAction.Launch, null);
    }

    private static bool TryResolveLaunchCandidate(string argument, out string launchUrl)
    {
        if (YouTubeUrlHelper.TryParse(argument, out _))
        {
            launchUrl = argument;
            return true;
        }

        launchUrl = string.Empty;
        return false;
    }
}

/// <summary>
/// Routes the pure startup decision through injected effects so help-versus-normal exclusivity is
/// testable without constructing a WPF application or displaying a modal dialog.
/// </summary>
internal static class StartupDispatcher
{
    internal static void Dispatch(
        StartupRequest request,
        Action<string> showHelp,
        Action<int> shutdown,
        Action<string?> startNormal)
    {
        ArgumentNullException.ThrowIfNull(showHelp);
        ArgumentNullException.ThrowIfNull(shutdown);
        ArgumentNullException.ThrowIfNull(startNormal);

        switch (request.Action)
        {
            case StartupAction.ShowHelp:
                showHelp(StartupArgumentPolicy.HelpText);
                shutdown(0);
                return;

            case StartupAction.Launch:
                startNormal(request.LaunchUrl);
                return;

            default:
                throw new ArgumentOutOfRangeException(nameof(request), request.Action, "Unknown startup action.");
        }
    }
}
