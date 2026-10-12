using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>
/// Result of <c>aspose-cli cells inspect</c>: a compact structural summary designed
/// as the first step of the projection ladder (metadata before structure,
/// structure before values). Never contains full cell data.
/// </summary>
public sealed record CellsInspectResult() : EngineResultEnvelope("inspect-result", 2)
{
    /// <summary>The inspected file.</summary>
    [JsonPropertyOrder(-49)]
    public required InputInfo Input { get; init; }

    /// <summary>Workbook-level summary.</summary>
    public required CellsWorkbookSummary Workbook { get; init; }
}

/// <summary>Structural summary of a workbook.</summary>
public sealed record CellsWorkbookSummary
{
    /// <summary>File name without directory, e.g. <c>report.xlsx</c>.</summary>
    public required string Name { get; init; }

    /// <summary>Total number of worksheets, including hidden ones.</summary>
    [Minimum(0)]
    public required int SheetCount { get; init; }

    /// <summary>Per-sheet summaries, in workbook order.</summary>
    public required IReadOnlyList<CellsSheetInfo> Sheets { get; init; }

    /// <summary><c>true</c> when the workbook contains VBA macros.</summary>
    public required bool HasMacros { get; init; }

    /// <summary>Number of defined names in the workbook.</summary>
    [Minimum(0)]
    public required int DefinedNameCount { get; init; }

    /// <summary>
    /// <c>true</c> when the workbook structure is protected: Excel then refuses to add, delete,
    /// rename, move or hide sheets. Edits are not held to it; see PROTECTION_NOT_ENFORCED.
    /// </summary>
    public required bool StructureProtected { get; init; }

    /// <summary><c>true</c> when removing the structure protection needs a password.</summary>
    public required bool StructurePasswordProtected { get; init; }

    /// <summary>Document author from built-in properties; omitted when empty.</summary>
    public string? Author { get; init; }

    /// <summary>Document title from built-in properties; omitted when empty.</summary>
    public string? Title { get; init; }

    /// <summary>Defined names; present only with <c>--detail names</c>.</summary>
    public IReadOnlyList<CellsDefinedNameInfo>? DefinedNames { get; init; }

    /// <summary>Cells whose formula evaluates to an error; present only with <c>--detail errors</c>.</summary>
    public IReadOnlyList<CellsCellError>? FormulaErrors { get; init; }

    /// <summary>Distinct font names used in the workbook; present only with <c>--detail fonts</c>.</summary>
    public IReadOnlyList<string>? Fonts { get; init; }

    /// <summary>Tables (list objects); present only with <c>--detail tables</c>.</summary>
    public IReadOnlyList<CellsTableInfo>? Tables { get; init; }

    /// <summary>Charts; present only with <c>--detail charts</c>.</summary>
    public IReadOnlyList<CellsChartInfo>? Charts { get; init; }

    /// <summary>Pivot tables; present only with <c>--detail pivots</c>.</summary>
    public IReadOnlyList<CellsPivotInfo>? Pivots { get; init; }

    /// <summary>Data validations; present only with <c>--detail validation</c>.</summary>
    public IReadOnlyList<CellsValidationInfo>? Validations { get; init; }

    /// <summary>The layout of every sheet, in workbook order; present only with <c>--detail layout</c>.</summary>
    public IReadOnlyList<CellsSheetLayoutInfo>? Layouts { get; init; }
}

/// <summary>Structural summary of one worksheet.</summary>
public sealed record CellsSheetInfo
{
    /// <summary>Sheet name, exactly as shown in Excel.</summary>
    public required string Name { get; init; }

    /// <summary>1-based position in the tab order, as add_sheet and move_sheet take it.</summary>
    [Minimum(1)]
    public required int Position { get; init; }

    /// <summary>
    /// A1 range covering all cells that hold data (e.g. <c>A1:G120</c>);
    /// omitted for empty sheets.
    /// </summary>
    public string? UsedRange { get; init; }

    /// <summary>Number of data rows inside <c>usedRange</c>.</summary>
    [Minimum(0)]
    public required int RowCount { get; init; }

    /// <summary>Number of data columns inside <c>usedRange</c>.</summary>
    [Minimum(0)]
    public required int ColumnCount { get; init; }

    /// <summary><c>true</c> when the sheet is hidden.</summary>
    public required bool Hidden { get; init; }

    /// <summary>
    /// <c>true</c> when the sheet is protected: Excel then lets users change only unlocked cells
    /// and the actions the protection allows. Edits are not held to it; see PROTECTION_NOT_ENFORCED.
    /// </summary>
    public required bool Protected { get; init; }

    /// <summary><c>true</c> when removing the sheet's protection needs a password.</summary>
    public required bool PasswordProtected { get; init; }

    /// <summary>Number of charts on the sheet.</summary>
    [Minimum(0)]
    public required int ChartCount { get; init; }

    /// <summary>Number of pivot tables on the sheet.</summary>
    [Minimum(0)]
    public required int PivotTableCount { get; init; }

    /// <summary>
    /// Optional sample of display values (first rows of the used range),
    /// present only when <c>--sample</c> was requested. Row-major; a null
    /// entry is an empty cell.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<string?>>? Preview { get; init; }

    /// <summary>The most columns a <see cref="Preview"/> row holds.</summary>
    public const int PreviewColumns = 20;
}

/// <summary>
/// The view and print layout of one sheet, in the terms of the operations that set it:
/// freeze_panes, group_rows, group_columns, set_autofilter, set_print_area and set_page_setup.
/// A field is omitted when the sheet does not have that setting.
/// </summary>
public sealed record CellsSheetLayoutInfo
{
    /// <summary>The sheet's name.</summary>
    public required string Sheet { get; init; }

