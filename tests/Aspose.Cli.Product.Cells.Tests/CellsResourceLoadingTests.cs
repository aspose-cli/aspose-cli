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

    /// <summary>
    /// Cells reads no HTML or MHTML, whatever the file's name: reading it, the engine would open
    /// the images it names by path, past every resource hook. The input is refused before the
    /// engine loads it, so nothing it names is requested.
    /// </summary>
    [Theory]
    [InlineData("html")]
    [InlineData("htm")]
    [InlineData("xls")]
    [InlineData("mhtml")]
    [InlineData("mht")]
    public async Task HtmlInputs_AreRefusedBeforeAnythingTheyNameIsRequested(string extension)
    {
        Requires.Windows();
        using var fixture = new CellsFixture();
        await using var server = new ResourceHttpServer();
        string input = fixture.Temp.File("input." + extension);
        string html = $"""
            <html><head><link rel="stylesheet" href="{server.Url}/style.css"></head>
            <body><table><tr><td>Resource test</td><td><img src="{server.Url}/image.png" width="20" height="20"></td></tr></table></body></html>
            """;
        File.WriteAllText(input, extension.StartsWith("mht", StringComparison.Ordinal) ? Mhtml(html) : html);

        CliException inspected = Assert.Throws<CliException>(() => CellsInfo.Run(fixture.Session, new CellsInspectRequest { Input = input }));
        CliException converted = Assert.Throws<CliException>(() => CellsConvert.Run(fixture.Session, new CellsConvertRequest
        {
            Input = input,
            Output = TestOutput.At(fixture.Temp.File("converted-" + extension + ".xlsx"), format: "xlsx"),
        }));

        foreach (CliException refused in new[] { inspected, converted })
        {
            // The Host restates it as FORMAT_UNSUPPORTED or FORMAT_MISMATCH where routing says so.
            Assert.Equal(ErrorCodes.FileCorrupt, refused.Code);
            Assert.StartsWith("cells writes html, mhtml", refused.Hint, StringComparison.Ordinal);
        }
        Assert.False(File.Exists(fixture.Temp.File("converted-" + extension + ".xlsx")));
        Assert.True(server.RequestCount == 0, string.Join("; ", server.Requests));
    }

    [Fact]
    public async Task Review_OmissionsMakeEvidenceIncomplete()
    {
        Requires.Windows();
        using var fixture = new CellsFixture();
        await using var server = new ResourceHttpServer();
        string input = LinkedWorkbook(fixture.Temp.File("review.xlsx"), server.Url + "/image.png");
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
    public async Task EditVerification_ReportsSourceResourceOmissions()
    {
        Requires.Windows();
        using var workspace = new TempWorkspace();
        await using var server = new ResourceHttpServer();
        LinkedWorkbook(workspace.File("input.xlsx"), server.Url + "/image.png");

        CliResult edited = workspace.Run("cells", "edit", "input.xlsx", "--set", "Data!A1=New value",
            "--out", "edited.xlsx", "--verify", "--output-format", "json");

        Assert.Equal(8, edited.ExitCode);
        var verification = System.Text.Json.Nodes.JsonNode.Parse(edited.StdOut)!["verification"]!;
        Assert.False(verification["ok"]!.GetValue<bool>());
        Assert.Contains(verification["issues"]!.AsArray(),
            issue => issue!["code"]!.GetValue<string>() == WarningCodes.RemoteResourcesBlocked.Name);
        Assert.Equal(0, server.RequestCount);
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
        Assert.Contains(warnings ?? [], warning => warning.Code == WarningCodes.RemoteResourcesBlocked && warning.AffectsCompleteness);

    // A workbook whose one picture links to reference and stores no copy of it.
    private static string LinkedWorkbook(string path, string reference)
    {
        using var workbook = new Workbook();
        Worksheet sheet = workbook.Worksheets[0];
        sheet.Name = "Data";
        sheet.Cells["A1"].PutValue("Linked picture");
        using var image = new MemoryStream(ResourceHttpServer.Image, writable: false);
        Aspose.Cells.Drawing.Picture picture = sheet.Pictures[sheet.Pictures.Add(2, 1, image)];
        picture.SourceFullName = reference;
        workbook.Save(path, SaveFormat.Xlsx);
        return path;
    }

    private static string Mhtml(string html) =>
        "MIME-Version: 1.0\r\nContent-Type: multipart/related; boundary=resource-test\r\n\r\n"
        + "--resource-test\r\nContent-Type: text/html; charset=utf-8\r\n"
        + "Content-Transfer-Encoding: 8bit\r\n\r\n" + html
        + "\r\n--resource-test\r\nContent-Type: image/png\r\nContent-Location: local.png\r\n"
        + "Content-Transfer-Encoding: base64\r\n\r\n" + Convert.ToBase64String(ResourceHttpServer.Image)
        + "\r\n--resource-test--\r\n";
}
