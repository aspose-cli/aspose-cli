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
        Requires.Windows();
        using var fixture = new WordsFixture();
        await using var server = new ResourceHttpServer();
        string input = fixture.Temp.File("template.html");
        File.WriteAllBytes(fixture.Temp.File("local.png"), ResourceHttpServer.Image);
        File.WriteAllText(input, $"<html><body><p>Template</p><img src='local.png'><img src='{server.Url}/remote.png'></body></html>");
        string output = fixture.Temp.File("created.docx");
        var created = fixture.Engine.Create(new NewDocumentRequest
        {
            Output = TestOutput.At(output),
            TemplatePath = input,
            Title = "Resource owner",
        });
        Assert.Contains(created.Warnings!, warning => warning.Code == WarningCodes.RemoteResourcesBlocked);
        var document = new Document(output);
        Assert.Contains(document.GetChildNodes(NodeType.Shape, true).Cast<Shape>(), shape => shape.HasImage);
        document.Cleanup();
        Assert.Equal(0, server.RequestCount);
    }

    [Fact]
    public void UpdateFields_DoesNotIncludeFilesOutsideTheInputDirectory()
    {
        using var fixture = new WordsFixture();
        string secret = fixture.Temp.File("secret.txt");
        File.WriteAllText(secret, "OUTSIDE-SECRET");
        string documents = Directory.CreateDirectory(fixture.Temp.File("documents")).FullName;
        string input = Path.Combine(documents, "include.docx");
        var source = new Document();
        new DocumentBuilder(source).InsertField($"INCLUDETEXT \"{secret.Replace("\\", "\\\\")}\"", "placeholder");
        source.Save(input);
        string output = Path.Combine(documents, "updated.docx");

        fixture.Engine.ApplyOps(input, new WordsOpsBatch { Ops = [new UpdateFieldsOp()] },
            new WordsEditRequest { Output = TestOutput.At(output) });

        Assert.DoesNotContain("OUTSIDE-SECRET", new Document(output).GetText(), StringComparison.Ordinal);
    }

    [Fact]
    public void BlankDocumentWithoutAPolicySourceDeniesEveryResource()
    {
        using var fixture = new WordsFixture();
        string text = fixture.Temp.File("body.txt");
        File.WriteAllText(text, "Body");

        Document blank = WordsDocumentLoader.CreateBlank(policySource: null);
        new DocumentBuilder(blank).InsertField($"INCLUDETEXT \"{text.Replace("\\", "\\\\")}\"", "placeholder");
        blank.UpdateFields();

        Assert.NotNull(blank.ResourceLoadingCallback);
        Assert.DoesNotContain("Body", blank.GetText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RemoteFieldsLinkedImagesAndSvgImages_AreNeverFetched()
    {
        using var fixture = new WordsFixture();
        await using var server = new ResourceHttpServer();
        string input = fixture.Temp.File("fields.docx");
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.Writeln("Remote resources");
        builder.InsertField($"INCLUDEPICTURE \"{server.Url}/include-picture.png\" \\d", "");
        builder.InsertField($"INCLUDETEXT \"{server.Url}/include-text.docx\"", "placeholder");
        builder.InsertImage(ResourceHttpServer.Image).ImageData.SourceFullName = $"{server.Url}/linked.png";
        source.Save(input);
        string svg = fixture.Temp.File("remote.svg");
        File.WriteAllText(svg, $"""<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="20" height="20"><style>@import url('{server.Url}/svg.css');</style><image xlink:href="{server.Url}/svg.png" width="10" height="10"/></svg>""");
        string edited = fixture.Temp.File("edited.docx");

        fixture.Engine.ApplyOps(input, new WordsOpsBatch
        {
            Ops =
            [
                new InsertImageOp { At = new WordsTarget { Block = 1 }, Position = "after", Path = svg },
                new UpdateFieldsOp(),
            ],
        }, new WordsEditRequest { Output = TestOutput.At(edited) });
        fixture.Engine.Read(edited, new DocumentReadRequest());
        fixture.Engine.Render(edited, new WordsRenderRequest { Output = TestOutput.At(fixture.Temp.File("page.png"), format: "png") });
        fixture.Engine.Convert(edited, new WordsConvertRequest { Output = TestOutput.At(fixture.Temp.File("edited.pdf"), format: "pdf") });

        Assert.True(server.RequestCount == 0, string.Join("; ", server.Requests));
    }

    [Fact]
    public async Task Html_PreservesLocalImageAndReportsRemoteOmissionsWithoutHttp()
    {
        Requires.Windows();
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
            Output = TestOutput.At(output, format: "docx"),
        });
        Assert.Contains(converted.Warnings!, warning =>
            warning.Code == WarningCodes.RemoteResourcesBlocked && warning.AffectsCompleteness);
        Assert.Equal(0, server.RequestCount);
        File.WriteAllBytes(fixture.Temp.File("local image.png"), []);
    }
}
