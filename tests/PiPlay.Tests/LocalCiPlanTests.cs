using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class LocalCiPlanTests
{
    [Fact]
    public async Task Plan_is_side_effect_free_and_pins_the_shared_command_contract()
    {
        var first = await RunPlanAsync();
        var second = await RunPlanAsync();

        Assert.Equal(1, first.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(24, first.GetProperty("requirements").GetProperty("nodeMajor").GetInt32());
        Assert.Equal("global.json",
            first.GetProperty("requirements").GetProperty("dotnetGlobalJson").GetString());
        Assert.Equal("pwsh",
            first.GetProperty("requirements").GetProperty("powerShell").GetString());
        Assert.True(first.GetProperty("cleanupTestDataRoot").GetBoolean());

        var firstRoot = first.GetProperty("testDataRoot").GetString();
        var secondRoot = second.GetProperty("testDataRoot").GetString();
        Assert.False(string.IsNullOrWhiteSpace(firstRoot));
        Assert.False(string.IsNullOrWhiteSpace(secondRoot));
        Assert.NotEqual(firstRoot, secondRoot);
        Assert.False(Directory.Exists(firstRoot));
        Assert.False(Directory.Exists(secondRoot));

        var steps = first.GetProperty("steps").EnumerateArray().ToArray();
        Assert.Equal(new[] { "node-version", "dotnet-info", "restore", "test", "build" },
            steps.Select(step => step.GetProperty("name").GetString()).ToArray());

        AssertStep(steps[0], "node", "--version");
        AssertStep(steps[1], "dotnet", "--info");
        AssertStep(steps[2], "dotnet", "restore", "PiPlay.sln", "-p:BuildInParallel=false");
        AssertStep(steps[3], "dotnet", "test", "PiPlay.sln", "--configuration", "Debug", "--no-restore");
        Assert.Equal(firstRoot,
            steps[3].GetProperty("environment").GetProperty("PIPLAY_DATA_ROOT").GetString());

        Assert.Equal("pwsh", steps[4].GetProperty("filePath").GetString());
        var buildArguments = Arguments(steps[4]);
        Assert.Equal("-NoProfile", buildArguments[0]);
        Assert.Equal("-File", buildArguments[1]);
        Assert.Equal("Build-PiPlay.ps1", Path.GetFileName(buildArguments[2]));
        Assert.Equal(new[] { "-Stage", "Build", "-NoVersionBump", "-NoBuildNumberBump" },
            buildArguments[3..]);
    }

    private static void AssertStep(JsonElement step, string filePath, params string[] arguments)
    {
        Assert.Equal(filePath, step.GetProperty("filePath").GetString());
        Assert.Equal(arguments, Arguments(step));
    }

    private static string[] Arguments(JsonElement step) =>
        step.GetProperty("arguments").EnumerateArray()
            .Select(argument => argument.GetString() ?? string.Empty)
            .ToArray();

    private static async Task<JsonElement> RunPlanAsync()
    {
        var repoRoot = FindRepoRoot();
        var scriptPath = Path.Combine(repoRoot, "scripts", "Test-LocalCI.ps1");
        Assert.True(File.Exists(scriptPath), $"Local CI script was not found at {scriptPath}.");

        var startInfo = new ProcessStartInfo
        {
            FileName = "pwsh",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(scriptPath);
        startInfo.ArgumentList.Add("-Plan");
        startInfo.ArgumentList.Add("-AsJson");

        using var process = new Process { StartInfo = startInfo };
        Assert.True(process.Start(), "Local CI plan process did not start.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();

        var exitTask = process.WaitForExitAsync();
        var completed = await Task.WhenAny(exitTask, Task.Delay(TimeSpan.FromSeconds(15)));
        if (completed != exitTask)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* best-effort timeout cleanup */ }
            Assert.Fail("Local CI plan exceeded the 15-second deterministic-test budget.");
        }

        await exitTask;
        var output = await standardOutput;
        var error = await standardError;
        Assert.True(process.ExitCode == 0,
            $"Local CI plan exited with code {process.ExitCode}.{Environment.NewLine}{error}{Environment.NewLine}{output}");

        using var document = JsonDocument.Parse(output);
        return document.RootElement.Clone();
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PiPlay.sln")))
            directory = directory.Parent;

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate the PiPlay repository root.");
    }
}
