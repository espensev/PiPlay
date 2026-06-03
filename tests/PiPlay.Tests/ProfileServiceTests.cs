using PiPlay.Models;
using PiPlay.Services;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class ProfileServiceTests
{
    private static AppSettings WithProfiles(params string[] names)
    {
        var s = new AppSettings();
        foreach (var n in names)
            s.Profiles.Add(new Profile { Name = n, Url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ" });
        return s;
    }

    [Fact]
    public void Find_and_Exists_are_case_insensitive()
    {
        var s = WithProfiles("Lo-fi");
        Assert.True(ProfileService.Exists(s, "lo-fi"));
        Assert.NotNull(ProfileService.Find(s, "LO-FI"));
        Assert.False(ProfileService.Exists(s, "jazz"));
    }

    [Fact]
    public void Save_new_appends_and_returns_false()
    {
        var s = WithProfiles();
        var replaced = ProfileService.Save(s, new Profile { Name = "Lo-fi", Url = "https://youtu.be/dQw4w9WgXcQ" });
        Assert.False(replaced);
        Assert.Single(s.Profiles);
    }

    [Fact]
    public void Save_existing_overwrites_by_name_and_returns_true()
    {
        var s = WithProfiles("Lo-fi");
        var replaced = ProfileService.Save(s,
            new Profile { Name = "lo-fi", Url = "https://youtu.be/new12345678", Topmost = true });
        Assert.True(replaced);
        Assert.Single(s.Profiles);
        Assert.Equal("https://youtu.be/new12345678", s.Profiles[0].Url);
    }

    [Fact]
    public void Remove_returns_true_only_when_present()
    {
        var s = WithProfiles("Lo-fi");
        Assert.True(ProfileService.Remove(s, "Lo-fi"));
        Assert.False(ProfileService.Remove(s, "Lo-fi"));
        Assert.Empty(s.Profiles);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("not a url", false)]
    [InlineData("https://example.com/watch?v=dQw4w9WgXcQ", false)]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", true)]
    [InlineData("https://youtu.be/dQw4w9WgXcQ", true)]
    public void ValidateUrl_accepts_only_supported_youtube_urls(string? url, bool ok)
    {
        Assert.Equal(ok, ProfileService.ValidateUrl(url).Ok);
    }
}
