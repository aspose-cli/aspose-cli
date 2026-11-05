using System.Diagnostics;
using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Invariants.Tests;

/// <summary>
/// Aspose.Cells evaluation mode opens at most 100 files in one process, which a worker reaches
/// across commands and a command line in a process of its own never does: a command that fails
/// with <c>EVALUATION_LIMIT</c> in a worker that ran other commands runs again in a new worker,
/// and any other failure is the command's result. Each test runs its commands in a lane of its
/// own, whose first command starts a new worker.
/// </summary>
[Collection(CliWorkerLaneCollection.Name)]
public sealed class CliWorkerEvaluationLimitTests
{
    /// <summary>More inspections of a workbook than Aspose.Cells evaluation mode opens files in one process.</summary>
    private const int Inspections = 110;

    /// <summary>
    /// Inspecting a workbook more times than evaluation mode opens files in one process, in the
    /// workers of one evaluation lane, succeeds every time, and an inspection the first worker
    /// refused ran again in a new worker. The inspections write the table format, whose error is a
    /// line of text.
    /// </summary>
    [Fact]
    public void InspectionsPastTheEvaluationFileLimitOfAWorker_AllSucceed()
    {
        Assert.SkipUnless(CliWorkers.Enabled, $"{CliWorkers.CountVariable}=0 runs every command as a child process.");
        using var workspace = new TempWorkspace();
        File.WriteAllBytes(workspace.File("book.xlsx"), ScenarioFixtures.Read("cells.xlsx"));
        var lane = new Lane(workspace);
        var failures = new List<string>();
        int runs = 0;
        var watch = Stopwatch.StartNew();
        for (int inspection = 1; inspection <= Inspections; inspection++)
        {
            (CliResult result, int made) = lane.Run("cells", "inspect", "book.xlsx", "--output-format", "table");
            runs += made;
            if (result.ExitCode != 0)
            {
                failures.Add($"inspection {inspection}: exit {result.ExitCode}: {result.StdErr.Trim()}");
            }
        }
        TestContext.Current.TestOutputHelper?.WriteLine($"{Inspections} inspections, {runs} runs in workers, {watch.Elapsed.TotalSeconds:N1}s.");

        Assert.True(failures.Count == 0, $"{failures.Count} of {Inspections} inspections failed, the first:{Environment.NewLine}" + string.Join(Environment.NewLine, failures.Take(3)));
        Assert.True(runs > Inspections, $"No inspection ran again in a new worker ({runs} runs for {Inspections} inspections), so none reached the evaluation file limit of its worker.");
    }

    /// <summary>
    /// A command's own failure is its result: an <c>EVALUATION_LIMIT</c> that the first command of
    /// a worker reaches does not run again; one that a worker that ran other commands reaches runs
    /// again in a new worker, which reaches it too; and a license error that shares its exit code,
    /// 7, does not run again in either output format.
    /// </summary>
    [Fact]
    public void ACommandsOwnLicenseError_IsItsResult()
    {
        Assert.SkipUnless(CliWorkers.Enabled, $"{CliWorkers.CountVariable}=0 runs every command as a child process.");
        using var workspace = new TempWorkspace();
        Assert.Equal(0, workspace.Run("cells", "create", "book.xlsx", "--sheet-names", "Dashboard,Detail").ExitCode);
        var lane = new Lane(workspace);
        // Evaluation mode exports the first sheet to a text format whatever sheet is asked for,
        // which the command refuses.
        string[] secondSheet = ["cells", "convert", "book.xlsx", "--to", "csv", "--sheets", "2", "--out", "report.csv", "--output-format", "json"];

        (CliResult first, int firstRuns) = lane.Run(secondSheet);
        (CliResult again, int againRuns) = lane.Run(secondSheet);
        (CliResult json, int jsonRuns) = lane.Run("cells", "inspect", "book.xlsx", "--license", "missing.lic", "--output-format", "json");
        (CliResult text, int textRuns) = lane.Run("cells", "inspect", "book.xlsx", "--license", "missing.lic", "--output-format", "table");

        Assert.Equal((7, "EVALUATION_LIMIT", 1), (first.ExitCode, Code(first), firstRuns));
        Assert.Equal((7, "EVALUATION_LIMIT", 2), (again.ExitCode, Code(again), againRuns));
        Assert.False(File.Exists(workspace.File("report.csv")));
        Assert.Equal((7, "LICENSE_FILE_NOT_FOUND", 1), (json.ExitCode, Code(json), jsonRuns));
        Assert.Equal((7, 1), (text.ExitCode, textRuns));
        Assert.Contains("error LICENSE_FILE_NOT_FOUND: ", text.StdErr, StringComparison.Ordinal);
    }

    /// <summary>The code of the error envelope a command wrote to stderr in a JSON format.</summary>
    private static string Code(CliResult result) => JsonNode.Parse(result.StdErr)?["error"]?["code"]?.GetValue<string>() ?? result.StdErr;

    /// <summary>A lane of its own for the evaluation environment of a workspace.</summary>
    private sealed class Lane
    {
        private readonly string _name = "evaluation:" + Guid.NewGuid().ToString("N");
        private readonly string _directory;
        private readonly IDictionary<string, string?> _environment = new ProcessStartInfo().Environment;

        public Lane(TempWorkspace workspace)
        {
            _directory = workspace.Path;
            CliEnvironment.Evaluation(Path.GetDirectoryName(workspace.ConfigDirectory)!).Apply(_environment);
        }

        /// <summary>Runs a command line in the lane, with the runs workers made for it.</summary>
        public (CliResult Result, int Runs) Run(params string[] args)
        {
            int before = CliWorkers.ThreadRuns;
            CliResult result = CliWorkers.Run(_name, CliRunner.ExecutablePath, _directory, args, _environment, TimeSpan.FromSeconds(120));
            return (result, CliWorkers.ThreadRuns - before);
        }
    }
}
