namespace Aspose.Cli.Product.Cells.Contracts;

// Ops that add embedded objects (charts, pivot tables) to a sheet.

/// <summary>Adds a native chart plotting a data range.</summary>
public sealed record CreateChartOp() : Op
{
    /// <summary>Chart type; one of <see cref="ChartTypes"/>.</summary>
    public required string Type { get; init; }

    /// <summary>
    /// Data to plot, including headers, e.g. <c>A1:C5</c>. May be sheet-qualified
    /// (e.g. <c>Data!A1:C5</c>) to plot data from another sheet — a chart on a
    /// dashboard sheet can source a data sheet; otherwise the op's sheet.
    /// </summary>
    public required string DataRange { get; init; }

    /// <summary>Cell range the chart is placed over, on the op's sheet, e.g. <c>E2:L18</c>.</summary>
    public required string At { get; init; }

    /// <summary>Chart title.</summary>
    public string? Title { get; init; }

    /// <summary>Plot series from rows instead of columns; columns when omitted.</summary>
    public bool? SeriesInRows { get; init; }

    /// <summary>Legend visibility and placement.</summary>
    public ChartLegendData? Legend { get; init; }

    /// <summary>Axis titles. Pie charts have no axes and reject these.</summary>
    public ChartAxisTitlesData? AxisTitles { get; init; }

    /// <summary>
    /// Series colors as <c>#RRGGBB</c>, applied in series order; a pie's
    /// slices count as the series. Extra colors beyond the count are ignored.
    /// </summary>
    public IReadOnlyList<string>? SeriesColors { get; init; }

    /// <summary>Data label visibility and number format.</summary>
    public ChartDataLabelsData? DataLabels { get; init; }
}

/// <summary>Accepted values of <see cref="CreateChartOp.Type"/>.</summary>
public static class ChartTypes
{
    public const string Column = "column";
    public const string Bar = "bar";
    public const string Line = "line";
    public const string Pie = "pie";
    public const string Scatter = "scatter";
    public const string Area = "area";

    /// <summary>Every chart type, in documentation order.</summary>
    public static IReadOnlyList<string> All { get; } = [Column, Bar, Line, Pie, Scatter, Area];
}

/// <summary>
/// Legend settings of a chart. Only the fields present are applied; there is
/// no <c>none</c> position — hide the legend with <see cref="Visible"/> false.
/// </summary>
public sealed record ChartLegendData
{
    /// <summary>Show or hide the legend.</summary>
    public bool? Visible { get; init; }

    /// <summary>Placement; one of <see cref="LegendPositions"/>.</summary>
    public string? Position { get; init; }
}

/// <summary>Accepted values of <see cref="ChartLegendData.Position"/>.</summary>
public static class LegendPositions
{
    public const string Right = "right";
    public const string Bottom = "bottom";
    public const string Top = "top";
    public const string Left = "left";

    /// <summary>Every legend position, in documentation order.</summary>
    public static IReadOnlyList<string> All { get; } = [Right, Bottom, Top, Left];
}

/// <summary>
/// Axis titles of a chart. Only the fields present are applied. Pie charts
/// have no axes and reject these.
/// </summary>
public sealed record ChartAxisTitlesData
{
    /// <summary>Title of the category (X) axis.</summary>
    public string? Category { get; init; }

    /// <summary>Title of the value (Y) axis.</summary>
    public string? Value { get; init; }
}

/// <summary>Data label settings of a chart's series.</summary>
public sealed record ChartDataLabelsData
{
    /// <summary>Show each point's value next to it.</summary>
    public bool? Visible { get; init; }

    /// <summary>Number format code for the labels, e.g. <c>#,##0</c>.</summary>
    public string? Format { get; init; }
}

/// <summary>Adds a pivot table summarizing a source range.</summary>
public sealed record CreatePivotOp() : Op
{
    /// <summary>
    /// Source data including headers; may be sheet-qualified
    /// (e.g. <c>Data!A1:D100</c>), otherwise the op's sheet.
    /// </summary>
    public required string SourceRange { get; init; }

    /// <summary>Top-left anchor of the pivot table, on the op's sheet.</summary>
    public required string At { get; init; }

    /// <summary>Pivot table name; generated when omitted.</summary>
    public string? Name { get; init; }

    /// <summary>Header names of the row fields.</summary>
    public IReadOnlyList<string>? Rows { get; init; }

    /// <summary>Header names of the column fields.</summary>
    public IReadOnlyList<string>? Columns { get; init; }

    /// <summary>Value fields with their aggregation.</summary>
    public required IReadOnlyList<PivotValueField> Values { get; init; }
}

