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
    /// <summary>
    /// Another command's in-place edit holds the input open with delete access while it
    /// replaces the file; a load at that moment still reads the input.
    /// </summary>
    [Fact]
    public void Load_ReadsAnInputThatAnInPlaceEditIsReplacing()
    {
        using var fixture = new CellsFixture();
        string input = fixture.Temp.File("book.csv");
        File.WriteAllText(input, "Region,Revenue\nEast,1\n");
        var loader = CellsWorkbookLoader.ForFontInspection(ProductTestBudgets.Create<CellsModule>());

        using (new FileStream(input, FileMode.Open, FileAccess.Read,
                   FileShare.ReadWrite | FileShare.Delete, 1, FileOptions.DeleteOnClose))
        {
            using LoadedWorkbook loaded = loader.Open(input, null);
            Assert.Equal("East", loaded.Workbook.Worksheets[0].Cells["A2"].StringValue);
        }
    }

    /// <summary>
    /// A delimited text sheet is named after its file as the engine's path load names it; a file
    /// name without a stem keeps the engine's default sheet name.
    /// </summary>
    [Theory]
    [InlineData("book.csv", "book")]
    [InlineData("book [2024].csv", "book (2024)")]
    [InlineData("a file name longer than thirty-one characters.csv", "a file name longer than thirty-")]
    [InlineData("'quoted'.csv", "'quoted'")]
    [InlineData("'.csv", "'")]
    [InlineData(".csv", "Sheet1")]
    public void Load_NamesADelimitedTextSheetAfterItsFile(string name, string sheet)
    {
        using var fixture = new CellsFixture();
        string input = fixture.Temp.File(name);
        File.WriteAllText(input, "Region,Revenue\nEast,1\n");
        var loader = CellsWorkbookLoader.ForFontInspection(ProductTestBudgets.Create<CellsModule>());

        using LoadedWorkbook loaded = loader.Open(input, null);

        Assert.Equal(sheet, loaded.Workbook.Worksheets[0].Name);
    }

    [Theory]
    [InlineData("html")]
    [InlineData("xls")]
    [InlineData("mht")]
    public async Task HtmlInputs_ImportNoImagesAndNeverFetchRemoteResources(string extension)
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
        var loader = CellsWorkbookLoader.ForFontInspection(ProductTestBudgets.Create<CellsModule>());
        using (LoadedWorkbook loaded = loader.Open(input, null))
        {
            Assert.Equal(0, loaded.Workbook.Worksheets.Cast<Worksheet>().Sum(sheet => sheet.Pictures.Count));
            Assert.True(server.RequestCount == 0, string.Join("; ", server.Requests));
            AssertOmission(loaded.Warnings());
        }
        AssertOmission(CellsInfo.Run(fixture.Session, new CellsInspectRequest { Input = input }).Warnings);
        AssertOmission(CellsRead.Run(fixture.Session, new CellsReadRequest { Input = input }).Warnings);
        string output = fixture.Temp.File("converted-" + extension + ".xlsx");
        AssertOmission(CellsConvert.Run(fixture.Session, new CellsConvertRequest
        {
            Input = input,
            Output = TestOutput.At(output, format: "xlsx"),
        }).Warnings);
        var sink = new MemoryArtifactSink();
        AssertOmission(CellsView.Render(fixture.Session, input,
            new ViewRenderRequest
            {
                View = CellsViews.Workbook,
                MaxPartCount = 8,
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
            MaxPartCount = 10,
            Purpose = ViewPurpose.Evidence,
        };
        var sink = new MemoryArtifactSink();
        ViewManifest rendered = adapter.Render(fixture.Session, input, request, sink);
        Assert.All(sink.Paths, path => Assert.NotEmpty(sink.Bytes(path)));
        ProductReviewAssessment assessment = adapter.Assess(fixture.Session, input, request, rendered);
        Assert.False(assessment.Complete);
        AssertOmission(rendered.Warnings);
        AssertOmission(assessment.Warnings);
        Assert.Equal(0, server.RequestCount);
    }

    [Fact]
    public void Mhtml_EmbeddedImageIsNotImported()
    {
        Requires.Windows();
        using var fixture = new CellsFixture();
        string input = fixture.Temp.File("embedded.mht");
        File.WriteAllText(input, Mhtml("<html><body><table><tr><td>Embedded</td><td><img src='local.png'></td></tr></table></body></html>"));
        Assert.False(File.Exists(fixture.Temp.File("local.png")));
        using LoadedWorkbook loaded = CellsWorkbookLoader.ForFontInspection(ProductTestBudgets.Create<CellsModule>()).Open(input, null);
        Assert.Equal(0, loaded.Workbook.Worksheets.Cast<Worksheet>().Sum(sheet => sheet.Pictures.Count));
        Assert.Contains(loaded.Warnings() ?? [], warning => warning.Code.Name == "IMAGES_SKIPPED" && warning.AffectsCompleteness);
    }

    [Fact]
    public async Task EditVerification_ReportsSourceResourceOmissions()
    {
        Requires.Windows();
        using var workspace = new TempWorkspace();
        await using var server = new ResourceHttpServer();
        File.WriteAllText(workspace.File("input.html"),
            $"<html><body><table><tr><td>Old value</td></tr></table><img src='{server.Url}/image.png'></body></html>");
        CliResult inspected = workspace.Run("cells", "inspect", "input.html", "--output-format", "json");
        Assert.True(inspected.ExitCode == 0, inspected.StdErr);
        string sheet = System.Text.Json.Nodes.JsonNode.Parse(inspected.StdOut)!["workbook"]!["sheets"]![0]!["name"]!.GetValue<string>();
        string ops = System.Text.Json.JsonSerializer.Serialize(new
        {
            ops = new[] { new { op = "set_values", sheet, range = "A1", values = new[] { new[] { "New value" } } } },
        });
        CliResult edited = workspace.Run("cells", "edit", "input.html", "--ops", ops,
            "--out", "edited.xlsx", "--verify", "--output-format", "json");
        Assert.Equal(8, edited.ExitCode);
        var verification = System.Text.Json.Nodes.JsonNode.Parse(edited.StdOut)!["verification"]!;
        Assert.False(verification["ok"]!.GetValue<bool>());
        // An HTML image is never imported: the omission is reported as a skipped image.
        Assert.Contains(verification["issues"]!.AsArray(),
            issue => issue!["code"]!.GetValue<string>() == "IMAGES_SKIPPED");
        Assert.Equal(0, server.RequestCount);
    }

    /// <summary>
    /// An HTML input imports no images: the warning names each image its img elements write,
    /// wherever it is, and points to insert_image, while the omission warning names only the
    /// other resources, such as a missing style sheet, with the relative-path advice.
    /// </summary>
    [Fact]
    public void Html_NamesTheImagesItLeavesOutApartFromOtherOmittedResources()
    {
        Requires.Windows();
        using var fixture = new CellsFixture();
        string folder = Directory.CreateDirectory(fixture.Temp.File("page")).FullName;
        Directory.CreateDirectory(fixture.Temp.File("shared"));
        byte[] png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGP4z8DwHwAFAAH/iZk9HQAAAABJRU5ErkJggg==");
        File.WriteAllBytes(Path.Combine(folder, "beside.png"), png);
        File.WriteAllBytes(fixture.Temp.File("shared/far.png"), png);
        string input = Path.Combine(folder, "page.html");
        File.WriteAllText(input,
            "<html><head><link rel='stylesheet' href='gone.css'></head><body><table><tr><td>a"
            + "<img src='beside.png'><img src='images/missing.png'><img src='../shared/far.png'></td></tr></table></body></html>");

        using LoadedWorkbook loaded = CellsWorkbookLoader.ForFontInspection(ProductTestBudgets.Create<CellsModule>()).Open(input, null);

        Warning[] warnings = [.. loaded.Warnings() ?? []];
        Warning images = Assert.Single(warnings, static warning => warning.Code.Name == "IMAGES_SKIPPED");
        foreach (string source in new[] { "beside.png", "images/missing.png", "../shared/far.png" })
        {
            Assert.Contains(source, images.Message, StringComparison.Ordinal);
        }

        Assert.Contains("insert_image", images.Hint, StringComparison.Ordinal);
        Assert.DoesNotContain("relative path", images.Hint, StringComparison.Ordinal);
        Warning omitted = Assert.Single(warnings, static warning => warning.Code.Name == "REMOTE_RESOURCES_BLOCKED");
        Assert.Contains("gone.css (not found)", omitted.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(".png", omitted.Message, StringComparison.Ordinal);
        Assert.Contains("relative path", omitted.Hint, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.Temp.File("shared"), string.Join(" ", warnings.Select(static warning => warning.Message)), StringComparison.OrdinalIgnoreCase);
    }

    // A compressed SVG cannot be read for the references it names (CELLS-SVG-EGRESS), so the
    // CLI refuses it; an SVG's references are covered by CellsSvgPictureTests.
    [Theory]
    [InlineData("remote.svgz", true)]
    [InlineData("local.svg", false)]
    public async Task InsertImage_RefusesACompressedSvgWithoutARequest(string name, bool refused)
    {
        using var workspace = new TempWorkspace();
        await using var server = new ResourceHttpServer();
        Assert.Equal(0, workspace.Run("cells", "create", "source.xlsx", "--sheet-names", "Data").ExitCode);
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

        CliResult edited = workspace.Run("cells", "edit", "source.xlsx", "--ops", ops, "--out", "result.xlsx", "--output-format", "json");

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
                || warning.Code == CellsDiagnostics.MhtmlResourceCoverageUnverified
                || warning.Code.Name == "IMAGES_SKIPPED") && warning.AffectsCompleteness);

    private static string Mhtml(string html) =>
        "MIME-Version: 1.0\r\nContent-Type: multipart/related; boundary=resource-test\r\n\r\n"
        + "--resource-test\r\nContent-Type: text/html; charset=utf-8\r\n"
        + "Content-Transfer-Encoding: 8bit\r\n\r\n" + html
        + "\r\n--resource-test\r\nContent-Type: image/png\r\nContent-Location: local.png\r\n"
        + "Content-Transfer-Encoding: base64\r\n\r\n" + Convert.ToBase64String(ResourceHttpServer.Image)
        + "\r\n--resource-test--\r\n";
}
