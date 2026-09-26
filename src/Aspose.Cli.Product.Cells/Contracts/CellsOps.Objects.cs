using System.Globalization;
using System.Text;
using Aspose.Cli.Sdk.Operations;

namespace Aspose.Cli.Product.Cells.Contracts;

// Ops that add embedded objects (charts, pivot tables, pictures, tables) to a sheet.

/// <summary>
/// Adds a native chart that plots a data range. A pie chart has no axes and rejects
/// axisTitles; legend, axisTitles and dataLabels each set at least one field.
/// </summary>
[Operation("create_chart")]
public sealed record CreateChartOp : CellsOp
{
    [AllowedValues(typeof(ChartTypes))] public required string Type { get; init; }

    /// <summary>
    /// The data to plot, including headers, such as A1:C5. It may name another sheet, such as
    /// Data!A1:C5, so a chart on a dashboard sheet can plot a data sheet.
    /// </summary>
    [A1Reference] public required string DataRange { get; init; }

    /// <summary>The range of at least two cells the chart is placed over, such as E2:L18.</summary>
    [A1Range] public required string At { get; init; }

    public string? Title { get; init; }

    /// <summary>Whether series are plotted from rows rather than columns.</summary>
    public bool SeriesInRows { get; init; }

    public ChartLegend? Legend { get; init; }

    public ChartAxisTitles? AxisTitles { get; init; }

    /// <summary>
    /// Series colors in series order; a pie's slices count as the series. Colors beyond the
    /// series count are ignored.
    /// </summary>
    [MinItems(1), HexColor] public IReadOnlyList<string>? SeriesColors { get; init; }

    public ChartDataLabels? DataLabels { get; init; }

    /// <inheritdoc />
    protected override BoundedOperation Validated()
    {
        OperationInvalidException.Require(A1.ParseRange(At).Range.CellCount > 1,
            "'at' must be a range the chart is placed over, e.g. \"E2:L18\"");
        RequireAxesForTitles(Type, AxisTitles);
        return this;
    }

    /// <summary>
    /// Rejects axis titles on a pie chart, which has no axes. update_chart checks it when the
    /// operation names the type; otherwise the engine, which learns the real type, enforces it.
    /// </summary>
    internal static void RequireAxesForTitles(string? type, ChartAxisTitles? axisTitles) =>
        OperationInvalidException.Require(type != ChartTypes.Pie || axisTitles is null, "a 'pie' chart has no axes; omit 'axisTitles'");
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
}

/// <summary>
/// The legend of a chart; only the fields present are applied. There is no none position: hide
/// the legend with visible false.
/// </summary>
[MinProperties(1)]
public sealed record ChartLegend
{
    public bool? Visible { get; init; }

    [AllowedValues(typeof(LegendPositions))] public string? Position { get; init; }
}

/// <summary>Accepted values of <see cref="ChartLegend.Position"/>.</summary>
public static class LegendPositions
{
    public const string Right = "right";
    public const string Bottom = "bottom";
    public const string Top = "top";
    public const string Left = "left";
}

/// <summary>The axis titles of a chart; only the fields present are applied. Pie charts have no axes.</summary>
[MinProperties(1)]
public sealed record ChartAxisTitles
{
    /// <summary>The title of the category (X) axis.</summary>
    public string? Category { get; init; }

    /// <summary>The title of the value (Y) axis.</summary>
    public string? Value { get; init; }
}

/// <summary>The data labels of a chart's series; only the fields present are applied.</summary>
[MinProperties(1)]
public sealed record ChartDataLabels
{
    /// <summary>Whether each point's value shows next to it.</summary>
    public bool? Visible { get; init; }

    /// <summary>The number format code of the labels, such as #,##0.</summary>
    public string? Format { get; init; }
}

/// <summary>Adds a pivot table that summarizes a source range.</summary>
[Operation("create_pivot")]
public sealed record CreatePivotOp : CellsOp
{
    /// <summary>The source data including headers; it may name another sheet, such as Data!A1:D100.</summary>
    [A1Reference] public required string SourceRange { get; init; }

