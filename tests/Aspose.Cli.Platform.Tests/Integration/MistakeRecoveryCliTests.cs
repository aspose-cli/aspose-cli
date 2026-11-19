using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>Common mistakes in each product's commands are refused with what the command takes.</summary>
public sealed class MistakeRecoveryCliTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    /// <summary>Every unknown field of one operation is reported at once.</summary>
    [Theory]
    [InlineData("pdf", "a.pdf", """{"op":"add_page_numbers","txet":"{n}","placment":"bottom-center"}""")]
    public void SeveralUnknownFieldsOfOneOperation_AreListedTogether(string product, string file, string operation)
    {
        File.WriteAllText(_workspace.File(file), "not a document");
        File.WriteAllText(_workspace.File("ops.json"), $$"""{"ops":[{{operation}}]}""");

        CliResult result = _workspace.Run(product, "edit", file, "--ops", "ops.json", "--output-format", "json");

        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Equal("OPS_INVALID", error["code"]!.GetValue<string>());
        JsonArray errors = error["details"]!["errors"]!.AsArray();
        Assert.Equal(2, errors.Count);
        Assert.All(errors, item => Assert.Equal("op-0001", item!["id"]!.GetValue<string>()));
        Assert.StartsWith("2 problems make one operation invalid", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    /// <summary>An unknown op names the entry by the op it wrote.</summary>
    [Fact]
    public void UnknownOperation_StatesTheOpItWrote()
    {
        File.WriteAllText(_workspace.File("a.pdf"), "not a document");
        File.WriteAllText(_workspace.File("ops.json"), """{"ops":[{"op":"add_image_stamp","image":"x.png"}]}""");

        CliResult result = _workspace.Run("pdf", "edit", "a.pdf", "--ops", "ops.json", "--output-format", "json");

        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Equal("OPS_INVALID", error["code"]!.GetValue<string>());
        Assert.Equal("op-0001", error["details"]!["id"]!.GetValue<string>());
        Assert.Equal("add_image_stamp", error["details"]!["op"]!.GetValue<string>());
    }

    /// <summary>A <c>--to</c> format the command does not write but a command beside it does points to that command.</summary>
    [Theory]
    [InlineData("cells convert a.xlsx --to png")]
    [InlineData("pdf convert a.pdf --to png")]
    [InlineData("slides convert a.pptx --to png")]
    [InlineData("slides convert a.pptx --to jpeg")]
    [InlineData("slides convert a.pptx --to svg")]
    public void TargetFormatAnotherCommandWrites_PointsToThatCommand(string commandLine)
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
    }

    /// <summary>
    /// The command beside a convert of an encrypted workbook, read with <c>--password-env</c>,
    /// that writes the image format the convert was given passes the same variable on, never the
    /// password, and runs as written to the same <c>--out</c>.
    /// </summary>
    [Fact]
    public void TargetFormatAnotherCommandWrites_PassesTheInputsPasswordVariableOn()
    {
        ScenarioLicense.Project(_workspace.Path);
        var password = new Dictionary<string, string?> { ["BOOK_PASSWORD"] = "sibling-hint-secret-5e1b" };
        Assert.Equal(0, _workspace.Run("cells", "create", "plain.xlsx", "--output-format", "json").ExitCode);
        Assert.Equal(0, _workspace.Run("cells", "edit", "plain.xlsx", "--in-place", "--ops", """{"ops":[{"set":"sheet/Sheet1/cell/A1","props":{"value":"Region"}}]}""", "--output-format", "json").ExitCode);
        CliResult created = _workspace.RunWithEnv(password,
            "cells", "convert", "plain.xlsx", "--to", "xlsx", "--out", "book.xlsx", "--encrypt-env", "BOOK_PASSWORD", "--output-format", "json");
        Assert.True(created.ExitCode == 0, created.StdErr);

        CliResult refused = _workspace.RunWithEnv(password,
            "cells", "convert", "book.xlsx", "--password-env", "BOOK_PASSWORD", "--to", "png", "--out", "x.png", "--output-format", "json");

        Assert.Equal(2, refused.ExitCode);
        const string Command = "aspose-cli cells render book.xlsx --sheet Sheet1 --password-env BOOK_PASSWORD --to png --out x.png --output-format json";
        CliResult rendered = _workspace.RunCommandLine(Command, password);
        Assert.True(rendered.ExitCode == 0, rendered.StdErr);
        JsonArray outputs = JsonNode.Parse(rendered.StdOut)!["outputs"]!.AsArray();
        Assert.NotEmpty(outputs);
        Assert.All(outputs, static output => Assert.True(File.Exists(output!["path"]!.GetValue<string>())));
    }

    [Fact]
    public void EditOutputFormatTheEditCannotWrite_ListsOnlyWhatTheEditWrites()
    {
        Assert.Equal(0, _workspace.Run("cells", "create", "a.xlsx", "--output-format", "json").ExitCode);

        CliResult result = _workspace.Run("cells", "edit", "a.xlsx", "--ops", """{"ops":[{"do":"calculate"}]}""", "--out", "x.png", "--output-format", "json");

        Assert.Equal(2, result.ExitCode);
        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.Equal([".xlsx"], error["details"]!["available"]!.AsArray().Select(static extension => extension!.GetValue<string>()));
        Assert.Null(error["details"]!["supported"]);
        Assert.EndsWith("Must be one of: .xlsx.", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("words convert a.docx --out m3.docz", ".docx")]
    [InlineData("pdf convert a.pdf --out t3.dox", ".docx")]
    [InlineData("cells convert a.xlsx --out b.xslx", ".xlsx")]
    public void MistypedOutputExtension_ListsTheExtensionMeant(string commandLine, string extension)
    {
        File.WriteAllText(_workspace.File("a.docx"), "not a document");
        File.WriteAllText(_workspace.File("a.pdf"), "not a document");
        File.WriteAllText(_workspace.File("a.xlsx"), "not a document");

        CliResult result = _workspace.Run([.. commandLine.Split(' '), "--output-format", "json"]);

        Assert.Equal(2, result.ExitCode);
        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.Contains(extension, error["details"]!["available"]!.AsArray().Select(static item => item!.GetValue<string>()));
    }

    /// <summary>
    /// An image command given an extension it does not write lists only extensions it writes. Each
    /// row lists the extensions its product's render writes.
    /// </summary>
    [Theory]
    [InlineData("pdf render a.pdf --out x.gif", ".png .jpg .jpeg .svg")]
    [InlineData("slides render a.pptx --out x.gifx", ".png .jpg .jpeg .svg .tiff .tif")]
    public void RenderOutputOfAnotherCommandsFormat_ListsOnlyExtensionsRenderWrites(string commandLine, string rendered)
    {
        File.WriteAllText(_workspace.File("a.pdf"), "not a document");
        File.WriteAllText(_workspace.File("a.pptx"), "not a document");

        CliResult result = _workspace.Run([.. commandLine.Split(' '), "--output-format", "json"]);

        Assert.Equal(2, result.ExitCode);
        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        string[] available = [.. error["details"]!["available"]!.AsArray().Select(static extension => extension!.GetValue<string>())];
        Assert.Subset(new HashSet<string>(rendered.Split(' ')), new HashSet<string>(available));
    }

    /// <summary>
    /// An image format that convert does not write, named by its extension as in <c>--to jpg</c>,
    /// points to the render beside it, with the format render calls it and the same output.
    /// </summary>
    [Theory]
    [InlineData("slides convert a.pptx --to jpg --out x.jpg")]
    [InlineData("pdf convert a.pdf --to jpg --out x.jpg")]
    [InlineData("words convert a.docx --to jpg --out x.jpg")]
    [InlineData("cells convert a.xlsx --to jpg")]
    public void ImageFormatNamedByItsExtension_PointsToRenderWithTheSameOutput(string commandLine)
    {
        File.WriteAllText(_workspace.File("a.xlsx"), "not a document");
        File.WriteAllText(_workspace.File("a.pptx"), "not a document");
        File.WriteAllText(_workspace.File("a.pdf"), "not a document");
        File.WriteAllText(_workspace.File("a.docx"), "not a document");

        CliResult result = _workspace.Run([.. commandLine.Split(' '), "--output-format", "json"]);

        Assert.Equal(2, result.ExitCode);
        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.Equal("--to", error["details"]!["option"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("slides edit a.pptx --ops slides.json --out noext", "noext")]
    [InlineData("slides edit a.pptx --ops slides.json --out dotonly.", "dotonly")]
    [InlineData("slides create noext", "noext")]
    [InlineData("slides create dotonly.", "dotonly")]
    [InlineData("words edit a.docx --ops words.json --out noext", "noext")]
    [InlineData("words create noext --from-markdown deck.md", "noext")]
    public void OutputWithoutExtension_IsAnInvalidOption(string commandLine, string output)
    {
        // A Slides edit checks its output against the format of the input it opened.
        _workspace.Run("slides", "create", "a.pptx").Succeeded();
        File.WriteAllText(_workspace.File("a.docx"), "not a document");
        File.WriteAllText(_workspace.File("slides.json"), """{"ops":[{"set":"slide/256","props":{"hidden":true}}]}""");
        File.WriteAllText(_workspace.File("words.json"), """{"ops":[{"op":"set_properties","title":"x"}]}""");
        File.WriteAllText(_workspace.File("deck.md"), "# Title");

        CliResult result = _workspace.Run([.. commandLine.Split(' '), "--output-format", "json"]);

        Assert.Equal(2, result.ExitCode);
        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.Contains("has no extension", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.NotEmpty(error["details"]!["available"]!.AsArray());
        Assert.False(File.Exists(_workspace.File(output)));
    }
}
