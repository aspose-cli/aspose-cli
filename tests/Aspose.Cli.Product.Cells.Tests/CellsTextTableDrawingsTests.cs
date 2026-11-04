using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cells;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// A csv or tsv output holds the cells of one sheet as a table, as an md output does, so the
/// charts, pictures and other drawings of that sheet are not written: whether
/// <c>cells convert</c> or <c>cells edit --out</c> writes it, a <c>LOSSY_CONVERSION</c> warning
/// counts them, and an output that keeps them, or a sheet that has none, warns of nothing lost.
/// The sheet with the drawings is the first and active one, which both a licensed and an
/// evaluation export write; the commands run with the test license when the run has one.
/// </summary>
public sealed class CellsTextTableDrawingsTests : IDisposable
{
    private const string LossyConversion = "LOSSY_CONVERSION";

    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    [Theory]
    [InlineData("convert", "out.csv")]
    [InlineData("convert", "out.tsv")]
    [InlineData("convert", "out.txt")]
    [InlineData("edit", "out.csv")]
    [InlineData("edit", "out.tsv")]
    public void TextTableOutput_OfASheetWithAChartAndAPicture_WarnsOfTheDrawingsItLeftOut(string command, string output)
    {
        Book("book.xlsx", withDrawings: true);

        CliResult written = Write(command, "book.xlsx", output);

        Assert.True(written.ExitCode == 0, $"exit {written.ExitCode}: {written.StdErr}{written.StdOut}");
        Assert.Contains("North", File.ReadAllText(_workspace.File(output)), StringComparison.Ordinal);
        JsonNode[] warnings = Warnings(JsonNode.Parse(written.StdOut)!);
        JsonNode lossy = Assert.Single(warnings, static warning => Code(warning) == LossyConversion);
        string message = lossy["message"]!.GetValue<string>();
        Assert.Contains("1 chart(s) and 1 picture(s)", message, StringComparison.Ordinal);
        Assert.Contains("'Summary'", message, StringComparison.Ordinal);
        Assert.True(lossy["affectsCompleteness"]!.GetValue<bool>());
        Assert.Contains(warnings, static warning => Code(warning) == "SHEETS_DROPPED");
    }

    /// <summary>
    /// Only a text table of a sheet with drawings leaves drawings out: the same workbook without
    /// them converts to csv with no such warning, and an xlsx output keeps the chart and the
    /// picture and warns of nothing lost.
    /// </summary>
    [Theory]
    [InlineData(true, "out.csv", true)]
    [InlineData(false, "out.csv", false)]
    [InlineData(true, "out.xlsx", false)]
    public void Convert_WarnsOfLeftOutDrawings_OnlyWhenATextTableLeavesThemOut(bool withDrawings, string output, bool lossy)
    {
        Book("book.xlsx", withDrawings);

        CliResult converted = Write("convert", "book.xlsx", output);

        Assert.True(converted.ExitCode == 0, $"exit {converted.ExitCode}: {converted.StdErr}{converted.StdOut}");
        JsonNode[] warnings = Warnings(JsonNode.Parse(converted.StdOut)!);
        Assert.Equal(lossy ? 1 : 0, warnings.Count(static warning => Code(warning) == LossyConversion));
        if (output.EndsWith(".xlsx", StringComparison.Ordinal))
        {
            using var workbook = new Workbook(_workspace.File(output));
            Worksheet summary = workbook.Worksheets["Summary"];
            Assert.Single(summary.Charts);
            Assert.Single(summary.Pictures);
        }
    }

    /// <summary>
    /// A verified edit to csv reports what the same edit of the workbook without drawings reports,
    /// with the same exit code and the same csv, and adds only the left-out drawings: one
    /// warning and the verification issue it makes.
    /// </summary>
    [Fact]
    public void VerifiedEditToCsv_OfASheetWithDrawings_AddsOnlyTheLeftOutDrawingsToWhatItReports()
    {
        Book("drawings.xlsx", withDrawings: true);
        Book("plain.xlsx", withDrawings: false);
        Directory.CreateDirectory(_workspace.File("drawings"));
        Directory.CreateDirectory(_workspace.File("plain"));

        CliResult drawn = Write("edit", "drawings.xlsx", Path.Combine("drawings", "out.csv"), "--verify");
        CliResult plain = Write("edit", "plain.xlsx", Path.Combine("plain", "out.csv"), "--verify");

        Assert.True(plain.ExitCode == drawn.ExitCode, $"exit {drawn.ExitCode}, without drawings {plain.ExitCode}: {drawn.StdErr}{drawn.StdOut}");
        JsonNode result = JsonNode.Parse(drawn.StdOut)!;
        JsonNode baseline = JsonNode.Parse(plain.StdOut)!;
        JsonNode lossy = Assert.Single(Warnings(result), static warning => Code(warning) == LossyConversion);
        Assert.Contains("1 chart(s) and 1 picture(s)", lossy["message"]!.GetValue<string>(), StringComparison.Ordinal);
        JsonArray issues = result["verification"]!["issues"]!.AsArray();
        JsonNode issue = Assert.Single(issues, static issue => Code(issue!) == LossyConversion)!;
        Assert.Equal(lossy["message"]!.GetValue<string>(), issue["message"]!.GetValue<string>());

        issues.Remove(issue);
        JsonArray warnings = result["warnings"]!.AsArray();
        warnings.Remove(warnings.Single(static warning => Code(warning!) == LossyConversion));
        foreach (string member in new[] { "dryRun", "recalculated", "applied", "verification", "warnings" })
        {
            Assert.True(JsonNode.DeepEquals(baseline[member], result[member]),
                $"'{member}' differs from the edit without drawings:\n{result[member]?.ToJsonString()}\n{baseline[member]?.ToJsonString()}");
        }

        Assert.True(JsonNode.DeepEquals(baseline["output"]!["fingerprint"], result["output"]!["fingerprint"]),
            "The csv differs from the one written without drawings.");
    }

