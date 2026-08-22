namespace PiPlay.Services;

/// <summary>
/// Pure decisions for the compact (PiPlay shell) error/fallback path (Q-6): maps shell-reported
/// YouTube IFrame API error codes to user-facing
/// messages, decides when a recovered playback auto-dismisses the error state, and owns the
/// host-side "the IFrame API never came up" timeout. No UI or WebView dependencies, so every
/// fallback decision is unit-testable. The wire transport lives in <see cref="PlayerShellProtocol"/>;
/// the surface that consumes these decisions is the Popout Player's error bar.
/// </summary>
public static class PlayerShellErrorPolicy
{
    // YouTube IFrame Player API onError codes (official API reference).
    public const string CodeInvalidParam = "2";
    public const string CodeHtml5Error = "5";
    public const string CodeNotFound = "100";
    public const string CodeEmbedDisabled = "101";
    public const string CodeEmbedDisabledDisguised = "150";   // documented as identical to 101

    // YouTube IFrame Player API getPlayerState() value for "playing".
    public const int StatePlaying = 1;

    /// <summary>Message for a shell navigation that failed outright (the shell never loaded).</summary>
    public const string ShellLoadFailedMessage =
        "The compact player couldn't load.";

    /// <summary>Message for a shell that loaded but whose IFrame API never reported back.</summary>
    public const string ReadyTimeoutMessage =
        "The compact player isn't responding (YouTube may be unreachable).";

    /// <summary>
    /// How long after navigating to the shell the host waits for any bridge message (ready, state,
    /// or error) before treating the IFrame API as dead. Generous on purpose: it must catch "the
    /// iframe_api script never loaded" without false-firing on a slow cold start.
    /// </summary>
    public static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(20);

    /// <summary>
    /// User-facing message for a shell-reported IFrame API error code.
    /// Unknown/null codes get a generic message — the bar must stay meaningful for codes this
    /// policy has never seen.
    /// </summary>
    public static string Describe(string? code) => code switch
    {
        CodeEmbedDisabled or CodeEmbedDisabledDisguised =>
            "This video doesn't allow embedded playback.",
        CodeNotFound =>
            "This video is unavailable (it may be private or removed).",
        CodeInvalidParam =>
            "This video reference isn't valid.",
        CodeHtml5Error =>
            "The video can't play in the compact player right now.",
        _ =>
            "The compact player hit a playback error.",
    };

    /// <summary>
    /// Whether a later shell state report should clear a showing error: only an actually-playing
    /// state counts (e.g. a playlist auto-advanced past a dead entry, or the user retried inside
    /// the YouTube UI). Buffering/paused/cued don't prove recovery.
    /// </summary>
    public static bool ShouldAutoDismiss(int playerState) => playerState == StatePlaying;
}
