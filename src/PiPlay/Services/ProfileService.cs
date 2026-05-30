using PiPlay.Models;

namespace PiPlay.Services;

/// <summary>
/// Basic profile management for the MVP (spec 17): find, upsert (overwrite by name),
/// remove, and graceful URL validation. Profiles live in <see cref="AppSettings.Profiles"/>;
/// persistence is owned by <see cref="SettingsService"/>.
/// </summary>
public static class ProfileService
{
    public static Profile? Find(AppSettings settings, string name) =>
        settings.Profiles.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

    public static bool Exists(AppSettings settings, string name) => Find(settings, name) is not null;

    /// <summary>Insert or overwrite a profile by name (case-insensitive). Returns true if an existing one was replaced.</summary>
    public static bool Save(AppSettings settings, Profile profile)
    {
        var existing = Find(settings, profile.Name);
        if (existing is not null)
        {
            settings.Profiles.Remove(existing);
            settings.Profiles.Add(profile);
            return true;
        }
        settings.Profiles.Add(profile);
        return false;
    }

    public static bool Remove(AppSettings settings, string name)
    {
        var existing = Find(settings, name);
        if (existing is null) return false;
        settings.Profiles.Remove(existing);
        return true;
    }

    /// <summary>Validate a profile URL. Broken URLs must fail gracefully, even in the MVP (spec 17).</summary>
    public static (bool Ok, string? Error) ValidateUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return (false, "Enter a YouTube URL.");
        if (!YouTubeUrlHelper.TryParse(url, out _))
            return (false, "That doesn't look like a supported YouTube video or playlist URL.");
        return (true, null);
    }
}
