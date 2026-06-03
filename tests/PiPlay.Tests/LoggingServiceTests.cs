using PiPlay.Services;

namespace PiPlay.Tests;

/// <summary>
/// Locks the URL-redaction contract (spec §18): logs must never carry the query string, which
/// can hold tokens, timestamps, or playlist context. Surfaced by the spec-conformance review
/// as untested security-relevant logic.
/// </summary>
[Trait(TestCategories.Key, TestCategories.Logic)]
public class LoggingServiceTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void RedactUrl_handles_empty(string? url)
    {
        Assert.Equal("(none)", Log.RedactUrl(url));
    }

    [Fact]
    public void RedactUrl_drops_the_query_string_for_valid_urls()
    {
        var redacted = Log.RedactUrl("https://www.youtube.com/watch?v=dQw4w9WgXcQ&t=30s");
        Assert.Equal("https://www.youtube.com/watch", redacted);
        Assert.DoesNotContain("dQw4w9WgXcQ", redacted); // the video id lives in the query
    }

    [Fact]
    public void RedactUrl_never_leaks_an_auth_token()
    {
        var redacted = Log.RedactUrl("https://accounts.google.com/signin?token=SUPERSECRET");
        Assert.Equal("https://accounts.google.com/signin", redacted);
        Assert.DoesNotContain("SUPERSECRET", redacted);
    }

    [Fact]
    public void RedactUrl_redacts_query_even_when_url_is_unparseable()
    {
        var redacted = Log.RedactUrl("not a url?token=SUPERSECRET");
        Assert.DoesNotContain("SUPERSECRET", redacted);
        Assert.Contains("<redacted>", redacted);
    }
}
