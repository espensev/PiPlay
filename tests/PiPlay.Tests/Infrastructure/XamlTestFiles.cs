using System.IO;
using System.Xml.Linq;

namespace PiPlay.Tests;

/// <summary>Locates and loads the source .xaml files as XML for markup-invariant assertions.</summary>
internal static class XamlTestFiles
{
    public static readonly XNamespace Pres = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    public static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>Absolute path to <c>src/PiPlay</c>, found by walking up to the repo root (PiPlay.sln).</summary>
    public static string SrcDir { get; } = ResolveSrcDir();

    public static XDocument Load(string fileName) =>
        XDocument.Load(Path.Combine(SrcDir, fileName.Replace('/', Path.DirectorySeparatorChar)), LoadOptions.SetLineInfo);

    private static string ResolveSrcDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PiPlay.sln")))
            dir = dir.Parent;
        if (dir is null)
            throw new InvalidOperationException("Could not locate repo root (PiPlay.sln) from " + AppContext.BaseDirectory);
        return Path.Combine(dir.FullName, "src", "PiPlay");
    }
}
