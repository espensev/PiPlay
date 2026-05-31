using System.IO;
using System.Runtime.CompilerServices;

namespace PiPlay.Tests;

/// <summary>
/// Point PiPlay's on-disk root at a throwaway temp dir for the entire test process, so any
/// code that touches <see cref="PiPlay.Services.AppPaths"/> (e.g. constructing MainWindow in
/// Layer 3) never reads or writes the developer's real %LOCALAPPDATA%\PiPlay. Runs before any
/// test via <see cref="ModuleInitializerAttribute"/>.
/// </summary>
internal static class TestDataRoot
{
    [ModuleInitializer]
    public static void Init()
    {
        // Don't clobber an override the caller set deliberately.
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PIPLAY_DATA_ROOT")))
            return;
        var dir = Path.Combine(Path.GetTempPath(), "PiPlayTests", "data-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Environment.SetEnvironmentVariable("PIPLAY_DATA_ROOT", dir);
    }
}
