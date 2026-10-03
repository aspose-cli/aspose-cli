using System.Text.Json.Nodes;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.Output;
using Aspose.Cli.Host.Tests;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

/// <summary>
/// A product that cannot open its input as its own format names the product whose format the
/// content actually has, so a renamed file leads straight to the command that reads it.
/// </summary>
[Category(TestCategory.Slow)]
public sealed class CorruptInputDetectionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProductCommand_OnAWordDocumentNamedPdf_NamesWordsAndItsCommand(bool supervised)
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.File("doc.md"), "# Title\n\nFirst paragraph.\n");
        workspace.Run("words", "create", "doc.docx", "--markdown", "doc.md", "--output", "json").Succeeded();
        File.Move(workspace.File("doc.docx"), workspace.File("renamed.pdf"));
        string[] args = ["pdf", "inspect", "renamed.pdf", "--output", "json"];
        if (supervised) { args = ["--timeout", "60", .. args]; }

        CliResult inspected = workspace.Run(args);

        Assert.Equal(3, inspected.ExitCode);
        JsonNode error = JsonNode.Parse(inspected.StdErr)!["error"]!;
        Assert.Equal("FILE_CORRUPT", error["code"]!.GetValue<string>());
        Assert.Equal("words", Assert.Single(error["details"]!["detected"]!.AsArray())!.GetValue<string>());
        Assert.Equal(workspace.File("renamed.pdf"), error["details"]!["path"]!.GetValue<string>());
        string hint = error["hint"]!.GetValue<string>();
        Assert.Contains("looks like a words document", hint, StringComparison.Ordinal);
        Assert.Contains("'aspose-cli words inspect'", hint, StringComparison.Ordinal);
    }

    [Fact]
    public void RelativeReportedPath_IsResolvedAgainstTheWorkDirNotTheProcessDirectory()
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.File("doc.md"), "# Title\n\nFirst paragraph.\n");
        workspace.Run("words", "create", "doc.docx", "--markdown", "doc.md", "--output", "json").Succeeded();
        File.Move(workspace.File("doc.docx"), workspace.File("renamed.pdf"));
        Assert.False(File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "renamed.pdf")));
        ParsedInvocation invocation = ActualCommandTree.Parser.Parse(["pdf", "inspect", "renamed.pdf"]);
        var globals = new GlobalValues(OutputMode.Json, Quiet: true, Verbose: false, LicensePath: null,
            workspace.Path, TimeoutSeconds: null, MaxInputBytes: 1024 * 1024);
        var corrupt = new CliException(
            ErrorCodes.FileCorrupt, "The file is corrupt.", details: new JsonObject { ["path"] = "renamed.pdf" });

        var explained = Assert.IsType<CliException>(CorruptInputDetection.Explain(
            corrupt, invocation.ParseResult, globals, ActualCommandTree.Host.Catalog));

        Assert.Equal("words", Assert.Single(explained.Details!["detected"]!.AsArray())!.GetValue<string>());
    }

    [Fact]
    public void ProductCommand_OnUnrecognizableContent_KeepsItsOwnError()
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.File("noise.pdf"), "neither a PDF nor anything else");

        CliResult inspected = workspace.Run("pdf", "inspect", "noise.pdf", "--output", "json");

        Assert.Equal(3, inspected.ExitCode);
        JsonNode error = JsonNode.Parse(inspected.StdErr)!["error"]!;
        Assert.Equal("FILE_CORRUPT", error["code"]!.GetValue<string>());
        Assert.Null(error["details"]?["detected"]);
    }
}
