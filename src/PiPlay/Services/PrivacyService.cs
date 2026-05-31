using Microsoft.Web.WebView2.Core;

namespace PiPlay.Services;

/// <summary>
/// Privacy actions (spec 19, Phase 2). Owns the user-facing wording for both actions as
/// constants so "worded separately" (REQ-PRIVACY-02) and the login-kept / signed-out promises
/// are regression-testable, plus the browsing-data scope and a thin clear adapter. The Settings
/// window binds its visible text to these constants; MainWindow performs the work.
/// </summary>
public static class PrivacyService
{
    /// <summary>Clearing AllProfile (cookies + cache + site storage) signs the user out of YouTube.</summary>
    public const CoreWebView2BrowsingDataKinds ClearKinds = CoreWebView2BrowsingDataKinds.AllProfile;

    // --- Reset app state (REQ-PRIVACY-01) — keeps the YouTube session ---
    public const string ResetActionLabel = "Reset app state";
    public const string ResetDescription =
        "Clears PiPlay's settings, saved profiles, and window placement. You'll stay signed in to YouTube.";
    public const string ResetConfirmTitle = "Reset app state?";
    public const string ResetConfirmBody =
        "This clears PiPlay's settings, saved profiles, and window placement.\n\nYou'll stay signed in to YouTube.";
    public const string ResetConfirmButton = "Reset app state";
    public const string ResetDoneTitle = "App state reset";
    public const string ResetDoneBody = "PiPlay's settings were reset. You're still signed in to YouTube.";

    // --- Clear browser data (REQ-PRIVACY-02) — separate, confirmed, signs the user out ---
    public const string ClearActionLabel = "Clear browser data";
    public const string ClearDescription =
        "Signs you out of YouTube and clears PiPlay's browsing data — cookies, cache, and site data. " +
        "You'll need to sign in again.";
    public const string ClearConfirmTitle = "Clear browser data?";
    public const string ClearConfirmBody =
        "This signs you out of YouTube and clears PiPlay's browsing data — cookies, cache, and site data." +
        "\n\nYou'll need to sign in again next time.";
    public const string ClearConfirmButton = "Clear browser data";
    public const string ClearDoneTitle = "Browser data cleared";
    public const string ClearDoneBody = "Browser data cleared. You've been signed out of YouTube.";
    public const string ClearBrowserNotReady = "PiPlay's browser isn't ready yet. Try again in a moment.";
    public const string ClearFailed = "PiPlay couldn't clear the browser data. Please try again.";

    /// <summary>Clear the shared WebView2 profile's browsing data (signs the user out). Needs a live core.</summary>
    public static Task ClearBrowserDataAsync(CoreWebView2 core) =>
        core.Profile.ClearBrowsingDataAsync(ClearKinds);
}
