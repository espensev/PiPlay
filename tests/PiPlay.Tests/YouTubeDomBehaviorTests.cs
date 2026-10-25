using System.Diagnostics;
using System.IO;
using System.Text.Json;
using PiPlay.Services;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class YouTubeDomBehaviorTests
{
    private const string Nonce = "0123456789abcdef0123456789abcdef";
    private const string DocumentToken = "fedcba9876543210fedcba9876543210";
    private const string ReplacementDocumentToken = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public async Task Generated_surface_scripts_enforce_executable_dom_compliance_Q3_Q5_Q8()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(XamlTestFiles.SrcDir, "..", ".."));
        var harnessPath = Path.Combine(
            repoRoot, "tests", "PiPlay.Tests", "Infrastructure", "YouTubeDomBehaviorHarness.cjs");
        Assert.True(File.Exists(harnessPath), $"DOM behavior harness was not found at {harnessPath}.");

        var input = JsonSerializer.Serialize(new
        {
            nonce = Nonce,
            documentToken = DocumentToken,
            replacementDocumentToken = ReplacementDocumentToken,
            passiveScript = YouTubeDomBridge.BuildPassiveSurfaceDragScript(Nonce, 4, 4),
            passiveAuthorizeScript = YouTubeDomBridge.BuildPassiveSurfaceDocumentTokenScript(DocumentToken),
            passiveReauthorizeScript = YouTubeDomBridge.BuildPassiveSurfaceDocumentTokenScript(ReplacementDocumentToken),
            focusedScript = YouTubeDomBridge.BuildPlayerFirstSurfaceScript(
                Nonce, "#2BAED0", fadeEnabled: true, fadeDelayMs: 2500),
            focusedAuthorizeScript = YouTubeDomBridge.BuildPlayerFirstDocumentTokenScript(DocumentToken),
            focusedStateRequestScript = YouTubeDomBridge.BuildPlayerFirstStateRequestScript(),
            focusedConfigurePinnedMaxScript = YouTubeDomBridge.BuildPlayerFirstConfigurationScript(
                "#FF5500", fadeEnabled: true, fadeDelayMs: 1800, topmost: true, maximized: true),
            focusedConfigureUnpinnedRestoreScript = YouTubeDomBridge.BuildPlayerFirstConfigurationScript(
                "#2BAED0", fadeEnabled: true, fadeDelayMs: 2500, topmost: false, maximized: false),
        });

        var startInfo = new ProcessStartInfo
        {
            FileName = "node",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(harnessPath);

        using var process = new Process { StartInfo = startInfo };
        Assert.True(process.Start(), "Node DOM behavior harness did not start.");

        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.StandardInput.WriteAsync(input);
        process.StandardInput.Close();

        var exitTask = process.WaitForExitAsync();
        var completed = await Task.WhenAny(exitTask, Task.Delay(TimeSpan.FromSeconds(15)));
        if (completed != exitTask)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* best-effort timeout cleanup */ }
            Assert.Fail("Node DOM behavior harness exceeded the 15-second deterministic-test budget.");
        }

        await exitTask;
        var output = await standardOutput;
        var error = await standardError;
        var diagnostics = $"{output}{Environment.NewLine}{error}".Trim();

        Assert.True(process.ExitCode == 0,
            $"Node DOM behavior harness exited with code {process.ExitCode}.{Environment.NewLine}{diagnostics}");
        Assert.Contains("DOM HARNESS PASS", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Generated_surface_scripts_escape_tokens_format_thresholds_invariantly_and_clamp_fade_delay()
    {
        const string nonce = "nonce-with-\"quote";
        const string documentToken = "document-token-with-\"quote";

        var passiveScript = YouTubeDomBridge.BuildPassiveSurfaceDragScript(nonce, 4.25, 7.5);
        var passiveAuthorize = YouTubeDomBridge.BuildPassiveSurfaceDocumentTokenScript(documentToken);
        var focusedLowFade = YouTubeDomBridge.BuildPlayerFirstSurfaceScript(
            nonce, "#2BAED0", fadeEnabled: true, fadeDelayMs: 100);
        var focusedHighFade = YouTubeDomBridge.BuildPlayerFirstConfigurationScript(
            "#2BAED0", fadeEnabled: false, fadeDelayMs: 25_000, topmost: true, maximized: true);
        var focusedAuthorize = YouTubeDomBridge.BuildPlayerFirstDocumentTokenScript(documentToken);

        Assert.Contains($"const nonce = {JsonSerializer.Serialize(nonce)};", passiveScript);
        Assert.Contains("const thresholdX = 4.25;", passiveScript);
        Assert.Contains("const thresholdY = 7.5;", passiveScript);
        Assert.Contains($"const documentToken = {JsonSerializer.Serialize(documentToken)};", passiveAuthorize);

        Assert.Contains($"const nonce = {JsonSerializer.Serialize(nonce)};", focusedLowFade);
        Assert.Contains("fadeDelayMs: 500", focusedLowFade);
        Assert.Contains("fadeDelayMs: 10000", focusedHighFade);
        Assert.Contains($"const documentToken = {JsonSerializer.Serialize(documentToken)};", focusedAuthorize);
    }
}

