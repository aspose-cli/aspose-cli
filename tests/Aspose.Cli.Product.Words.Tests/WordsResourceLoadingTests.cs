using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.TestKit;
using Aspose.Words;
using Aspose.Words.Drawing;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

public sealed class WordsResourceLoadingTests
{
    [Fact]
    public async Task CreateFromHtmlTemplate_KeepsResourcesAliveThroughPublication()
    {
        if (!OperatingSystem.IsWindows()) { return; }
        using var fixture = new WordsFixture();
        await using var server = new ResourceHttpServer();
        string input = fixture.Temp.File("template.html");
        File.WriteAllBytes(fixture.Temp.File("local.png"), ResourceHttpServer.Image);
        File.WriteAllText(input, $"<html><body><p>Template</p><img src='local.png'><img src='{server.Url}/remote.png'></body></html>");
        string output = fixture.Temp.File("created.docx");
        var created = fixture.Engine.CreateDocument(new NewDocumentRequest
        {
            TemplatePath = input, OutputPath = output, Title = "Resource owner",
        });
        Assert.Contains(created.Warnings!, warning => warning.Code == WarningCodes.RemoteResourcesBlocked);
        var document = new Document(output);
        Assert.Contains(document.GetChildNodes(NodeType.Shape, true).Cast<Shape>(), shape => shape.HasImage);
        document.Cleanup();
        Assert.Equal(0, server.RequestCount);
    }

    [Fact]
    public async Task Html_PreservesLocalImageAndReportsRemoteOmissionsWithoutHttp()
    {
        if (!OperatingSystem.IsWindows()) { return; }
        using var fixture = new WordsFixture();
        await using var server = new ResourceHttpServer();
        string input = fixture.Temp.File("input.html");
        File.WriteAllBytes(fixture.Temp.File("local image.png"), ResourceHttpServer.Image);
        File.WriteAllText(input, $"""
            <html><head><link rel="stylesheet" href="{server.Url}/style.css"></head>
            <body><p>Local content</p><img src="local%20image.png">
            <img src="{server.Url}/image.png"></body></html>
            """);
        var loader = new WordsDocumentLoader(ProductTestBudgets.Create<WordsModule>());
        using (LoadedDocument loaded = loader.Open(input, null))
        {
            Assert.Contains(loaded.Document.GetChildNodes(NodeType.Shape, true).Cast<Shape>(),
                shape => shape.HasImage && shape.ImageData.ImageBytes.Length > 0);
            Assert.True(loaded.RemoteResourcesBlocked > 0);
        }
        var read = fixture.Engine.Read(input, new DocumentReadRequest());
        Assert.Contains(read.Warnings!, warning =>
            warning.Code == WarningCodes.RemoteResourcesBlocked && warning.AffectsCompleteness);
        string output = fixture.Temp.File("converted.docx");
        var converted = fixture.Engine.Convert(input, new WordsConvertRequest
        {
            OutputPath = output, TargetFormatId = "docx",
        });
        Assert.Contains(converted.Warnings!, warning =>
            warning.Code == WarningCodes.RemoteResourcesBlocked && warning.AffectsCompleteness);
        Assert.Equal(0, server.RequestCount);
        File.WriteAllBytes(fixture.Temp.File("local image.png"), []);
    }
}
