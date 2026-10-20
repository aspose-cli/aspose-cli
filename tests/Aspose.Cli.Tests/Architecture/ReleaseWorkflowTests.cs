using System.Diagnostics;

namespace Aspose.Cli.Tests;

/// <summary>
/// The release job publishes a tag with a pre-release suffix (<c>v1.1.0-rc.1</c>) as a GitHub
/// pre-release, so <c>releases/latest</c>, which every installed CLI updates from, never names it.
/// The test runs the job's publishing script with a stand-in <c>gh</c> that records its arguments.
/// </summary>
public sealed class ReleaseWorkflowTests : IDisposable
{
    private const string PublishStep = "- name: Publish the GitHub release";
    private static readonly string Workflow = Path.Combine(RepositoryPaths.Root, ".github", "workflows", "release.yml");
    private readonly TempDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    [Theory]
    [InlineData("v1.1.0", false)]
    [InlineData("v1.1.0-rc.1", true)]
    public async Task APreReleaseTag_IsPublishedAsAPreRelease(string tag, bool prerelease)
    {
        string assets = Directory.CreateDirectory(Path.Combine(_directory.Path, "artifacts", "release", "win-x64")).FullName;
        File.WriteAllText(Path.Combine(assets, "aspose-cli.zip"), "asset");
        string script = Path.Combine(_directory.Path, "publish.ps1");
        File.WriteAllText(script, string.Join('\n',
            "function gh { $global:LASTEXITCODE = 0; [Console]::Out.WriteLine('GH ' + ($args -join ' ')) }",
            PublishScript()));

        var start = new ProcessStartInfo(ToolPath.Require("pwsh"))
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = _directory.Path,
        };
        foreach (string argument in new[] { "-NoProfile", "-NonInteractive", "-File", script })
        {
            start.ArgumentList.Add(argument);
        }
        start.Environment["GITHUB_REF_NAME"] = tag;
        using Process process = Process.Start(start)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await process.WaitForExitAsync(timeout.Token);

        Assert.True(process.ExitCode == 0, await error);
        string[] arguments = Assert.Single(
            (await output).ReplaceLineEndings("\n").Split('\n'),
            static line => line.StartsWith("GH ", StringComparison.Ordinal))
            .Split(' ');
        Assert.Equal(["GH", "release", "create", tag], arguments[..4]);
        Assert.Equal(prerelease, arguments.Contains("--prerelease", StringComparer.Ordinal));
    }

    /// <summary>The <c>run</c> block of the publishing step, without its YAML indentation.</summary>
    private static string PublishScript()
    {
        string[] lines = File.ReadAllText(Workflow).ReplaceLineEndings("\n").Split('\n');
        int step = Array.FindIndex(lines, static line => line.Trim() == PublishStep);
        Assert.True(step >= 0, $"release.yml has no step '{PublishStep}'.");
        int run = Array.FindIndex(lines, step, static line => line.Trim() == "run: |");
        Assert.True(run > step, "The publishing step has no 'run: |' block.");
        string first = lines[run + 1];
        int indent = first.Length - first.TrimStart().Length;
        return string.Join('\n', lines
            .Skip(run + 1)
            .TakeWhile(line => line.Trim().Length == 0 || line.Length - line.TrimStart().Length >= indent)
            .Select(line => line.Length >= indent ? line[indent..] : string.Empty));
    }
}
