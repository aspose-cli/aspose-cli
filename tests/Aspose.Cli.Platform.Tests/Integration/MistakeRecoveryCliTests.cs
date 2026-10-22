using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>Common mistakes in each product's edit are answered with the one fix that works.</summary>
public sealed class MistakeRecoveryCliTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

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

    /// <summary>
    /// A file in a format this CLI only writes, such as the xps a cells convert wrote, is no
    /// product's to choose with --product: the hint points to a format a product reads, while a
    /// file whose content shows nothing keeps the advice to name its product.
    /// </summary>
    [Fact]
    public void ReviewOfAFormatNoProductReads_PointsToAPdfOfTheSameContent()
    {
        Assert.Equal(0, _workspace.Run("cells", "create", "book.xlsx", "--output-format", "json").ExitCode);
        Assert.Equal(0, _workspace.Run("cells", "convert", "book.xlsx", "--to", "xps", "--out", "sales.xps", "--output-format", "json").ExitCode);
        File.WriteAllText(_workspace.File("unknown.bin"), "not a document");

        CliResult written = _workspace.Run("review", "sales.xps", "--out", "review", "--output-format", "json");
        CliResult unknown = _workspace.Run("review", "unknown.bin", "--out", "review", "--output-format", "json");

        JsonNode error = JsonNode.Parse(written.StdErr)!["error"]!;
        Assert.Equal("FORMAT_UNSUPPORTED", error["code"]!.GetValue<string>());
        string hint = error["hint"]!.GetValue<string>();
        Assert.DoesNotContain("--product", hint, StringComparison.Ordinal);
        Assert.Contains("'aspose-cli cells convert <document> --to pdf'", hint, StringComparison.Ordinal);
        Assert.Contains("'aspose-cli review'", hint, StringComparison.Ordinal);
        Assert.Contains("--product", JsonNode.Parse(unknown.StdErr)!["error"]!["hint"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    /// <summary>A <c>--to</c> format the command does not write but a command beside it does points to that command.</summary>
    [Theory]
    [InlineData("cells convert a.xlsx --to png", "Run 'aspose-cli cells render a.xlsx --to png' for png.")]
    [InlineData("pdf convert a.pdf --to png", "Run 'aspose-cli pdf render a.pdf --to png' for png.")]
    [InlineData("slides convert a.pptx --to png", "Run 'aspose-cli slides render a.pptx --to png' for png.")]
    [InlineData("slides convert a.pptx --to jpeg", "Run 'aspose-cli slides render a.pptx --to jpeg' for jpeg.")]
    [InlineData("slides convert a.pptx --to svg", "Run 'aspose-cli slides render a.pptx --to svg' for svg.")]
    public void TargetFormatAnotherCommandWrites_PointsToThatCommand(string commandLine, string producer)
    {
        File.WriteAllText(_workspace.File("a.xlsx"), "not a document");
        File.WriteAllText(_workspace.File("a.pptx"), "not a document");
        File.WriteAllText(_workspace.File("a.pdf"), "not a document");
        string[] arguments = commandLine.Split(' ');

        CliResult result = _workspace.Run([.. arguments, "--output-format", "json"]);

        Assert.Equal(2, result.ExitCode);
        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.Equal("--to", error["details"]!["option"]!.GetValue<string>());
        Assert.Contains(producer, error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
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
            [".xlsx", ".xltx", ".xlsm", ".xltm", ".xlsb", ".xls", ".ods", ".csv", ".txt", ".tsv"],
            error["details"]!["available"]!.AsArray().Select(static extension => extension!.GetValue<string>()));
        Assert.Null(error["details"]!["supported"]);
        Assert.EndsWith("Must be one of: .xlsx, .xltx, .xlsm, .xltm, .xlsb, .xls, .ods, .csv, .txt, .tsv.", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
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

    /// <summary>
    /// An extension close to none the command writes but to one a sibling command writes, such as
    /// <c>.pfd</c> beside the <c>.pdf</c> of convert, suggests that extension and the sibling
    /// that writes it, as the extension itself would.
    /// </summary>
    [Theory]
    [InlineData("words edit a.docx --ops words.json --out x.pfd", ".pdf", "then run 'aspose-cli words convert <that file> --to pdf' for pdf")]
    [InlineData("cells edit a.xlsx --set Sheet1!A1=5 --out x.pfd", ".pdf", "then run 'aspose-cli cells convert <that file> --to pdf' for pdf")]
    public void MistypedExtensionOfASiblingsFormat_SuggestsItAndTheSibling(string commandLine, string suggestion, string producer)
    {
        File.WriteAllText(_workspace.File("a.docx"), "not a document");
        File.WriteAllText(_workspace.File("a.xlsx"), "not a document");
        File.WriteAllText(_workspace.File("words.json"), """{"ops":[{"op":"set_properties","title":"x"}]}""");

        CliResult result = _workspace.Run([.. commandLine.Split(' '), "--output-format", "json"]);

        Assert.Equal(2, result.ExitCode);
        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.Equal([suggestion], error["details"]!["suggestions"]!.AsArray().Select(static extension => extension!.GetValue<string>()));
        Assert.Equal("pdf", error["details"]!["format"]!.GetValue<string>());
        string hint = error["hint"]!.GetValue<string>();
        Assert.StartsWith($"Did you mean '{suggestion}'?", hint, StringComparison.Ordinal);
        Assert.Contains(producer, hint, StringComparison.Ordinal);
        Assert.False(File.Exists(_workspace.File("x.pfd")));
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
