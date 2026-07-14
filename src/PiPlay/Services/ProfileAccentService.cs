using PiPlay.Models;
using PiPlay.Theme;

namespace PiPlay.Services;

/// <summary>Pure helpers for active profile identity color state.</summary>
public static class ProfileAccentService
{
    public static Profile? ActiveProfile(AppSettings settings) =>
        settings.ActiveProfileName is null ? null : ProfileService.Find(settings, settings.ActiveProfileName);

    /// <summary>
    /// The accent the app actually paints with (roadmap P2). The active profile's identity color IS the
    /// app accent; the global accent is the fallback, used when no profile is active, when the active
    /// profile carries no color of its own, or when its stored value is unusable. A colorless profile
    /// must inherit the global accent rather than blank the app out.
    /// </summary>
    /// <remarks>
    /// Returns the stored hex EXACTLY. Dark colors are lifted for visibility by
    /// <see cref="ThemeColors.DeriveAccentSet"/> at paint time — that presentation step must never leak
    /// back into the stored value.
    /// </remarks>
    public static string ResolvedAccentColor(AppSettings settings, string globalAccent) =>
        ProfileService.NormalizeAccentForStorage(ActiveProfile(settings)?.AccentColor)
        ?? ThemeCatalog.NormalizeAccentColor(globalAccent);

    public static void SetActiveProfile(AppSettings settings, Profile? profile) =>
        settings.ActiveProfileName = profile?.Name;

    public static void ClearActiveProfile(AppSettings settings) => settings.ActiveProfileName = null;

    public static void ClearActiveProfileIfMatches(AppSettings settings, string name)
    {
        if (string.Equals(settings.ActiveProfileName, name, StringComparison.OrdinalIgnoreCase))
            settings.ActiveProfileName = null;
    }

    public static void RenameActiveProfileIfMatches(AppSettings settings, string oldName, string newName)
    {
        if (string.Equals(settings.ActiveProfileName, oldName, StringComparison.OrdinalIgnoreCase))
            settings.ActiveProfileName = newName;
    }

    public static void ReconcileActiveProfile(AppSettings settings)
    {
        if (settings.ActiveProfileName is not null && ActiveProfile(settings) is null)
            settings.ActiveProfileName = null;
    }

    /// <summary>
    /// Commits a color picked in Settings to whatever is actually painting the app (P2, spec decision 7):
    /// the active profile's own color if it has one, otherwise the global default.
    /// <para>
    /// Writing unconditionally to the global would be the bug: with a colored profile overriding it, the
    /// user would pick a color, watch it preview live, press Done — and the app would snap back to the
    /// profile's color. Editing the value that is driving the app keeps the preview truthful. The Settings
    /// hint (<c>accentEditContext</c>) names the target, so this is labelled, not silent.
    /// </para>
    /// </summary>
    /// <returns>The normalized color that was stored.</returns>
    public static string CommitAccent(AppSettings settings, string hex)
    {
        var normalized = ProfileService.NormalizeAccentForStorage(hex) ?? ThemeCatalog.DefaultAccentColor;

        if (AccentOverridingProfile(settings) is { } profile) profile.AccentColor = normalized;
        else settings.Theme.AccentColor = normalized;

        return normalized;
    }

    /// <summary>
    /// The active profile ONLY when it carries its own valid color — i.e. when it is overriding the
    /// global accent. Null when no profile is active, or the active one inherits the global.
    /// </summary>
    public static Profile? AccentOverridingProfile(AppSettings settings) =>
        ActiveProfile(settings) is { } p && ProfileService.NormalizeAccentForStorage(p.AccentColor) is not null
            ? p
            : null;
}
