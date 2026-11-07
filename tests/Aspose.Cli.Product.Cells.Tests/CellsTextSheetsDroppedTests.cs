using System.Text.Json.Nodes;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// A table of cells (csv, tsv, md) writes one worksheet, and its <c>SHEETS_DROPPED</c> names
/// the others: each by name up to ten, and how many more beyond them. The evaluation warning
/// sheet an evaluation save adds is no worksheet of the workbook, so a workbook made in
/// evaluation mode drops the same sheets as one made with a license, and a workbook of one
/// sheet drops none. The commands run with the test license when the run has one, and in
/// evaluation mode otherwise, where every input carries that warning sheet.
/// </summary>
public sealed class CellsTextSheetsDroppedTests : IDisposable
{
    private const string SheetsDropped = "SHEETS_DROPPED";

    private readonly TempWorkspace _workspace = new();

    public CellsTextSheetsDroppedTests() => ScenarioLicense.Project(_workspace.Path);

    public void Dispose() => _workspace.Dispose();

    [Theory]
    [InlineData("Summary", "csv", null)]
    [InlineData("Summary,Notes,Archive", "csv", "so 2 other worksheet(s) were not written: 'Notes', 'Archive'.")]
    [InlineData("Summary,Notes,Archive", "md", "so 2 other worksheet(s) were not written: 'Notes', 'Archive'.")]
    [InlineData("S01,S02,S03,S04,S05,S06,S07,S08,S09,S10,S11,S12,S13", "tsv",
        "so 12 other worksheet(s) were not written: 'S02', 'S03', 'S04', 'S05', 'S06', 'S07', 'S08', 'S09', 'S10', 'S11' and 2 more.")]
    public void TextTable_NamesTheWorksheetsItLeavesOut_AndNoEvaluationWarningSheet(string sheets, string format, string? dropped)
    {
        Assert.Equal(0, _workspace.Run("cells", "create", "book.xlsx", "--sheet-names", sheets).ExitCode);

        CliResult converted = _workspace.Run("cells", "convert", "book.xlsx", "--to", format, "--out", $"out.{format}", "--output-format", "json");

        Assert.True(converted.ExitCode == 0, $"exit {converted.ExitCode}: {converted.StdErr}{converted.StdOut}");
        JsonNode[] warnings = [.. (JsonNode.Parse(converted.StdOut)!["warnings"]?.AsArray() ?? []).Select(static warning => warning!)
            .Where(static warning => warning["code"]!.GetValue<string>() == SheetsDropped)];
        if (dropped is null)
        {
            Assert.Empty(warnings);
            return;
        }

        string message = Assert.Single(warnings)["message"]!.GetValue<string>();
        string first = sheets.Split(',')[0];
        Assert.Equal($"Only worksheet '{first}' was exported; the '{format}' output holds one worksheet, {dropped}", message);
    }
}
