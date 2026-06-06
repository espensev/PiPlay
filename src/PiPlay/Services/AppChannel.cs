using System.Reflection;

namespace PiPlay.Services;

/// <summary>Release channels PiPlay can be built and run as.</summary>
public enum PiPlayChannel
{
    /// <summary>The normal dev/installed app: %LOCALAPPDATA%\PiPlay data, the original single-instance identity.</summary>
    Default,

    /// <summary>A stable copy deployed for side-by-side test use (spec: stable publish). Portable data
    /// beside the exe and its own single-instance identity so it never collides with the dev app.</summary>
    Stable,
}

/// <summary>
/// Resolves which <see cref="PiPlayChannel"/> this binary is. The channel is baked into the
/// assembly at build time (<c>-p:PiPlayChannel=Stable</c> → an <c>[AssemblyMetadata("PiPlay.Channel", …)]</c>
/// attribute) so a copied exe stays unmistakably itself. A <c>PIPLAY_CHANNEL</c> environment variable is
/// honored first as a diagnostic/test override, mirroring how <see cref="AppPaths"/> honors
/// <c>PIPLAY_DATA_ROOT</c>. Resolved per access so an override set at process start is always picked up.
/// </summary>
public static class AppChannel
{
    /// <summary>Build-time metadata key written by <c>&lt;AssemblyMetadata Include="PiPlay.Channel" …/&gt;</c>.</summary>
    public const string MetadataKey = "PiPlay.Channel";

    public static PiPlayChannel Current => Resolve(
        Environment.GetEnvironmentVariable("PIPLAY_CHANNEL"),
        ReadBakedChannel());

    /// <summary>Channel name (e.g. "Stable") used in identifiers and the window title.</summary>
    public static string Name => Current.ToString();

    /// <summary>
    /// True when <paramref name="channel"/> keeps its data beside the exe rather than in the user profile.
    /// Single source of truth for "which channels are portable"; <see cref="AppPaths.ResolveRoot"/> uses it
    /// so the portability rule lives in exactly one place.
    /// </summary>
    public static bool IsPortableChannel(PiPlayChannel channel) => channel == PiPlayChannel.Stable;

    /// <summary>
    /// Pure resolution: the env override wins (diagnostic/test), then the baked build value,
    /// then <see cref="PiPlayChannel.Default"/>. Anything that is not "Stable" (case-insensitive) is Default.
    /// </summary>
    internal static PiPlayChannel Resolve(string? envOverride, string? bakedChannel)
    {
        if (!string.IsNullOrWhiteSpace(envOverride)) return Parse(envOverride);
        return Parse(bakedChannel);
    }

    internal static PiPlayChannel Parse(string? raw) =>
        string.Equals(raw?.Trim(), nameof(PiPlayChannel.Stable), StringComparison.OrdinalIgnoreCase)
            ? PiPlayChannel.Stable
            : PiPlayChannel.Default;

    private static string? ReadBakedChannel()
    {
        var asm = Assembly.GetEntryAssembly() ?? typeof(AppChannel).Assembly;
        foreach (var meta in asm.GetCustomAttributes<AssemblyMetadataAttribute>())
        {
            if (string.Equals(meta.Key, MetadataKey, StringComparison.OrdinalIgnoreCase))
                return meta.Value;
        }
        return null;
    }
}