    /// <summary>
    /// The top-left cell of the scrolling pane when panes are frozen, as freeze_panes takes it:
    /// <c>A2</c> freezes row 1, <c>B2</c> row 1 and column A.
    /// </summary>
    public string? FreezePanes { get; init; }

    /// <summary>Row outline groups, outer levels first, each a run of rows at its level or deeper.</summary>
    public IReadOnlyList<CellsRowGroupInfo>? RowGroups { get; init; }

    /// <summary>Column outline groups, outer levels first, each a run of columns at its level or deeper.</summary>
    public IReadOnlyList<CellsColumnGroupInfo>? ColumnGroups { get; init; }

    /// <summary>The range the sheet's AutoFilter covers, header row included.</summary>
    public string? AutoFilter { get; init; }

    /// <summary>The print area, such as <c>A1:H50</c>; several areas are comma-separated.</summary>
    public string? PrintArea { get; init; }

    /// <summary>The rows repeated on every printed page, such as <c>1:2</c>.</summary>
    public string? TitleRows { get; init; }

    /// <summary>The columns repeated on every printed page, such as <c>A:B</c>.</summary>
    public string? TitleColumns { get; init; }

    /// <summary>The page orientation: <c>portrait</c> or <c>landscape</c>.</summary>
    [AllowedValues(typeof(CellsPageOrientations))]
    public required string Orientation { get; init; }

    /// <summary>The number of pages the printout fits across, 0 automatic; present when it fits to pages.</summary>
    [Minimum(0)]
    public int? FitToWidth { get; init; }

    /// <summary>The number of pages the printout fits down, 0 automatic; present when it fits to pages.</summary>
    [Minimum(0)]
    public int? FitToHeight { get; init; }

    /// <summary>The zoom percentage; present when the printout does not fit to pages.</summary>
    public int? Scale { get; init; }

    /// <summary>The center header text, with Excel codes such as &amp;P.</summary>
    public string? Header { get; init; }

    /// <summary>The center footer text, with Excel codes such as &amp;P and &amp;N.</summary>
    public string? Footer { get; init; }
}

/// <summary>A row outline group.</summary>
public sealed record CellsRowGroupInfo
{
    /// <summary>The first row of the group (1-based).</summary>
    [Minimum(1)]
    public required int From { get; init; }

    /// <summary>The last row of the group (1-based).</summary>
    [Minimum(1)]
    public required int To { get; init; }

    /// <summary>The outline level, 1 for the outermost group.</summary>
    [Minimum(1)]
    public required int Level { get; init; }

    /// <summary><c>true</c> when every row of the group is hidden, as a collapsed group's rows are.</summary>
    public required bool Collapsed { get; init; }
}

/// <summary>A column outline group.</summary>
public sealed record CellsColumnGroupInfo
{
    /// <summary>The first column of the group.</summary>
    public required string From { get; init; }

    /// <summary>The last column of the group.</summary>
    public required string To { get; init; }

    /// <summary>The outline level, 1 for the outermost group.</summary>
    [Minimum(1)]
    public required int Level { get; init; }

    /// <summary><c>true</c> when every column of the group is hidden, as a collapsed group's columns are.</summary>
    public required bool Collapsed { get; init; }
}

/// <summary>A workbook-scoped defined name.</summary>
public sealed record CellsDefinedNameInfo
{
    /// <summary>The name.</summary>
    public required string Name { get; init; }

    /// <summary>What it refers to (a range or formula).</summary>
    public required string RefersTo { get; init; }
}

/// <summary>A table (list object) in a workbook.</summary>
public sealed record CellsTableInfo
{
    /// <summary>Sheet the table is on.</summary>
    public required string Sheet { get; init; }

    /// <summary>The table's display name.</summary>
    public required string Name { get; init; }

    /// <summary>A1 range the table covers, e.g. <c>A1:D20</c>.</summary>
    public required string Range { get; init; }
}

/// <summary>A chart in a workbook.</summary>
public sealed record CellsChartInfo
{
    /// <summary>Sheet the chart is on.</summary>
    public required string Sheet { get; init; }

    /// <summary>The 1-based index of the chart on its sheet, as update_chart and delete_chart take it.</summary>
    [Minimum(1)]
    public required int Index { get; init; }

    /// <summary>The chart's name.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// The chart kind: a create_chart type such as <c>column</c>, or the lower-camel engine name
    /// of a type outside that vocabulary, such as <c>columnStacked</c>.
    /// </summary>
    [AllowedValues(typeof(CellsChartTypes)), OpenEnum("^[a-z][A-Za-z0-9]*$")]
    public required string Kind { get; init; }
}

/// <summary>A pivot table in a workbook.</summary>
public sealed record CellsPivotInfo
{
    /// <summary>Sheet the pivot table is on.</summary>
    public required string Sheet { get; init; }

    /// <summary>The pivot table's name.</summary>
    public required string Name { get; init; }

    /// <summary>A1 range the pivot report covers.</summary>
    public required string Range { get; init; }
}

/// <summary>A data validation in a workbook.</summary>
public sealed record CellsValidationInfo
{
    /// <summary>Sheet the validation is on.</summary>
    public required string Sheet { get; init; }

    /// <summary>A1 range the validation applies to.</summary>
    public required string Range { get; init; }

    /// <summary>
    /// The validation kind: a set_validation type such as <c>list</c> or <c>wholeNumber</c>, or
    /// the lower-camel engine name of a type outside that vocabulary, such as <c>anyValue</c>.
    /// </summary>
    [AllowedValues(typeof(CellsValidationTypes)), OpenEnum("^[a-z][A-Za-z0-9]*$")]
    public required string Kind { get; init; }
}
