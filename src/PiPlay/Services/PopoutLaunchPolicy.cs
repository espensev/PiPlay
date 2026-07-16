namespace PiPlay.Services;

/// <summary>Fail-closed preconditions that must hold before Popout construction can begin.</summary>
internal static class PopoutLaunchPolicy
{
    /// <summary>
    /// Transfer playback ownership away from Source or throw into MainWindow's existing rollback
    /// path. Code after this await is unreachable when suppression was not acknowledged.
    /// </summary>
    public static async Task RequireAcknowledgedSourceSuppressionAsync(
        Func<Task<bool>> suppressSourceAsync)
    {
        ArgumentNullException.ThrowIfNull(suppressSourceAsync);
        if (!await suppressSourceAsync())
        {
            throw new InvalidOperationException(
                "Source playback suppression was not acknowledged; Popout launch was aborted.");
        }
    }
}
