using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Licensing;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>Text export must never substitute another worksheet silently.</summary>
public sealed class CellsTextSelectionTests : IClassFixture<CellsFixture>
{
    private readonly CellsFixture _fixture;

    public CellsTextSelectionTests(CellsFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData("csv")]
    [InlineData("tsv")]
    [InlineData("md")]
    public void Convert_SelectedNonFirstSheet_ExportsItOrRefusesEvaluationBeforePublication(string format)
    {
        string source = SourceWorkbook($"text-selection-{format}.xlsx");
        string output = _fixture.Temp.File($"text-selection.{format}");
        byte[] sentinel = "existing report must remain intact"u8.ToArray();
        File.WriteAllBytes(output, sentinel);
        byte[] sourceBytes = File.ReadAllBytes(source);
        var request = new ConvertRequest
        {
            TargetFormatId = format,
            SheetName = "Detail",
            OutputPath = output,
            Overwrite = true,
        };

        if (_fixture.LicenseState == LicenseState.Evaluation)
        {
            CliException error = Assert.Throws<CliException>(() => _fixture.Engine.Convert(source, request));
            Assert.Equal(ErrorCodes.EvaluationLimit, error.Code);
            Assert.Contains("Detail", error.Message, StringComparison.Ordinal);
            Assert.Contains("Dashboard", error.Message, StringComparison.Ordinal);
            Assert.Contains("license", error.Hint!, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(sentinel, File.ReadAllBytes(output));
            string missingOutput = _fixture.Temp.File($"not-published.{format}");
            Assert.Throws<CliException>(() => _fixture.Engine.Convert(source, request with { OutputPath = missingOutput }));
            Assert.False(File.Exists(missingOutput));
        }
        else
        {
            ConvertResult result = _fixture.Engine.Convert(source, request);
            Assert.Equal("Detail", result.Sheet);
            string text = File.ReadAllText(output);
            Assert.Contains("SO-001", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Executive overview", text, StringComparison.Ordinal);
        }

        Assert.Equal(sourceBytes, File.ReadAllBytes(source));
    }

    [Theory]
    [InlineData("csv")]
    [InlineData("tsv")]
    [InlineData("md")]
    public void Convert_FirstSheet_RemainsAvailableWithTruthfulContent(string format)
    {
        string source = SourceWorkbook($"text-first-{format}.xlsx");
        string output = _fixture.Temp.File($"text-first.{format}");
        ConvertResult result = _fixture.Engine.Convert(source, new ConvertRequest
        {
            TargetFormatId = format,
            SheetName = "Dashboard",
            OutputPath = output,
            Overwrite = true,
        });

        Assert.Equal("Dashboard", result.Sheet);
        string text = File.ReadAllText(output);
        Assert.Contains("Executive overview", text, StringComparison.Ordinal);
        Assert.DoesNotContain("SO-001", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("csv")]
    [InlineData("tsv")]
    [InlineData("md")]
    public void Convert_DefaultSelection_ReportsTheSheetActuallyExported(string format)
    {
        string source = SourceWorkbook($"text-default-{format}.xlsx");
        source = _fixture.Engine.ApplyOps(source, OpsParser.Parse(
            """{ "ops": [{ "op": "set_active_sheet", "sheet": "Detail" }] }"""), new EditRequest
            {
                OutputPath = _fixture.Temp.File($"text-default-active-{format}.xlsx"),
                Overwrite = true,
            }).Output!.Path;
        string output = _fixture.Temp.File($"text-default.{format}");
        ConvertResult result = _fixture.Engine.Convert(source, new ConvertRequest
        {
            TargetFormatId = format,
            OutputPath = output,
            Overwrite = true,
        });

        bool evaluation = _fixture.LicenseState == LicenseState.Evaluation;
        string expectedSheet = evaluation ? "Dashboard" : "Detail";
        string text = File.ReadAllText(output);
        Assert.Contains(evaluation ? "Executive overview" : "SO-001", text, StringComparison.Ordinal);
        Assert.DoesNotContain(evaluation ? "SO-001" : "Executive overview", text, StringComparison.Ordinal);
        Assert.Contains(result.Warnings!, warning => warning.Code == "SHEETS_DROPPED"
            && warning.Message.Contains($"'{expectedSheet}'", StringComparison.Ordinal));
    }
    private string SourceWorkbook(string output)
    {
        string source = _fixture.Engine.CreateWorkbook(new NewWorkbookRequest
        {
            OutputPath = _fixture.Temp.File(output),
            SheetNames = ["Dashboard", "Detail"],
            Overwrite = true,
        }).Output.Path;
        return _fixture.Engine.ApplyOps(source, OpsParser.Parse(
            """
            { "ops": [
              { "op": "set_values", "sheet": "Dashboard", "range": "A1", "values": [["Executive overview",1276.5]] },
              { "op": "set_values", "sheet": "Detail", "range": "A1",
                "values": [["Order","Net revenue"],["SO-001",1234.5],["SO-002",42]] },
              { "op": "set_active_sheet", "sheet": "Dashboard" }
            ] }
            """), new EditRequest
            {
                OutputPath = _fixture.Temp.File($"seeded-{output}"),
                Overwrite = true,
            }).Output!.Path;
    }
}
