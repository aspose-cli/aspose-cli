using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Aspose.Cli.Tests;

/// <summary>
/// CI plans the test projects of its scope with scripts/test.ps1 -Plan and runs each in its own
/// job with -TestProject, so together the jobs run exactly the projects of one whole run.
/// </summary>
public sealed partial class CiTestJobPlanTests
{
    private static readonly string Root = RepositoryPaths.Root;
    private static readonly string Workflow = Path.Combine(Root, ".github", "workflows", "ci.yml");

    [Theory]
    [InlineData("Fast")]
    [InlineData("Full")]
    public async Task EachPlannedProjectRunsAloneAndTheJobsTogetherRunTheWholeScope(string scope)
    {
        string[] whole = await PlannedProjects(scope);
        Assert.NotEmpty(whole);
        Assert.Equal(whole.Distinct(StringComparer.Ordinal).Count(), whole.Length);

        string[] projects = TestProjectNames();
        string[][] alone = await Task.WhenAll(projects.Select(project => PlannedProjects(scope, project)));
        for (int index = 0; index < projects.Length; index++)
        {
            // A project the scope leaves out is skipped, as in a whole run; CI plans no job for it.
            string[] expected = whole.Contains(projects[index], StringComparer.Ordinal) ? [projects[index]] : [];
            Assert.Equal(expected, alone[index]);
        }
        Assert.Equal(whole.Order(StringComparer.Ordinal), alone.SelectMany(static run => run).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task AnUnknownProjectNameIsRejected()
    {
        (int exitCode, string output) = await RunPlan("Fast", "Aspose.Cli.NoSuch.Tests");
        Assert.NotEqual(0, exitCode);
        Assert.Contains("-TestProject names no test project: Aspose.Cli.NoSuch.Tests", output, StringComparison.Ordinal);
    }

    [Fact]
    public void CiRunsEachPlannedProjectInItsOwnJobAndVerifyNeedsEveryJob()
    {
        string workflow = File.ReadAllText(Workflow).ReplaceLineEndings("\n");
        // The plan job reads the TEST lines that PlannedProjects reads here.
        Assert.Contains("-Plan 6>&1", workflow, StringComparison.Ordinal);
        Assert.Contains($"'{TestLine().ToString()}'", workflow, StringComparison.Ordinal);
        Assert.Contains("project: ${{ fromJSON(needs.plan.outputs.projects) }}", workflow, StringComparison.Ordinal);
        Assert.Contains("-TestProject $env:PROJECT", workflow, StringComparison.Ordinal);

        string jobs = workflow[(workflow.IndexOf("\njobs:\n", StringComparison.Ordinal) + 1)..];
        string[] names = JobName().Matches(jobs).Select(static match => match.Groups["name"].Value).ToArray();
        Assert.Contains("verify", names);
        Match verify = Regex.Match(jobs, @"(?m)^  verify:\n    needs: \[(?<needs>[^\]]*)\]\n    if: always\(\)\n");
        Assert.True(verify.Success, "The verify job must need its jobs and run whatever their result (if: always()).");
        Assert.Equal(
            names.Where(static name => name != "verify").Order(StringComparer.Ordinal),
            verify.Groups["needs"].Value.Split(',', StringSplitOptions.TrimEntries).Order(StringComparer.Ordinal));
    }

    private static string[] TestProjectNames() =>
        Directory.EnumerateFiles(Path.Combine(Root, "tests"), "*.csproj", SearchOption.AllDirectories)
            .Where(static path => !Path.GetRelativePath(Root, path).Split(Path.DirectorySeparatorChar)
                .Any(static segment => segment is "bin" or "obj"))
            .Select(Path.GetFileNameWithoutExtension)
            .Where(static name => name != "Aspose.Cli.TestKit")
            .Select(static name => name!)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static async Task<string[]> PlannedProjects(string scope, string? project = null)
    {
        (int exitCode, string output) = await RunPlan(scope, project);
        Assert.True(exitCode == 0, output);
        return output.Split('\n')
            .Select(static line => TestLine().Match(line.TrimEnd('\r')))
            .Where(static match => match.Success)
            .Select(static match => match.Groups[1].Value)
            .ToArray();
    }

    private static async Task<(int ExitCode, string Output)> RunPlan(string scope, string? project)
    {
        var start = new ProcessStartInfo(ToolPath.Require("pwsh"))
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        string[] arguments = ["-NoProfile", "-NonInteractive", "-File", Path.Combine(Root, "scripts", "test.ps1"), "-Scope", scope, "-Plan"];
        foreach (string argument in project is null ? arguments : [.. arguments, "-TestProject", project])
        {
            start.ArgumentList.Add(argument);
        }
        using Process process = Process.Start(start)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await process.WaitForExitAsync(timeout.Token);
        return (process.ExitCode, await output + await error);
    }

    [GeneratedRegex(@"^TEST (\S+) ")]
    private static partial Regex TestLine();

    [GeneratedRegex(@"(?m)^  (?<name>[a-z][\w-]*):\n")]
    private static partial Regex JobName();
}
