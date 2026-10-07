using System.IO.Compression;
using System.Xml.Linq;
using Aspose.Cells;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>An output path's declared extension decides the saved format, templates included.</summary>
public sealed class CellsOutputFormatTests : IClassFixture<CellsFixture>
{
    private const string Edit = """{ "ops": [ { "op": "set_values", "sheet": "Data", "range": "A5", "values": [["edited"]] } ] }""";

    private readonly CellsFixture _fixture;

    public CellsOutputFormatTests(CellsFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData(".xltx", SaveFormat.Xltx, "xltx", FileFormatType.Xltx)]
    [InlineData(".xltm", SaveFormat.Xltm, "xltm", FileFormatType.Xltm)]
    public void EditingATemplateInPlace_KeepsItATemplate(string extension, SaveFormat format, string formatId, FileFormatType detected)
    {
        string path = _fixture.Temp.File("template" + extension);
        using (var workbook = new Workbook(_fixture.CreateSalesWorkbook("template-source.xlsx")))
        {
            workbook.Save(path, format);
        }

        EditResult result = Apply(path, path);

        Assert.Equal(formatId, result.Output!.Format);
        Assert.Equal(detected, FileFormatUtil.DetectFileFormat(path).FileFormatType);
        using var reopened = new Workbook(path);
        Assert.Equal("edited", reopened.Worksheets["Data"].Cells["A5"].StringValue);
    }

    [Fact]
    public void AnHtmOutput_IsWrittenAsHtml()
    {
        string output = _fixture.Temp.File("report.htm");

        EditResult result = Apply(_fixture.CreateSalesWorkbook("htm-source.xlsx"), output);

        Assert.Equal("html", result.Output!.Format);
        Assert.Contains("edited", File.ReadAllText(output), StringComparison.Ordinal);
    }

    [Fact]
    public void ConvertingToXltx_WritesATemplateAndReportsIt()
    {
        string output = _fixture.Temp.File("converted.xltx");

        ConvertResult result = _fixture.Engine.Convert(_fixture.CreateSalesWorkbook("convert-source.xlsx"), new ConvertRequest
        {
            Output = TestOutput.At(output, format: "xltx", overwrite: true),
        });

        Assert.Equal("xltx", result.Output.Format);
        Assert.Equal(FileFormatType.Xltx, FileFormatUtil.DetectFileFormat(output).FileFormatType);

        using ZipArchive package = ZipFile.OpenRead(output);
        using Stream types = package.GetEntry("[Content_Types].xml")!.Open();
        Assert.Contains(
            XDocument.Load(types).Descendants(),
            static node => (string?)node.Attribute("PartName") == "/xl/workbook.xml"
                && ((string?)node.Attribute("ContentType"))!.Contains("template", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("book.xltx", "xltx")]
    [InlineData("book.XLTM", "xltm")]
    [InlineData("book.htm", "html")]
    public void ACreatedWorkbook_IsTheFormatItsExtensionNames(string path, string format)
    {
        using var workspace = new TempWorkspace();

        CliResult created = workspace.Run("cells", "create", path, "--output", "json");

        Assert.True(created.ExitCode == 0, created.StdErr);
        Assert.Equal(format, System.Text.Json.Nodes.JsonNode.Parse(created.StdOut)!["output"]!["format"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("book")]
    [InlineData("book.docx")]
    public void ACreatedWorkbookWithoutAWorkbookExtension_IsRefusedBeforeAnythingIsWritten(string path)
    {
        using var workspace = new TempWorkspace();

        CliResult refused = workspace.Run("cells", "create", path, "--output", "json");

        Assert.Equal(2, refused.ExitCode);
        Assert.Contains("\"USAGE_ERROR\"", refused.StdErr, StringComparison.Ordinal);
        Assert.Contains(".xlsx", refused.StdErr, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFileSystemEntries(workspace.Path));
    }

    private EditResult Apply(string source, string output) =>
        _fixture.Engine.ApplyOps(
            source,
            CellsOp.Catalog.Parse<CellsOpsBatch>(Edit, Aspose.Cli.Generated.ProductJsonContext.Definition),
            new EditRequest { Output = TestOutput.At(output, overwrite: true) });
}
