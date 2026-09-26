using Aspose.Cells;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Product.Cells.Engine;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Views;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

public sealed class CellsResourceLoadingTests
{
    [Theory]
    [InlineData("html")]
    [InlineData("xls")]
    [InlineData("mht")]
    public async Task HtmlInputs_UseLocalResourcesAndNeverFetchRemoteResources(string extension)
    {
        Requires.Windows();
        using var fixture = new CellsFixture();
        await using var server = new ResourceHttpServer();
        string input = fixture.Temp.File("input." + extension);
        File.WriteAllBytes(fixture.Temp.File("local.png"), ResourceHttpServer.Image);
        string html = $"""
            <html><head><link rel="stylesheet" href="{server.Url}/style.css"></head>
            <body><table><tr><td>Resource test</td><td><img src="local.png" width="20" height="20"></td>
            <td><img src="{server.Url}/image.png" width="20" height="20"></td></tr></table></body></html>
            """;
        File.WriteAllText(input, extension == "mht" ? Mhtml(html) : html);
        var loader = new CellsWorkbookLoader(ProductTestBudgets.Create<CellsModule>());
        using (LoadedWorkbook loaded = loader.Open(input, null))
        {
            Assert.True(loaded.Workbook.Worksheets.Cast<Worksheet>().Sum(sheet => sheet.Pictures.Count) > 0);
            Assert.True(server.RequestCount == 0, string.Join("; ", server.Requests));
            AssertOmission(loaded.Warnings());
        }
        AssertOmission(fixture.Engine.GetInfo(input, new InfoRequest()).Warnings);
        AssertOmission(fixture.Engine.Read(input, new ReadRequest()).Warnings);
        string output = fixture.Temp.File("converted-" + extension + ".xlsx");
        AssertOmission(fixture.Engine.Convert(input, new ConvertRequest
        {
            TargetFormatId = "xlsx", OutputPath = output,
        }).Warnings);
        var sink = new MemoryArtifactSink();
        AssertOmission(fixture.Engine.RenderView(
            input,
            new ViewRenderRequest
            {
                View = CellsViews.Workbook,
                MaxParts = 8,
                Purpose = ViewPurpose.Display,
            },
            sink).Warnings);
        Assert.All(sink.Paths, path => Assert.NotEmpty(sink.Bytes(path)));
        Assert.True(server.RequestCount == 0, string.Join("; ", server.Requests));
        File.WriteAllBytes(fixture.Temp.File("local.png"), []);
    }

    [Fact]
    public async Task Review_OmissionsMakeEvidenceIncomplete()
    {
        Requires.Windows();
        using var fixture = new CellsFixture();
        await using var server = new ResourceHttpServer();
        string input = fixture.Temp.File("review.html");
        File.WriteAllText(input, $"<html><body><table><tr><td>Data</td></tr></table><img src='{server.Url}/image.png'></body></html>");
        var adapter = new CellsViewAdapter();
        var request = new ViewRenderRequest
        {
            View = CellsViews.Sheets,
            MaxParts = 10,
            Purpose = ViewPurpose.Evidence,
        };
        var sink = new MemoryArtifactSink();
        ViewManifest rendered = adapter.Render(fixture.Engine, input, request, sink);
        Assert.All(sink.Paths, path => Assert.NotEmpty(sink.Bytes(path)));
        ProductReviewAssessment assessment = adapter.Assess(fixture.Engine, input, request, rendered);
        Assert.False(assessment.Complete);
        AssertOmission(rendered.Warnings);
        AssertOmission(assessment.Warnings);
        Assert.Equal(0, server.RequestCount);
    }

    [Fact]
    public void Mhtml_EmbeddedImageNeedsNoExternalFile()
    {
        Requires.Windows();
        using var fixture = new CellsFixture();
        string input = fixture.Temp.File("embedded.mht");
        File.WriteAllText(input, Mhtml("<html><body><table><tr><td>Embedded</td><td><img src='local.png'></td></tr></table></body></html>"));
        Assert.False(File.Exists(fixture.Temp.File("local.png")));
        using LoadedWorkbook loaded = new CellsWorkbookLoader(
            ProductTestBudgets.Create<CellsModule>()).Open(input, null);
        Assert.Contains(loaded.Workbook.Worksheets.Cast<Worksheet>().SelectMany(
            sheet => sheet.Pictures.Cast<Aspose.Cells.Drawing.Picture>()),
            picture => picture.Data is { Length: > 0 } && picture.OriginalWidth == 1 && picture.OriginalHeight == 1);
    }

