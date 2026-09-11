using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.TestKit;
using Aspose.Pdf;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

public sealed class PdfResourceLoadingTests
{
    [Fact]
    public async Task HtmlCreation_PreservesLocalImageAndReportsCallbackDecisions()
    {
        if (!OperatingSystem.IsWindows()) { return; }
        using var fixture = new PdfEngineFixture();
        await using var server = new ResourceHttpServer();
        string input = fixture.File("input.html");
        string output = fixture.File("output.pdf");
        File.WriteAllBytes(fixture.File("local.png"), ResourceHttpServer.Image);
        File.WriteAllText(input, $"""
            <html><head><link rel="stylesheet" href="{server.Url}/style.css"></head>
            <body><p>Local content</p><img src="local.png" width="30" height="30">
            <img src="{server.Url}/image.png"></body></html>
            """);
        var result = fixture.Engine.Create(new NewPdfRequest { HtmlPath = input, OutputPath = output });
        Assert.Contains(result.Warnings!, warning =>
            warning.Code == WarningCodes.RemoteResourcesBlocked && warning.AffectsCompleteness);
        using (var document = new Document(output))
        {
            Assert.NotEmpty(document.Pages[1].Resources.Images);
        }
        Assert.True(new Uri(server.Url).IsLoopback);
        Assert.All(server.Requests, request => Assert.Contains(request, new[]
        {
            "GET /style.css HTTP/1.1",
            "GET /image.png HTTP/1.1",
        }));
        File.WriteAllBytes(fixture.File("local.png"), []);
    }
}
