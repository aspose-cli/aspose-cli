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
    [InlineData("slides", "a.pptx", """{"op":"set_body","slide":1,"paragraphs":[{"text":"x","bullet":true}]}""", "list")]
    [InlineData("slides", "a.pptx", """{"op":"set_body","slide":1,"paragraphs":[{"text":"x","level":2}]}""", "list")]
    [InlineData("slides", "a.pptx", """{"op":"set_body","slide":1,"paragraphs":[{"text":"x","listLevel":2}]}""", "list")]
    [InlineData("cells", "a.xlsx", """{"op":"set_style","range":"A1","style":{"bold":true}}""", "format_range")]
    [InlineData("pdf", "a.pdf", """{"op":"add_watermark","text":"DRAFT"}""", "add_watermark_text")]
    public void EditOperationMistake_SuggestsTheNameThatWorks(string product, string file, string operation, string suggestion)
    {
        // The document is read only after its operations parse, so any bytes do.
        File.WriteAllText(_workspace.File(file), "not a document");
        File.WriteAllText(_workspace.File("ops.json"), $$"""{"ops":[{{operation}}]}""");

        CliResult result = _workspace.Run(product, "edit", file, "--ops", "ops.json", "--output-format", "json");

        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Equal("OPS_INVALID", error["code"]!.GetValue<string>());
        Assert.Equal(suggestion, error["details"]!["suggestions"]![0]!.GetValue<string>());
        Assert.StartsWith($"Did you mean '{suggestion}'", error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    /// <summary>Every unknown field of one operation is reported at once, each with the field meant.</summary>
    [Theory]
    [InlineData("slides", "a.pptx", """{"op":"format_shape","slide":1,"shapeId":4,"style":{"fill":"#FF0000","size":20}}""", "fillColor", "fontSize")]
    [InlineData("pdf", "a.pdf", """{"op":"add_page_numbers","txet":"{n}","placment":"bottom-center"}""", "text", "placement")]
    public void SeveralUnknownFieldsOfOneOperation_AreListedTogether(string product, string file, string operation, string first, string second)
    {
        File.WriteAllText(_workspace.File(file), "not a document");
        File.WriteAllText(_workspace.File("ops.json"), $$"""{"ops":[{{operation}}]}""");

        CliResult result = _workspace.Run(product, "edit", file, "--ops", "ops.json", "--output-format", "json");

        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Equal("OPS_INVALID", error["code"]!.GetValue<string>());
        JsonArray errors = error["details"]!["errors"]!.AsArray();
        Assert.Equal(2, errors.Count);
        Assert.All(errors, item => Assert.Equal("op-0001", item!["id"]!.GetValue<string>()));
        Assert.Equal([first, second], errors.Select(static item => item!["suggestions"]![0]!.GetValue<string>()));
        Assert.StartsWith("2 problems make one operation invalid", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    /// <summary>An unknown op names the entry by the op it wrote, and the same words in another order are the name meant.</summary>
    [Fact]
    public void UnknownOperation_StatesTheOpItWroteAndSuggestsTheReorderedName()
    {
        File.WriteAllText(_workspace.File("a.pdf"), "not a document");
        File.WriteAllText(_workspace.File("ops.json"), """{"ops":[{"op":"add_image_stamp","image":"x.png"}]}""");

        CliResult result = _workspace.Run("pdf", "edit", "a.pdf", "--ops", "ops.json", "--output-format", "json");

        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Equal("OPS_INVALID", error["code"]!.GetValue<string>());
        Assert.Equal("op-0001", error["details"]!["id"]!.GetValue<string>());
        Assert.Equal("add_image_stamp", error["details"]!["op"]!.GetValue<string>());
        Assert.Equal("add_stamp_image", error["details"]!["suggestions"]![0]!.GetValue<string>());
    }

    [Theory]
    [InlineData("""{"op":"clear_rnage","range":"A1"}""", "clear_range")]
    [InlineData("""{"op":"clear_range","range":"A1","waht":"all"}""", "what")]
    [InlineData("""{"op":"clear_range","range":"A1","what":"contnets"}""", "contents")]
    public void CellsOperationMistake_SuggestsAnArrayOfNames(string operation, string suggestion)
    {
        File.WriteAllText(_workspace.File("ops.json"), $$"""{"ops":[{{operation}}]}""");
        CliResult created = _workspace.Run("cells", "create", "a.xlsx", "--output-format", "json");
        Assert.Equal(0, created.ExitCode);

        CliResult result = _workspace.Run("cells", "edit", "a.xlsx", "--ops", "ops.json", "--dry-run", "--output-format", "json");

        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Equal("OPS_INVALID", error["code"]!.GetValue<string>());
        Assert.Equal(suggestion, error["details"]!["suggestions"]!.AsArray()[0]!.GetValue<string>());
        Assert.Null(error["details"]!["suggestion"]);
    }

    [Theory]
    [InlineData("cells edit a.xlsx --set Sheet1!A1=5 --out x.pdf", "--out", "Must be one of: .xlsx,", "then run 'aspose-cli cells convert <that file> --to pdf' for pdf")]
    [InlineData("slides edit a.pptx --ops ops.json --out x.pdf", "--out", "Must be one of: .pptx,", "then run 'aspose-cli slides convert <that file> --to pdf' for pdf")]
    [InlineData("slides create x.ppt --from-markdown deck.md", "file", "Must be one of: .pptx,", "then run 'aspose-cli slides convert <that file> --to ppt' for ppt")]
    [InlineData("cells create x.pdf", "file", "Must be one of: .xlsx,", "then run 'aspose-cli cells convert <that file> --to pdf' for pdf")]
    [InlineData("pdf edit a.pdf --ops ops.json --out x.md", "--out", "Must be one of: .pdf.", "then run 'aspose-cli pdf convert <that file> --to md' for md")]
    [InlineData("pdf create x.md --from-text deck.md", "file", "Must be one of: .pdf.", "then run 'aspose-cli pdf convert <that file> --to md' for md")]
    [InlineData("words edit a.docx --set bookmark:A=x --out x.pdf", "--out", "Must be one of: .doc,", "then run 'aspose-cli words convert <that file> --to pdf' for pdf")]
    [InlineData("words edit a.docx --set bookmark:A=x --out x.md", "--out", "Must be one of: .doc,", "then run 'aspose-cli words convert <that file> --to md' for md")]
    [InlineData("words create x.pdf --from-markdown deck.md", "file", "Must be one of: .doc,", "then run 'aspose-cli words convert <that file> --to pdf' for pdf")]
    [InlineData("words create x.md --from-markdown deck.md", "file", "Must be one of: .doc,", "then run 'aspose-cli words convert <that file> --to md' for md")]
    [InlineData("cells render a.xlsx --out x.pdf", "--out", "Must be one of: .png,", "or run 'aspose-cli cells convert a.xlsx --to pdf --out x.pdf' for pdf")]
    [InlineData("slides render a.pptx --out x.pdf", "--out", "Must be one of: .png,", "or run 'aspose-cli slides convert a.pptx --to pdf --out x.pdf' for pdf")]
    public void OutputFormatTheCommandCannotWrite_PointsToTheCommandThatWritesIt(
        string commandLine, string option, string writes, string producer)
    {
        File.WriteAllText(_workspace.File("a.xlsx"), "not a document");
        File.WriteAllText(_workspace.File("a.pptx"), "not a document");
        File.WriteAllText(_workspace.File("a.pdf"), "not a document");
        File.WriteAllText(_workspace.File("a.docx"), "not a document");
        File.WriteAllText(_workspace.File("ops.json"), """{"ops":[{"op":"set_notes","slide":1,"text":"x"}]}""");
        File.WriteAllText(_workspace.File("deck.md"), "# Title");
        string[] arguments = commandLine.Split(' ');

        CliResult result = _workspace.Run([.. arguments, "--output-format", "json"]);

        Assert.Equal(2, result.ExitCode);
        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        // A positional argument such as file is details.argument; an option is details.option.
        Assert.Equal(option, error["details"]![option.StartsWith('-') ? "option" : "argument"]!.GetValue<string>());
        Assert.StartsWith($"{option} '", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains($"which aspose-cli {arguments[0]} {arguments[1]} does not write. {writes}", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
        // The extension names a format another command writes, so it is no mistake to correct.
        Assert.NotNull(error["details"]!["format"]);
        Assert.Null(error["details"]!["suggestions"]);
        Assert.Contains(producer, error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.False(File.Exists(_workspace.File(arguments[1] == "create" ? arguments[2] : arguments[Array.IndexOf(arguments, "--out") + 1])));
    }

    [Theory]
    [InlineData("pdf render a.pdf --out x.pdf")]
    [InlineData("cells render a.xlsx --out x.xlsx")]
    public void RenderOutputInTheInputsOwnFormat_SaysTheInputIsAlreadyThatFormat(string commandLine)
    {
        File.WriteAllText(_workspace.File("a.pdf"), "not a document");
        File.WriteAllText(_workspace.File("a.xlsx"), "not a document");
        string[] arguments = commandLine.Split(' ');

        CliResult result = _workspace.Run([.. arguments, "--output-format", "json"]);

        Assert.Equal(2, result.ExitCode);
        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        string message = error["message"]!.GetValue<string>();
        Assert.Contains("extension of its input, which already is that file", message, StringComparison.Ordinal);
        Assert.Contains($"aspose-cli {arguments[0]} render writes only images. Must be one of: .png,", message, StringComparison.Ordinal);
        Assert.DoesNotContain("convert", error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Null(error["details"]!["suggestions"]);
        Assert.Null(error["details"]!["format"]);
    }

    [Fact]
    public void EditOutputFormatTheEditCannotWrite_ListsOnlyWhatTheEditWrites()
    {
        File.WriteAllText(_workspace.File("a.xlsx"), "not a document");

        CliResult result = _workspace.Run("cells", "edit", "a.xlsx", "--set", "Sheet1!A1=5", "--out", "x.png", "--output-format", "json");

        Assert.Equal(2, result.ExitCode);
        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.Equal(
            [".xlsx", ".xltx", ".xlsm", ".xltm", ".xlsb", ".xls", ".ods", ".csv", ".txt", ".tsv", ".html", ".htm", ".mhtml"],
            error["details"]!["available"]!.AsArray().Select(static extension => extension!.GetValue<string>()));
        Assert.Null(error["details"]!["supported"]);
        Assert.EndsWith("Must be one of: .xlsx, .xltx, .xlsm, .xltm, .xlsb, .xls, .ods, .csv, .txt, .tsv, .html, .htm, .mhtml.", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.DoesNotContain(".pdf", error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains("then run 'aspose-cli cells render <that file> --to png'", error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Table output states a mistake's question once, in the hint, for a target the document does
    /// not contain and for an unknown option alike.
    /// </summary>
    [Theory]
    [InlineData("cells read a.xlsx --range Shet1!A1:B2", "error SHEET_NOT_FOUND: ", "Did you mean 'Sheet1'?")]
    [InlineData("cells read a.xlsx --rnage Sheet1!A1", "error USAGE_ERROR: ", "Did you mean '--range'?")]
    public void TableOutput_AsksTheMistakesQuestionOnce(string commandLine, string error, string question)
    {
        Assert.Equal(0, _workspace.Run("cells", "create", "a.xlsx", "--output-format", "json").ExitCode);

        CliResult result = _workspace.Run([.. commandLine.Split(' '), "--output-format", "table"]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(error, result.StdErr, StringComparison.Ordinal);
        Assert.Contains($"  hint: {question}", result.StdErr, StringComparison.Ordinal);
        Assert.Single(result.StdErr.Split('\n'), static line => line.Contains("did you mean", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("words convert a.docx --out m3.docz", ".docx")]
    [InlineData("pdf convert a.pdf --out t3.dox", ".docx")]
    [InlineData("cells convert a.xlsx --out b.xslx", ".xlsx")]
    [InlineData("cells edit a.xlsx --set Sheet1!A1=5 --out b.xslx", ".xlsx")]
    public void MistypedOutputExtension_SuggestsTheExtensionMeant(string commandLine, string suggestion)
    {
        File.WriteAllText(_workspace.File("a.docx"), "not a document");
        File.WriteAllText(_workspace.File("a.pdf"), "not a document");
        File.WriteAllText(_workspace.File("a.xlsx"), "not a document");

        CliResult result = _workspace.Run([.. commandLine.Split(' '), "--output-format", "json"]);

        Assert.Equal(2, result.ExitCode);
        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.Contains(suggestion, error["details"]!["suggestions"]!.AsArray().Select(static extension => extension!.GetValue<string>()));
        Assert.Contains(suggestion, error["details"]!["available"]!.AsArray().Select(static extension => extension!.GetValue<string>()));
        Assert.StartsWith("Did you mean ", error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains($"'{suggestion}'", error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("slides edit a.pptx --ops ops.json --out noext", "noext")]
    [InlineData("slides edit a.pptx --ops ops.json --out dotonly.", "dotonly")]
    [InlineData("slides create noext --from-markdown deck.md", "noext")]
    [InlineData("slides create dotonly. --from-markdown deck.md", "dotonly")]
    [InlineData("words edit a.docx --ops words.json --out noext", "noext")]
    [InlineData("words create noext --from-markdown deck.md", "noext")]
    public void OutputWithoutExtension_IsAnInvalidOption(string commandLine, string output)
    {
        File.WriteAllText(_workspace.File("a.pptx"), "not a document");
        File.WriteAllText(_workspace.File("a.docx"), "not a document");
        File.WriteAllText(_workspace.File("ops.json"), """{"ops":[{"op":"set_notes","slide":1,"text":"x"}]}""");
        File.WriteAllText(_workspace.File("words.json"), """{"ops":[{"op":"set_properties","title":"x"}]}""");
        File.WriteAllText(_workspace.File("deck.md"), "# Title");

        CliResult result = _workspace.Run([.. commandLine.Split(' '), "--output-format", "json"]);

        Assert.Equal(2, result.ExitCode);
        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.Contains("has no extension", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Null(error["details"]!["suggestions"]);
        Assert.NotEmpty(error["details"]!["available"]!.AsArray());
        Assert.False(File.Exists(_workspace.File(output)));
    }
}
