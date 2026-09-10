using System.Text;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Preview;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

public sealed class CellsPreviewTests : IClassFixture<CellsFixture>
{
    private readonly CellsFixture _fixture;

    public CellsPreviewTests(CellsFixture fixture) => _fixture = fixture;

    [Fact]
    public void PayloadProducers_UseTheCurrentContractVersion()
    {
        Assert.Equal(
            2,
            CellsPreviewPayloads.Selector(new CellsPreviewSelector("Data")).SchemaVersion);
        Assert.Equal(
            2,
            CellsPreviewPayloads.Hint(new CellsPreviewHint("Data", "A1")).SchemaVersion);
    }

    [Fact]
    public void WorkbookView_WritesSelfContainedHtmlThroughArtifactSink()
    {
        string path = _fixture.CreateSalesWorkbook("preview-workbook.xlsx");
        var artifacts = new MemoryArtifactSink();

        PreviewRenderOutcome result = _fixture.Engine.RenderPreview(
            path,
            new PreviewRenderRequest { View = CellsPreviewViews.Workbook },
            artifacts);

        Assert.Equal("index.html", result.EntryFileName);
        Assert.Equal("xlsx", result.SourceFormatId);
        Assert.Equal(new FileInfo(path).Length, result.SourceSizeBytes);
        Assert.Equal(["index.html"], artifacts.Paths);

        string html = artifacts.Text("index.html");
        Assert.Contains(PreviewExporter.ActiveSheetMetaName, html, StringComparison.Ordinal);
        Assert.Contains("sheetName='Data'", html, StringComparison.Ordinal);
        Assert.Contains("sheetName='Second'", html, StringComparison.Ordinal);
        if (_fixture.LicenseState == LicenseState.Evaluation)
        {
            Assert.Contains("Evaluation Only", html, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SheetView_WritesBrowserEntryAndSelectedPngThroughArtifactSink()
    {
        string path = _fixture.CreateSalesWorkbook("preview-sheet.xlsx");
        var artifacts = new MemoryArtifactSink();
        PreviewRenderer renderer = new CellsPreviewAdapter().CreateRenderer(
            _fixture.Engine,
            path,
            new ProductPreviewRequest(
                CellsPreviewViews.Sheet,
                Selector: CellsPreviewPayloads.Selector(
                    new CellsPreviewSelector("Second"))));

        PreviewRenderOutcome result = renderer(new PreviewRenderContext(artifacts));

        Assert.Equal("sheet.html", result.EntryFileName);
        Assert.Equal(["frame.png", "sheet.html"], artifacts.Paths);
        Assert.True(IsPng(artifacts.Binary("frame.png")));
        Assert.Contains(
            "src=\"/asset/frame.png\"",
            artifacts.Text("sheet.html"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void SheetView_RejectsOversizedRasterBeforePublishingArtifacts()
    {
        using var workbook = new Aspose.Cells.Workbook();
        Aspose.Cells.Worksheet sheet = workbook.Worksheets[0];
        for (int column = 0; column < 200; column++)
        {
            sheet.Cells[0, column].PutValue("bounded preview");
            sheet.Cells.SetColumnWidth(column, 255);
        }
        var artifacts = new MemoryArtifactSink();

        CliException failure = Assert.Throws<CliException>(() =>
            PreviewExporter.ExportImage(workbook, sheet, artifacts));

        Assert.Equal(ErrorCodes.RenderTooLarge, failure.Code);
        Assert.Empty(artifacts.Paths);
    }

    [Fact]
    public void WorkbookView_StopsHtmlGenerationAtItsProductBudget()
    {
        using var stream = new PreviewExporter.LimitedMemoryStream(8);

        CliException failure = Assert.Throws<CliException>(() =>
            stream.Write(new byte[9]));

        Assert.Equal(ErrorCodes.PreviewBudgetExceeded, failure.Code);
        Assert.Equal(0, stream.Length);
    }

    [Fact]
    public void WorkbookView_PassesPasswordWhilePublishingThroughArtifactSink()
    {
        string path = _fixture.CreateEncryptedWorkbook(
            "preview-secret",
            "preview-encrypted.xlsx");
        var artifacts = new MemoryArtifactSink();
        PreviewRenderer renderer = new CellsPreviewAdapter().CreateRenderer(
            _fixture.Engine,
            path,
            new ProductPreviewRequest(
                CellsPreviewViews.Workbook,
                Password: "preview-secret"));

        PreviewRenderOutcome result = renderer(new PreviewRenderContext(artifacts));

        Assert.Contains(
            "classified",
            artifacts.Text(result.EntryFileName),
            StringComparison.Ordinal);
    }

    private sealed class MemoryArtifactSink : IPreviewArtifactSink
    {
        private readonly Dictionary<string, byte[]> _artifacts =
            new(StringComparer.Ordinal);

        public IReadOnlyList<string> Paths =>
            _artifacts.Keys.Order(StringComparer.Ordinal).ToArray();

        public void Write(string relativePath, Action<Stream> contentWriter)
        {
            using var stream = new MemoryStream();
            contentWriter(stream);
            _artifacts.Add(relativePath, stream.ToArray());
        }

        public void WriteText(string relativePath, string content) =>
            _artifacts.Add(relativePath, Encoding.UTF8.GetBytes(content));

        public byte[] Binary(string relativePath) => _artifacts[relativePath];

        public string Text(string relativePath) =>
            Encoding.UTF8.GetString(Binary(relativePath));
    }

    private static bool IsPng(byte[] content) =>
        content.AsSpan().StartsWith(
            new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a });
}
