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
        CliResult reviewed = workspace.Run("review", "renamed.pdf", "--output", "json");

        // The product command and generic routing report a renamed file the same way.
        foreach (CliResult result in new[] { inspected, reviewed })
        {
            Assert.Equal(6, result.ExitCode);
            JsonNode failure = JsonNode.Parse(result.StdErr)!["error"]!;
            Assert.Equal("FORMAT_MISMATCH", failure["code"]!.GetValue<string>());
            Assert.Equal("pdf", failure["details"]!["declared"]!.GetValue<string>());
            Assert.Equal("words", Assert.Single(failure["details"]!["detected"]!.AsArray())!.GetValue<string>());
            Assert.Equal(workspace.File("renamed.pdf"), failure["details"]!["path"]!.GetValue<string>());
        }
        string hint = JsonNode.Parse(inspected.StdErr)!["error"]!["hint"]!.GetValue<string>();
        Assert.Contains("looks like a words document", hint, StringComparison.Ordinal);
        Assert.Contains("'aspose-cli words inspect'", hint, StringComparison.Ordinal);
    }

    [Fact]
    public void MultiInputCommand_UsesTheFileTheProductNames()
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.File("doc.md"), "# Title\n\nFirst paragraph.\n");
        workspace.Run("words", "create", "doc.docx", "--markdown", "doc.md", "--output", "json").Succeeded();
        File.Move(workspace.File("doc.docx"), workspace.File("renamed.pdf"));
        File.WriteAllText(workspace.File("other.pdf"), "%PDF-1.7\n");
        ParsedInvocation invocation = ActualCommandTree.Parser.Parse(
            ["pdf", "merge", "other.pdf", "renamed.pdf", "--out", "merged.pdf"]);
        var globals = new GlobalValues(OutputMode.Json, Quiet: true, Verbose: false, LicensePath: null,
            workspace.Path, TimeoutSeconds: null, MaxInputBytes: 1024 * 1024);
        var unnamed = new CliException(ErrorCodes.FileCorrupt, "The file is corrupt.");
        var named = new CliException(
            ErrorCodes.FileCorrupt, "The file is corrupt.",
            details: new JsonObject { ["path"] = workspace.File("renamed.pdf") });

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

        Assert.Equal(ErrorCodes.FormatMismatch, explained.Code);
        Assert.Equal("words", Assert.Single(explained.Details!["detected"]!.AsArray())!.GetValue<string>());
    }

    [Fact]
    public void ProductCommand_OnADamagedFileOfAFormatItReads_KeepsItsOwnError()
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.File("broken.pdf"), "%PDF-1.7\n1 0 obj\n<< /Type /Catalog /Pages");

        CliResult converted = workspace.Run(
            "words", "convert", "broken.pdf", "--to", "docx", "--out", "out.docx", "--output", "json");

        // Words reads PDF, so a damaged PDF is its own corrupt input, not a renamed file.
        Assert.Equal(3, converted.ExitCode);
        JsonNode error = JsonNode.Parse(converted.StdErr)!["error"]!;
        Assert.Equal("FILE_CORRUPT", error["code"]!.GetValue<string>());
        Assert.Null(error["details"]?["detected"]);
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
