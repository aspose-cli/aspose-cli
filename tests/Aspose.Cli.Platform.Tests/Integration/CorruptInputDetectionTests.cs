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
    [Fact]
    public void MultiInputCommand_UsesTheFileTheProductNames()
    {
        using var workspace = new TempWorkspace();
        WriteWordDocumentNamedPdf(workspace);
        File.WriteAllText(workspace.File("other.pdf"), "%PDF-1.7\n");
        ParsedInvocation invocation = ActualCommandTree.Parser.Parse(
            ["pdf", "merge", "other.pdf", "renamed.pdf", "--out", "merged.pdf"]);
        GlobalValues globals = Globals(workspace);
        // A product names the file; an error another process reported may not.
        CliException unnamed = CliErrors.FromRemote("FILE_CORRUPT", ExitCode.InputError, "The file is corrupt.");
        CliException named = CliErrors.InputUnreadable(
            workspace.File("renamed.pdf"), "PDF document", "the PDF header was not found", "Check the file.");

        // Without a named file, a command with several inputs cannot tell which one failed.
        Assert.Same(unnamed, CorruptInputDetection.Explain(
            unnamed, invocation.ParseResult, globals, ActualCommandTree.Host.Catalog));
        var explained = Assert.IsType<CliException>(CorruptInputDetection.Explain(
            named, invocation.ParseResult, globals, ActualCommandTree.Host.Catalog));

        Assert.Equal(ErrorCodes.FormatMismatch, explained.Code);
        Assert.Equal("words", Assert.Single(explained.Details!["detected"]!.AsArray())!.GetValue<string>());
        Assert.Contains("'aspose-cli words", explained.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void RelativeReportedPath_IsResolvedAgainstTheWorkDirNotTheProcessDirectory()
    {
        using var workspace = new TempWorkspace();
        WriteWordDocumentNamedPdf(workspace);
        Assert.False(File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "renamed.pdf")));
        ParsedInvocation invocation = ActualCommandTree.Parser.Parse(["pdf", "inspect", "renamed.pdf"]);
        GlobalValues globals = Globals(workspace);
        CliException corrupt = CliErrors.FromRemote(
            "FILE_CORRUPT", ExitCode.InputError, "The file is corrupt.", details: new JsonObject { ["path"] = "renamed.pdf" });

        var explained = Assert.IsType<CliException>(CorruptInputDetection.Explain(
            corrupt, invocation.ParseResult, globals, ActualCommandTree.Host.Catalog));

        Assert.Equal(ErrorCodes.FormatMismatch, explained.Code);
        Assert.Equal("words", Assert.Single(explained.Details!["detected"]!.AsArray())!.GetValue<string>());
    }

    [Theory]
    [InlineData("empty.docx", "")]
    [InlineData("damaged.docx", "PK\u0003\u0004 truncated header")]
    public void RoutedCommand_OnContentNoProductRecognizes_IsCorruptAsInTheProduct(string file, string content)
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.File(file), content);

        CliResult inspected = workspace.Run("words", "inspect", file, "--output-format", "json");
        CliResult reviewed = workspace.Run("review", file, "--output-format", "json");

        // The extension names words and no product recognizes the content: a damaged file, not a renamed one.
        foreach (CliResult result in new[] { inspected, reviewed })
        {
            Assert.Equal(3, result.ExitCode);
            JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
            Assert.Equal("FILE_CORRUPT", error["code"]!.GetValue<string>());
            Assert.Equal(workspace.File(file), error["details"]!["path"]!.GetValue<string>());
            Assert.False(string.IsNullOrWhiteSpace(error["details"]!["reason"]?.GetValue<string>()));
        }
    }

    // A Word document named renamed.pdf in the workspace.
    private static void WriteWordDocumentNamedPdf(TempWorkspace workspace)
    {
        File.WriteAllText(workspace.File("doc.md"), "# Title\n\nFirst paragraph.\n");
        workspace.Run("words", "create", "doc.docx", "--from-markdown", "doc.md", "--output-format", "json").Succeeded();
        File.Move(workspace.File("doc.docx"), workspace.File("renamed.pdf"));
    }

    private static GlobalValues Globals(TempWorkspace workspace) =>
        new(OutputMode.Json, Quiet: true, Verbose: false, LicensePath: null,
            workspace.Path, TimeoutSeconds: null, MaxInputBytes: 1024 * 1024);
}
