using System.Text;
using Aspose.Cells;
using Aspose.Cells.Drawing;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Product.Cells.Engine;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Views;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

public sealed class CellsLinkedPictureTests
{
    [Theory]
    [InlineData("missing")]
    [InlineData("local")]
    [InlineData("http")]
    public async Task CachedPicture_SurvivesConversionAndPreviewWithoutReadingItsSource(string source)
    {
        using var fixture = new CellsFixture();
        using var workspace = new TempWorkspace();
        await using var server = new ResourceHttpServer();
        string reference = source == "http" ? server.Url + "/image.png" : workspace.File("external.png");
        if (source == "local")
        {
            File.WriteAllText(reference, "This is deliberately not the cached image.");
        }
        string input = workspace.File("cached.xlsx");
        CreateWorkbook(fixture, input, reference, cached: true);
        byte[] original = File.ReadAllBytes(input);
        using var before = new Workbook(input);
        Picture expected = Assert.Single(before.Worksheets[0].Pictures.Cast<Picture>());
        Assert.True(expected.IsLink);
        Assert.Equal(ResourceHttpServer.Image, expected.Data);

        CliResult result = workspace.Run("cells", "convert", input, "--to", "xlsx",
            "--out", "converted.xlsx", "--output", "json");
        Assert.True(result.ExitCode == 0, result.StdErr);
        Assert.DoesNotContain(WarningCodes.RemoteResourcesBlocked, result.StdOut, StringComparison.Ordinal);
        using var after = new Workbook(workspace.File("converted.xlsx"));
        Picture actual = Assert.Single(after.Worksheets[0].Pictures.Cast<Picture>());
        Assert.Equal(expected.Data, actual.Data);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Left, actual.Left);
        Assert.Equal(expected.Top, actual.Top);
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);
        Assert.False(actual.IsLink);
        Assert.True(string.IsNullOrEmpty(actual.SourceFullName));

        var sink = new HtmlSink();
        fixture.Engine.RenderView(
            input,
            new ViewRenderRequest
            {
                View = CellsViews.Workbook,
                MaxParts = 8,
                Purpose = ViewPurpose.Display,
            },
            sink);
        Assert.Contains("data:image/png;base64,", sink.Html, StringComparison.Ordinal);
        Assert.Contains(System.Convert.ToBase64String(ResourceHttpServer.Image), sink.Html, StringComparison.Ordinal);
        Assert.Equal(original, File.ReadAllBytes(input));
        Assert.Equal(0, server.RequestCount);
        if (source == "local")
        {
            Assert.Equal("This is deliberately not the cached image.", File.ReadAllText(reference));
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void UncachedPicture_UsesOnlyAvailableLocalData(bool available)
    {
        if (!OperatingSystem.IsWindows()) { return; }
        using var fixture = new CellsFixture();
        string reference = fixture.Temp.File("local.png");
        if (available) { File.WriteAllBytes(reference, ResourceHttpServer.Image); }
        string input = fixture.Temp.File("linked.xlsx");
        CreateWorkbook(fixture, input, reference, cached: false);
        using LoadedWorkbook loaded = new WorkbookLoadService(
            ProductTestBudgets.Create<CellsModule>()).Open(input, null);
        if (available)
        {
            Picture picture = Assert.Single(loaded.Workbook.Worksheets[0].Pictures.Cast<Picture>());
            Assert.Equal(ResourceHttpServer.Image, picture.Data);
            Assert.False(picture.IsLink);
            Assert.DoesNotContain(loaded.Warnings() ?? [], warning => warning.AffectsCompleteness);
        }
        else
        {
            Assert.Empty(loaded.Workbook.Worksheets[0].Pictures);
            Assert.Contains(loaded.Warnings() ?? [], warning => warning.Code == WarningCodes.RemoteResourcesBlocked);
        }
    }

    private static void CreateWorkbook(CellsFixture fixture, string path, string reference, bool cached)
    {
        using var workbook = new Workbook();
        Worksheet sheet = workbook.Worksheets[0];
        sheet.Name = "Data";
        sheet.Cells["A1"].PutValue("Stored picture");
        using var image = new MemoryStream(ResourceHttpServer.Image, writable: false);
        Picture picture = sheet.Pictures[sheet.Pictures.Add(2, 1, image)];
        picture.Name = "cached-logo";
        picture.Width = 48;
        picture.Height = 32;
        picture.SourceFullName = reference;
        if (cached) { picture.Data = ResourceHttpServer.Image; }
        workbook.Save(path, SaveFormat.Xlsx);
    }

    private sealed class HtmlSink : IViewArtifactSink
    {
        public string Html { get; private set; } = string.Empty;
        public void Write(string relativePath, Action<Stream> contentWriter)
        {
            using var output = new MemoryStream();
            contentWriter(output);
            Html += Encoding.UTF8.GetString(output.ToArray());
        }
        public void WriteText(string relativePath, string content) => Html += content;
    }
}
