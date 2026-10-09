using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>
/// Result of <c>aspose-cli cells inspect</c>: a compact structural summary designed
/// as the first step of the projection ladder (metadata before structure,
/// structure before values). Never contains full cell data.
/// </summary>
public sealed record WorkbookInfoResult() : EngineResultEnvelope("workbook-info", 2)
{
    /// <summary>Document kind discriminator; always <c>workbook</c> for cells.</summary>
    [JsonPropertyOrder(-50)]
    public string Kind { get; } = DocumentKinds.Workbook;

    /// <summary>The inspected file.</summary>
    [JsonPropertyOrder(-49)]
    [AlwaysPresent("fingerprint")]
    public required SourceInfo Source { get; init; }

    /// <summary>Workbook-level summary.</summary>
    public required WorkbookSummary Workbook { get; init; }
}

/// <summary>Well-known values of the <c>kind</c> discriminator.</summary>
public static class DocumentKinds
{
    public const string Workbook = "workbook";
}

/// <summary>Structural summary of a workbook.</summary>
public sealed record WorkbookSummary
{
    /// <summary>File name without directory, e.g. <c>report.xlsx</c>.</summary>
    public required string Name { get; init; }

    /// <summary>Total number of worksheets, including hidden ones.</summary>
    [Minimum(0)]
    public required int SheetCount { get; init; }

    /// <summary>Per-sheet summaries, in workbook order.</summary>
    public required IReadOnlyList<SheetInfo> Sheets { get; init; }

    /// <summary><c>true</c> when the workbook contains VBA macros.</summary>
    public required bool HasVba { get; init; }

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
    public IReadOnlyList<DefinedNameInfo>? DefinedNames { get; init; }

    /// <summary>Cells whose formula evaluates to an error; present only with <c>--detail errors</c>.</summary>
    public IReadOnlyList<CellError>? FormulaErrors { get; init; }

    /// <summary>Distinct font names used in the workbook; present only with <c>--detail fonts</c>.</summary>
    public IReadOnlyList<string>? Fonts { get; init; }

    /// <summary>Tables (list objects); present only with <c>--detail tables</c>.</summary>
    public IReadOnlyList<TableInfo>? Tables { get; init; }

    /// <summary>Charts; present only with <c>--detail charts</c>.</summary>
    public IReadOnlyList<ChartInfo>? Charts { get; init; }

    /// <summary>Pivot tables; present only with <c>--detail pivots</c>.</summary>
    public IReadOnlyList<PivotInfo>? Pivots { get; init; }

    /// <summary>Data validations; present only with <c>--detail validation</c>.</summary>
    public IReadOnlyList<ValidationInfo>? Validations { get; init; }

    /// <summary>The layout of every sheet, in workbook order; present only with <c>--detail layout</c>.</summary>
    public IReadOnlyList<SheetLayoutInfo>? Layouts { get; init; }
}

/// <summary>
/// The view and print layout of one sheet, in the terms of the operations that set it:
/// freeze_panes, group_rows, group_columns, set_autofilter, set_print_area and set_page_setup.
/// A field is omitted when the sheet does not have that setting.
/// </summary>
public sealed record SheetLayoutInfo
{
    /// <summary>The sheet's name.</summary>
    public required string Sheet { get; init; }

    /// <summary>
    /// The top-left cell of the scrolling pane when panes are frozen, as freeze_panes takes it:
    /// <c>A2</c> freezes row 1, <c>B2</c> row 1 and column A.
    /// </summary>
    public string? FreezePanes { get; init; }

    /// <summary>Row outline groups, outer levels first, each a run of rows at its level or deeper.</summary>
    public IReadOnlyList<RowGroupInfo>? RowGroups { get; init; }

    /// <summary>Column outline groups, outer levels first, each a run of columns at its level or deeper.</summary>
    public IReadOnlyList<ColumnGroupInfo>? ColumnGroups { get; init; }

    /// <summary>The range the sheet's AutoFilter covers, header row included.</summary>
    public string? AutoFilter { get; init; }

    /// <summary>The print area, such as <c>A1:H50</c>; several areas are comma-separated.</summary>
    public string? PrintArea { get; init; }

    /// <summary>The rows repeated on every printed page, such as <c>1:2</c>.</summary>
    public string? TitleRows { get; init; }

    /// <summary>The columns repeated on every printed page, such as <c>A:B</c>.</summary>
    public string? TitleColumns { get; init; }

    /// <summary>The page orientation: <c>portrait</c> or <c>landscape</c>.</summary>
    [AllowedValues(typeof(PageOrientations))]
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
public sealed record RowGroupInfo
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
public sealed record ColumnGroupInfo
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
public sealed record DefinedNameInfo
{
    /// <summary>The name.</summary>
    public required string Name { get; init; }

