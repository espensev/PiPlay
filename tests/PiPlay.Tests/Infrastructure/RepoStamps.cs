using System.IO;

namespace PiPlay.Tests;

/// <summary>
/// The committed release identity (<c>VERSION</c> / <c>BUILD_NUMBER</c> at the repository root).
/// Tests that check a stamped value compare against these files instead of literals, so a release
/// bump cannot leave a pinned expectation behind.
/// </summary>
internal static class RepoStamps
{
    public static string RepoRoot { get; } = FindRepoRoot();

    public static string Version => File.ReadAllText(Path.Combine(RepoRoot, "VERSION")).Trim();

    public static int BuildNumber => int.Parse(File.ReadAllText(Path.Combine(RepoRoot, "BUILD_NUMBER")).Trim());

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PiPlay.sln")))
            dir = dir.Parent;
        return dir?.FullName
            ?? throw new InvalidOperationException("PiPlay.sln was not found above the test output directory.");
    }
}
