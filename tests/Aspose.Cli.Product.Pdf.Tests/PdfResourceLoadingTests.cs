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
        using var resources = new HtmlImportResources(input, ProductTestBudgets.Create<PdfModule>(), allowNetwork: false);

        Aspose.Pdf.LoadOptions.ResourceLoadingResult result = resources.Load("http://127.0.0.1:9/late.png");

        Assert.Empty(result.Data);
        Assert.False(result.LoadingCancelled);
        CliException failure = Assert.Throws<CliException>(() => resources.Warnings);
        Assert.Equal(ErrorCodes.FeatureUnsupported, failure.Code);
        Assert.Contains("http://127.0.0.1:9/late.png", failure.Message, StringComparison.Ordinal);
    }

    // The importer runs script, so script can request addresses no static check sees.
    [Theory]
    [InlineData("""<script>var i = new Image(); i.src = 'http:/' + '/{0}/script.png';</script>""")]
    [InlineData("""<img src="missing.png" onerror="this.src = 'http:/' + '/{0}/handler.png'">""")]
    [InlineData("""<svg:script>document.write('x')</svg:script>""")]
    [InlineData("""<a href="javascript:void(0)">script URL</a>""")]
    public async Task HtmlCreation_RefusesScriptBeforeTheImporterRuns(string markup)
    {
        using var fixture = new PdfEngineFixture();
        await using var server = new ResourceHttpServer();
        string input = fixture.File("input.html");
        string output = fixture.File("output.pdf");
        File.WriteAllText(input, "<html><body><p>Local content</p>"
            + string.Format(System.Globalization.CultureInfo.InvariantCulture, markup, server.Url["http://".Length..])
            + "</body></html>");

        CliException refused = Assert.Throws<CliException>(() =>
            fixture.Engine.Create(new NewPdfRequest { HtmlPath = input, OutputPath = output }));

        Assert.Equal(ErrorCodes.FeatureUnsupported, refused.Code);
        Assert.Contains("script", refused.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
        Assert.True(server.RequestCount == 0, string.Join("; ", server.Requests));
    }

    // A supplied local stylesheet or SVG is parsed by the importer, which would request the
    // addresses it names before consulting the loader, so it is refused without a request.
    [Theory]
    [InlineData("style.css", "@import url('{0}/nested.css'); p {{ color: red; }}", """<link rel="stylesheet" href="style.css">""")]
    [InlineData("image.svg", """<svg xmlns="http://www.w3.org/2000/svg" width="9" height="9"><image href="{0}/nested.png" width="9" height="9"/></svg>""", """<img src="image.svg">""")]
    public async Task HtmlCreation_RefusesALocalResourceThatNamesANetworkAddress(string name, string resource, string markup)
    {
        Requires.Windows();
        using var fixture = new PdfEngineFixture();
        await using var server = new ResourceHttpServer();
        string input = fixture.File("input.html");
        string output = fixture.File("output.pdf");
        File.WriteAllText(fixture.File(name), string.Format(System.Globalization.CultureInfo.InvariantCulture, resource, server.Url));
        File.WriteAllText(input, $"<html><head>{markup}</head><body><p>Local content</p></body></html>");

        CliException refused = Assert.Throws<CliException>(() =>
            fixture.Engine.Create(new NewPdfRequest { HtmlPath = input, OutputPath = output }));

        Assert.Equal(ErrorCodes.FeatureUnsupported, refused.Code);
        Assert.Contains(name, refused.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
        Assert.True(server.RequestCount == 0, string.Join("; ", server.Requests));
    }

    [Fact]
    public async Task HtmlCreation_AllowNetworkResources_RequestsDisclosesAndKeepsTheLocalBoundary()
    {
        Requires.Windows();
        using var workspace = new TempWorkspace();
        await using var server = new ResourceHttpServer();
        string directory = Directory.CreateDirectory(workspace.File("site")).FullName;
        string input = Path.Combine(directory, "input.html");
        string output = workspace.File("output.pdf");
        File.WriteAllBytes(workspace.File("outside.png"), ResourceHttpServer.Image);
        File.WriteAllText(input, $"""
            <html><head><link rel="stylesheet" href="{server.Url}/style.css"></head><body><p>Trusted content</p>
            <img src="{server.Url}/remote.png" width="30" height="30">
            <img src="../outside.png" width="30" height="30"></body></html>
            """);

        CliResult result = workspace.Run(
            ["pdf", "create", output, "--from-html", input, "--allow-network-resources", "--output", "json"]);

        Assert.True(result.ExitCode == 0, result.StdErr);
        Assert.Equal(2, server.RequestCount);
        JsonArray warnings = JsonNode.Parse(result.StdOut)!["warnings"]!.AsArray();
        JsonNode requested = Assert.Single(warnings, warning => warning!["code"]!.GetValue<string>() == "NETWORK_RESOURCES_REQUESTED")!;
        string message = requested["message"]!.GetValue<string>();
        Assert.Contains("2 network resource(s)", message, StringComparison.Ordinal);
        Assert.Contains($"{server.Url}/style.css", message, StringComparison.Ordinal);
        Assert.Contains($"{server.Url}/remote.png", message, StringComparison.Ordinal);
        // The local file outside the HTML directory is still omitted, and disclosed as such.
        Assert.Contains(warnings, warning => warning!["code"]!.GetValue<string>() == "REMOTE_RESOURCES_BLOCKED"
            && warning["affectsCompleteness"]!.GetValue<bool>());
        using var document = new Document(output);
        Assert.Single(document.Pages[1].Resources.Images.Cast<XImage>(), image => image.Width == 1 && image.Height == 1);
    }

    [Fact]
    public void HtmlImport_AllowedNetworkKeepsTheFetchAndSharesStayOmitted()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("input.html");
        File.WriteAllText(input, "<html><body>Local</body></html>");
        using var resources = new HtmlImportResources(input, ProductTestBudgets.Create<PdfModule>(), allowNetwork: true);

        Aspose.Pdf.LoadOptions.ResourceLoadingResult fetched = resources.Load("https://example.test/a.png");
        Aspose.Pdf.LoadOptions.ResourceLoadingResult share = resources.Load("file://server/share/b.png");

        Assert.True(fetched.LoadingCancelled);
        Assert.Empty(share.Data);
        Assert.False(share.LoadingCancelled);
        Assert.Collection(resources.Warnings,
            omitted => Assert.Equal(WarningCodes.RemoteResourcesBlocked, omitted.Code),
            requested =>
            {
                Assert.Equal("NETWORK_RESOURCES_REQUESTED", requested.Code);
                Assert.Contains("https://example.test/a.png", requested.Message, StringComparison.Ordinal);
                Assert.DoesNotContain("server/share", requested.Message, StringComparison.Ordinal);
            });
    }

    // The importer waits up to 100 seconds for each unanswered request and cannot be cancelled
    // in-process; the supervised worker ends at the operation deadline without publishing.
    [Category(TestCategory.Slow)]
    [Fact]
    public async Task HtmlCreation_AllowNetworkResources_StopsAtTheOperationDeadline()
    {
        using var workspace = new TempWorkspace();
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        using var stop = new CancellationTokenSource();
        var accepted = new System.Collections.Concurrent.ConcurrentQueue<System.Net.Sockets.TcpClient>();
        Task silent = Task.Run(async () =>
        {
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    accepted.Enqueue(await listener.AcceptTcpClientAsync(stop.Token));
                }
            }
            catch (OperationCanceledException) { }
        });
        string input = workspace.File("input.html");
        string output = workspace.File("output.pdf");
        File.WriteAllText(input, $"<html><body><p>x</p><img src=\"http://127.0.0.1:{port}/never.png\"></body></html>");
        var watch = System.Diagnostics.Stopwatch.StartNew();

        // The deadline leaves a loaded machine time to start the worker and reach the silent server.
        CliResult result = workspace.Run(
            ["pdf", "create", output, "--from-html", input, "--allow-network-resources", "--timeout", "15", "--output", "json"]);

        watch.Stop();
        await stop.CancelAsync();
        listener.Stop();
        await silent;
        foreach (System.Net.Sockets.TcpClient client in accepted) { client.Dispose(); }
        Assert.Equal("OPERATION_TIMEOUT", JsonNode.Parse(result.StdErr)!["error"]!["code"]!.GetValue<string>());
        Assert.NotEmpty(accepted);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(60), $"The import ran {watch.Elapsed}.");
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void MarkdownCreation_RejectsAllowingNetworkResources()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("input.md");
        File.WriteAllText(input, "# Report\n");

        CliException rejected = Assert.Throws<CliException>(() => fixture.Engine.Create(new NewPdfRequest
        {
            TextPath = input,
            Markdown = true,
            AllowNetworkResources = true,
            OutputPath = fixture.File("output.pdf"),
        }));

        Assert.Equal(ErrorCodes.OptionInvalid, rejected.Code);
    }
}
