using PiPlay.Models;
using PiPlay.Services;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class ProfileAccentServiceTests
{
    // P2 (2026-07-14): the ACTIVE PROFILE drives the app accent. This reverses the v0.6.0 split, which
    // these tests previously pinned. The global accent is now the fallback, not the always-answer.

    [Fact]
    public void Active_profile_color_drives_the_app_accent_P2()
    {
        var settings = SettingsWithProfiles();

        ProfileAccentService.SetActiveProfile(settings, settings.Profiles.Single(p => p.Name == "Violet"));
        Assert.Equal("#A78BFA", ProfileAccentService.ResolvedAccentColor(settings, "#00D4FF"));
    }

    [Fact]
    public void Profile_without_a_color_falls_back_to_the_global_accent()
    {
        var settings = SettingsWithProfiles();

        // A colorless profile must INHERIT the global accent, not blank the app out.
        ProfileAccentService.SetActiveProfile(settings, settings.Profiles.Single(p => p.Name == "Plain"));
        Assert.Equal("#00D4FF", ProfileAccentService.ResolvedAccentColor(settings, "#00D4FF"));
    }

    [Fact]
    public void No_active_profile_falls_back_to_the_global_accent()
    {
        var settings = SettingsWithProfiles();

        ProfileAccentService.ClearActiveProfile(settings);
        Assert.Equal("#00D4FF", ProfileAccentService.ResolvedAccentColor(settings, "#00D4FF"));
    }

    [Fact]
    public void Invalid_stored_profile_hex_falls_back_to_the_global_accent()
    {
        var settings = SettingsWithProfiles();
        settings.Profiles.Single(p => p.Name == "Violet").AccentColor = "not-a-color";

        // A corrupt stored value must never become the app accent.
        ProfileAccentService.SetActiveProfile(settings, settings.Profiles.Single(p => p.Name == "Violet"));
        Assert.Equal("#00D4FF", ProfileAccentService.ResolvedAccentColor(settings, "#00D4FF"));
    }

    [Fact]
    public void Very_dark_profile_color_resolves_to_its_EXACT_stored_hex()
    {
        var settings = SettingsWithProfiles();
        settings.Profiles.Single(p => p.Name == "Violet").AccentColor = "#0B0E11";

        ProfileAccentService.SetActiveProfile(settings, settings.Profiles.Single(p => p.Name == "Violet"));

        // Resolution returns the stored color EXACTLY. Contrast lifting is a PRESENTATION step that
        // happens downstream in ThemeColors.DeriveAccentSet - it must never leak back into the value.
        Assert.Equal("#0B0E11", ProfileAccentService.ResolvedAccentColor(settings, "#00D4FF"));
    }

    // Spec decision 7: the accent picker edits WHATEVER IS PAINTING THE APP, so the live preview is
    // truthful and Done always sticks. If it always wrote to the global while a colored profile was
    // overriding it, the user would pick a color, see it preview, press Done - and watch the app snap
    // back. That reads as a bug.

    [Fact]
    public void Commit_edits_the_ACTIVE_PROFILES_color_when_that_profile_is_driving_the_accent()
    {
        var settings = SettingsWithProfiles();

        ProfileAccentService.SetActiveProfile(settings, settings.Profiles.Single(p => p.Name == "Violet"));
        ProfileAccentService.CommitAccent(settings, "#38D996");

        Assert.Equal("#38D996", settings.Profiles.Single(p => p.Name == "Violet").AccentColor);
        Assert.Equal("#38D996", ProfileAccentService.ResolvedAccentColor(settings, "#00D4FF"));
        // The global default is left ALONE - it is still what a colorless profile inherits.
        Assert.Equal("#00D4FF", settings.Theme.AccentColor);
    }

    [Fact]
    public void Commit_edits_the_GLOBAL_accent_when_no_profile_is_overriding_it()
    {
        var settings = SettingsWithProfiles();

        // A profile with no color of its own is inheriting the global, so the picker edits the global.
        ProfileAccentService.SetActiveProfile(settings, settings.Profiles.Single(p => p.Name == "Plain"));
        ProfileAccentService.CommitAccent(settings, "#FFC857");
        Assert.Equal("#FFC857", settings.Theme.AccentColor);
        Assert.Null(settings.Profiles.Single(p => p.Name == "Plain").AccentColor);
        Assert.Equal("#FFC857", ProfileAccentService.ResolvedAccentColor(settings, settings.Theme.AccentColor));

        // Same with no profile at all.
        ProfileAccentService.ClearActiveProfile(settings);
        ProfileAccentService.CommitAccent(settings, "#38D996");
        Assert.Equal("#38D996", settings.Theme.AccentColor);
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
        // The rename must not lose the accent - the profile is still active, still colored.
        Assert.Equal("#A78BFA", ProfileAccentService.ResolvedAccentColor(settings, "#00D4FF"));

        ProfileService.Remove(settings, "Purple");
        ProfileAccentService.ReconcileActiveProfile(settings);
        Assert.Null(settings.ActiveProfileName);
        // Deleting the active profile must fall the app back to the global accent, not strand it.
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