    /// <summary>What it refers to (a range or formula).</summary>
    public required string RefersTo { get; init; }
}

/// <summary>A cell whose formula evaluates to an error — a delivery-time QA signal.</summary>
public sealed record CellError
{
    /// <summary>Sheet the cell is on.</summary>
    public required string Sheet { get; init; }

    /// <summary>Cell address, e.g. <c>C7</c>.</summary>
    public required string Cell { get; init; }

    /// <summary>The error value, e.g. <c>#REF!</c>, <c>#DIV/0!</c>, <c>#VALUE!</c>.</summary>
    public required string Error { get; init; }

    /// <summary>
    /// <c>true</c> in edit verification when the input cell had the same formula and the same
    /// error value; omitted otherwise.
    /// </summary>
    [AllowedValues(true)]
    public bool? Preexisting { get; init; }
}

/// <summary>A table (list object) in a workbook.</summary>
public sealed record TableInfo
{
    /// <summary>Sheet the table is on.</summary>
    public required string Sheet { get; init; }

    /// <summary>The table's display name.</summary>
    public required string Name { get; init; }

    /// <summary>A1 range the table covers, e.g. <c>A1:D20</c>.</summary>
    public required string Range { get; init; }
}

/// <summary>A chart in a workbook.</summary>
public sealed record ChartInfo
{
    /// <summary>Sheet the chart is on.</summary>
    public required string Sheet { get; init; }

    /// <summary>The zero-based index of the chart on its sheet, as update_chart and delete_chart take it.</summary>
    [Minimum(0)]
    public required int Index { get; init; }

    /// <summary>The chart's name.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// The chart type: a create_chart type such as <c>column</c>, or the lower-camel engine name
    /// of a type outside that vocabulary, such as <c>columnStacked</c>.
    /// </summary>
    [AllowedValues(typeof(ChartTypes)), OpenEnum("^[a-z][A-Za-z0-9]*$")]
    public required string Type { get; init; }
}

/// <summary>A pivot table in a workbook.</summary>
public sealed record PivotInfo
{
    /// <summary>Sheet the pivot table is on.</summary>
    public required string Sheet { get; init; }

    /// <summary>The pivot table's name.</summary>
    public required string Name { get; init; }

    /// <summary>A1 range the pivot report covers.</summary>
    public required string Range { get; init; }
}

/// <summary>A data validation in a workbook.</summary>
public sealed record ValidationInfo
{
    /// <summary>Sheet the validation is on.</summary>
    public required string Sheet { get; init; }

    /// <summary>A1 range the validation applies to.</summary>
    public required string Range { get; init; }

    /// <summary>
    /// The validation type: a set_validation type such as <c>list</c> or <c>wholeNumber</c>, or
    /// the lower-camel engine name of a type outside that vocabulary, such as <c>anyValue</c>.
    /// </summary>
    [AllowedValues(typeof(ValidationTypes)), OpenEnum("^[a-z][A-Za-z0-9]*$")]
    public required string Type { get; init; }
}

/// <summary>Accepted values of <c>cells inspect --detail</c>.</summary>
public static class InfoDetails
{
    /// <summary>Defined names.</summary>
    public const string Names = "names";

    /// <summary>A formula-error scan (cells evaluating to #REF!, #DIV/0!, etc.).</summary>
    public const string Errors = "errors";

    /// <summary>The distinct fonts the workbook uses (a rendering-fidelity check).</summary>
    public const string Fonts = "fonts";

    /// <summary>Tables (list objects) in the workbook.</summary>
    public const string Tables = "tables";

    /// <summary>Charts in the workbook.</summary>
    public const string Charts = "charts";

    /// <summary>Pivot tables in the workbook.</summary>
    public const string Pivots = "pivots";

    /// <summary>Data validations in the workbook.</summary>
    public const string Validation = "validation";

    /// <summary>
    /// The layout of each sheet: frozen panes, outline groups, AutoFilter, print area and
    /// titles, orientation, scaling, header and footer.
    /// </summary>
    public const string Layout = "layout";

    /// <summary>Every detail id, in documentation order.</summary>
    public static IReadOnlyList<string> All { get; } = [Names, Errors, Fonts, Tables, Charts, Pivots, Validation, Layout];
}

/// <summary>Structural summary of one worksheet.</summary>
public sealed record SheetInfo
{
    /// <summary>Sheet name, exactly as shown in Excel.</summary>
    public required string Name { get; init; }

    /// <summary>Zero-based position in the tab order, as add_sheet and move_sheet take it.</summary>
    [Minimum(0)]
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
    /// present only when <c>--preview</c> was requested. Row-major; a null
    /// entry is an empty cell.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<string?>>? Preview { get; init; }
}
