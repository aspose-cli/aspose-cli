using Aspose.Cells;
using Aspose.Cells.Rendering;
using Aspose.Cli.Sdk.Licensing;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// The engine save options <see cref="WorkbookSavePlan"/> chooses for each convert format: text
/// exports write values as they are, in UTF-8 with a byte order mark only on request; html is
/// one self-contained file; a pdf prints the selected sheets; every other format needs none.
/// </summary>
public sealed class CellsSaveOptionsTests
{
    public static TheoryData<string> ConvertFormats() =>
        [.. CellsFormats.Definitions.Where(static format => format.Uses.HasFlag(FormatUse.Convert)).Select(static format => format.Id)];

    [Theory]
    [MemberData(nameof(ConvertFormats))]
    public void Create_ChoosesTheSaveOptionsOfEachConvertFormat(string formatId)
    {
        foreach (bool byteOrderMark in new[] { false, true })
        {
            SaveOptions? options = Plan(formatId, byteOrderMark, selectedSheets: null).Options;
            switch (formatId)
            {
                case "csv" or "tsv":
                    TxtSaveOptions text = Assert.IsType<TxtSaveOptions>(options);
                    Assert.Equal(formatId == "csv" ? SaveFormat.Csv : SaveFormat.TabDelimited, text.SaveFormat);
                    Assert.Equal(CellValueFormatStrategy.None, text.FormatStrategy);
                    Assert.False(text.TrimLeadingBlankRowAndColumn);
                    Assert.Equal("utf-8", text.Encoding.WebName);
                    Assert.Equal(byteOrderMark, text.Encoding.GetPreamble().Length > 0);
                    break;
                case "html":
                    HtmlSaveOptions html = Assert.IsType<HtmlSaveOptions>(options);
                    Assert.True(html.SaveAsSingleFile);
                    Assert.True(html.ExportImagesAsBase64);
                    break;
                case "md":
                    Assert.Equal(CellValueFormatStrategy.None, Assert.IsType<MarkdownSaveOptions>(options).FormatStrategy);
                    break;
                default:
                    // pdf without a selection, and the formats whose save format alone is enough.
                    Assert.Null(options);
                    break;
            }
        }
    }

    [Theory]
    [InlineData(new[] { 0, 2 })]
    [InlineData(new int[0])]
    public void Create_PrintsTheSelectedSheetsToAPdf(int[] selectedSheets)
    {
        PdfSaveOptions pdf = Assert.IsType<PdfSaveOptions>(Plan("pdf", byteOrderMark: false, selectedSheets).Options);

        Assert.NotNull(pdf.SheetSet);
        Assert.NotSame(SheetSet.All, pdf.SheetSet);
        Assert.NotSame(SheetSet.Visible, pdf.SheetSet);
        Assert.NotSame(SheetSet.Active, pdf.SheetSet);
    }

    [Fact]
    public void Create_ChoosesNoOptionsForASheetSelectionOutsideAPdf() =>
        Assert.Null(Plan("xlsx", byteOrderMark: false, [0]).Options);

    private static WorkbookSavePlan Plan(string formatId, bool byteOrderMark, IReadOnlyList<int>? selectedSheets) =>
        WorkbookSavePlan.Create(CellsFormats.Definitions.Single(format => format.Id == formatId), LicenseState.Licensed,
            OutputProtection.None, selectedSheets, byteOrderMark);
}
