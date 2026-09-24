using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Execution;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.TestKit;
using Aspose.Words;
using Aspose.Words.Drawing;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

public sealed class WordsResourceBoundaryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InsertMarkdown_UsesTheDocumentDirectoryAndNeverFetchesRemoteResources(bool supervised)
    {
        using var workspace = new TempWorkspace();
        await using var server = new ResourceHttpServer();
        Directory.CreateDirectory(workspace.File("documents"));
        string input = workspace.File("documents/input.docx");
        var original = new Document();
        new DocumentBuilder(original).Write("Resource anchor");
        original.Save(input);
        original.Cleanup();
        File.WriteAllBytes(workspace.File("documents/local.png"), ResourceHttpServer.Image);
        File.WriteAllBytes(workspace.File("outside.png"), ResourceHttpServer.Image);
        string markdown = $"![local](local.png)\n\n![remote]({server.Url}/image.png)\n\n![outside]({new Uri(workspace.File("outside.png")).AbsoluteUri})";
        string ops = JsonSerializer.Serialize(new
        {
            schemaVersion = 2,
            ops = new[] { new { op = "insert_markdown", at = new { find = "Resource anchor" }, position = "after", markdown } },
        });
        string[] args = ["words", "edit", input, "--ops", ops, "--out", workspace.File("edited.docx"), "--output", "json",
            .. supervised ? new[] { "--timeout", "30" } : Array.Empty<string>()];
        CliResult result = workspace.Run(args);

        Assert.True(result.ExitCode == 0, result.StdErr);
        Assert.Equal(0, server.RequestCount);
        Assert.Contains(JsonNode.Parse(result.StdOut)!["warnings"]!.AsArray(),
            warning => warning!["code"]!.GetValue<string>() == WarningCodes.RemoteResourcesBlocked);
        var reopened = new Document(workspace.File("edited.docx"));
        try
        {
            Assert.Contains(reopened.GetChildNodes(NodeType.Shape, true).Cast<Shape>(),
                shape => shape.HasImage && shape.ImageData.ImageBytes.AsSpan().SequenceEqual(ResourceHttpServer.Image));
        }
        finally { reopened.Cleanup(); }
    }

    [Fact]
    public void InlineMarkdown_IsChargedBeforeItsUtf8BufferIsAllocated()
    {
        using var fixture = new WordsFixture();
        string input = fixture.CreateReport();
        using var deadline = OperationDeadline.Start(null);
        var limits = WordsModule.Manifest.ResourceBudgets.ToDictionary(item => item.Kind, item => item.Default);
        limits[ResourceBudgetKinds.MemoryBufferBytes] = 8;
        var budgets = new ResourceBudgetLedger(deadline, limits);
        var loader = new WordsDocumentLoader(budgets);
        using LoadedDocument loaded = loader.Open(input, null);

        CliException error = Assert.Throws<CliException>(() => loader.OpenMarkdown(new string('a', 9), loaded));
        Assert.Equal(ErrorCodes.InputBudgetExceeded, error.Code);
        string output = fixture.Temp.File("budget-failure.docx");
        var engine = new WordsDocumentEngine(fixture.Gate, budgets, new SafeFileWriter(budgets));
        CliException batchError = Assert.Throws<CliException>(() => engine.ApplyOps(input,
            new WordsOpsBatch { Ops = [new InsertMarkdownOp
            {
                At = new WordsTarget { Find = "Quarterly report" }, Position = "after", Markdown = new string('a', 9),
            }] }, new WordsEditRequest { OutputPath = output, Options = new EditCommandOptions { BestEffort = true } }));
        Assert.Equal(ErrorCodes.InputBudgetExceeded, batchError.Code);
        Assert.False(File.Exists(output));
    }

    // Supervised runs publish from a worker whose staging directory is removed afterwards;
    // direct runs save with the same options and have no such cleanup to survive.
    [Theory]
    [InlineData("html")]
    [InlineData("html-fixed")]
    [InlineData("md")]
    [InlineData("svg")]
    public void SingleFileExports_KeepImagesAfterWorkerCleanup(string format)
    {
        using var workspace = new TempWorkspace();
        File.WriteAllBytes(workspace.File("local.png"), ResourceHttpServer.Image);
        File.WriteAllText(workspace.File("source.html"),
            "<html><body><p>Embedded resource</p><img src='local.png' width='20' height='20'></body></html>");
        string extension = format == "html-fixed" ? "html" : format;
        string output = workspace.File($"export/result.{extension}");
        string[] args = ["words", format == "svg" ? "render" : "convert", workspace.File("source.html"),
            "--to", format, "--out", output, "--output", "json", "--timeout", "30"];
        CliResult result = workspace.Run(args);

        Assert.True(result.ExitCode == 0, result.StdErr);
        Assert.Equal([Path.GetFileName(output)], Directory.GetFiles(Path.GetDirectoryName(output)!).Select(Path.GetFileName));
        string text = File.ReadAllText(output);
        Assert.Contains("data:image/", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("output.stage", text, StringComparison.Ordinal);
    }
}
