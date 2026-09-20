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
        if (!OperatingSystem.IsWindows()) { return; }
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
        var loader = new WorkbookLoadService(ProductTestBudgets.Create<CellsModule>());
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
        var sink = new ArtifactSink();
        AssertOmission(fixture.Engine.RenderView(
            input,
            new ViewRenderRequest
            {
                View = CellsViews.Workbook,
                MaxParts = 8,
                Purpose = ViewPurpose.Display,
            },
            sink).Warnings);
        Assert.True(server.RequestCount == 0, string.Join("; ", server.Requests));
        File.WriteAllBytes(fixture.Temp.File("local.png"), []);
    }

    [Fact]
    public async Task Review_OmissionsMakeEvidenceIncomplete()
    {
        if (!OperatingSystem.IsWindows()) { return; }
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
        ViewManifest rendered = adapter.Render(fixture.Engine, input, request, new ArtifactSink());
        ProductReviewAssessment assessment = adapter.Assess(fixture.Engine, input, request, rendered);
        Assert.False(assessment.Complete);
        AssertOmission(rendered.Warnings);
        AssertOmission(assessment.Warnings);
        Assert.Equal(0, server.RequestCount);
    }

    [Fact]
    public void Mhtml_EmbeddedImageNeedsNoExternalFile()
    {
        if (!OperatingSystem.IsWindows()) { return; }
        using var fixture = new CellsFixture();
        string input = fixture.Temp.File("embedded.mht");
        File.WriteAllText(input, Mhtml("<html><body><table><tr><td>Embedded</td><td><img src='local.png'></td></tr></table></body></html>"));
        Assert.False(File.Exists(fixture.Temp.File("local.png")));
        using LoadedWorkbook loaded = new WorkbookLoadService(
            ProductTestBudgets.Create<CellsModule>()).Open(input, null);
        Assert.Contains(loaded.Workbook.Worksheets.Cast<Worksheet>().SelectMany(
            sheet => sheet.Pictures.Cast<Aspose.Cells.Drawing.Picture>()),
            picture => picture.Data is { Length: > 0 } && picture.OriginalWidth == 1 && picture.OriginalHeight == 1);
    }

    [Fact]
    public async Task EditVerification_ReportsSourceResourceOmissions()
    {
        if (!OperatingSystem.IsWindows()) { return; }
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

    private sealed class ArtifactSink : IViewArtifactSink
    {
        public void Write(string relativePath, Action<Stream> contentWriter)
        {
            using var stream = new MemoryStream();
            contentWriter(stream);
            Assert.True(stream.Length > 0);
        }
        public void WriteText(string relativePath, string content) => Assert.NotEmpty(content);
    }
}
