namespace PiPlay.Services;

/// <summary>
/// Pure decision logic for the Popout Player controls fade (spec 11, Phase 2). Kept free of
/// WPF types so the idle/hover state machine can be unit tested without a window. The
/// <see cref="PlayerWindow"/> feeds it live input (pointer-over, dragging, idle-timer state)
/// and animates the chrome strip based on the result.
/// </summary>
public static class FadePolicy
{
    /// <summary>Idle delay before the controls fade out, in milliseconds.</summary>
    public const int IdleDelayMs = 2500;

    /// <summary>Opacity animation duration for show/hide, in milliseconds.</summary>
    public const int FadeDurationMs = 150;

    /// <summary>
    /// Whether the chrome strip should be hidden right now.
    /// Hiding requires fade to be enabled AND nothing keeping the controls up:
    /// the pointer is away, no drag is in progress, and the idle delay has elapsed.
    /// </summary>
    public static bool ShouldHide(bool fadeEnabled, bool isPointerOver, bool isDragging, bool idleElapsed)
    {
        if (!fadeEnabled) return false;   // fade off: controls always visible (MVP behavior)
        if (isPointerOver) return false;  // never hide under the cursor
        if (isDragging) return false;     // never hide mid-drag
        return idleElapsed;               // hide only once idle
    }

    /// <summary>
    /// Whether the chrome strip should be fully visible and hit-testable right now.
    /// Always true when fade is disabled; otherwise true whenever the user is engaging
    /// (pointer over or dragging) so controls are ready the moment they are needed.
    /// </summary>
    public static bool ShouldShow(bool fadeEnabled, bool isPointerOver, bool isDragging)
    {
        if (!fadeEnabled) return true;
        return isPointerOver || isDragging;
    }
}
