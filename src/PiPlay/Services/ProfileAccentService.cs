using PiPlay.Models;
using PiPlay.Theme;

namespace PiPlay.Services;

/// <summary>Pure helpers for active profile identity color state.</summary>
public static class ProfileAccentService
{
    public static Profile? ActiveProfile(AppSettings settings) =>
        settings.ActiveProfileName is null ? null : ProfileService.Find(settings, settings.ActiveProfileName);

    public static string ResolvedAccentColor(AppSettings settings, string globalAccent) =>
        ThemeCatalog.NormalizeAccentColor(globalAccent);

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

    public static string CommitAccent(AppSettings settings, string hex)
    {
        var normalized = ProfileService.NormalizeAccentForStorage(hex) ?? ThemeCatalog.DefaultAccentColor;
        settings.Theme.AccentColor = normalized;
        return normalized;
    }
}
