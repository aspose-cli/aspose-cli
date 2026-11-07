using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// A verified edit to a text table (csv, tsv) compares what the table holds: the values of the one
/// sheet it was written from, under that sheet's name. A formula cell compares by its value, and
/// one whose value moved is a formula result; the sheets and drawings the table leaves out fail
/// the verification as the warnings that name them, not as changes. The commands run with the
/// test license when the run has one, and in evaluation mode otherwise, whose marks (a warning
/// sheet, a notice row) the comparisons leave aside.
/// </summary>
public sealed partial class CellsTextTableVerificationTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    /// <summary>
    /// C22: the edit writes Summary to csv or tsv and exits 0. The table's sheet keeps the name
    /// Summary, the formula cells whose values stayed the same are no change, and the totals the
    /// new value moved are formula results.
    /// </summary>
    [Theory]
    [InlineData("x.csv")]
    [InlineData("x.tsv")]
    public void VerifiedEditToATextTable_ComparesTheValuesOfTheWrittenSheetUnderItsName(string output)
    {
        Book("book.xlsx");

        CliResult edited = Edit("book.xlsx", output, "--set", "Summary!B3=1010");

        Assert.True(edited.ExitCode == 0, $"exit {edited.ExitCode}: {edited.StdErr}{edited.StdOut}");
        JsonNode verification = Verification(edited);
        Assert.True(verification["ok"]!.GetValue<bool>());
        Assert.Empty(verification["issues"]!.AsArray());
        Assert.Equal(["Summary!B3 960 -> 1010"], Changes(verification["directChanges"]));
        Assert.Equal(
            ["Summary!D3 1975 =B3+C3 -> 2025", "Summary!B5 3660 =SUM(B2:B4) -> 3710", "Summary!D5 7523 =SUM(D2:D4) -> 7573"],
            Changes(verification["formulaResultChanges"]));
        Assert.Empty(OtherChanges(verification));
        Assert.Equal(["op-0001"], CheckedOps(verification));
    }

    /// <summary>
    /// The verification of an edit written to csv is the one of the same edit written to xlsx
    /// without the formulas of the output, which the csv does not hold: the same direct changes,
    /// formula results and other changes under the same sheet names, the same checked operations,
    /// formula errors and issues, and the same exit code. The edits change a value, write a
    /// formula, rename the sheet, make a formula error, and change a value of an input that
    /// already holds one.
    /// </summary>
    [Theory]
    [InlineData("""{"ops":[{"op":"set_values","sheet":"Summary","range":"B3","values":[[1010]]}]}""", false)]
    [InlineData("""{"ops":[{"op":"set_formula","sheet":"Summary","range":"D3","formula":"=B3*2"}]}""", false)]
    [InlineData("""{"ops":[{"op":"set_values","sheet":"Summary","range":"B3","values":[[1010]]},{"op":"rename_sheet","sheet":"Summary","name":"Report"}]}""", false)]
    [InlineData("""{"ops":[{"op":"set_formula","sheet":"Summary","range":"E2","formula":"=1/0"}]}""", false)]
    [InlineData("""{"ops":[{"op":"set_values","sheet":"Summary","range":"B3","values":[[1010]]}]}""", true)]
    public void VerifiedEditToCsv_ReportsWhatTheSameEditToXlsxReports_WithoutTheOutputFormulas(string ops, bool brokenInput)
    {
        Book("book.xlsx", brokenInput: brokenInput);

        CliResult csv = Edit("book.xlsx", "out.csv", "--ops", ops);
        CliResult xlsx = Edit("book.xlsx", "out.xlsx", "--ops", ops);

        Assert.True(csv.ExitCode == xlsx.ExitCode, $"csv exit {csv.ExitCode}, xlsx exit {xlsx.ExitCode}: {csv.StdErr}{csv.StdOut}");
        JsonNode expected = Verification(xlsx);
        JsonNode actual = Verification(csv);
        foreach (string list in new[] { "directChanges", "formulaResultChanges" })
        {
            Assert.Equal(Changes(expected[list], rightFormulas: false), Changes(actual[list]));
        }

        Assert.Equal(OtherChanges(expected, rightFormulas: false), OtherChanges(actual));
        foreach (string member in new[] { "checkedOps", "formulaErrors", "issues", "ok", "truncated" })
        {
            Assert.True(JsonNode.DeepEquals(expected[member], actual[member]),
                $"'{member}' differs from the xlsx output's:\n{actual[member]?.ToJsonString()}\n{expected[member]?.ToJsonString()}");
        }
    }

    /// <summary>
    /// Guardrails: a sheet the csv does not write still fails a verified edit (exit 8), as the
    /// <c>SHEETS_DROPPED</c> issue that names it, not as a removed sheet. An edit of only that
    /// sheet fails the same way, with no change listed and its operation not among the checked
    /// ones, since the csv holds nothing it did.
    /// </summary>
    [Theory]
    [InlineData("Summary!B3=1010", new[] { "Summary!B3 960 -> 1010" }, new[] { "op-0001" })]
    [InlineData("Other!A1=Done", new string[0], new string[0])]
    public void VerifiedEditToCsv_OfAWorkbookWithAnotherSheet_FailsOnTheSheetItDidNotWrite(string set, string[] direct, string[] checkedOps)
    {
        Book("book.xlsx", otherSheet: true);

        CliResult edited = Edit("book.xlsx", "x.csv", "--set", set);

        Assert.True(edited.ExitCode == 8, $"exit {edited.ExitCode}: {edited.StdErr}{edited.StdOut}");
        JsonNode verification = Verification(edited);
        Assert.False(verification["ok"]!.GetValue<bool>());
        JsonNode issue = Assert.Single(verification["issues"]!.AsArray())!;
        Assert.Equal("SHEETS_DROPPED", issue["code"]!.GetValue<string>());
        Assert.Contains("'Other'", issue["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal(direct, Changes(verification["directChanges"]));
        Assert.Empty(OtherChanges(verification));
        Assert.Equal(checkedOps, CheckedOps(verification));
    }

    /// <summary>
    /// Guardrail: an xlsx output holds the formulas, so its verification compares them as before:
    /// a changed value moves the results of the formulas that read it, each listed with its formula
    /// on both sides, and a formula the edit wrote is a direct change with both formulas.
    /// </summary>
    [Fact]
    public void VerifiedEditToXlsx_ComparesTheFormulasOfBothSides()
    {
        Book("book.xlsx");

        CliResult value = Edit("book.xlsx", "value.xlsx", "--set", "Summary!B3=1010");
        CliResult formula = Edit("book.xlsx", "formula.xlsx", "--ops",
            """{"ops":[{"op":"set_formula","sheet":"Summary","range":"D3","formula":"=B3*2"}]}""");

        Assert.True(value.ExitCode == 0, $"exit {value.ExitCode}: {value.StdErr}{value.StdOut}");
        JsonNode changed = Verification(value);
        Assert.Equal(["Summary!B3 960 -> 1010"], Changes(changed["directChanges"]));
        Assert.Equal(
            ["Summary!D3 1975 =B3+C3 -> 2025 =B3+C3", "Summary!B5 3660 =SUM(B2:B4) -> 3710 =SUM(B2:B4)", "Summary!D5 7523 =SUM(D2:D4) -> 7573 =SUM(D2:D4)"],
            Changes(changed["formulaResultChanges"]));
        Assert.Empty(OtherChanges(changed));
        Assert.True(formula.ExitCode == 0, $"exit {formula.ExitCode}: {formula.StdErr}{formula.StdOut}");
        JsonNode written = Verification(formula);
        Assert.Equal(["Summary!D3 1975 =B3+C3 -> 1920 =B3*2"], Changes(written["directChanges"]));
        Assert.Equal(["Summary!D5 7523 =SUM(D2:D4) -> 7468 =SUM(D2:D4)"], Changes(written["formulaResultChanges"]));
        Assert.Empty(OtherChanges(written));
    }

    /// <summary>
    /// A csv input's sheet is named after its file, and so is the verification's copy of the input:
    /// an edit of a csv, in place or to xlsx, reports its change on that sheet and no rename.
    /// </summary>
    [Theory]
    [InlineData("data.csv")]
    [InlineData("data.xlsx")]
    public void VerifiedEditOfACsv_ReportsItsSheetUnderTheNameOfTheFile(string output)
    {
        File.WriteAllText(_workspace.File("data.csv"), "Region,Q1\nEast,10\nWest,20\n");

        CliResult edited = output == "data.csv"
            ? Cli("cells", "edit", "data.csv", "--set", "data!B2=11", "--in-place", "--verify", "--output-format", "json")
            : Edit("data.csv", output, "--set", "data!B2=11");

        Assert.True(edited.ExitCode == 0, $"exit {edited.ExitCode}: {edited.StdErr}{edited.StdOut}");
        JsonNode verification = Verification(edited);
        Assert.Equal(["data!B2 10 -> 11"], Changes(verification["directChanges"]));
        Assert.Empty(OtherChanges(verification));
    }

    private CliResult Edit(string input, string output, params string[] change) =>
        Cli(["cells", "edit", input, .. change, "--out", output, "--verify", "--output-format", "json"]);

    /// <summary>
    /// A workbook whose first and active sheet, Summary, holds a table of two quarters with a
    /// total column and row of formulas; with <paramref name="otherSheet"/> a second sheet,
    /// Other, follows it, and with <paramref name="brokenInput"/> E2 holds a division by zero.
    /// </summary>
    private void Book(string name, bool otherSheet = false, bool brokenInput = false)
    {
        Assert.Equal(0, Cli("cells", "create", name, "--sheet-names", otherSheet ? "Summary,Other" : "Summary").ExitCode);
        var ops = new List<object>
        {
            new
            {
                op = "set_values", sheet = "Summary", range = "A1",
                values = new object?[][]
                {
                    ["Region", "Q1", "Q2", "Total"],
                    ["East", 1280, 1350, null],
                    ["North", 960, 1015, null],
                    ["South", 1420, 1498, null],
                    ["Total", null, null, null],
                },
            },
            new { op = "set_formula", sheet = "Summary", range = "D2:D4", formula = "=B2+C2" },
            new { op = "set_formula", sheet = "Summary", range = "B5:D5", formula = "=SUM(B2:B4)" },
        };
        if (otherSheet)
        {
            ops.Add(new { op = "set_values", sheet = "Other", range = "A1", values = new object[][] { ["Notes"] } });
        }

        if (brokenInput)
        {
            ops.Add(new { op = "set_formula", sheet = "Summary", range = "E2", formula = "=1/0" });
        }

        CliResult edited = Cli("cells", "edit", name, "--in-place",
            "--ops", JsonSerializer.Serialize(new { ops }), "--output-format", "json");
        Assert.True(edited.ExitCode == 0, edited.StdErr + edited.StdOut);
    }

    /// <summary>
    /// Runs the CLI with the test license when the run has one (a workspace strips every other
    /// license source), and in evaluation mode otherwise.
    /// </summary>
    private CliResult Cli(params string[] args) =>
        _workspace.Run(TestLicense.Path is { } license ? [.. args, "--license", license] : args);

    private static JsonNode Verification(CliResult result) => JsonNode.Parse(result.StdOut)!["verification"]!;

    private static string[] CheckedOps(JsonNode verification) =>
        [.. verification["checkedOps"]!.AsArray().Select(static id => id!.GetValue<string>())];

    /// <summary>
    /// Each change as <c>Sheet!Cell left -> right</c>, a side as its value and its formula, without
    /// the marks of evaluation mode: its warning sheet and the notice row it adds to a text table.
    /// </summary>
    private static string[] Changes(JsonNode? changes, bool rightFormulas = true) =>
        [.. (changes?.AsArray() ?? []).Where(static change => !IsEvaluationMark(change!)).Select(change => Describe(change!, rightFormulas))];

    /// <summary>
    /// The other changes as <see cref="Changes"/> describes them, a sheet change as
    /// <c>Sheet status</c>, without the marks of evaluation mode.
    /// </summary>
    private static string[] OtherChanges(JsonNode verification, bool rightFormulas = true) =>
        [.. verification["otherChanges"]!.AsArray()
            .Where(static change => !IsEvaluationMark(change!))
            .Select(change => change!["cell"] is null
                ? $"{change["sheet"]} {change["status"]}"
                : $"{Describe(change, rightFormulas)} ({change["status"]})")];

    private static bool IsEvaluationMark(JsonNode change) =>
        EvaluationSheet().IsMatch(change["sheet"]!.GetValue<string>())
        || (change["right"]?["v"] is JsonValue value && value.TryGetValue(out string? text) && CellsEvaluation.IsNotice(text));

    private static string Describe(JsonNode change, bool rightFormulas) =>
        $"{change["sheet"]}!{change["cell"]} {Side(change["left"], formula: true)} -> {Side(change["right"], rightFormulas)}";

    private static string Side(JsonNode? side, bool formula) =>
        side is null ? "(empty)" : formula && side["f"] is { } f ? $"{side["v"]} {f}" : $"{side["v"]}";

    [GeneratedRegex(@"^Evaluation Warning( \(\d+\))?$")]
    private static partial Regex EvaluationSheet();
}