/// <summary>One aggregated value field of a pivot table.</summary>
public sealed record PivotValueField
{
    /// <summary>Header name of the source column.</summary>
    public required string Field { get; init; }

    /// <summary>Aggregation; one of <see cref="PivotFunctions"/>. <c>sum</c> when omitted.</summary>
    public string? Function { get; init; }

    /// <summary>Number format code applied to this value field, e.g. <c>#,##0</c>.</summary>
    public string? NumberFormat { get; init; }
}

/// <summary>Accepted values of <see cref="PivotValueField.Function"/>.</summary>
public static class PivotFunctions
{
    public const string Sum = "sum";
    public const string Count = "count";
    public const string Average = "average";
    public const string Max = "max";
    public const string Min = "min";

    /// <summary>Every function, in documentation order.</summary>
    public static IReadOnlyList<string> All { get; } = [Sum, Count, Average, Max, Min];
}

/// <summary>Inserts a picture from a file, anchored at a cell.</summary>
public sealed record InsertImageOp() : Op
{
    /// <summary>Path to the image file (png, jpeg, gif, bmp).</summary>
    public required string Path { get; init; }

    /// <summary>Top-left anchor cell, e.g. <c>E2</c>.</summary>
    public required string At { get; init; }

    /// <summary>Width in pixels; the image's natural width when omitted.</summary>
    public int? Width { get; init; }

    /// <summary>Height in pixels; the image's natural height when omitted.</summary>
    public int? Height { get; init; }
}

/// <summary>
/// Recalculates pivot tables so they reflect changed source data (pivots do not
/// refresh automatically). Refreshes the named pivot, or every pivot on the
/// sheet when <see cref="Name"/> is omitted.
/// </summary>
public sealed record RefreshPivotOp() : Op
{
    /// <summary>Pivot table name; all pivots on the sheet when omitted.</summary>
    public string? Name { get; init; }
}

/// <summary>
/// Turns a range into a native table (ListObject) with filtering, an optional
/// built-in style and an optional totals row.
/// </summary>
public sealed record CreateTableOp() : Op
{
    /// <summary>The range to convert, including its header row, e.g. <c>A1:D20</c>.</summary>
    public required string Range { get; init; }

    /// <summary>Table name; generated when omitted.</summary>
    public string? Name { get; init; }

    /// <summary>Built-in table style name, e.g. <c>TableStyleMedium2</c>.</summary>
    public string? Style { get; init; }

    /// <summary>Add a totals row below the table.</summary>
    public bool? TotalsRow { get; init; }
}

/// <summary>
/// Updates an existing chart's title, data range, type or series orientation.
/// Identify the chart by <see cref="Index"/> or <see cref="Name"/>, then set any
/// of the other fields to change them.
/// </summary>
public sealed record UpdateChartOp() : Op
{
    /// <summary>Zero-based index of the chart on the sheet. Give this or <see cref="Name"/>.</summary>
    public int? Index { get; init; }

    /// <summary>Chart name. Give this or <see cref="Index"/>.</summary>
    public string? Name { get; init; }

    /// <summary>New title.</summary>
    public string? Title { get; init; }

    /// <summary>New data range, e.g. <c>A1:C5</c>; may be sheet-qualified (e.g. <c>Data!A1:C5</c>).</summary>
    public string? DataRange { get; init; }

    /// <summary>New chart type; one of <see cref="ChartTypes"/>.</summary>
    public string? Type { get; init; }

    /// <summary>Plot series from rows instead of columns; applies only with <see cref="DataRange"/>.</summary>
    public bool? SeriesInRows { get; init; }

    /// <summary>Legend visibility and placement.</summary>
    public ChartLegendData? Legend { get; init; }

    /// <summary>Axis titles. Pie charts have no axes and reject these.</summary>
    public ChartAxisTitlesData? AxisTitles { get; init; }

    /// <summary>
    /// Series colors as <c>#RRGGBB</c>, applied in series order; a pie's
    /// slices count as the series. Extra colors beyond the count are ignored.
    /// </summary>
    public IReadOnlyList<string>? SeriesColors { get; init; }

    /// <summary>Data label visibility and number format.</summary>
    public ChartDataLabelsData? DataLabels { get; init; }
}

/// <summary>
/// Removes a chart from a sheet. Identify the chart by <see cref="Index"/> or
/// <see cref="Name"/>, exactly as <see cref="UpdateChartOp"/> does.
/// </summary>
public sealed record DeleteChartOp() : Op
{
    /// <summary>Zero-based index of the chart on the sheet. Give this or <see cref="Name"/>.</summary>
    public int? Index { get; init; }

    /// <summary>Chart name. Give this or <see cref="Index"/>.</summary>
    public string? Name { get; init; }
}