    /// <summary>The top-left cell of the pivot table.</summary>
    [A1Cell] public required string At { get; init; }

    /// <summary>The pivot table name; generated when omitted.</summary>
    public string? Name { get; init; }

    /// <summary>The header names of the row fields.</summary>
    [MinItems(1)] public IReadOnlyList<string>? Rows { get; init; }

    /// <summary>The header names of the column fields.</summary>
    [MinItems(1)] public IReadOnlyList<string>? Columns { get; init; }

    /// <summary>The value fields with their aggregation.</summary>
    [MinItems(1)] public required IReadOnlyList<PivotValueField> Values { get; init; }
}

/// <summary>One aggregated value field of a pivot table.</summary>
public sealed record PivotValueField
{
    /// <summary>The header name of the source column.</summary>
    [Pattern(@"\S")] public required string Field { get; init; }

    [AllowedValues(typeof(PivotFunctions))] public string Function { get; init; } = PivotFunctions.Sum;

    /// <summary>The number format code of the field, such as #,##0.</summary>
    [Pattern(@"\S")] public string? NumberFormat { get; init; }
}

/// <summary>Accepted values of <see cref="PivotValueField.Function"/>.</summary>
public static class PivotFunctions
{
    public const string Sum = "sum";
    public const string Count = "count";
    public const string Average = "average";
    public const string Max = "max";
    public const string Min = "min";
}

/// <summary>Inserts a picture from a file, anchored at a cell.</summary>
[Operation("insert_image")]
public sealed record InsertImageOp : CellsOp
{
    /// <summary>The image file (png, jpeg, gif or bmp), relative to the working directory.</summary>
    [InputPath, Pattern(@"\S")] public required string Path { get; init; }

    /// <summary>The top-left anchor cell, such as E2.</summary>
    [A1Cell] public required string At { get; init; }

    /// <summary>The width in pixels; the image's natural width when omitted.</summary>
    [Minimum(1)] public int? Width { get; init; }

    /// <summary>The height in pixels; the image's natural height when omitted.</summary>
    [Minimum(1)] public int? Height { get; init; }
}

/// <summary>
/// Recalculates pivot tables so they reflect changed source data; pivots do not refresh on
/// their own.
/// </summary>
[Operation("refresh_pivot")]
public sealed record RefreshPivotOp : CellsOp
{
    /// <summary>The pivot table name; every pivot table on the sheet when omitted.</summary>
    public string? Name { get; init; }
}

/// <summary>Turns a range into a native table with filtering, an optional style and an optional totals row.</summary>
[Operation("create_table")]
public sealed record CreateTableOp : CellsOp
{
    /// <summary>The range to convert, including its header row, such as A1:D20.</summary>
    [A1Range] public required string Range { get; init; }

    /// <summary>
    /// The table name, unique among the workbook's tables and defined names: a letter, '_' or
    /// '\' first, then letters, digits, '.', '_', '\' or '?'; never text that reads as a cell
    /// reference such as T1 or R1C1. Generated when omitted.
    /// </summary>
    [MinLength(1), MaxLength(255)] public string? Name { get; init; }

    /// <summary>
    /// A built-in table style (TableStyleLight1-21, TableStyleMedium1-28, TableStyleDark1-11),
    /// such as TableStyleMedium2, or a custom table style the workbook defines.
    /// </summary>
    [MinLength(1)] public string? Style { get; init; }

    /// <summary>Whether a totals row is added below the table.</summary>
    public bool TotalsRow { get; init; }

    /// <inheritdoc />
    protected override BoundedOperation Validated()
    {
        if (Name is { } name)
        {
            const string Hint = "Use a descriptive name such as SalesTable or tbl_Sales.";
            Rune[] runes = [.. name.EnumerateRunes()];
            OperationInvalidException.Require(
                (Rune.IsLetter(runes[0]) || runes[0].Value is '_' or '\\')
                    && runes.Skip(1).All(static rune => Rune.IsLetterOrDigit(rune) || IsCombiningMark(rune) || rune.Value is '.' or '_' or '\\' or '?'),
                $"table name '{name}' must start with a letter, '_' or '\\' and continue with letters, digits, '.', '_', '\\' or '?'",
                Hint);
            OperationInvalidException.Require(!ReadsAsCellReference(name), $"table name '{name}' reads as a cell reference", Hint);
        }

        return this;

        static bool IsCombiningMark(Rune rune) =>
            Rune.GetUnicodeCategory(rune) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark;
    }