    /// <summary>
    /// A workbook of one sheet loses nothing else to csv, so the left-out drawings alone fail a
    /// verified edit: exit 8, with them as its only issue, where the same edit without
    /// <c>--verify</c> exits 0 and warns of them. An evaluation save adds its warning sheet to
    /// the workbook, which then has a second sheet the csv leaves out (<c>SHEETS_DROPPED</c>).
    /// </summary>
    [Fact]
    public void VerifiedEditToCsv_OfAOneSheetWorkbookWithDrawings_FailsOnTheLeftOutDrawingsAlone()
    {
        Book("book.xlsx", withDrawings: true, otherSheet: false);

        CliResult verified = Write("edit", "book.xlsx", "verified.csv", "--verify");
        CliResult written = Write("edit", "book.xlsx", "written.csv");

        Assert.True(verified.ExitCode == 8, $"exit {verified.ExitCode}: {verified.StdErr}{verified.StdOut}");
        JsonNode verification = JsonNode.Parse(verified.StdOut)!["verification"]!;
        Assert.False(verification["ok"]!.GetValue<bool>());
        string[] issues = [.. verification["issues"]!.AsArray().Select(static issue => Code(issue!))];
        Assert.Equal(TestLicense.Path is null ? ["SHEETS_DROPPED", LossyConversion] : [LossyConversion], issues);
        Assert.True(written.ExitCode == 0, $"exit {written.ExitCode}: {written.StdErr}{written.StdOut}");
        Assert.Single(Warnings(JsonNode.Parse(written.StdOut)!), static warning => Code(warning) == LossyConversion);
    }

    private CliResult Write(string command, string input, string output, params string[] options) => command switch
    {
        "convert" => Cli(["cells", "convert", input, "--out", output, .. options, "--output-format", "json"]),
        "edit" => Cli(["cells", "edit", input, "--ops",
            """{"ops":[{"op":"set_values","sheet":"Summary","range":"A4","values":[["South"]]}]}""",
            "--out", output, .. options, "--output-format", "json"]),
        _ => throw new ArgumentOutOfRangeException(nameof(command), command, null),
    };

    /// <summary>
    /// A workbook whose first and active sheet, Summary, holds a small table and, with drawings,
    /// a chart of it and a picture; with <paramref name="otherSheet"/>, a second sheet follows it.
    /// </summary>
    private void Book(string name, bool withDrawings, bool otherSheet = true)
    {
        File.WriteAllBytes(_workspace.File("logo.png"), ResourceHttpServer.Image);
        Assert.Equal(0, Cli("cells", "create", name, "--sheet-names", otherSheet ? "Summary,Other" : "Summary").ExitCode);
        var ops = new List<object>
        {
            new
            {
                op = "set_values", sheet = "Summary", range = "A1",
                values = new object[][] { ["Dept", "Revenue"], ["East", 700], ["North", 580] },
            },
        };
        if (otherSheet)
        {
            ops.Add(new { op = "set_values", sheet = "Other", range = "A1", values = new object[][] { ["Notes"] } });
        }

        if (withDrawings)
        {
            ops.Add(new { op = "insert_chart", sheet = "Summary", kind = "column", dataRange = "A1:B3", at = "E2:J12", title = "Revenue" });
            ops.Add(new { op = "insert_image", sheet = "Summary", at = "E14", path = "logo.png" });
        }

        CliResult edited = Cli("cells", "edit", name, "--in-place",
            "--ops", JsonSerializer.Serialize(new { ops }), "--output-format", "json");
        Assert.True(edited.ExitCode == 0, edited.StdErr);
    }

    /// <summary>
    /// Runs the CLI with the test license when the run has one (a workspace strips every other
    /// license source), and in evaluation mode otherwise.
    /// </summary>
    private CliResult Cli(params string[] args) =>
        _workspace.Run(TestLicense.Path is { } license ? [.. args, "--license", license] : args);

    private static JsonNode[] Warnings(JsonNode result) =>
        [.. (result["warnings"]?.AsArray() ?? []).Select(static warning => warning!)];

    private static string Code(JsonNode warning) => warning["code"]!.GetValue<string>();
}
