using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>Common mistakes in each product's edit are answered with the one fix that works.</summary>
public sealed class MistakeRecoveryCliTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    [Theory]
    [InlineData("words", "a.docx", """{"op":"replace_text","find":"30","replacement":"15"}""", "replace")]
    [InlineData("slides", "a.pptx", """{"op":"replace_text","find":"30","replacement":"15"}""", "replace")]
    [InlineData("slides", "a.pptx", """{"op":"insert_chart","slide":1,"type":"bar"}""", "kind")]
    [InlineData("slides", "a.pptx", """{"op":"insert_chart","slide":1,"box":{}}""", "rect")]
    [InlineData("cells", "a.xlsx", """{"op":"set_style","range":"A1","style":{"bold":true}}""", "format_range")]
    [InlineData("pdf", "a.pdf", """{"op":"add_watermark","text":"DRAFT"}""", "add_watermark_text")]
    public void EditOperationMistake_SuggestsTheNameThatWorks(string product, string file, string operation, string suggestion)
    {
        // The document is read only after its operations parse, so any bytes do.
        File.WriteAllText(_workspace.File(file), "not a document");
        File.WriteAllText(_workspace.File("ops.json"), $$"""{"ops":[{{operation}}]}""");

        CliResult result = _workspace.Run(product, "edit", file, "--ops", "ops.json", "--output", "json");

        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Equal("OPS_INVALID", error["code"]!.GetValue<string>());
        Assert.Equal(suggestion, error["details"]!["suggestions"]![0]!.GetValue<string>());
        Assert.StartsWith($"Did you mean '{suggestion}'", error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"op":"clear_rnage","range":"A1"}""", "clear_range")]
    [InlineData("""{"op":"clear_range","range":"A1","waht":"all"}""", "what")]
    [InlineData("""{"op":"clear_range","range":"A1","what":"contnets"}""", "contents")]
    public void CellsOperationMistake_SuggestsAnArrayOfNames(string operation, string suggestion)
    {
        File.WriteAllText(_workspace.File("ops.json"), $$"""{"ops":[{{operation}}]}""");
        CliResult created = _workspace.Run("cells", "create", "a.xlsx", "--output", "json");
        Assert.Equal(0, created.ExitCode);

        CliResult result = _workspace.Run("cells", "edit", "a.xlsx", "--ops", "ops.json", "--dry-run", "--output", "json");

        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Equal("OPS_INVALID", error["code"]!.GetValue<string>());
        Assert.Equal(suggestion, error["details"]!["suggestions"]!.AsArray()[0]!.GetValue<string>());
        Assert.Null(error["details"]!["suggestion"]);
    }

    [Theory]
    [InlineData("cells edit a.xlsx --set Sheet1!A1=5 --out x.pdf", "--out", "use .xlsx,", "aspose-cli cells convert <that file> --to pdf")]
    [InlineData("slides edit a.pptx --ops ops.json --out x.pdf", "--out", "use .pptx,", "aspose-cli slides convert <that file> --to pdf")]
    [InlineData("slides create x.ppt --from-markdown deck.md", "file", "use .pptx,", "aspose-cli slides convert <that file> --to ppt")]
    public void OutputFormatTheCommandCannotWrite_PointsToTheCommandThatWritesIt(
        string commandLine, string option, string writes, string producer)
    {
        File.WriteAllText(_workspace.File("a.xlsx"), "not a document");
        File.WriteAllText(_workspace.File("a.pptx"), "not a document");
        File.WriteAllText(_workspace.File("ops.json"), """{"ops":[{"op":"set_notes","slide":1,"text":"x"}]}""");
        File.WriteAllText(_workspace.File("deck.md"), "# Title");
        string[] arguments = commandLine.Split(' ');

        CliResult result = _workspace.Run([.. arguments, "--output", "json"]);

        Assert.Equal(2, result.ExitCode);
        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Equal("USAGE_ERROR", error["code"]!.GetValue<string>());
        Assert.Equal(option, error["details"]!["option"]!.GetValue<string>());
        Assert.StartsWith($"{option} '", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains($"which aspose-cli {arguments[0]} {arguments[1]} does not write; {writes}", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
        // The command writes its own formats, never the one asked for.
        Assert.DoesNotContain(error["details"]!["requested"]!.GetValue<string>(), error["details"]!["supported"]!.AsArray().Select(static id => id!.GetValue<string>()));
        Assert.Contains($"then run '{producer}'", error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.False(File.Exists(_workspace.File(arguments[1] == "create" ? arguments[2] : "x.pdf")));
    }

    [Fact]
    public void EditOutputFormatTheEditCannotWrite_ListsOnlyWhatTheEditWrites()
    {
        File.WriteAllText(_workspace.File("a.xlsx"), "not a document");

        CliResult result = _workspace.Run("cells", "edit", "a.xlsx", "--set", "Sheet1!A1=5", "--out", "x.png", "--output", "json");

        Assert.Equal(2, result.ExitCode);
        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Equal("USAGE_ERROR", error["code"]!.GetValue<string>());
        Assert.Equal(
            ["xlsx", "xltx", "xlsm", "xltm", "xlsb", "xls", "ods", "csv", "tsv", "html", "mhtml"],
            error["details"]!["supported"]!.AsArray().Select(static id => id!.GetValue<string>()));
        Assert.EndsWith("use .xlsx, .xltx, .xlsm, .xltm, .xlsb, .xls, .ods, .csv, .tsv, .html, .htm, .mhtml.", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.DoesNotContain(".pdf", error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains("then run 'aspose-cli cells render <that file> --to png'", error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Table output states a mistake's question once, in the hint, for a target the document does
    /// not contain and for an unknown option alike.
    /// </summary>
    [Theory]
    [InlineData("cells query range a.xlsx --range Shet1!A1:B2", "error SHEET_NOT_FOUND: ", "Did you mean 'Sheet1'?")]
    [InlineData("cells query range a.xlsx --rnage Sheet1!A1", "error USAGE_ERROR: ", "Did you mean '--range'?")]
    public void TableOutput_AsksTheMistakesQuestionOnce(string commandLine, string error, string question)
    {
        Assert.Equal(0, _workspace.Run("cells", "create", "a.xlsx", "--output", "json").ExitCode);

        CliResult result = _workspace.Run([.. commandLine.Split(' '), "--output", "table"]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(error, result.StdErr, StringComparison.Ordinal);
        Assert.Contains($"  hint: {question}", result.StdErr, StringComparison.Ordinal);
        Assert.Single(result.StdErr.Split('\n'), static line => line.Contains("did you mean", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("slides edit a.pptx --ops ops.json --out noext", "noext")]
    [InlineData("slides edit a.pptx --ops ops.json --out dotonly.", "dotonly")]
    [InlineData("slides create noext --from-markdown deck.md", "noext")]
    [InlineData("slides create dotonly. --from-markdown deck.md", "dotonly")]
    [InlineData("words edit a.docx --ops words.json --out noext", "noext")]
    [InlineData("words create noext --markdown deck.md", "noext")]
    public void OutputWithoutExtension_IsAUsageError(string commandLine, string output)
    {
        File.WriteAllText(_workspace.File("a.pptx"), "not a document");
        File.WriteAllText(_workspace.File("a.docx"), "not a document");
        File.WriteAllText(_workspace.File("ops.json"), """{"ops":[{"op":"set_notes","slide":1,"text":"x"}]}""");
        File.WriteAllText(_workspace.File("words.json"), """{"ops":[{"op":"set_properties","title":"x"}]}""");
        File.WriteAllText(_workspace.File("deck.md"), "# Title");

        CliResult result = _workspace.Run([.. commandLine.Split(' '), "--output", "json"]);

        Assert.Equal(2, result.ExitCode);
        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Equal("USAGE_ERROR", error["code"]!.GetValue<string>());
        Assert.Contains("has no extension", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Null(error["details"]!["requested"]);
        Assert.NotEmpty(error["details"]!["extensions"]!.AsArray());
        Assert.False(File.Exists(_workspace.File(output)));
    }
}
