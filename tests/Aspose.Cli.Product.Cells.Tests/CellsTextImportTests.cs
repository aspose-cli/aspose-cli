using System.Text;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Errors;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// A delimited text input is never decoded or parsed by guesswork: text that UTF-8 or
/// invariant number formats would change is refused, and cells convert reads it with the
/// encoding and culture the caller names.
/// </summary>
public sealed class CellsTextImportTests : IClassFixture<CellsFixture>
{
    private readonly CellsFixture _fixture;

    public CellsTextImportTests(CellsFixture fixture)
    {
        _fixture = fixture;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    [Fact]
    public void NonUtf8Text_IsRefusedAndConvertReadsItWithTheNamedEncoding()
    {
        string path = _fixture.Temp.File("erp-gbk.csv");
        File.WriteAllBytes(path, Encoding.GetEncoding("GB18030").GetBytes("大区,金额\n华东一区,\"1,234.50\"\n"));

        CliException refused = Assert.Throws<CliException>(() => _fixture.Engine.Read(path, new ReadRequest()));
        Assert.Equal(ErrorCodes.InputEncodingInvalid, refused.Code);
        Assert.Contains($"cells convert \"{path}\" --to xlsx --encoding gb18030", refused.Hint, StringComparison.Ordinal);

        WorkbookReadResult read = ImportAndRead(path, new TextImportOptions { Encoding = "gb18030" });
        Assert.Equal("华东一区", read.Sheet!.Cells![1][0].V?.ToString());
        Assert.Equal("1234.5", read.Sheet!.Cells![1][1].V?.ToString());
    }

    [Fact]
    public void DecimalCommas_AreRefusedAndConvertReadsThemWithTheNamedCulture()
    {
        string path = _fixture.Temp.File("partner.csv");
        File.WriteAllText(path, "Land;Preis;Betrag;Datum\nDE;2,71;1.253,96;20.08.2026\n", new UTF8Encoding(true));

        CliException refused = Assert.Throws<CliException>(() => _fixture.Engine.Read(path, new ReadRequest()));
        Assert.Equal(ErrorCodes.FormatAmbiguous, refused.Code);
        Assert.Equal("2,71", refused.Details!["sample"]!.GetValue<string>());
        Assert.Contains($"cells convert \"{path}\" --to xlsx --culture de-DE", refused.Hint, StringComparison.Ordinal);

        WorkbookReadResult read = ImportAndRead(path, new TextImportOptions { Culture = "de-DE" });
        Assert.Equal("2.71", read.Sheet!.Cells![1][1].V?.ToString());
        Assert.Equal("1253.96", read.Sheet.Cells![1][2].V?.ToString());
        Assert.Equal(CellValueTypes.DateTime, read.Sheet.Cells![1][3].T);
    }

    [Fact]
    public void ThousandsCommasAndUtf8_ReadWithoutOptions()
    {
        string path = _fixture.Temp.File("us.csv");
        File.WriteAllText(path, "Region,Amount,Note\nNortheast,\"1,234.50\",Müller\n");

        WorkbookReadResult read = _fixture.Engine.Read(path, new ReadRequest());

        Assert.Equal("1234.5", read.Sheet!.Cells![1][1].V?.ToString());
        Assert.Equal("Müller", read.Sheet.Cells![1][2].V?.ToString());
    }

    [Theory]
    [InlineData("é", 1)]
    [InlineData("华", 1)]
    [InlineData("华", 2)]
    [InlineData("😀", 1)]
    [InlineData("😀", 2)]
    [InlineData("😀", 3)]
    public void Utf8Characters_SplitAcrossReadBlocks_AreValid(string character, int bytesBeforeBoundary)
    {
        string path = _fixture.Temp.File($"split-{Guid.NewGuid():N}.csv");
        var content = new List<byte>(Encoding.ASCII.GetBytes("text\n"));
        foreach (int boundary in SplitBoundaries)
        {
            AppendCharacterAcross(content, character, boundary, bytesBeforeBoundary);
        }

        File.WriteAllBytes(path, [.. content]);

        WorkbookReadResult read = _fixture.Engine.Read(path, new ReadRequest());

        Assert.Equal("text", read.Sheet!.Cells![0][0].V?.ToString());
    }

    [Fact]
    public void InvalidUtf8_AfterASplitCharacter_IsRefusedAtItsOffset()
    {
        string path = _fixture.Temp.File("split-invalid.csv");
        var content = new List<byte>(Encoding.ASCII.GetBytes("text\n"));
        AppendCharacterAcross(content, "华", SplitBoundaries[0], 2);
        int invalidOffset = content.Count + 10;
        content.AddRange(Encoding.ASCII.GetBytes("0123456789"));
        content.Add(0xFF);
        content.AddRange(Encoding.ASCII.GetBytes("\n"));
        File.WriteAllBytes(path, [.. content]);

        CliException refused = Assert.Throws<CliException>(() => _fixture.Engine.Read(path, new ReadRequest()));

        Assert.Equal(ErrorCodes.InputEncodingInvalid, refused.Code);
        Assert.Equal(invalidOffset, refused.Details!["offset"]!.GetValue<long>());
    }

    [Fact]
    public void TruncatedUtf8_AtAReadBlockEnd_IsRefusedAtItsFirstByte()
    {
        string path = _fixture.Temp.File("split-truncated.csv");
        var content = new List<byte>(Encoding.ASCII.GetBytes("text\n"));
        AppendFiller(content, SplitBoundaries[0] - 2);
        content.AddRange([0xE5, 0x8D]);
        content.AddRange(Encoding.ASCII.GetBytes("ab\n"));
        File.WriteAllBytes(path, [.. content]);

        CliException refused = Assert.Throws<CliException>(() => _fixture.Engine.Read(path, new ReadRequest()));

        Assert.Equal(ErrorCodes.InputEncodingInvalid, refused.Code);
        Assert.Equal(SplitBoundaries[0] - 2, refused.Details!["offset"]!.GetValue<long>());
    }

    // Power-of-two offsets cover the encoding check's read block boundaries.
    private static readonly int[] SplitBoundaries = [1 << 16, 1 << 19];

    private static void AppendCharacterAcross(List<byte> content, string character, int boundary, int bytesBeforeBoundary)
    {
        AppendFiller(content, boundary - bytesBeforeBoundary);
        content.AddRange(Encoding.UTF8.GetBytes(character));
        content.Add((byte)'\n');
    }

    private static void AppendFiller(List<byte> content, int end)
    {
        while (content.Count + 64 < end)
        {
            content.AddRange(Encoding.ASCII.GetBytes(new string('a', 63) + "\n"));
        }

        while (content.Count < end)
        {
            content.Add((byte)'a');
        }
    }

    [Theory]
    [InlineData("no-such-encoding", null, "--encoding")]
    [InlineData(null, "xx-NOPE", "--culture")]
    public void UnknownEncodingOrCulture_IsAnOptionError(string? encoding, string? culture, string option)
    {
        string path = _fixture.CreateCsv("options.csv");

        CliException error = Assert.Throws<CliException>(() => ImportText(path, new TextImportOptions { Encoding = encoding, Culture = culture }));

        Assert.Equal(ErrorCodes.OptionInvalid, error.Code);
        Assert.Contains(option, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TextOptions_AreRefusedForAWorkbook()
    {
        string path = _fixture.CreateSalesWorkbook("not-text.xlsx");

        CliException error = Assert.Throws<CliException>(() => ImportText(path, new TextImportOptions { Culture = "de-DE" }));

        Assert.Equal(ErrorCodes.OptionInvalid, error.Code);
    }

    private ConvertResult ImportText(string path, TextImportOptions import) =>
        _fixture.Engine.Convert(path, new ConvertRequest
        {
            TargetFormatId = "xlsx",
            OutputPath = _fixture.Temp.File(Path.GetFileNameWithoutExtension(path) + "-" + Guid.NewGuid().ToString("N") + ".xlsx"),
            TextImport = import,
        });

    private WorkbookReadResult ImportAndRead(string path, TextImportOptions import) =>
        _fixture.Engine.Read(ImportText(path, import).Output.Path, new ReadRequest());
}
