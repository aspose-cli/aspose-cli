using System.Text.Json.Nodes;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>The hints of <c>cells convert</c> say what the run that follows them can do.</summary>
public sealed class CellsConvertHintTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    /// <summary>
    /// A workbook an evaluation save marked converts to valid JSON with a license, without the
    /// evaluation warning sheets, whose one row the engine would write as invalid JSON.
    /// </summary>
    [LicensedFact]
    public void LicensedJsonExport_OfAnEvaluationSavedWorkbook_IsValidJsonWithoutTheWarningSheets()
    {
        Aspose.Cli.TestKit.Scenarios.ScenarioLicense.Project(_workspace.Path);
        Assert.Equal(0, _workspace.Run("cells", "create", "book.xlsx", "--license-mode", "evaluation").ExitCode);
        Assert.Equal(0, _workspace.Run("cells", "edit", "book.xlsx", "--in-place", "--set", "Sheet1!A1=Name", "--set", "Sheet1!A2=alpha", "--license-mode", "evaluation").ExitCode);

        CliResult converted = _workspace.Run("cells", "convert", "book.xlsx", "--to", "json", "--out", "book.json", "--output-format", "json");

        Assert.True(converted.ExitCode == 0, converted.StdErr);
        string written = File.ReadAllText(_workspace.File("book.json"));
        using (System.Text.Json.JsonDocument.Parse(written))
        {
        }

        Assert.DoesNotContain("Evaluation", written, StringComparison.Ordinal);
    }

    /// <summary>
    /// In evaluation mode a text export writes only the first sheet, so when it skips the
    /// evaluation warning sheet its hint says another sheet needs a license, rather than naming
    /// a --sheets selection that evaluation mode refuses.
    /// </summary>
    [Theory]
    [InlineData("csv")]
    [InlineData("md")]
    public void EvaluationTextExport_SkippingTheWarningSheet_SaysAnotherSheetNeedsALicense(string format)
    {
        Assert.Equal(0, _workspace.Run("cells", "create", "book.xlsx", "--license-mode", "evaluation").ExitCode);
        Assert.Equal(0, _workspace.Run("cells", "edit", "book.xlsx", "--in-place", "--set", "Sheet1!A1=5", "--license-mode", "evaluation").ExitCode);

        CliResult converted = _workspace.Run("cells", "convert", "book.xlsx", "--to", format, "--out", $"book.{format}", "--license-mode", "evaluation", "--output-format", "json");

        Assert.True(converted.ExitCode == 0, converted.StdErr);
        string hint = Assert.Single(JsonNode.Parse(converted.StdOut)!["warnings"]!.AsArray(), static warning => warning!["code"]!.GetValue<string>() == "ACTIVE_SHEET_SKIPPED")!["hint"]!.GetValue<string>();
        Assert.StartsWith($"Evaluation mode exports only the first sheet to {format};", hint, StringComparison.Ordinal);
        Assert.Contains("license", hint, StringComparison.Ordinal);
    }

    /// <summary>
    /// The engine writes invalid JSON for a sheet whose data is only its header row
    /// (CELLS-JSON-HEADER-ONLY), so a JSON export of one is refused before anything is written;
    /// a sheet with data rows still converts.
    /// </summary>
    [Fact]
    public void JsonExport_OfAHeaderOnlySheet_IsRefusedBeforeWriting()
    {
        File.WriteAllText(_workspace.File("header.csv"), "Name,Qty\n");
        File.WriteAllText(_workspace.File("rows.csv"), "Name,Qty\nalpha,3\n");

        CliResult refused = _workspace.Run("cells", "convert", "header.csv", "--to", "json", "--out", "header.json", "--output-format", "json");
        CliResult converted = _workspace.Run("cells", "convert", "rows.csv", "--to", "json", "--out", "rows.json", "--output-format", "json");

        JsonNode error = JsonNode.Parse(refused.StdErr)!["error"]!;
        Assert.Equal("FEATURE_UNSUPPORTED", error["code"]!.GetValue<string>());
        Assert.Equal("engine-defect", error["details"]!["reason"]!.GetValue<string>());
        Assert.Contains("'header'", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.False(File.Exists(_workspace.File("header.json")));
        Assert.True(converted.ExitCode == 0, converted.StdErr);
        using System.Text.Json.JsonDocument rows = System.Text.Json.JsonDocument.Parse(File.ReadAllText(_workspace.File("rows.json")));
    }
}
