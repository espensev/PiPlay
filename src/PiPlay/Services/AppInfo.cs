using System.Reflection;

namespace PiPlay.Services;

/// <summary>
/// Display-facing build identity: the clean semantic version, the pipeline-stamped build number,
/// and the channel-aware window title. The title for the <see cref="PiPlayChannel.Default"/> channel
/// stays exactly "PiPlay" so the normal app is visually unchanged; non-default channels (e.g. Stable)
/// surface their channel + version so a deployed copy is differentiable at a glance.
/// </summary>
public static class AppInfo
{
    public static PiPlayChannel Channel => AppChannel.Current;

    /// <summary>Clean semantic version from <c>AssemblyInformationalVersion</c> (no +git-sha; see csproj), e.g. "0.3.0".</summary>
    public static string Version => ReadInformationalVersion();

    /// <summary>Monotonic build number = the 4th octet of FileVersion stamped by the pipeline; 0 when unstamped.</summary>
    public static int BuildNumber => ReadBuildNumber();

    /// <summary>Window/taskbar title for this build's channel.</summary>
    public static string WindowTitle => FormatTitle(Channel, Version, BuildNumber);

    /// <summary>
    /// Pure title formatter. Default channel -> "PiPlay" (unchanged). Other channels ->
    /// "PiPlay - &lt;Channel&gt; v&lt;version&gt; (b&lt;build&gt;)" (with an em dash), dropping the
    /// build segment when it is 0 and the version segment when blank.
    /// </summary>
    internal static string FormatTitle(PiPlayChannel channel, string version, int buildNumber)
    {
        if (channel == PiPlayChannel.Default) return "PiPlay";

        var build = buildNumber > 0 ? $" (b{buildNumber})" : string.Empty;
        var ver = string.IsNullOrWhiteSpace(version) ? string.Empty : $" v{version}";
        return $"PiPlay — {channel}{ver}{build}";
    }

    private static string ReadInformationalVersion()
    {
        var asm = Assembly.GetEntryAssembly() ?? typeof(AppInfo).Assembly;
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(info))
        {
            var v = asm.GetName().Version;
            return v is null ? "0.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
        }

        // Defensive: strip any build-metadata suffix even though the SDK is told not to append one.
        var plus = info.IndexOf('+');
        return plus >= 0 ? info[..plus] : info;
    }

    private static int ReadBuildNumber()
    {
        var asm = Assembly.GetEntryAssembly() ?? typeof(AppInfo).Assembly;
        var fileVersion = asm.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
        // Fully qualified: the bare name "Version" would bind to this class's own string Version property.
        if (System.Version.TryParse(fileVersion, out var fv) && fv.Revision > 0) return fv.Revision;

        var assemblyVersion = asm.GetName().Version;
        return assemblyVersion is { Revision: > 0 } ? assemblyVersion.Revision : 0;
    }
}
