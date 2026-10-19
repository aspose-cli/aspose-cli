using System.IO.Compression;
using Aspose.Cells;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// Every format the product declares is one the engine can serve: an input format is one the
/// engine detects, a convert format has a save format and a render format an image type.
/// </summary>
public sealed class CellsEngineFormatTests
{
    [Fact]
    public void EveryDeclaredFormat_HasAnEngineMapping()
    {
        var missing = new List<string>();
        foreach (FormatDescriptor format in CellsFormats.Definitions)
        {
            CellsEngineFormat? engine = CellsEngineFormats.All.SingleOrDefault(entry => entry.Id == format.Id);
            if (format.Uses.HasFlag(FormatUse.Input) && engine?.Detected is null)
            {
                missing.Add($"{format.Id}: no detected engine format");
            }

            if (format.Uses.HasFlag(FormatUse.Convert) && engine?.Save is null)
            {
                missing.Add($"{format.Id}: no engine save format");
            }

            if (format.Uses.HasFlag(FormatUse.Render) && engine?.Image is null)
            {
                missing.Add($"{format.Id}: no engine image type");
            }
        }

        Assert.True(missing.Count == 0, string.Join(Environment.NewLine, missing));
    }

    [Fact]
    public void EveryEngineMapping_IsADeclaredFormat()
    {
        HashSet<string> declared = CellsFormats.Definitions.Select(static format => format.Id).ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain(CellsEngineFormats.All, entry => !declared.Contains(entry.Id));
        Assert.Equal(
            CellsEngineFormats.All.Count,
            CellsEngineFormats.All.Select(static entry => entry.Id).Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// The SDK alone: an XPS lays a sheet out on its printed pages as a PDF does, so a chart
    /// reaching two of them is drawn on both, and on one once the sheet fits one page wide.
    /// </summary>
    [Fact]
    public void Xps_DrawsAChartOnEachPrintedPageItReaches()
    {
        using var fixture = new CellsFixture();
        string split = fixture.Temp.File("split.xlsx");
        string fitted = fixture.Temp.File("fitted.xlsx");
        CellsReviewTests.CreateWideChartWorkbook(split, fitToOnePageWide: false);
        CellsReviewTests.CreateWideChartWorkbook(fitted, fitToOnePageWide: true);

        // Data's pages, then Notes'; evaluation mode appends the pages of its warning sheet.
        Assert.Equal([true, true, false], PagesDrawingShapes(split)[..3]);
        Assert.Equal([true, false], PagesDrawingShapes(fitted)[..2]);
    }

    // Whether each page of the workbook saved as XPS draws a vector shape; the cells of the
    // fixture are text, so a shape is the chart.
    private static bool[] PagesDrawingShapes(string path)
    {
        using var workbook = new Workbook(path);
        using var xps = new MemoryStream();
        workbook.Save(xps, SaveFormat.Xps);
        xps.Position = 0;
        using var package = new ZipArchive(xps);
        return
        [
            .. package.Entries
                .Where(static entry => entry.FullName.EndsWith(".fpage", StringComparison.Ordinal))
                .OrderBy(static entry => int.Parse(Path.GetFileNameWithoutExtension(entry.Name), System.Globalization.CultureInfo.InvariantCulture))
                .Select(static entry =>
                {
                    using var reader = new StreamReader(entry.Open());
                    return reader.ReadToEnd().Contains("<Path", StringComparison.Ordinal);
                }),
        ];
    }

    /// <summary>The formats the engine both writes and reads back, where a round trip shows what an output keeps.</summary>
    public static TheoryData<string> LoadableFormats() =>
        [.. CellsFormats.Definitions.Where(static format => format.Uses.HasFlag(FormatUse.Input | FormatUse.Convert)).Select(static format => format.Id)];

    /// <summary>
    /// The engine alone: a protected sheet saved in the format and read back is still protected
    /// exactly when the format is declared to keep sheet protection, as the edit warning says.
    /// </summary>
    [Theory]
    [MemberData(nameof(LoadableFormats))]
    public void KeepsSheetProtection_IsWhatTheEngineKeeps(string formatId)
    {
        using var workbook = new Workbook();
        Worksheet sheet = workbook.Worksheets[0];
        sheet.Name = "Data";
        sheet.Cells["A1"].PutValue("content");
        sheet.Protect(ProtectionType.All);
        SaveFormat save = CellsEngineFormats.Save(formatId);
        using var saved = new MemoryStream();
        workbook.Save(saved, save);
        saved.Position = 0;

        using var reopened = new Workbook(saved, new LoadOptions(FileFormatUtil.SaveFormatToLoadFormat(save)));
        Worksheet read = reopened.Worksheets["Data"] ?? reopened.Worksheets[0];

        Assert.Equal(CellsFormats.Definitions.Single(format => format.Id == formatId).Protectable, read.IsProtected);
    }

    [Theory]
    [InlineData(Aspose.Cells.FileFormatType.Excel97To2003, "xls")]
    [InlineData(Aspose.Cells.FileFormatType.TabDelimited, "tsv")]
    [InlineData(Aspose.Cells.FileFormatType.Excel2, "excel2")]
    public void DetectedFormats_MapToTheirPublicIdOrTheEngineName(Aspose.Cells.FileFormatType detected, string id) =>
        Assert.Equal(id, CellsEngineFormats.IdOf(detected));
}
