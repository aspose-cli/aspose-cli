using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Aspose.Cli.Tests;

/// <summary>
/// CI plans the test projects of its scope with scripts/test.ps1 -Plan and runs each in its own
/// job with -TestProject, so together the jobs run exactly the projects of one whole run and make
/// its checks; verify passes only when every job passed (scripts/require-jobs.ps1).
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
        Plan whole = await PlanOf(scope);
        Assert.NotEmpty(whole.Tests);
        Assert.Equal(whole.Tests.Distinct(StringComparer.Ordinal).Count(), whole.Tests.Length);

        string[] projects = TestProjectNames();
        Plan[] alone = await Task.WhenAll(projects.Select(project => PlanOf(scope, project)));
        for (int index = 0; index < projects.Length; index++)
        {
            // A project the scope leaves out is skipped, as in a whole run; CI plans no job for it.
            string[] expected = whole.Tests.Contains(projects[index], StringComparer.Ordinal) ? [projects[index]] : [];
            Assert.Equal(expected, alone[index].Tests);
            // Every job checks the category-only projects of the scope (such as the installer
            // tests in Fast), which no job runs, for tests without their category.
            Assert.Equal(whole.CategoryChecks, alone[index].CategoryChecks);
        }
        Assert.Equal(whole.Tests.Order(StringComparer.Ordinal), alone.SelectMany(static run => run.Tests).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task AnUnknownProjectNameIsRejected()
    {
        (int exitCode, string output) = await RunPowerShell("test.ps1", "-Scope", "Fast", "-Plan", "-TestProject", "Aspose.Cli.NoSuch.Tests");
        Assert.NotEqual(0, exitCode);
        Assert.Contains("-TestProject names no test project: Aspose.Cli.NoSuch.Tests", output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("success", "success", "success", true)]
    [InlineData("success", "success", "skipped", true)]
    [InlineData("success", "failure", "skipped", false)]
    [InlineData("success", "cancelled", "success", false)]
    [InlineData("success", "success", "failure", false)]
    [InlineData("success", "success", "cancelled", false)]
    [InlineData("failure", "skipped", "skipped", false)]
    [InlineData("cancelled", "skipped", "skipped", false)]
    [InlineData("success", "skipped", "skipped", false)]
    public async Task VerifyPassesOnlyWhenEveryJobPassed(string plan, string test, string package, bool passes)
    {
        // The shape of toJSON(needs).
        string needs = System.Text.Json.JsonSerializer.Serialize(
            new Dictionary<string, object> { ["plan"] = Job(plan), ["test"] = Job(test), ["package"] = Job(package) });
        (int exitCode, string output) = await RunPowerShell("require-jobs.ps1", "-Needs", needs, "-MaySkip", "package");
        Assert.True((exitCode == 0) == passes, output);

        static object Job(string result) => new { result, outputs = new { } };
    }

    [Fact]
    public void CiRunsEachPlannedProjectInItsOwnJobAndVerifyNeedsEveryJob()
    {
        string workflow = File.ReadAllText(Workflow).ReplaceLineEndings("\n");
        // The plan job reads the TEST lines that PlanOf reads here.
        Assert.Contains("-Plan 6>&1", workflow, StringComparison.Ordinal);
        Assert.Contains($"'{TestLine()}'", workflow, StringComparison.Ordinal);
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
        // Only package skips itself, when no packaging input changed.
        Assert.Contains("run: ./scripts/require-jobs.ps1 -Needs $env:NEEDS -MaySkip package\n", workflow, StringComparison.Ordinal);
    }

    private sealed record Plan(string[] Tests, string[] CategoryChecks);

    private static string[] TestProjectNames() =>
        Directory.EnumerateFiles(Path.Combine(Root, "tests"), "*.csproj", SearchOption.AllDirectories)
            .Where(static path => !Path.GetRelativePath(Root, path).Split(Path.DirectorySeparatorChar)
                .Any(static segment => segment is "bin" or "obj"))
            .Select(Path.GetFileNameWithoutExtension)
            .Where(static name => name != "Aspose.Cli.TestKit")
            .Select(static name => name!)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static async Task<Plan> PlanOf(string scope, string? project = null)
    {
        string[] arguments = ["-Scope", scope, "-Plan"];
        (int exitCode, string output) = await RunPowerShell("test.ps1", project is null ? arguments : [.. arguments, "-TestProject", project]);
        Assert.True(exitCode == 0, output);
        string[] lines = output.Split('\n').Select(static line => line.TrimEnd('\r')).ToArray();
        return new Plan(
            lines.Select(static line => TestLine().Match(line)).Where(static match => match.Success).Select(static match => match.Groups[1].Value).ToArray(),
            lines.Select(static line => CategoryCheckLine().Match(line)).Where(static match => match.Success).Select(static match => match.Groups[1].Value).ToArray());
    }

    private static async Task<(int ExitCode, string Output)> RunPowerShell(string script, params string[] arguments)
    {
        var start = new ProcessStartInfo(ToolPath.Require("pwsh"))
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in (string[])["-NoProfile", "-NonInteractive", "-File", Path.Combine(Root, "scripts", script), .. arguments])
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

    // test.ps1 prints this line for each project it records for the category-only check.
    [GeneratedRegex(@"^SKIP (\S+) \(its tests are all \w+ tests, ")]
    private static partial Regex CategoryCheckLine();

    [GeneratedRegex(@"(?m)^  (?<name>[a-z][\w-]*):\n")]
    private static partial Regex JobName();
}
