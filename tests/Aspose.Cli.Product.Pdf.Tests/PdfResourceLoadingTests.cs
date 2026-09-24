using System.Text.Json.Nodes;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.TestKit;
using Aspose.Pdf;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

public sealed class PdfResourceLoadingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HtmlCreation_RelativeImageHonorsTheOriginalDirectoryBoundary(bool worker)
    {
        using var workspace = new TempWorkspace();
        string directory = workspace.File("本地 资源");
        Directory.CreateDirectory(directory);
        string input = Path.Combine(directory, "input.html");
        string output = workspace.File("local-image.pdf");
        File.WriteAllBytes(Path.Combine(directory, "brand.png"), ResourceHttpServer.Image);
        const string html = "<html><body><p>Local brand</p><img src=\"brand.png\" width=\"30\" height=\"30\"></body></html>";
        File.WriteAllText(input, html);
        byte[] original = File.ReadAllBytes(input);
        string[] supervision = worker ? ["--timeout", "30"] : [];

        CliResult result = workspace.Run(
            ["pdf", "create", output, "--from-html", input, "--output", "json", .. supervision]);

        Assert.True(result.ExitCode == 0, result.StdErr);
        JsonNode response = JsonNode.Parse(result.StdOut)!;
        if (OperatingSystem.IsWindows())
        {
            Assert.DoesNotContain(response["warnings"]?.AsArray() ?? [],
                warning => warning!["code"]!.GetValue<string>() == "REMOTE_RESOURCES_BLOCKED");
            using var document = new Document(output);
            Assert.Contains(document.Pages[1].Resources.Images.Cast<XImage>(),
                image => image.Width == 1 && image.Height == 1);
            Assert.DoesNotContain(document.Pages[1].Resources.Images.Cast<XImage>(),
                image => image.Width == 32 && image.Height == 32);
        }
        else
        {
            // Platforms without a verified file-handle boundary must disclose the refusal.
            Assert.Contains(response["warnings"]!.AsArray(),
                warning => warning!["code"]!.GetValue<string>() == "REMOTE_RESOURCES_BLOCKED"
                    && warning["affectsCompleteness"]!.GetValue<bool>());
        }
        Assert.Equal(original, File.ReadAllBytes(input));
    }

    // Known product defect, kept failing on purpose: HTML creation reports the remote resources as
    // blocked, yet Aspose.PDF still requests them over HTTP despite CustomLoaderOfExternalResources.
    [Fact]
    [Trait("ProductDefect", "pdf-html-egress")]
    public async Task HtmlCreation_NeverRequestsRemoteResourcesAndReportsThemBlocked()
    {
        Requires.Windows();
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
            Assert.Contains(document.Pages[1].Resources.Images.Cast<XImage>(),
                image => image.Width == 1 && image.Height == 1);
        }
        Assert.True(server.RequestCount == 0, string.Join("; ", server.Requests));
        File.WriteAllBytes(fixture.File("local.png"), []);
    }
}