    [Fact]
    public async Task EditVerification_ReportsSourceResourceOmissions()
    {
        Requires.Windows();
        using var workspace = new TempWorkspace();
        await using var server = new ResourceHttpServer();
        File.WriteAllText(workspace.File("input.html"),
            $"<html><body><table><tr><td>Old value</td></tr></table><img src='{server.Url}/image.png'></body></html>");
        CliResult inspected = workspace.Run("cells", "inspect", "input.html", "--output", "json");
        Assert.True(inspected.ExitCode == 0, inspected.StdErr);
        string sheet = System.Text.Json.Nodes.JsonNode.Parse(inspected.StdOut)!["workbook"]!["sheets"]![0]!["name"]!.GetValue<string>();
        string ops = System.Text.Json.JsonSerializer.Serialize(new
        {
            ops = new[] { new { op = "set_values", sheet, range = "A1", values = new[] { new[] { "New value" } } } },
        });
        CliResult edited = workspace.Run("cells", "edit", "input.html", "--ops", ops,
            "--out", "edited.xlsx", "--verify", "--output", "json");
        Assert.Equal(8, edited.ExitCode);
        var verification = System.Text.Json.Nodes.JsonNode.Parse(edited.StdOut)!["verification"]!;
        Assert.False(verification["ok"]!.GetValue<bool>());
        Assert.Contains(verification["issues"]!.AsArray(),
            issue => issue!["code"]!.GetValue<string>() == WarningCodes.RemoteResourcesBlocked);
        Assert.Equal(0, server.RequestCount);
    }

    // Adding an SVG picture makes Aspose.Cells fetch the SVG's external images, and no resource
    // provider governs that request (gate CELLS-SVG-EGRESS), so the CLI refuses such an image.
    [Theory]
    [InlineData("remote.svg", true)]
    [InlineData("remote.svgz", true)]
    [InlineData("local.svg", false)]
    public async Task InsertImage_RefusesAnSvgThatNamesANetworkAddressWithoutARequest(string name, bool refused)
    {
        using var workspace = new TempWorkspace();
        await using var server = new ResourceHttpServer();
        Assert.Equal(0, workspace.Run("cells", "create", "source.xlsx", "--sheets", "Data").ExitCode);
        string svg = name == "local.svg"
            ? """<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20"><rect width="10" height="10"/></svg>"""
            : $"""<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="20" height="20"><image xlink:href="{server.Url}/svg.png" width="10" height="10"/></svg>""";
        byte[] content = System.Text.Encoding.UTF8.GetBytes(svg);
        if (name.EndsWith(".svgz", StringComparison.Ordinal))
        {
            using var compressed = new MemoryStream();
            using (var gzip = new System.IO.Compression.GZipStream(compressed, System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true))
            {
                gzip.Write(content);
            }
            content = compressed.ToArray();
        }
        File.WriteAllBytes(workspace.File(name), content);
        string ops = System.Text.Json.JsonSerializer.Serialize(new
        {
            ops = new[] { new { op = "insert_image", sheet = "Data", at = "B2", path = workspace.File(name) } },
        });

        CliResult edited = workspace.Run("cells", "edit", "source.xlsx", "--ops", ops, "--out", "result.xlsx", "--output", "json");

        if (refused)
        {
            Assert.NotEqual(0, edited.ExitCode);
            Assert.Contains(ErrorCodes.FeatureUnsupported.Name, edited.StdOut + edited.StdErr, StringComparison.Ordinal);
            Assert.False(File.Exists(workspace.File("result.xlsx")));
        }
        else
        {
            Assert.True(edited.ExitCode == 0, edited.StdErr);
        }
        Assert.True(server.RequestCount == 0, string.Join("; ", server.Requests));
    }

    private static void AssertOmission(IReadOnlyList<Warning>? warnings) =>
        Assert.Contains(warnings ?? [], warning =>
            (warning.Code == WarningCodes.RemoteResourcesBlocked
                || warning.Code == CellsDiagnostics.MhtmlResourceCoverageUnverified) && warning.AffectsCompleteness);

    private static string Mhtml(string html) =>
        "MIME-Version: 1.0\r\nContent-Type: multipart/related; boundary=resource-test\r\n\r\n"
        + "--resource-test\r\nContent-Type: text/html; charset=utf-8\r\n"
        + "Content-Transfer-Encoding: 8bit\r\n\r\n" + html
        + "\r\n--resource-test\r\nContent-Type: image/png\r\nContent-Location: local.png\r\n"
        + "Content-Transfer-Encoding: base64\r\n\r\n" + Convert.ToBase64String(ResourceHttpServer.Image)
        + "\r\n--resource-test--\r\n";
}
