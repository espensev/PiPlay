namespace PiPlay.Services;

/// <summary>Pure final gate immediately before a WebView drag request enters the native move loop.</summary>
internal static class PlayerSurfaceDragPolicy
{
    internal static bool CanBegin(bool isClosing, bool isNormalWindowState, bool leftButtonDown) =>
        !isClosing && isNormalWindowState && leftButtonDown;
}
