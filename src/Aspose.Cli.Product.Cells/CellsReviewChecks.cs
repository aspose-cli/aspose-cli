using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Product.Cells;

/// <summary>Every check the Cells review can report; findings are built only from these.</summary>
internal static class CellsReviewChecks
{
    public static ReviewCheck SheetHidden { get; } = new(
        "CELLS_SHEET_HIDDEN",
        ReviewSeverities.Info,
        "A worksheet is hidden and therefore excluded from the visual evidence.");

    public static ReviewCheck SheetEmpty { get; } = new(
        "CELLS_SHEET_EMPTY",
        ReviewSeverities.Info,
        "A visible worksheet has no cells or drawing objects, so its image is a blank placeholder.");

    public static ReviewCheck UsedRangeSparse { get; } = new(
        "CELLS_USED_RANGE_SPARSE",
        ReviewSeverities.Warning,
        "Less than 1% of a worksheet's used area of at least 100 cells contains data.");

    public static ReviewCheck CellsClipped { get; } = new(
        "CELLS_VALUES_CLIPPED",
        ReviewSeverities.Warning,
        "Values are wider than their columns: text is cut off by the next cell, or a number cannot show in full (#### or rounded).");

    public static ReviewCheck PopulatedColumnsHidden { get; } = new(
        "CELLS_POPULATED_COLUMNS_HIDDEN",
        ReviewSeverities.Warning,
        "Columns that contain data are hidden.");

    public static ReviewCheck PopulatedColumnsNarrow { get; } = new(
        "CELLS_POPULATED_COLUMNS_NARROW",
        ReviewSeverities.Warning,
        "Columns that contain data are narrower than 3 character units.");

    public static ReviewCheck PopulatedColumnsWide { get; } = new(
        "CELLS_POPULATED_COLUMNS_WIDE",
        ReviewSeverities.Warning,
        "Columns that contain data are wider than 80 character units.");

    public static ReviewCheck PopulatedRowsHidden { get; } = new(
        "CELLS_POPULATED_ROWS_HIDDEN",
        ReviewSeverities.Warning,
        "Rows that contain data are hidden.");

    public static ReviewCheck PopulatedRowsShort { get; } = new(
        "CELLS_POPULATED_ROWS_SHORT",
        ReviewSeverities.Warning,
        "Rows that contain data are shorter than 8 points.");

    public static ReviewCheck PopulatedRowsTall { get; } = new(
        "CELLS_POPULATED_ROWS_TALL",
        ReviewSeverities.Warning,
        "Rows that contain data are taller than 120 points.");

    public static ReviewCheck PrintAreaInvalid { get; } = new(
        "CELLS_PRINT_AREA_INVALID",
        ReviewSeverities.Warning,
        "A saved print area cannot be interpreted as bounded A1 ranges.");

    public static ReviewCheck PrintAreaExcludesContent { get; } = new(
        "CELLS_PRINT_AREA_EXCLUDES_CONTENT",
        ReviewSeverities.Warning,
        "A print area does not contain every populated cell of its worksheet.");

    public static ReviewCheck PrintAreaExcessive { get; } = new(
        "CELLS_PRINT_AREA_EXCESSIVE",
        ReviewSeverities.Warning,
        "A print area is more than 20 times the bounds of the populated content.");

    public static ReviewCheck PrintAreaExcludesChart { get; } = new(
        "CELLS_PRINT_AREA_EXCLUDES_CHART",
        ReviewSeverities.Warning,
        "A print area does not intersect a chart on its worksheet.");

    public static ReviewCheck ChartSplitAcrossPages { get; } = new(
        "CELLS_CHART_SPLIT_ACROSS_PAGES",
        ReviewSeverities.Warning,
        "A chart reaches more than one printed page, so printing and PDF export split it.");

    public static ReviewCheck ChartHidden { get; } = new(
        "CELLS_CHART_HIDDEN",
        ReviewSeverities.Warning,
        "A chart object is hidden and provides no visible evidence.");

    public static ReviewCheck ChartTooSmall { get; } = new(
        "CELLS_CHART_TOO_SMALL",
        ReviewSeverities.Warning,
        "A chart is narrower than 120 or shorter than 80 pixels.");

    public static ReviewCheck ChartWithoutSeries { get; } = new(
        "CELLS_CHART_WITHOUT_SERIES",
        ReviewSeverities.Warning,
        "A chart has no data series.");

    public static ReviewCheck ChartAnchoredInHiddenCells { get; } = new(
        "CELLS_CHART_ANCHORED_IN_HIDDEN_CELLS",
        ReviewSeverities.Warning,
        "A chart's anchor corner lies in a hidden row or column.");

    public static ReviewCheck FormulaError { get; } = new(
        "CELLS_FORMULA_ERROR",
        ReviewSeverities.Error,
        "A formula evaluates to an error value.");

    public static ReviewCheck VbaPresent { get; } = new(
        "CELLS_VBA_PRESENT",
        ReviewSeverities.Warning,
        "The workbook contains VBA, which static images do not exercise.");

    public static ReviewCheck ReviewTruncated { get; } = new(
        "CELLS_REVIEW_TRUNCATED",
        ReviewSeverities.Warning,
        "The artifact limit stopped rendering before every visible worksheet had an image.");

    public static IReadOnlyList<ReviewCheck> All { get; } =
    [
        SheetHidden,
        SheetEmpty,
        CellsClipped,
        UsedRangeSparse,
        PopulatedColumnsHidden,
        PopulatedColumnsNarrow,
        PopulatedColumnsWide,
        PopulatedRowsHidden,
        PopulatedRowsShort,
        PopulatedRowsTall,
        PrintAreaInvalid,
        PrintAreaExcludesContent,
        PrintAreaExcessive,
        PrintAreaExcludesChart,
        ChartHidden,
        ChartTooSmall,
        ChartWithoutSeries,
        ChartAnchoredInHiddenCells,
        ChartSplitAcrossPages,
        FormulaError,
        VbaPresent,
        ReviewTruncated,
    ];
}
