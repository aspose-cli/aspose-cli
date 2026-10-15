using System.Text.Json.Nodes;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>The hints of <c>cells convert</c> say what the run that follows them can do.</summary>
public sealed class CellsConvertHintTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

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
}
