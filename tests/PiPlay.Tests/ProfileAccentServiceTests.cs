using PiPlay.Models;
using PiPlay.Services;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class ProfileAccentServiceTests
{
    [Fact]
    public void Resolved_accent_uses_active_profile_override_else_global()
    {
        var settings = SettingsWithProfiles();

        ProfileAccentService.SetActiveProfile(settings, settings.Profiles.Single(p => p.Name == "Violet"));
        Assert.Equal("#A78BFA", ProfileAccentService.ResolvedAccentColor(settings, "#00D4FF"));

        ProfileAccentService.SetActiveProfile(settings, settings.Profiles.Single(p => p.Name == "Plain"));
        Assert.Equal("#00D4FF", ProfileAccentService.ResolvedAccentColor(settings, "#00D4FF"));
    }

    [Fact]
    public void Commit_routes_to_profile_only_when_profile_overrides()
    {
        var settings = SettingsWithProfiles();

        ProfileAccentService.SetActiveProfile(settings, settings.Profiles.Single(p => p.Name == "Violet"));
        ProfileAccentService.CommitAccent(settings, "#38D996");
        Assert.Equal("#38D996", settings.Profiles.Single(p => p.Name == "Violet").AccentColor);
        Assert.Equal("#00D4FF", settings.Theme.AccentColor);

        ProfileAccentService.SetActiveProfile(settings, settings.Profiles.Single(p => p.Name == "Plain"));
        ProfileAccentService.CommitAccent(settings, "#FFC857");
        Assert.Equal("#FFC857", settings.Theme.AccentColor);
        Assert.Null(settings.Profiles.Single(p => p.Name == "Plain").AccentColor);
    }

    [Fact]
    public void Active_profile_delete_and_rename_reconcile_accent()
    {
        var settings = new AppSettings
        {
            ActiveProfileName = "Violet",
            Theme = new ThemeSettings { AccentColor = "#00D4FF" },
            Profiles =
            {
                new Profile { Name = "Violet", Url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ", AccentColor = "#A78BFA" },
            },
        };

        Assert.Equal("#A78BFA", ProfileAccentService.ResolvedAccentColor(settings, "#00D4FF"));

        settings.Profiles.Single().Name = "Purple";
        ProfileAccentService.RenameActiveProfileIfMatches(settings, "Violet", "Purple");
        Assert.Equal("Purple", settings.ActiveProfileName);
        Assert.Equal("#A78BFA", ProfileAccentService.ResolvedAccentColor(settings, "#00D4FF"));

        ProfileService.Remove(settings, "Purple");
        ProfileAccentService.ReconcileActiveProfile(settings);
        Assert.Null(settings.ActiveProfileName);
        Assert.Equal("#00D4FF", ProfileAccentService.ResolvedAccentColor(settings, "#00D4FF"));
    }

    private static AppSettings SettingsWithProfiles() => new()
    {
        Theme = new ThemeSettings { AccentColor = "#00D4FF" },
        Profiles =
        {
            new Profile { Name = "Violet", Url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ", AccentColor = "#A78BFA" },
            new Profile { Name = "Plain", Url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ" },
        },
    };
}
