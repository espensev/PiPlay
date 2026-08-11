using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using PiPlay.Services;

namespace PiPlay.Tests;

/// <summary>
/// Static invariants for the compact-player shell assets (spec 10.3): structure, the host/bridge
/// contract, no third-party origins, no credential-bearing strings, and that the build copies them
/// to the output (so a CopyToOutputDirectory misconfig fails here, not at runtime as a blank player).
/// </summary>
[Trait(TestCategories.Key, TestCategories.Markup)]
public class PlayerShellAssetTests
{
    private static string ShellDir => Path.Combine(XamlTestFiles.SrcDir, "PlayerShell");
    private static string Read(string name) => File.ReadAllText(Path.Combine(ShellDir, name));

    private static readonly Regex HttpUrl = new(@"https?://([A-Za-z0-9.\-]+)", RegexOptions.Compiled);

    // Credential-VALUE indicators (intentionally specific so "youtube-nocookie" can't false-positive).
    private static readonly string[] CredentialTokens =
    {
        "password", "client_secret", "access_token", "refresh_token", "api_key", "apikey", "bearer ",
    };

    [Fact]
    public void Shell_assets_exist()
    {
        Assert.True(File.Exists(Path.Combine(ShellDir, "player.html")));
        Assert.True(File.Exists(Path.Combine(ShellDir, "player-shell.js")));
    }

    [Fact]
    public void Player_html_hosts_the_shell_script_and_the_youtube_iframe_api()
    {
        var html = Read("player.html");
        Assert.Contains("player-shell.js", html);
        Assert.Contains("https://www.youtube.com/iframe_api", html);
        Assert.Contains("id=\"player\"", html);
    }

    [Fact]
    public void Player_html_references_no_third_party_origin()
    {
        // Only YouTube (the IFrame API / embed) and the shell's own host (piplay.local) may appear —
        // no third-party scripts/origins. The shell's own origin in a comment/CSP is not third-party.
        var html = Read("player.html");
        foreach (Match m in HttpUrl.Matches(html))
        {
            var host = m.Groups[1].Value.ToLowerInvariant();
            Assert.True(NavigationPolicy.IsYouTubeHost(host) || host == NavigationPolicy.ShellHost,
                $"player.html references a third-party origin: {host}");
        }
    }

    [Fact]
    public void Shell_js_uses_the_iframe_api_and_the_webview_string_bridge()
    {
        var js = Read("player-shell.js");
        Assert.Contains("onYouTubeIframeAPIReady", js);
        Assert.Contains("enablejsapi", js);
        Assert.Contains("origin", js);
        Assert.Contains("window.chrome.webview", js);   // host bridge, not a third-party transport
        Assert.Contains("postMessage", js);
        Assert.Contains("JSON.stringify", js);          // string transport (host reads TryGetWebMessageAsString)
    }

    [Fact]
    public void Shell_js_implements_every_protocol_message_type()
    {
        var js = Read("player-shell.js");
        foreach (var type in new[]
        {
            PlayerShellProtocol.TypeReady, PlayerShellProtocol.TypeState, PlayerShellProtocol.TypeError,
            PlayerShellProtocol.TypeRequest,
            PlayerShellProtocol.TypePlay, PlayerShellProtocol.TypePause, PlayerShellProtocol.TypeSeek,
            PlayerShellProtocol.TypeRequestState,
        })
        {
            Assert.Contains($"\"{type}\"", js);   // catches a renamed message *type*
        }
    }

    [Fact]
    public void Shell_js_uses_every_consumed_payload_field_name()
    {
        // The fields the host actually CONSUMES (a one-sided rename here silently reads 0/null
        // over the bridge). Driven from the C# constants so the field names have one source of truth.
        var js = Read("player-shell.js");
        foreach (var field in new[]
        {
            PlayerShellProtocol.FieldCurrentTime, PlayerShellProtocol.FieldPlayerState,
            PlayerShellProtocol.FieldDuration, PlayerShellProtocol.FieldCode, PlayerShellProtocol.FieldSeconds,
            PlayerShellProtocol.FieldAction, PlayerShellProtocol.FieldVideoId,
        })
        {
            Assert.Contains(field, js);   // catches field-level drift between the JS and the C# protocol
        }
    }

    [Fact]
    public void Shell_js_allowlists_exactly_the_protocol_request_actions()
    {
        // Both sides allowlist the request channel (spec 12.5 / docs/YouTube_Compliance.md): the JS set must equal
        // the C# closed set, or one side could widen the channel unilaterally.
        var js = Read("player-shell.js");
        var declaration = Regex.Match(js, @"REQUEST_ACTIONS\s*=\s*\[([^\]]*)\]");
        Assert.True(declaration.Success, "player-shell.js no longer declares REQUEST_ACTIONS.");

        var jsActions = Regex.Matches(declaration.Groups[1].Value, "\"([^\"]+)\"")
            .Select(m => m.Groups[1].Value).ToArray();
        Assert.Equal(new[]
        {
            PlayerShellProtocol.ActionClose, PlayerShellProtocol.ActionPinToggle,
            PlayerShellProtocol.ActionFullscreenToggle,
        }, jsActions);
    }

    [Fact]
    public void Shell_js_protocol_version_matches_the_csharp_contract()
    {
        // The version is declared on both sides; a one-sided bump is exactly the drift this
        // suite exists to catch (spec 10.3 versioned-contract rule).
        var declaration = Regex.Match(Read("player-shell.js"), @"PROTOCOL_VERSION\s*=\s*(\d+)");
        Assert.True(declaration.Success, "player-shell.js no longer declares PROTOCOL_VERSION.");
        Assert.Equal(PlayerShellProtocol.Version, int.Parse(declaration.Groups[1].Value));
    }

    [Fact]
    public void Shell_js_pulls_in_no_external_origin()
    {
        // All external loading is declared in player.html (the IFrame API); the script fetches nothing.
        Assert.DoesNotMatch(HttpUrl, Read("player-shell.js"));
    }

    [Fact]
    public void Shell_assets_carry_no_credential_bearing_tokens()
    {
        foreach (var name in new[] { "player.html", "player-shell.js" })
        {
            var text = Read(name).ToLowerInvariant();
            foreach (var token in CredentialTokens)
                Assert.False(text.Contains(token), $"{name} contains a credential-like token: {token}");
        }
    }

    [Fact]
    public void Csproj_copies_the_shell_assets_to_the_build_output()
    {
        // The assets are default-included as None, so the project Updates them to set the copy
        // behavior. Asserting this catches a copy misconfig here instead of as a runtime blank player.
        var csproj = XDocument.Load(Path.Combine(XamlTestFiles.SrcDir, "PiPlay.csproj"));
        foreach (var asset in new[] { @"PlayerShell\player.html", @"PlayerShell\player-shell.js" })
        {
            var item = csproj.Descendants("None")
                .FirstOrDefault(e => string.Equals((string?)e.Attribute("Update"), asset, StringComparison.OrdinalIgnoreCase));
            Assert.True(item is not null, $"PiPlay.csproj has no <None Update=\"{asset}\">.");
            var copy = item!.Elements("CopyToOutputDirectory").FirstOrDefault()?.Value;
            Assert.True(copy is "PreserveNewest" or "Always",
                $"{asset} must CopyToOutputDirectory (was '{copy}').");
        }
    }
}
