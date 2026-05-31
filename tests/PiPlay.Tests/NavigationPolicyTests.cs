using PiPlay.Services;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class NavigationPolicyTests
{
    private static Uri U(string s) => new(s);

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=abc")]
    [InlineData("https://m.youtube.com/")]
    [InlineData("https://music.youtube.com/")]
    [InlineData("https://youtu.be/abc")]
    [InlineData("https://www.youtube-nocookie.com/embed/abc")]
    public void Youtube_allowed_on_both_surfaces(string url)
    {
        Assert.True(NavigationPolicy.IsAllowed(U(url), NavigationSurface.Source));
        Assert.True(NavigationPolicy.IsAllowed(U(url), NavigationSurface.Player));
    }

    [Theory]
    [InlineData("https://accounts.google.com/signin")]
    [InlineData("https://consent.google.com/")]
    [InlineData("https://myaccount.google.com/")]
    [InlineData("https://signin.google.com/")]
    // Regional sign-in domains must work too - a real login must never be bounced out.
    [InlineData("https://accounts.google.no/accounts/SetSID")]
    [InlineData("https://consent.google.de/")]
    [InlineData("https://accounts.google.co.uk/")]
    public void Google_auth_allowed_on_both_surfaces(string url)
    {
        // The allowlist is a guardrail, not a blocker: Google sign-in is allowed on the Source
        // Window and the Popout Player alike so a login/consent redirect never dead-ends.
        Assert.True(NavigationPolicy.IsAllowed(U(url), NavigationSurface.Source));
        Assert.True(NavigationPolicy.IsAllowed(U(url), NavigationSurface.Player));
    }

    [Theory]
    [InlineData("https://example.com/")]
    [InlineData("https://www.google.com/search?q=x")]  // Google Search is not sign-in - still external.
    [InlineData("https://maps.google.com/")]
    [InlineData("https://evil.youtube.com.attacker.test/")]
    [InlineData("ftp://youtube.com/")]
    public void Other_sites_blocked_everywhere(string url)
    {
        Assert.False(NavigationPolicy.IsAllowed(U(url), NavigationSurface.Source));
        Assert.False(NavigationPolicy.IsAllowed(U(url), NavigationSurface.Player));
    }

    [Fact]
    public void Lookalike_youtube_host_is_not_allowed()
    {
        Assert.False(NavigationPolicy.IsYouTubeHost("notyoutube.com"));
        Assert.False(NavigationPolicy.IsYouTubeHost("youtube.com.evil.test"));
        Assert.True(NavigationPolicy.IsYouTubeHost("www.youtube.com"));
        Assert.True(NavigationPolicy.IsYouTubeHost("youtube.com"));
    }

    [Fact]
    public void Google_auth_host_matching_is_precise()
    {
        // Real sign-in hosts across TLDs.
        Assert.True(NavigationPolicy.IsGoogleAuthHost("accounts.google.com"));
        Assert.True(NavigationPolicy.IsGoogleAuthHost("accounts.google.no"));
        Assert.True(NavigationPolicy.IsGoogleAuthHost("accounts.google.co.uk"));

        // Look-alikes and non-auth Google hosts must not match.
        Assert.False(NavigationPolicy.IsGoogleAuthHost("accounts.google.com.evil.test"));
        Assert.False(NavigationPolicy.IsGoogleAuthHost("accounts.notgoogle.com"));
        Assert.False(NavigationPolicy.IsGoogleAuthHost("www.google.com"));
        Assert.False(NavigationPolicy.IsGoogleAuthHost("google.com"));
    }

    // --- In-app runtime schemes + null guard (relied on by both NavigationStarting handlers) ---

    [Theory]
    [InlineData("about:blank")]
    [InlineData("data:text/html,<p>x</p>")]
    [InlineData("blob:https://www.youtube.com/abc")]
    public void Inapp_runtime_schemes_are_allowed(string url)
    {
        Assert.True(NavigationPolicy.IsAllowed(U(url), NavigationSurface.Source));
        Assert.True(NavigationPolicy.IsAllowed(U(url), NavigationSurface.Player));
    }

    [Fact]
    public void Null_uri_is_not_allowed()
    {
        Assert.False(NavigationPolicy.IsAllowed(null, NavigationSurface.Source));
    }
}
