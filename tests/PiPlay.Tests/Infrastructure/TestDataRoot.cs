using System.IO;
using System.Runtime.CompilerServices;

namespace PiPlay.Tests;

/// <summary>
/// Process-wide test setup that must run before any test or WPF resource resolution.
/// Runs via <see cref="ModuleInitializerAttribute"/> (the earliest user-code hook).
/// Points PiPlay's on-disk root at a throwaway temp dir so anything that touches AppPaths
/// (e.g. constructing MainWindow in Layer 3) never reads/writes the real %LOCALAPPDATA%\PiPlay.
/// (Resource resolution needs no setup: the windows reference resources via assembly-qualified
/// /PiPlay;component/... URIs, which are independent of Application.ResourceAssembly.)
/// </summary>
internal static class TestDataRoot
{
    [ModuleInitializer]
    public static void Init()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PIPLAY_DATA_ROOT")))
            return;
        var dir = Path.Combine(Path.GetTempPath(), "PiPlayTests", "data-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Environment.SetEnvironmentVariable("PIPLAY_DATA_ROOT", dir);
    }
}
