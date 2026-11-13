using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cells;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// What a verified edit says about the operations it did not read back, and which warnings fail
/// it. The commands run with the test license when the run has one, and in evaluation mode
/// otherwise.
/// </summary>
public sealed class CellsVerifyCoverageTests : IDisposable
{
    private readonly CellsFixture _fixture = new();
    private readonly TempWorkspace _workspace = new();

    public void Dispose()
    {
        _workspace.Dispose();
        _fixture.Dispose();
    }

    /// <summary>
    /// The comparison reads cell values, formulas and sheets, so a format is named with that
    /// reason. An edit it reads back whole has no such field.
    /// </summary>
    [Fact]
    public void Verify_NamesAnOperationItDoesNotCompareWithTheReason_AndOmitsTheFieldWhenItReadsEveryOne()
    {
        string input = _fixture.CreateSalesWorkbook();

        JsonNode partial = Verification(Edit(input, """{"ops":[{"id":"cell","op":"set_values","sheet":"Data","range":"A20","values":[[1]]},{"id":"bold","op":"format_range","sheet":"Data","range":"A20","style":{"bold":true}}]}"""));
        JsonNode whole = Verification(Edit(input, """{"ops":[{"id":"cell","op":"set_values","sheet":"Data","range":"A20","values":[[1]]}]}"""));

        Assert.True(partial["ok"]!.GetValue<bool>());
        Assert.Equal(["cell"], partial["checkedOps"]!.AsArray().Select(static id => id!.GetValue<string>()));
        JsonNode item = Assert.Single(partial["uncheckedOps"]!.AsArray())!;
        Assert.Equal("bold", item["id"]!.GetValue<string>());
        Assert.Contains("cell values, formulas and sheets", item["reason"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Null(whole["uncheckedOps"]);
    }

    /// <summary>The comparison stops at the first sixteen targets, and says so of the ones after.</summary>
    [Fact]
    public void Verify_NamesAnOperationBeyondTheTargetsItCompares()
    {
        string input = _fixture.CreateSalesWorkbook();
        object[] ops = [.. Enumerable.Range(1, 17).Select(static row => new { id = $"write{row}", op = "set_values", sheet = "Data", range = $"K{row}", values = new[] { new[] { row } } })];

        JsonNode verification = Verification(Edit(input, JsonSerializer.Serialize(new { ops })));

        JsonNode item = Assert.Single(verification["uncheckedOps"]!.AsArray())!;
        Assert.Equal("write17", item["id"]!.GetValue<string>());
        Assert.Contains("first 16 targets", item["reason"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    /// <summary>
    /// The text a person reads counts every direct change, also when the result lists only the
    /// first 100 of them.
    /// </summary>
    [Theory]
    [InlineData("table")]
    [InlineData("markdown")]
    public void Verify_InText_CountsEveryDirectChange(string format)
    {
        string input = _fixture.CreateSalesWorkbook();
        string rows = string.Join(",", Enumerable.Range(1, 1003).Select(static row => $"[{row}]"));

        CliResult edited = Cli("cells", "edit", input, "--ops", $$"""{"ops":[{"op":"set_values","sheet":"Data","range":"K1","values":[{{rows}}]}]}""",
            "--out", "out.xlsx", "--overwrite", "--verify", "--output-format", format);

        Assert.True(edited.ExitCode == 0, $"exit {edited.ExitCode}: {edited.StdErr}{edited.StdOut}");
        Assert.Contains("1003 direct,", edited.StdOut, StringComparison.Ordinal);
    }

    /// <summary>An operation a best-effort batch could not apply is unchecked because it failed.</summary>
    [Fact]
    public void Verify_NamesAFailedOperationAsFailed()
    {
        string input = _fixture.CreateSalesWorkbook();
        CliResult edited = Cli("cells", "edit", input, "--best-effort", "--out", "out.xlsx", "--verify", "--output-format", "json",
            "--ops", """{"ops":[{"id":"bad","op":"set_values","sheet":"Missing","range":"A1","values":[[1]]},{"id":"cell","op":"set_values","sheet":"Data","range":"A20","values":[[1]]}]}""");

        Assert.True(edited.ExitCode == 8, $"exit {edited.ExitCode}: {edited.StdErr}{edited.StdOut}");
        JsonNode verification = Verification(edited);
        Assert.Equal(["cell"], verification["checkedOps"]!.AsArray().Select(static id => id!.GetValue<string>()));
        JsonNode item = Assert.Single(verification["uncheckedOps"]!.AsArray())!;
        Assert.Equal("bad", item["id"]!.GetValue<string>());
        Assert.Contains("failed", item["reason"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Unlike a Word or PowerPoint link, a linked picture the load cannot read is gone from the
    /// output: the warning about it is an issue, and the output has no picture.
    /// </summary>
    [Fact]
    public void Verify_OfAWorkbookWhoseLinkedPictureTheLoadCouldNotRead_FailsBecauseTheOutputLacksIt()
    {
        string input = _fixture.Temp.File("linked.xlsx");
        using (var workbook = new Workbook())
        {
            Worksheet sheet = workbook.Worksheets[0];
            sheet.Name = "Data";
            using var image = new MemoryStream(ResourceHttpServer.Image, writable: false);
            sheet.Pictures[sheet.Pictures.Add(2, 1, image)].SourceFullName = "http://127.0.0.1:9/logo.png";
            workbook.Save(input, SaveFormat.Xlsx);
        }

        CliResult edited = Run(input, """{"ops":[{"id":"cell","op":"set_values","sheet":"Data","range":"A1","values":[[1]]}]}""");

        Assert.True(edited.ExitCode == 8, $"exit {edited.ExitCode}: {edited.StdErr}{edited.StdOut}");
        JsonNode issue = Assert.Single(Verification(edited)["issues"]!.AsArray())!;
        Assert.Equal("REMOTE_RESOURCES_BLOCKED", issue["code"]!.GetValue<string>());
        using var output = new Workbook(_workspace.File("out.xlsx"));
        Assert.Empty(output.Worksheets["Data"].Pictures);
    }

    private CliResult Edit(string input, string ops)
    {
        CliResult edited = Run(input, ops);
        Assert.True(edited.ExitCode == 0, edited.StdErr + edited.StdOut);
        return edited;
    }

    private CliResult Run(string input, string ops) =>
        Cli("cells", "edit", input, "--ops", ops, "--out", "out.xlsx", "--overwrite", "--verify", "--output-format", "json");

    private CliResult Cli(params string[] args) =>
        _workspace.Run(TestLicense.Path is { } license ? [.. args, "--license", license] : args);

    private static JsonNode Verification(CliResult result) => JsonNode.Parse(result.StdOut)!["verification"]!;
}
