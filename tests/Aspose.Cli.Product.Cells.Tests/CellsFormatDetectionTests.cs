using System.Text;
using Aspose.Cells;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// The source format a command reports is the one the load detected, read once from the file
/// the engine opened, and the loaded workbook's own format stands only where detection cannot see it.
/// </summary>
public sealed class CellsFormatDetectionTests(CellsFixture fixture) : IClassFixture<CellsFixture>
{
    [Fact]
    public void SourceFormat_IsTheFormatDetectedWhenTheWorkbookWasLoaded()
    {
        string path = SaveWorkbook("loaded.xls", SaveFormat.Excel97To2003);
        string replacement = SaveWorkbook("replacement.xlsx", SaveFormat.Xlsx);

        using LoadedWorkbook loaded = fixture.Session.Loader.Open(path, password: null);
        File.Copy(replacement, path, overwrite: true);

        Assert.Equal("xls", CellsEngineSupport.BuildSource(path, loaded).Format);
    }

    [Fact]
    public void AnExcel2Workbook_IsReportedAsExcel2NotAsTheUpgradedModel()
    {
        string path = fixture.Temp.File("legacy.xls");
        File.WriteAllBytes(path, Biff2Workbook());

        WorkbookInfoResult result = CellsInfo.Run(fixture.Session, new InfoRequest { Input = path });

        Assert.Equal("excel2", result.Source.Format);
    }

    [Fact]
    public void AnEncryptedXlsx_IsReportedAsXlsxNotAsItsContainer()
    {
        string path = fixture.CreateEncryptedWorkbook("secret", "encrypted.xlsx");

        WorkbookInfoResult result = CellsInfo.Run(fixture.Session, new InfoRequest { Input = path, Password = new Secret("secret") });

        Assert.Equal("xlsx", result.Source.Format);
        Assert.True(result.Source.Encrypted);
    }

    [Fact]
    public void AnHtmlExportThatDefeatsDetection_IsReportedAsHtml()
    {
        string path = fixture.Temp.File("export.html");
        File.WriteAllText(
            path,
            "Quarterly report\r\n<table><tr><td>Region</td><td>Total</td></tr><tr><td>North</td><td>42</td></tr></table>",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        using (FileStream input = File.OpenRead(path))
        {
            Assert.Equal(FileFormatType.Unknown, FileFormatUtil.DetectFileFormat(input).FileFormatType);
        }

        WorkbookInfoResult result = CellsInfo.Run(fixture.Session, new InfoRequest { Input = path });

        Assert.Equal("html", result.Source.Format);
    }

    private string SaveWorkbook(string fileName, SaveFormat format)
    {
        string path = fixture.Temp.File(fileName);
        using var workbook = new Workbook();
        workbook.Worksheets[0].Cells["A1"].PutValue(fileName);
        workbook.Save(path, format);
        return path;
    }

    // A minimal BIFF2 worksheet stream: BOF, DIMENSIONS, one NUMBER cell and EOF.
    private static byte[] Biff2Workbook()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        WriteRecord(writer, 0x0009, [0x02, 0x00, 0x10, 0x00]);
        WriteRecord(writer, 0x0000, [0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00]);
        WriteRecord(writer, 0x0003, [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, .. BitConverter.GetBytes(42.0)]);
        WriteRecord(writer, 0x000A, []);
        writer.Flush();
        return stream.ToArray();
    }

    private static void WriteRecord(BinaryWriter writer, ushort type, byte[] data)
    {
        writer.Write(type);
        writer.Write((ushort)data.Length);
        writer.Write(data);
    }
}
