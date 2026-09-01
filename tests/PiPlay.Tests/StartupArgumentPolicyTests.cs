using PiPlay.Services;

namespace PiPlay.Tests;

// REQ-APP-02: executable help is an early, side-effect-free startup decision.
[Trait(TestCategories.Key, TestCategories.Logic)]
public class StartupArgumentPolicyTests
{
    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("/?")]
    public void Exact_help_alias_selects_help(string alias)
    {
        var request = StartupArgumentPolicy.Parse([alias]);

        Assert.Equal(StartupAction.ShowHelp, request.Action);
        Assert.Null(request.LaunchUrl);
    }

    [Fact]
    public void Help_wins_when_a_url_appears_first()
    {
        var request = StartupArgumentPolicy.Parse(
            ["https://www.youtube.com/watch?v=dQw4w9WgXcQ", "--help"]);

        Assert.Equal(StartupAction.ShowHelp, request.Action);
        Assert.Null(request.LaunchUrl);
    }

    [Fact]
    public void Repeated_help_among_unknown_arguments_is_still_one_help_request()
    {
        var request = StartupArgumentPolicy.Parse(["unknown", "-h", "--help", "/?"]);

        Assert.Equal(StartupAction.ShowHelp, request.Action);
        Assert.Null(request.LaunchUrl);
    }

    [Theory]
    [InlineData("--Help")]
    [InlineData("-H")]
    [InlineData("help")]
    [InlineData("--help=x")]
    [InlineData("foo--help")]
    public void Near_match_does_not_select_help(string argument)
    {
        var request = StartupArgumentPolicy.Parse([argument]);

        Assert.Equal(StartupAction.Launch, request.Action);
        Assert.Null(request.LaunchUrl);
    }

    [Fact]
    public void Help_text_inside_a_url_does_not_select_help()
    {
        const string url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ&note=--help";

        var request = StartupArgumentPolicy.Parse([url]);

        Assert.Equal(StartupAction.Launch, request.Action);
        Assert.Equal(url, request.LaunchUrl);
    }

    [Fact]
    public void Empty_argument_list_starts_normally_without_a_url()
    {
        var request = StartupArgumentPolicy.Parse([]);

        Assert.Equal(StartupAction.Launch, request.Action);
        Assert.Null(request.LaunchUrl);
    }

    [Fact]
    public void No_launch_candidate_starts_normally_without_a_url()
    {
        var request = StartupArgumentPolicy.Parse(["unknown", "--not-an-option"]);

        Assert.Equal(StartupAction.Launch, request.Action);
        Assert.Null(request.LaunchUrl);
    }

    [Fact]
    public void First_launch_candidate_wins()
    {
        var request = StartupArgumentPolicy.Parse(
            ["unknown", "https://example.com/", "youtu.be/dQw4w9WgXcQ"]);

        Assert.Equal(StartupAction.Launch, request.Action);
        Assert.Equal("youtu.be/dQw4w9WgXcQ", request.LaunchUrl);
    }

    [Theory]
    [InlineData("http://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ?t=42")]
    [InlineData("https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ")]
    public void Supported_explicit_target_is_preserved(string url)
    {
        var request = StartupArgumentPolicy.Parse([url]);

        Assert.Equal(StartupAction.Launch, request.Action);
        Assert.Equal(url, request.LaunchUrl);
    }

    [Theory]
    [InlineData("youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("youtu.be/dQw4w9WgXcQ")]
    [InlineData("www.youtube-nocookie.com/embed/dQw4w9WgXcQ")]
    [InlineData("dQw4w9WgXcQ")]
    public void Supported_scheme_less_target_is_preserved_for_shared_navigation_parsing(string url)
    {
        var request = StartupArgumentPolicy.Parse([url]);

        Assert.Equal(StartupAction.Launch, request.Action);
        Assert.Equal(url, request.LaunchUrl);
    }

    [Theory]
    [InlineData("youtube")]
    [InlineData("youtu-not-a-url")]
    [InlineData("http://example.com/watch?v=1")]
    [InlineData("https://example.com/watch?v=1")]
    [InlineData("youtube.evil.test/watch?v=dQw4w9WgXcQ")]
    [InlineData("youtu.be.evil.test/dQw4w9WgXcQ")]
    [InlineData("evil.test@youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("youtube.com@evil.test/watch?v=dQw4w9WgXcQ")]
    [InlineData("ftp://youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("youtube://evil.test/path")]
    public void Scheme_less_youtube_lookalike_is_not_a_launch_candidate(string argument)
    {
        var request = StartupArgumentPolicy.Parse([argument]);

        Assert.Equal(StartupAction.Launch, request.Action);
        Assert.Null(request.LaunchUrl);
    }

    [Fact]
    public void Help_dispatch_shows_usage_then_exits_zero_without_normal_startup()
    {
        var events = new List<string>();
        string? shownText = null;

        StartupDispatcher.Dispatch(
            new StartupRequest(StartupAction.ShowHelp, null),
            text =>
            {
                shownText = text;
                events.Add("show");
            },
            exitCode => events.Add($"shutdown:{exitCode}"),
            _ => events.Add("normal"));

        Assert.Equal(["show", "shutdown:0"], events);
        Assert.NotNull(shownText);
        Assert.Contains("PiPlay.exe --help", shownText, StringComparison.Ordinal);
        Assert.Contains("PiPlay.exe -h", shownText, StringComparison.Ordinal);
        Assert.Contains("PiPlay.exe /?", shownText, StringComparison.Ordinal);
    }

    [Fact]
    public void Launch_dispatch_enters_normal_startup_once_without_showing_help_or_shutdown()
    {
        const string url = "https://www.youtube.com/";
        var events = new List<string>();

        StartupDispatcher.Dispatch(
            new StartupRequest(StartupAction.Launch, url),
            _ => events.Add("show"),
            exitCode => events.Add($"shutdown:{exitCode}"),
            launchUrl => events.Add($"normal:{launchUrl}"));

        Assert.Equal(["normal:https://www.youtube.com/"], events);
    }
}