    private static bool ReadsAsCellReference(string name)
    {
        // R1C1: R or C alone, R<n>, C<n>, R<n>C<n> and their row- or column-only forms.
        int index = 0;
        bool row = TryTake(name, ref index, 'R');
        bool column = TryTake(name, ref index, 'C');
        if ((row || column) && index == name.Length)
        {
            return true;
        }

        // A1: one to three column letters within XFD, then a row number within the sheet.
        int letters = name.TakeWhile(char.IsAsciiLetter).Count();
        string digits = name[letters..];
        return letters is >= 1 and <= 3
            && digits.Length is >= 1 and <= 7
            && digits.All(char.IsAsciiDigit)
            && name[..letters].Aggregate(0, static (column, letter) => column * 26 + char.ToUpperInvariant(letter) - 'A' + 1) <= A1.MaxColumns
            && int.Parse(digits, CultureInfo.InvariantCulture) is >= 1 and <= A1.MaxRows;

        static bool TryTake(string text, ref int position, char marker)
        {
            if (position >= text.Length || char.ToUpperInvariant(text[position]) != marker)
            {
                return false;
            }

            position++;
            while (position < text.Length && char.IsAsciiDigit(text[position]))
            {
                position++;
            }

            return true;
        }
    }
}

/// <summary>
/// Updates an existing chart, identified by exactly one of index and name, with at least one of
/// title, dataRange, type, legend, axisTitles, seriesColors and dataLabels. seriesInRows applies
/// only together with dataRange; legend, axisTitles and dataLabels each set at least one field.
/// </summary>
[Operation("update_chart")]
[ExactlyOneOf("index", "name")]
[AtLeastOneOf("title", "dataRange", "type", "legend", "axisTitles", "seriesColors", "dataLabels")]
[DependentRequired("seriesInRows", "dataRange")]
public sealed record UpdateChartOp : CellsOp
{
    /// <summary>The zero-based index of the chart on the sheet.</summary>
    [Minimum(0)] public int? Index { get; init; }

    /// <summary>The chart name.</summary>
    [Pattern(@"\S")] public string? Name { get; init; }

    public string? Title { get; init; }

    /// <summary>The new data to plot, including headers; it may name another sheet, such as Data!A1:C5.</summary>
    [A1Reference] public string? DataRange { get; init; }

    [AllowedValues(typeof(ChartTypes))] public string? Type { get; init; }

    /// <summary>Whether series are plotted from rows rather than columns.</summary>
    public bool? SeriesInRows { get; init; }

    public ChartLegend? Legend { get; init; }

    public ChartAxisTitles? AxisTitles { get; init; }

    /// <summary>
    /// Series colors in series order; a pie's slices count as the series. Colors beyond the
    /// series count are ignored.
    /// </summary>
    [MinItems(1), HexColor] public IReadOnlyList<string>? SeriesColors { get; init; }

    public ChartDataLabels? DataLabels { get; init; }

    /// <inheritdoc />
    protected override BoundedOperation Validated()
    {
        CreateChartOp.RequireAxesForTitles(Type, AxisTitles);
        return this;
    }
}

/// <summary>Removes a chart from a sheet, identified by exactly one of index and name.</summary>
[Operation("delete_chart")]
[ExactlyOneOf("index", "name")]
public sealed record DeleteChartOp : CellsOp
{
    /// <summary>The zero-based index of the chart on the sheet.</summary>
    [Minimum(0)] public int? Index { get; init; }

    /// <summary>The chart name.</summary>
    [Pattern(@"\S")] public string? Name { get; init; }
}
