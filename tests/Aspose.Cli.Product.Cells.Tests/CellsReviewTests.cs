using Aspose.Cells;
using Aspose.Cells.Charts;
using Aspose.Cells.Rendering;
using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Views;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

public sealed class CellsReviewTests
{
    /// <summary>
    /// A print area of several areas draws one image per area, and a sheet's image holds the
    /// first: render says so, as incomplete evidence, with the command for each other area.
    /// </summary>
    [Fact]
    public void ASheetWithSeveralPrintAreas_SaysItsImageShowsTheFirstOnly()
    {
        Requires.Windows();
        using var workspace = new TempWorkspace();
        Assert.Equal(0, workspace.Run("cells", "create", "areas.xlsx", "--sheet-names", "Data").ExitCode);
        File.WriteAllText(workspace.File("ops.json"), """
            {"ops":[
              {"op":"set_values","sheet":"Data","range":"A1","values":[["Left",1],["Rows",2]]},
              {"op":"set_values","sheet":"Data","range":"E1","values":[["Right",3],["Rows",4]]},
              {"op":"set_print_area","sheet":"Data","range":"A1:B2,E1:F2"}
            ]}
            """);
        Assert.Equal(0, workspace.Run("cells", "edit", "areas.xlsx", "--ops", "ops.json", "--in-place").ExitCode);

        CliResult rendered = workspace.Run("cells", "render", "areas.xlsx", "--out", "areas.png", "--output-format", "json");

        Assert.True(rendered.ExitCode == 0, rendered.StdErr);
        JsonNode warning = Assert.Single(JsonNode.Parse(rendered.StdOut)!["warnings"]!.AsArray(), static warning => warning!["code"]!.GetValue<string>() == "SHEET_PARTIALLY_RENDERED")!;
        Assert.Contains("shows only the first, A1:B2", warning["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains("--range \"'Data'!E1:F2\"", warning["hint"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    /// <summary>
    /// A render taller than an image viewer or a model reads in full warns RENDER_IMAGE_LARGE with
    /// a window of the sheet to render instead; a render of that window does not.
    /// </summary>
    [Fact]
    public void Render_WarnsWhenItsImageIsTooLargeToReadAndNamesAWindow()
    {
        Requires.Windows();
        using var workspace = new TempWorkspace();
        Assert.Equal(0, workspace.Run("cells", "create", "long.xlsx", "--sheet-names", "Data").ExitCode);
        string rows = string.Join(",", Enumerable.Range(1, 1200).Select(static row => $"[\"Item {row}\",{row}]"));
        File.WriteAllText(workspace.File("ops.json"), $$"""{"ops":[{"op":"set_values","sheet":"Data","range":"A1","values":[{{rows}}]}]}""");
        Assert.Equal(0, workspace.Run("cells", "edit", "long.xlsx", "--ops", "ops.json", "--in-place").ExitCode);

        CliResult whole = workspace.Run("cells", "render", "long.xlsx", "--sheets", "1", "--out", "long.png", "--output-format", "json");
        CliResult window = workspace.Run("cells", "render", "long.xlsx", "--range", "Data!A1:B200", "--out", "window.png", "--output-format", "json");

        Assert.True(whole.ExitCode == 0, whole.StdErr);
        JsonNode warning = Assert.Single(JsonNode.Parse(whole.StdOut)!["warnings"]!.AsArray(), static warning => warning!["code"]!.GetValue<string>() == "RENDER_IMAGE_LARGE")!;
        Assert.Contains("more than 10000 on a side", warning["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains("--range \"'Data'!A1:B", warning["hint"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.True(window.ExitCode == 0, window.StdErr);
        Assert.DoesNotContain("RENDER_IMAGE_LARGE", window.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    public void ChartPages_DoNotCountTheCellAChartEndsExactlyAt()
    {
        Requires.Windows();
        using var fixture = new CellsFixture();
        using var workbook = new Workbook();
        Worksheet sheet = workbook.Worksheets[0];
        sheet.Cells[150, 40].PutValue("far corner");
        CellArea first = sheet.GetPrintingPageBreaks(new ImageOrPrintOptions())[0];
        Chart flush = sheet.Charts[sheet.Charts.Add(ChartType.Column, 1, 1, first.EndRow + 1, first.EndColumn + 1)];
        Chart over = sheet.Charts[sheet.Charts.Add(ChartType.Column, 1, 1, first.EndRow + 1, first.EndColumn + 1)];
        over.ChartObject.LowerDeltaX = 512;
        over.ChartObject.LowerDeltaY = 128;
        Assert.Equal(0, flush.ChartObject.LowerDeltaX);
        Assert.Equal(0, flush.ChartObject.LowerDeltaY);

        IReadOnlyList<(Chart Chart, int Pages)> pages = PrintedPages.ChartPages(sheet);

        Assert.Equal(1, pages[0].Pages);
        Assert.Equal(4, pages[1].Pages);
    }

    /// <summary>
    /// A chart on one printed page counts that page alone, whichever way the sheet prints a Chinese
    /// comment, and the sheet keeps its setting: the comment pages at the sheet's end hold no chart
    /// (CELLS-SHEET-END-COMMENT-PAGES). Fitted to one page, a chart that would cross a page break at
    /// full size stays on one page with the comments at the end too, as the PDF prints it.
    /// </summary>
    [Theory]
    [InlineData(PrintCommentsType.PrintSheetEnd, false)]
    [InlineData(PrintCommentsType.PrintSheetEnd, true)]
    [InlineData(PrintCommentsType.PrintInPlace, false)]
    public void ChartPages_CountOnlyTheCellPagesWhereverTheCommentsPrint(PrintCommentsType printComments, bool fitToOnePage)
    {
        Requires.Windows();
        using var fixture = new CellsFixture();
        using var workbook = new Workbook();
        Worksheet sheet = workbook.Worksheets[0];
        sheet.Cells.ImportTwoDimensionArray(new object[,] { { "Region", "Q1" }, { "North", 10 }, { "South", 8 } }, 0, 8);
        sheet.Comments[sheet.Comments.Add("J2")].Note = "已复核";
        if (fitToOnePage)
        {
            sheet.Cells[120, 9].PutValue("far below");
            sheet.Charts.Add(ChartType.Column, 45, 0, 60, 6);
            sheet.PageSetup.FitToPagesWide = 1;
            sheet.PageSetup.FitToPagesTall = 1;
        }
        else
        {
            sheet.Charts.Add(ChartType.Column, 0, 0, 15, 6);
        }
        sheet.PageSetup.PrintComments = printComments;

        IReadOnlyList<(Chart Chart, int Pages)> pages = PrintedPages.ChartPages(sheet);

        Assert.Equal(1, Assert.Single(pages).Pages);
        Assert.Equal(printComments, sheet.PageSetup.PrintComments);
    }

    /// <summary>A sheet whose chart spans columns D to Q, past the first portrait page, and a sheet of notes.</summary>
    internal static void CreateWideChartWorkbook(string path, bool fitToOnePageWide)
    {
        using var workbook = new Workbook();
        Worksheet data = workbook.Worksheets[0];
        data.Name = "Data";
        data.Cells["A1"].PutValue("Month");
        data.Cells["B1"].PutValue("Sales");
        for (int row = 1; row <= 4; row++)
        {
            data.Cells[row, 0].PutValue("M" + row);
            data.Cells[row, 1].PutValue(row * 10);
        }
        Chart chart = data.Charts[data.Charts.Add(ChartType.Column, 1, 3, 20, 16)];
        chart.Name = "Wide";
        chart.NSeries.Add("Data!B2:B5", true);
        if (fitToOnePageWide)
        {
            data.PageSetup.FitToPagesWide = 1;
            data.PageSetup.FitToPagesTall = 0;
        }
        workbook.Worksheets.Add("Notes").Cells["A1"].PutValue("Monthly sales");
        workbook.Save(path);
    }
}
