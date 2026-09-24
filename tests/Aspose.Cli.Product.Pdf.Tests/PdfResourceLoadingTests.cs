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

    // Aspose.PDF.Drawing 26.8 requests http(s) resources before it consults
    // CustomLoaderOfExternalResources (gate PDF-HTML-EGRESS), so the CLI refuses any HTML that
    // names a network address before the importer runs.
    [Theory]
    [InlineData("""<link rel="stylesheet" href="{0}/style.css">""")]
    [InlineData("""<img src="{0}/image.png">""")]
    [InlineData("""<style>@import url('{0}/import.css');</style>""")]
    [InlineData("""<p style="background: url({0}/background.png)">styled</p>""")]
    [InlineData("""<img src="{1}/scheme-relative.png">""")]
    [InlineData("""<img src="{2}/entity.png">""")]
    [InlineData("""<style>p {{ background: url('\68\74\74\70\3a\2f\2f {3}/escaped.png'); }}</style>""")]
    [InlineData("""<base href="{0}/"><img src="relative.png">""")]
    [InlineData("""<a href="{0}/page.html">a hyperlink is refused too</a>""")]
    public async Task HtmlCreation_RefusesNetworkAddressesBeforeTheImporterRuns(string reference)
    {
        using var fixture = new PdfEngineFixture();
        await using var server = new ResourceHttpServer();
        string host = server.Url["http://".Length..];
        string input = fixture.File("input.html");
        string output = fixture.File("output.pdf");
        File.WriteAllText(input, "<html><head></head><body><p>Local content</p>"
            + string.Format(System.Globalization.CultureInfo.InvariantCulture, reference,
                server.Url, "//" + host, "http&#58;&#47;&#47;" + host, host)
            + "</body></html>");

        CliException refused = Assert.Throws<CliException>(() =>
            fixture.Engine.Create(new NewPdfRequest { HtmlPath = input, OutputPath = output }));

        Assert.Equal(ErrorCodes.FeatureUnsupported, refused.Code);
        Assert.Contains("network address", refused.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
        Assert.True(server.RequestCount == 0, string.Join("; ", server.Requests));
    }

    [Fact]
    public async Task HtmlCreation_AcceptsNamespaceAndDocumentTypeIdentifiersWithoutRequestingThem()
    {
        using var fixture = new PdfEngineFixture();
        await using var server = new ResourceHttpServer();
        string input = fixture.File("input.xhtml.html");
        string output = fixture.File("output.pdf");
        File.WriteAllText(input, $"""
            <!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Strict//EN" "{server.Url}/xhtml1-strict.dtd">
            <html xmlns="{server.Url}/xhtml"><body><p>Local content</p>
            <svg xmlns="{server.Url}/svg" xmlns:xlink='{server.Url}/xlink' width="10" height="10"><rect width="5" height="5"/></svg>
            </body></html>
            """);

        PdfWriteResult result = fixture.Engine.Create(new NewPdfRequest { HtmlPath = input, OutputPath = output });

        Assert.True(result.Output.SizeBytes > 0);
        Assert.True(server.RequestCount == 0, string.Join("; ", server.Requests));
    }

    [Fact]
    public async Task MarkdownCreation_RefusesNetworkAddressesBeforeTheImporterRuns()
    {
        using var fixture = new PdfEngineFixture();
        await using var server = new ResourceHttpServer();
        string input = fixture.File("input.md");
        string output = fixture.File("output.pdf");
        File.WriteAllText(input, $"# Report\n\n![chart]({server.Url}/chart.png)\n");

        CliException refused = Assert.Throws<CliException>(() =>
            fixture.Engine.Create(new NewPdfRequest { TextPath = input, Markdown = true, OutputPath = output }));

        Assert.Equal(ErrorCodes.FeatureUnsupported, refused.Code);
        Assert.False(File.Exists(output));
        Assert.True(server.RequestCount == 0, string.Join("; ", server.Requests));
    }

    // An SVG image fetches its external images with no resource hook in Aspose.PDF, whether it
    // is placed as a page image or stamped, so an SVG that names a network address is refused.
    [Theory]
    [InlineData("create")]
    [InlineData("stamp")]
    [InlineData("watermark")]
    public async Task SvgImage_NamingANetworkAddressIsRefusedWithoutARequest(string use)
    {
        using var fixture = new PdfEngineFixture();
        await using var server = new ResourceHttpServer();
        string image = fixture.File("remote.svg");
        File.WriteAllText(image, $"""
            <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="20" height="20">
            <image xlink:href="{server.Url}/svg.png" width="10" height="10"/></svg>
            """);
        string output = fixture.File("output.pdf");

        CliException refused = Assert.ThrowsAny<CliException>(() =>
        {
            if (use == "create")
            {
                fixture.Engine.Create(new NewPdfRequest { ImagePaths = [image], OutputPath = output });
                return;
            }
            PdfOp op = use == "stamp"
                ? new AddStampImageOp { Page = 1, Path = image, Rect = new PdfRectInput { X = 20, Y = 20, Width = 40, Height = 40 } }
                : new AddWatermarkImageOp { Path = image };
            fixture.Engine.ApplyOps(fixture.CreateDocument(), new PdfOpsBatch { Ops = [op] },
                new PdfEditRequest { OutputPath = output });
        });

        Assert.Equal(ErrorCodes.FeatureUnsupported, refused.Code);
        Assert.Contains("network address", refused.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
        Assert.True(server.RequestCount == 0, string.Join("; ", server.Requests));
    }

    [Fact]
    public void HtmlCreation_ReportsOmittedLocalResourcesAndKeepsPermittedOnes()
    {
        Requires.Windows();
        using var fixture = new PdfEngineFixture();
        string directory = Directory.CreateDirectory(fixture.File("site")).FullName;
        string input = Path.Combine(directory, "input.html");
        string output = fixture.File("output.pdf");
        File.WriteAllBytes(Path.Combine(directory, "local.png"), ResourceHttpServer.Image);
        File.WriteAllBytes(fixture.File("outside.png"), ResourceHttpServer.Image);
        File.WriteAllText(input, """
            <html><body><p>Local content</p><img src="local.png" width="30" height="30">
            <img src="../outside.png" width="30" height="30"></body></html>
            """);

        PdfWriteResult result = fixture.Engine.Create(new NewPdfRequest { HtmlPath = input, OutputPath = output });

        Assert.Contains(result.Warnings!, warning =>
            warning.Code == WarningCodes.RemoteResourcesBlocked && warning.AffectsCompleteness);
        using var document = new Document(output);
        Assert.Contains(document.Pages[1].Resources.Images.Cast<XImage>(),
            image => image.Width == 1 && image.Height == 1);
    }

    [Fact]
    public void HtmlImport_NetworkAddressThatReachesTheLoaderFailsInsteadOfReportingItBlocked()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("input.html");
        File.WriteAllText(input, "<html><body>Local</body></html>");
        using var resources = new HtmlImportResources(input, ProductTestBudgets.Create<PdfModule>());

        Aspose.Pdf.LoadOptions.ResourceLoadingResult result = resources.Load("http://127.0.0.1:9/late.png");

        Assert.Empty(result.Data);
        Assert.False(result.LoadingCancelled);
        CliException failure = Assert.Throws<CliException>(() => resources.Warning);
        Assert.Equal(ErrorCodes.FeatureUnsupported, failure.Code);
        Assert.Contains("http://127.0.0.1:9/late.png", failure.Message, StringComparison.Ordinal);
    }
}
