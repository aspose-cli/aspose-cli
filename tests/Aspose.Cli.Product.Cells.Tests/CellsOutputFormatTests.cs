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
    [InlineData(".xltx", SaveFormat.Xltx, "xlsx", FileFormatType.Xltx)]
    [InlineData(".xltm", SaveFormat.Xltm, "xlsm", FileFormatType.Xltm)]
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
    public void ConvertingToATemplatePath_WritesATemplate()
    {
        string output = _fixture.Temp.File("converted.xltx");

        _fixture.Engine.Convert(_fixture.CreateSalesWorkbook("convert-source.xlsx"), new ConvertRequest
        {
            TargetFormatId = "xlsx",
            OutputPath = output,
            Overwrite = true,
        });

        using ZipArchive package = ZipFile.OpenRead(output);
        using Stream types = package.GetEntry("[Content_Types].xml")!.Open();
        Assert.Contains(
            XDocument.Load(types).Descendants(),
            static node => (string?)node.Attribute("PartName") == "/xl/workbook.xml"
                && ((string?)node.Attribute("ContentType"))!.Contains("template", StringComparison.Ordinal));
    }

    private EditResult Apply(string source, string output) =>
        _fixture.Engine.ApplyOps(
            source,
            CellsOp.Catalog.Parse<CellsOpsBatch>(Edit, Aspose.Cli.Generated.ProductJsonContext.Definition),
            new EditRequest { OutputPath = output, Overwrite = true });
}
