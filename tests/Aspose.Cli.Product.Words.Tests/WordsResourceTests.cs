using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Errors;
using Aspose.Words;
using Aspose.Words.Drawing;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>
/// The network and file boundary (SECURITY.md), through the command line: the engine asks its
/// resource callback for every external resource, which supplies files beneath the input's folder
/// and nothing else, when it loads, saves, renders and updates fields. Each command runs in a
/// process of its own, so the cases hold with and without a license.
/// </summary>
public sealed class WordsResourceTests
{
    [Fact]
    public async Task LinkedImagesAndIncludeFields_AreNeverFetched_ByAnyCommand()
    {
        using var workspace = new TempWorkspace();
        await using var server = new ResourceHttpServer();
        string input = workspace.File("fields.docx");
        string outside = workspace.File("outside.png");
        File.WriteAllBytes(outside, ResourceHttpServer.Image);
        Directory.CreateDirectory(workspace.File("documents"));
        input = workspace.File("documents/fields.docx");
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.Writeln("Remote resources");
        builder.InsertField($"INCLUDEPICTURE \"{server.Url}/include-picture.png\" \\d", "");
        builder.InsertField($"INCLUDETEXT \"{server.Url}/include-text.docx\"", "placeholder");
        builder.InsertField($"INCLUDETEXT \"{outside.Replace("\\", "\\\\")}\"", "placeholder");
        var linked = new Shape(source, ShapeType.Image) { Width = 20, Height = 20, WrapType = WrapType.Inline };
        linked.ImageData.SourceFullName = $"{server.Url}/linked.png";
        builder.InsertNode(linked);
        source.Save(input);
        string ops = workspace.File("documents/ops.json");
        File.WriteAllText(ops, """{"ops":[{"do":"update_fields"}]}""");
        string edited = workspace.File("documents/edited.docx");

        CliResult edit = workspace.Run(["words", "edit", input, "--ops", ops, "--out", edited]);
        CliResult read = workspace.Run(["words", "read", edited, "--at", "section/0/body/range"]);
        CliResult render = workspace.Run(["words", "render", edited, "--out", workspace.File("documents/page.png")]);
        CliResult convert = workspace.Run(["words", "convert", edited, "--to", "pdf"]);

        Assert.True(edit.ExitCode == 0, edit.StdErr);
        Assert.True(read.ExitCode == 0, read.StdErr);
        Assert.True(render.ExitCode == 0, render.StdErr);
        Assert.True(convert.ExitCode == 0, convert.StdErr);
        Assert.True(server.RequestCount == 0, string.Join("; ", server.Requests));
        Assert.Contains(JsonNode.Parse(edit.StdOut)!["warnings"]!.AsArray(),
            warning => warning!["code"]!.GetValue<string>() == WarningCodes.RemoteResourcesBlocked.Name);
        Assert.DoesNotContain("IHDR", JsonNode.Parse(read.StdOut)!["objects"]![0]!["props"]!["text"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task HtmlInput_KeepsTheImageBesideIt_AndFetchesNothing()
    {
        using var workspace = new TempWorkspace();
        await using var server = new ResourceHttpServer();
        string input = workspace.File("input.html");
        File.WriteAllBytes(workspace.File("local image.png"), ResourceHttpServer.Image);
        File.WriteAllText(input, $"""
            <html><head><link rel="stylesheet" href="{server.Url}/style.css"></head>
            <body><p>Local content</p><img src="local%20image.png">
            <img src="{server.Url}/image.png"></body></html>
            """);
        string output = workspace.File("converted.docx");

        CliResult convert = workspace.Run(["words", "convert", input, "--to", "docx", "--out", output]);

        Assert.True(convert.ExitCode == 0, convert.StdErr);
        Assert.Equal(0, server.RequestCount);
        Assert.Contains(JsonNode.Parse(convert.StdOut)!["warnings"]!.AsArray(),
            warning => warning!["code"]!.GetValue<string>() == WarningCodes.RemoteResourcesBlocked.Name);
        CliResult extract = workspace.Run(["words", "extract", output, "--what", "images", "--out-dir", workspace.File("images")]);
        Assert.True(extract.ExitCode == 0, extract.StdErr);
        Assert.Contains(Directory.GetFiles(workspace.File("images")), file => File.ReadAllBytes(file).AsSpan().SequenceEqual(ResourceHttpServer.Image));
    }

    [Fact]
    public async Task SvgImageWithExternalReferences_IsRefusedAsAnImageValue()
    {
        using var workspace = new TempWorkspace();
        await using var server = new ResourceHttpServer();
        string input = workspace.File("doc.docx");
        var document = new Document();
        new DocumentBuilder(document).Writeln("Picture here");
        document.Save(input);
        File.WriteAllText(workspace.File("remote.svg"), $"""<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="20" height="20"><image xlink:href="{server.Url}/svg.png" width="10" height="10"/></svg>""");
        File.WriteAllText(workspace.File("ops.json"), """{"ops":[{"add":"image","in":"section/0/body/paragraph/0","args":{"image":"remote.svg"}}]}""");

        CliResult edit = workspace.Run(["words", "edit", input, "--ops", workspace.File("ops.json"), "--out", workspace.File("out.docx")]);

        Assert.NotEqual(0, edit.ExitCode);
        Assert.False(File.Exists(workspace.File("out.docx")));
        Assert.Equal(0, server.RequestCount);
    }
}
