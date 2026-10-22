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
        string[] supervision = worker ? ["--timeout", TestDeadline.CliTimeout] : [];

        CliResult result = workspace.Run(
            ["pdf", "create", output, "--from-html", input, "--output-format", "json", .. supervision]);

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
    // CustomLoaderOfExternalResources (gate PDF-HTML-EGRESS), and no scan of the HTML can tell
    // which addresses it will request: the first case hides the address from a scan inside a
    // decoy namespace declaration. The process's network gate blocks every request instead, in
    // process and in a supervised worker, and the result lists what it blocked.
    [Theory]
    [InlineData("""<img alt='xmlns="' src={0}/decoy.png title='"'>""", false)]
    [InlineData("""<img alt='xmlns="' src={0}/decoy.png title='"'>""", true)]
    [InlineData("""<img src="{0}/image.png">""", false)]
    [InlineData("""<link rel="stylesheet" href="{0}/style.css">""", false)]
    [InlineData("""<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="9" height="9"><image xlink:href="{0}/inline-svg.png" width="9" height="9"/></svg>""", false)]
    public async Task HtmlCreation_TheGateBlocksEveryRequestAndListsIt(string reference, bool worker)
    {
        using var workspace = new TempWorkspace();
        await using var server = new ResourceHttpServer();
        string input = workspace.File("input.html");
        string output = workspace.File("output.pdf");
        File.WriteAllText(input, "<html><head></head><body><p>Local content</p>"
            + string.Format(System.Globalization.CultureInfo.InvariantCulture, reference, server.Url) + "</body></html>");
        string[] supervision = worker ? ["--timeout", TestDeadline.CliTimeout] : [];

        CliResult result = workspace.Run(["pdf", "create", output, "--from-html", input, "--output-format", "json", .. supervision]);

        Assert.True(server.RequestCount == 0, string.Join("; ", server.Requests));
        Assert.True(result.ExitCode == 0, result.StdErr);
        JsonNode blocked = Assert.Single(JsonNode.Parse(result.StdOut)!["warnings"]!.AsArray(),
            warning => warning!["code"]!.GetValue<string>() == "REMOTE_RESOURCES_BLOCKED")!;
        Assert.Contains(server.Url, blocked["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.True(blocked["affectsCompleteness"]!.GetValue<bool>());
    }

    // Every other way HTML can name an address, script and a data: SVG included, reaches no
    // network either, and every request the importer attempts is listed. A scheme-relative
    // address resolves against the HTML file as a file share with a port, which no API opens,
    // so the importer requests nothing there.
    [Theory]
    [InlineData("""<style>@import url('{0}/import.css');</style>""", true)]
    [InlineData("""<p style="background: url({0}/background.png)">styled</p>""", true)]
    [InlineData("""<img src="{1}/scheme-relative.png">""", false)]
    [InlineData("""<img src="{2}/entity.png">""", true)]
    [InlineData("""<style>p {{ background: url('\68\74\74\70\3a\2f\2f {3}/escaped.png'); }}</style>""", true)]
    [InlineData("""<base href="{0}/"><img src="relative.png">""", true)]
    [InlineData("""<script>var i = new Image(); i.src = 'http:/' + '/{3}/script.png';</script>""", true)]
    [InlineData("""<img src="missing.png" onerror="this.src = 'http:/' + '/{3}/handler.png'">""", true)]
    [InlineData("""<img src="data:image/svg+xml;base64,{4}" width="9" height="9">""", true)]
    public async Task HtmlCreation_ReachesNoNetworkAddress(string reference, bool listed)
    {
        using var workspace = new TempWorkspace();
        await using var server = new ResourceHttpServer();
        string host = server.Url["http://".Length..];
        string svg = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
            $"""<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="9" height="9"><image xlink:href="{server.Url}/data-svg.png" width="9" height="9"/></svg>"""));
        string input = workspace.File("input.html");
        File.WriteAllText(input, "<html><head></head><body><p>Local content</p>"
            + string.Format(System.Globalization.CultureInfo.InvariantCulture, reference,
                server.Url, "//" + host, "http&#58;&#47;&#47;" + host, host, svg)
            + "</body></html>");

        CliResult result = workspace.Run(["pdf", "create", workspace.File("output.pdf"), "--from-html", input, "--output-format", "json"]);

        Assert.True(server.RequestCount == 0, string.Join("; ", server.Requests));
        Assert.True(result.ExitCode == 0, result.StdErr);
        Assert.Equal(listed, (JsonNode.Parse(result.StdOut)!["warnings"]?.AsArray() ?? []).Any(
            warning => warning!["code"]!.GetValue<string>() == "REMOTE_RESOURCES_BLOCKED"));
    }

    // A supplied local stylesheet or SVG is parsed by the importer, which requests the
    // addresses it names before consulting the loader; the gate blocks them too.
    [Theory]
    [InlineData("style.css", "@import url('{0}/nested.css'); p {{ color: red; }}", """<link rel="stylesheet" href="style.css">""")]
    [InlineData("image.svg", """<svg xmlns="http://www.w3.org/2000/svg" width="9" height="9"><image href="{0}/nested.png" width="9" height="9"/></svg>""", """<img src="image.svg">""")]
    public async Task HtmlCreation_ALocalResourceThatNamesANetworkAddressReachesNoNetwork(string name, string resource, string markup)
    {
        using var workspace = new TempWorkspace();
        await using var server = new ResourceHttpServer();
        string input = workspace.File("input.html");
        File.WriteAllText(workspace.File(name), string.Format(System.Globalization.CultureInfo.InvariantCulture, resource, server.Url));
        File.WriteAllText(input, $"<html><head>{markup}</head><body><p>Local content</p></body></html>");

        CliResult result = workspace.Run(["pdf", "create", workspace.File("output.pdf"), "--from-html", input, "--output-format", "json"]);

        Assert.True(server.RequestCount == 0, string.Join("; ", server.Requests));
        Assert.True(result.ExitCode == 0, result.StdErr);
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

        PdfCreateResult result = PdfCreate.Run(fixture.Session, new PdfCreateRequest { Output = TestOutput.At(output), Source = new PdfHtmlSource(new SourceFile("--from-html", input), AllowNetworkResources: false) });

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
            PdfCreate.Run(fixture.Session, new PdfCreateRequest { Output = TestOutput.At(output), Source = new PdfMarkdownSource(new SourceFile("--from-markdown", input)) }));

        Assert.Equal(ErrorCodes.FeatureUnsupported, refused.Code);
        Assert.False(File.Exists(output));
        Assert.True(server.RequestCount == 0, string.Join("; ", server.Requests));
    }

    // An SVG image fetches its external images with no resource hook in Aspose.PDF, whether it
    // is placed as a page image or stamped; the gate blocks the request and lists it.
    [Theory]
    [InlineData("create")]
    [InlineData("stamp")]
    [InlineData("watermark")]
    public async Task SvgImage_NamingANetworkAddressReachesNoNetwork(string use)
    {
        using var workspace = new TempWorkspace();
        await using var server = new ResourceHttpServer();
        File.WriteAllText(workspace.File("remote.svg"), $"""
            <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="20" height="20">
            <image xlink:href="{server.Url}/svg.png" width="10" height="10"/></svg>
            """);
        File.WriteAllText(workspace.File("notes.txt"), "Notes\n");
        Assert.Equal(0, workspace.Run("pdf", "create", "notes.pdf", "--from-text", "notes.txt").ExitCode);
        string operation = use == "stamp"
            ? """{"op":"add_stamp_image","page":1,"path":"remote.svg","rect":{"x":20,"y":20,"width":40,"height":40}}"""
            : """{"op":"add_watermark_image","path":"remote.svg"}""";

        CliResult result = use == "create"
            ? workspace.Run("pdf", "create", "output.pdf", "--from-images", "remote.svg", "--output-format", "json")
            : workspace.Run("pdf", "edit", "notes.pdf", "--ops", $$"""{"ops":[{{operation}}]}""", "--out", "output.pdf", "--output-format", "json");

        Assert.True(server.RequestCount == 0, string.Join("; ", server.Requests));
        Assert.True(result.ExitCode == 0, result.StdErr);
        Assert.Contains(JsonNode.Parse(result.StdOut)!["warnings"]!.AsArray(),
            warning => warning!["code"]!.GetValue<string>() == "REMOTE_RESOURCES_BLOCKED"
                && warning["message"]!.GetValue<string>().Contains($"{server.Url}/svg.png", StringComparison.Ordinal)
                && warning["hint"]!.GetValue<string>().Contains("data:", StringComparison.Ordinal));
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

        PdfCreateResult result = PdfCreate.Run(fixture.Session, new PdfCreateRequest { Output = TestOutput.At(output), Source = new PdfHtmlSource(new SourceFile("--from-html", input), AllowNetworkResources: false) });

        Assert.Contains(result.Warnings!, warning =>
            warning.Code == WarningCodes.RemoteResourcesBlocked && warning.AffectsCompleteness);
        using var document = new Document(output);
        Assert.Contains(document.Pages[1].Resources.Images.Cast<XImage>(),
            image => image.Width == 1 && image.Height == 1);
    }

    // By default the network gate blocked the request and lists it; the loader supplies nothing
    // in its place and keeps the SDK's default loader off.
    [Fact]
    public void HtmlImport_NetworkAddressThatReachesTheLoaderIsLeftToTheGate()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("input.html");
        File.WriteAllText(input, "<html><body>Local</body></html>");
        using var resources = new HtmlImportResources(input, ProductTestBudgets.Create<PdfModule>(), allowNetwork: false);

        Aspose.Pdf.LoadOptions.ResourceLoadingResult result = resources.Load("http://127.0.0.1:9/late.png");

        Assert.Empty(result.Data);
        Assert.False(result.LoadingCancelled);
        Assert.Empty(resources.Warnings);
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
        string outside = workspace.File("outside.png");
        File.WriteAllText(input, $"""
            <html><head><link rel="stylesheet" href="{server.Url}/style.css"></head><body><p>Trusted content</p>
            <img src="{server.Url}/remote.png" width="30" height="30">
            <img src="../outside.png" width="30" height="30">
            <img src="{new Uri(outside).AbsoluteUri}" width="30" height="30">
            <img src="ftp://{server.Url["http://".Length..]}/ftp.png" width="30" height="30"></body></html>
            """);

        CliResult result = workspace.Run(
            ["pdf", "create", output, "--from-html", input, "--allow-network-resources", "--output-format", "json"]);

        Assert.True(result.ExitCode == 0, result.StdErr);
        Assert.Equal(2, server.RequestCount);
        JsonArray warnings = JsonNode.Parse(result.StdOut)!["warnings"]!.AsArray();
        JsonNode requested = Assert.Single(warnings, warning => warning!["code"]!.GetValue<string>() == "NETWORK_RESOURCES_REQUESTED")!;
        string message = requested["message"]!.GetValue<string>();
        Assert.Contains("2 network resource(s)", message, StringComparison.Ordinal);
        Assert.Contains($"{server.Url}/style.css", message, StringComparison.Ordinal);
        Assert.Contains($"{server.Url}/remote.png", message, StringComparison.Ordinal);
        // The local file outside the HTML directory is still omitted, and disclosed as such; the
        // opt-in opens http and https alone, so the gate blocks the file: and ftp: addresses.
        Assert.Contains(warnings, warning => warning!["code"]!.GetValue<string>() == "REMOTE_RESOURCES_BLOCKED"
            && warning["affectsCompleteness"]!.GetValue<bool>());
        JsonNode blocked = Assert.Single(warnings, warning => warning!["code"]!.GetValue<string>() == "REMOTE_RESOURCES_BLOCKED"
            && warning["message"]!.GetValue<string>().Contains("were blocked or removed", StringComparison.Ordinal))!;
        // The file is named, never its folder.
        Assert.Contains("outside.png", blocked["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.DoesNotContain(workspace.Path, blocked["message"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"ftp://{server.Url["http://".Length..]}/ftp.png", blocked["message"]!.GetValue<string>(), StringComparison.Ordinal);
        using var document = new Document(output);
        Assert.Single(document.Pages[1].Resources.Images.Cast<XImage>(), image => image.Width == 1 && image.Height == 1);
    }

    // The opt-in opens https as well as http: the request reaches a TLS listener, whatever the
    // engine then makes of its self-signed certificate. Without the opt-in it never does.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HtmlCreation_AllowNetworkResources_OpensHttps(bool allowed)
    {
        using var workspace = new TempWorkspace();
        await using var server = new TlsListener();
        string input = workspace.File("input.html");
        File.WriteAllText(input, $"""<html><body><p>Trusted content</p><img src="https://127.0.0.1:{server.Port}/secure.png"></body></html>""");
        string[] optIn = allowed ? ["--allow-network-resources"] : [];

        CliResult result = workspace.Run(
            ["pdf", "create", workspace.File("output.pdf"), "--from-html", input, .. optIn, "--timeout", TestDeadline.CliTimeout, "--output-format", "json"]);

        Assert.True(result.ExitCode == 0, result.StdErr);
        if (allowed)
        {
            Assert.True(server.Connections > 0, "No connection reached the https listener.");
        }
        else
        {
            Assert.Equal(0, server.Connections);
            Assert.Contains(JsonNode.Parse(result.StdOut)!["warnings"]!.AsArray(),
                warning => warning!["code"]!.GetValue<string>() == "REMOTE_RESOURCES_BLOCKED"
                    && warning["message"]!.GetValue<string>().Contains($"https://127.0.0.1:{server.Port}/secure.png", StringComparison.Ordinal));
        }
    }

    // A document must not read a local file outside its input directory: a sentinel image
    // beside it, named by a plain path, a UNC path to the same file (an SMB request) or a file
    // URI, must not appear in the output.
    public static TheoryData<string, string> SvgImageUses => Combine(["create", "stamp", "watermark"]);

    public static TheoryData<string, string> HtmlUses => Combine(["inline-svg", "data-svg", "img"]);

    [Theory]
    [MemberData(nameof(SvgImageUses))]
    public void SvgImage_ReadsNoFileOutsideTheInputDirectory(string use, string form)
    {
        using var workspace = new TempWorkspace();
        string reference = Sentinel(workspace, form);
        File.WriteAllText(workspace.File("in/image.svg"), SvgNaming(reference));
        File.WriteAllText(workspace.File("in/notes.txt"), "Notes\n");
        Assert.Equal(0, workspace.Run("pdf", "create", "in/notes.pdf", "--from-text", "in/notes.txt").ExitCode);
        string operation = use == "stamp"
            ? """{"op":"add_stamp_image","page":1,"path":"in/image.svg","rect":{"x":0,"y":0,"width":300,"height":300}}"""
            : """{"op":"add_watermark_image","path":"in/image.svg","scalePercent":100}""";

        CliResult result = use == "create"
            ? workspace.Run("pdf", "create", "in/output.pdf", "--from-images", "in/image.svg", "--output-format", "json")
            : workspace.Run("pdf", "edit", "in/notes.pdf", "--ops", $$"""{"ops":[{{operation}}]}""", "--out", "in/output.pdf", "--output-format", "json");

        AssertNoSentinel(workspace, result);
    }

    // A regression guard: the HTML importer reads none of these files on the stage branch either.
    [Theory]
    [MemberData(nameof(HtmlUses))]
    public void HtmlCreation_ReadsNoFileOutsideTheInputDirectory(string use, string form)
    {
        using var workspace = new TempWorkspace();
        string reference = Sentinel(workspace, form);
        string svg = SvgNaming(reference);
        string body = use switch
        {
            "inline-svg" => svg,
            "data-svg" => $"""<img src="data:image/svg+xml;base64,{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(svg))}" width="200" height="200">""",
            _ => $"""<img src="{reference}" width="200" height="200">""",
        };
        File.WriteAllText(workspace.File("in/input.html"), $"<html><body>{body}</body></html>");

        CliResult result = workspace.Run("pdf", "create", "in/output.pdf", "--from-html", "in/input.html", "--output-format", "json");

        AssertNoSentinel(workspace, result);
    }

    private static readonly SkiaSharp.SKColor SentinelColor = new(19, 215, 165);

    private static TheoryData<string, string> Combine(string[] uses)
    {
        var data = new TheoryData<string, string>();
        foreach (string use in uses)
        {
            foreach (string form in new[] { "path", "unc", "file-uri" })
            {
                data.Add(use, form);
            }
        }

        return data;
    }

    // Writes a solid sentinel image outside the input directory "in" and returns the reference.
    private static string Sentinel(TempWorkspace workspace, string form)
    {
        Directory.CreateDirectory(workspace.File("in"));
        string path = workspace.File("secret/sentinel.png");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using (var bitmap = new SkiaSharp.SKBitmap(40, 40))
        {
            bitmap.Erase(SentinelColor);
            using SkiaSharp.SKData png = bitmap.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(path, png.ToArray());
        }

        string full = Path.GetFullPath(path);
        string unc = $@"\\localhost\{full[0]}${full[2..]}";
        if (form == "unc")
        {
            // The case proves something only where the administrative share reaches the file.
            bool reachable;
            try
            {
                reachable = File.ReadAllBytes(unc).AsSpan().SequenceEqual(File.ReadAllBytes(full));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                reachable = false;
            }

            Assert.SkipUnless(reachable, $"The administrative share does not reach the sentinel image at {unc}.");
        }

        return form switch
        {
            "path" => full,
            "unc" => unc,
            _ => new Uri(full).AbsoluteUri,
        };
    }

    private static string SvgNaming(string reference) =>
        $"""<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="200" height="200"><image xlink:href="{System.Security.SecurityElement.Escape(reference)}" x="0" y="0" width="200" height="200"/></svg>""";

    // A refused command writes nothing; a written output must not show the sentinel's pixels.
    private static void AssertNoSentinel(TempWorkspace workspace, CliResult result)
    {
        string output = workspace.File("in/output.pdf");
        if (result.ExitCode != 0)
        {
            Assert.False(File.Exists(output), result.StdErr);
            return;
        }

        CliResult rendered = workspace.Run("pdf", "render", output, "--pages", "1", "--dpi", "72", "--out", "in/page.png");
        Assert.True(rendered.ExitCode == 0, rendered.StdErr);
        using SkiaSharp.SKBitmap page = SkiaSharp.SKBitmap.Decode(workspace.File("in/page.png"));
        int sentinel = page.Pixels.Count(static pixel =>
            Math.Abs(pixel.Red - SentinelColor.Red) < 8 && Math.Abs(pixel.Green - SentinelColor.Green) < 8
            && Math.Abs(pixel.Blue - SentinelColor.Blue) < 8);
        Assert.True(sentinel == 0, $"The output shows {sentinel} pixels of the sentinel image outside the input directory.");
    }

    /// <summary>A TLS endpoint with a self-signed certificate that counts the connections it accepts.</summary>
    private sealed class TlsListener : IAsyncDisposable
    {
        private readonly System.Net.Sockets.TcpListener _listener = new(System.Net.IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new();
        private readonly System.Security.Cryptography.X509Certificates.X509Certificate2 _certificate;
        private readonly Task _accepting;
        private int _connections;

        public TlsListener()
        {
            using var key = System.Security.Cryptography.RSA.Create(2048);
            var request = new System.Security.Cryptography.X509Certificates.CertificateRequest(
                "CN=127.0.0.1", key, System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.RSASignaturePadding.Pkcs1);
            using var created = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
            _certificate = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12(
                created.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Pfx), null);
            _listener.Start();
            Port = ((System.Net.IPEndPoint)_listener.LocalEndpoint).Port;
            _accepting = Accept();
        }

        public int Port { get; }

        public int Connections => Volatile.Read(ref _connections);

        private async Task Accept()
        {
            try
            {
                while (!_stop.IsCancellationRequested)
                {
                    System.Net.Sockets.TcpClient client = await _listener.AcceptTcpClientAsync(_stop.Token);
                    Interlocked.Increment(ref _connections);
                    _ = Task.Run(async () =>
                    {
                        using (client)
                        using (var tls = new System.Net.Security.SslStream(client.GetStream()))
                        {
                            try
                            {
                                await tls.AuthenticateAsServerAsync(_certificate).WaitAsync(TimeSpan.FromSeconds(10));
                                byte[] response = System.Text.Encoding.ASCII.GetBytes(
                                    $"HTTP/1.1 200 OK\r\nContent-Type: image/png\r\nContent-Length: {ResourceHttpServer.Image.Length}\r\nConnection: close\r\n\r\n");
                                await tls.WriteAsync(response);
                                await tls.WriteAsync(ResourceHttpServer.Image);
                            }
                            catch (Exception exception) when (exception is IOException or System.Security.Authentication.AuthenticationException or TimeoutException)
                            {
                                // An engine that distrusts the certificate ends the handshake; the connection still arrived.
                            }
                        }
                    });
                }
            }
            catch (Exception exception) when (_stop.IsCancellationRequested && exception is OperationCanceledException or System.Net.Sockets.SocketException)
            {
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _stop.CancelAsync();
            _listener.Stop();
            await _accepting;
            _stop.Dispose();
            _certificate.Dispose();
        }
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
                Assert.Equal("NETWORK_RESOURCES_REQUESTED", requested.Code.Name);
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
            ["pdf", "create", output, "--from-html", input, "--allow-network-resources", "--timeout", "15", "--output-format", "json"]);

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
}
