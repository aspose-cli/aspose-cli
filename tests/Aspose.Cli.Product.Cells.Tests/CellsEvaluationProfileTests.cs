using Aspose.Cells;
using Aspose.Cli.Sdk.Licensing;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// What <see cref="CellsEvaluationProfile"/> names for an output of each format id: a workbook
/// format keeps every warning sheet, a page format and an image print the visible ones, and a
/// text or JSON export carries the notice an unlicensed save writes as content.
/// </summary>
public sealed class CellsEvaluationProfileTests
{
    private const string Pages = "pages";
    private const string WholeWorkbook = "workbook";

    /// <summary>The marks each output format names, one row per declared format id.</summary>
    private static readonly (string Format, string Marks)[] Table =
    [
        ("xlsx", WholeWorkbook),
        ("xltx", WholeWorkbook),
        ("xlsm", WholeWorkbook),
        ("xltm", WholeWorkbook),
        ("xlsb", WholeWorkbook),
        ("xls", WholeWorkbook),
        ("ods", WholeWorkbook),
        ("csv", "as the last row of the csv output, after the data"),
        ("tsv", "as the last row of the tsv output, after the data"),
        ("html", Pages),
        ("mhtml", Pages),
        ("pdf", Pages),
        ("xps", Pages),
        ("json", "in the json output, as a {\"watermark\": ...} record after the records of each sheet that has data rows"),
        ("md", "as a heading after the table in the md output"),
        ("png", Pages),
        ("jpeg", Pages),
        ("svg", Pages),
    ];

    public static TheoryData<string, string> MarksByFormat() => [.. Table];

    [Theory]
    [MemberData(nameof(MarksByFormat))]
    public void Inspect_ByFormat_NamesTheMarksOfThatOutput(string format, string marks)
    {
        using Workbook workbook = WorkbookWithWarningSheets();
        string[] expected = marks switch
        {
            WholeWorkbook =>
            [
                "the evaluation warning sheet 'Evaluation Warning', which is the active sheet",
                "the evaluation warning sheet 'Evaluation Warning (1)'",
            ],
            Pages => [$"the pages of the evaluation warning sheet 'Evaluation Warning', which hold only the notice '{CellsEvaluation.Notice}...'"],
            _ => workbook.IsLicensed ? [] : [$"the notice '{CellsEvaluation.Notice}...' {marks}, which is not workbook data"],
        };

        EvaluationMarks inspected = new CellsEvaluationProfile().Inspect(workbook, format);

        Assert.Equal(expected, inspected.Marks);
        Assert.False(inspected.IsTruncated);
    }

    [Fact]
    public void MarksByFormat_CoversEveryDeclaredFormat() =>
        Assert.Equal(
            CellsFormats.Definitions.Select(static format => format.Id),
            Table.Select(static row => row.Format));

    /// <summary>
    /// The write pipeline passes the declared format id of every output, so an id the product
    /// does not declare is a defect of the caller, not a workbook.
    /// </summary>
    [Theory]
    [InlineData("docx")]
    [InlineData("jpg")]
    [InlineData("txt")]
    public void Inspect_AnUndeclaredFormat_Throws(string format)
    {
        using Workbook workbook = WorkbookWithWarningSheets();

        Assert.Throws<InvalidOperationException>(() => new CellsEvaluationProfile().Inspect(workbook, format));
    }

    // A content sheet, then a visible warning sheet that is active and a hidden one.
    private static Workbook WorkbookWithWarningSheets()
    {
        var workbook = new Workbook();
        workbook.Worksheets[0].Name = "Data";
        workbook.Worksheets[0].Cells["A1"].PutValue("content");
        Worksheet visible = workbook.Worksheets[workbook.Worksheets.Add()];
        visible.Name = "Evaluation Warning";
        visible.Cells["A1"].PutValue(CellsEvaluation.Notice + " for .NET.");
        Worksheet hidden = workbook.Worksheets[workbook.Worksheets.Add()];
        hidden.Name = "Evaluation Warning (1)";
        hidden.Cells["A1"].PutValue(CellsEvaluation.Notice + " for .NET.");
        hidden.IsVisible = false;
        workbook.Worksheets.ActiveSheetIndex = visible.Index;
        return workbook;
    }
}
