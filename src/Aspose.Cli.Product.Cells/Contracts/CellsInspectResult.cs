using System.Text.Json.Serialization;
using Aspose.Cli.Sdk.Operations;

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

    /// <summary>The workbook default font, as set_default_font sets it.</summary>
    public required CellsDefaultFontInfo DefaultFont { get; init; }

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

    /// <summary>Conditional formats, one per rule; present only with <c>--detail conditional-formats</c>.</summary>
    public IReadOnlyList<CellsConditionalFormatInfo>? ConditionalFormats { get; init; }

    /// <summary>Sparkline groups; present only with <c>--detail sparklines</c>.</summary>
    public IReadOnlyList<CellsSparklineInfo>? Sparklines { get; init; }

    /// <summary>Pictures; present only with <c>--detail images</c>.</summary>
    public IReadOnlyList<CellsImageInfo>? Images { get; init; }

    /// <summary>Cell comments (notes); present only with <c>--detail comments</c>.</summary>
    public IReadOnlyList<CellsCommentInfo>? Comments { get; init; }

    /// <summary>Hyperlinks; present only with <c>--detail hyperlinks</c>.</summary>
    public IReadOnlyList<CellsHyperlinkInfo>? Hyperlinks { get; init; }

    /// <summary>The layout of every sheet, in workbook order; present only with <c>--detail layout</c>.</summary>
    public IReadOnlyList<CellsSheetLayoutInfo>? Layouts { get; init; }
}

/// <summary>The workbook default (Normal style) font.</summary>
public sealed record CellsDefaultFontInfo
{
    /// <summary>The font family name.</summary>
    public required string Font { get; init; }

    /// <summary>The font size in points.</summary>
    public required double FontSize { get; init; }
}

/// <summary>Structural summary of one worksheet.</summary>
public sealed record CellsSheetInfo
{
    /// <summary>Sheet name, exactly as shown in Excel.</summary>
    public required string Name { get; init; }

    /// <summary>1-based position in the tab order, as insert_sheet and move_sheet take it.</summary>
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
/// The view and print layout of one sheet, read as the objects the operations that set it name:
/// set_sheet_view, set_tab_color, freeze_panes, resize_rows, resize_columns, group_rows,
/// group_columns, set_autofilter, protect_sheet, set_print_area and set_page_setup, each with the
/// fields of its operation. An object is omitted when the sheet does not have it.
/// </summary>
public sealed record CellsSheetLayoutInfo
{
    /// <summary>The sheet's name.</summary>
    public required string Sheet { get; init; }

    /// <summary>The on-screen view, as set_sheet_view sets it.</summary>
    public required CellsSheetViewInfo SheetView { get; init; }

    /// <summary>The tab color, as set_tab_color sets it.</summary>
    public CellsTabColorInfo? TabColor { get; init; }

    /// <summary>The frozen panes, as freeze_panes sets them.</summary>
    public CellsFreezePanesInfo? FreezePanes { get; init; }

    /// <summary>Runs of adjacent rows of one height set apart from the content, as resize_rows sets them.</summary>
    public IReadOnlyList<CellsRowHeightInfo>? RowHeights { get; init; }

    /// <summary>Runs of adjacent columns of one width other than the sheet's standard width, as resize_columns sets them.</summary>
    public IReadOnlyList<CellsColumnWidthInfo>? ColumnWidths { get; init; }

    /// <summary>Row outline groups, outer levels first, each a run of rows at its level or deeper.</summary>
    public IReadOnlyList<CellsRowGroupInfo>? RowGroups { get; init; }

    /// <summary>Column outline groups, outer levels first, each a run of columns at its level or deeper.</summary>
    public IReadOnlyList<CellsColumnGroupInfo>? ColumnGroups { get; init; }

    /// <summary>The sheet's AutoFilter, as set_autofilter sets it.</summary>
    public CellsAutofilterInfo? Autofilter { get; init; }

    /// <summary>The sheet's protection, as protect_sheet sets it; omitted when the sheet is not protected.</summary>
    public CellsSheetProtectionInfo? Protection { get; init; }

    /// <summary>The print area and print titles, as set_print_area sets them.</summary>
    public CellsPrintAreaInfo? PrintArea { get; init; }

    /// <summary>The page setup, as set_page_setup sets it.</summary>
    public required CellsPageSetupInfo PageSetup { get; init; }
}

/// <summary>A sheet's on-screen view.</summary>
public sealed record CellsSheetViewInfo
{
    /// <summary>Whether the on-screen gridlines show.</summary>
    public required bool Gridlines { get; init; }

    /// <summary>The zoom percentage.</summary>
    public required int Zoom { get; init; }

    /// <summary>Whether the row numbers and column letters show.</summary>
    public required bool Headings { get; init; }
}

/// <summary>A sheet's tab color.</summary>
public sealed record CellsTabColorInfo
{
    /// <summary>The tab color.</summary>
    [HexColor] public required string Color { get; init; }
}

/// <summary>A run of adjacent rows of one height.</summary>
public sealed record CellsRowHeightInfo
{
    /// <summary>The first row of the run (1-based).</summary>
    [Minimum(1)]
    public required int First { get; init; }

    /// <summary>The last row of the run (1-based).</summary>
    [Minimum(1)]
    public required int Last { get; init; }

    /// <summary>The height in points.</summary>
    [Minimum(0)]
    public required double Height { get; init; }
}

/// <summary>A run of adjacent columns of one width.</summary>
public sealed record CellsColumnWidthInfo
{
    /// <summary>The first column of the run.</summary>
    public required string First { get; init; }

    /// <summary>The last column of the run.</summary>
    public required string Last { get; init; }

    /// <summary>The width in characters of the default font's digit 0.</summary>
    [Minimum(0)]
    public required double WidthChars { get; init; }
}

/// <summary>A protected sheet's protection.</summary>
public sealed record CellsSheetProtectionInfo
{
    /// <summary>The actions users may still perform, in the order protect_sheet lists them.</summary>
    [AllowedValues(typeof(CellsProtectActions))]
    public required IReadOnlyList<string> Allow { get; init; }
}

/// <summary>A sheet's frozen panes.</summary>
public sealed record CellsFreezePanesInfo
{
    /// <summary>The top-left cell of the scrolling pane: <c>A2</c> freezes row 1, <c>B2</c> row 1 and column A.</summary>
    public required string Cell { get; init; }
}

/// <summary>A sheet's AutoFilter.</summary>
public sealed record CellsAutofilterInfo
{
    /// <summary>The range the AutoFilter covers, header row included.</summary>
    public required string Range { get; init; }
}

/// <summary>A sheet's print area and the rows and columns repeated on every printed page.</summary>
public sealed record CellsPrintAreaInfo
{
    /// <summary>The print area, such as <c>A1:H50</c>; several areas are comma-separated. Omitted when only titles are set.</summary>
    public string? Range { get; init; }

    /// <summary>The rows repeated on every printed page, such as <c>$1:$2</c>.</summary>
    public string? TitleRows { get; init; }

    /// <summary>The columns repeated on every printed page, such as <c>$A:$B</c>.</summary>
    public string? TitleColumns { get; init; }
}

/// <summary>A sheet's page setup for printing and PDF export.</summary>
public sealed record CellsPageSetupInfo
{
    /// <summary>The page orientation: <c>portrait</c> or <c>landscape</c>.</summary>
    [AllowedValues(typeof(CellsPageOrientations))]
    public required string Orientation { get; init; }

    /// <summary>The page size as the page is laid out: in landscape the width is the longer side, so it writes back unchanged with the orientation.</summary>
    public required PageSizeInfo PageSize { get; init; }

    /// <summary>The zoom percentage; present when the printout does not fit to pages.</summary>
    public int? ScalePercent { get; init; }

    /// <summary>The number of pages the printout fits across, 0 automatic; present when it fits to pages.</summary>
    [Minimum(0)]
    public int? FitToWidth { get; init; }

    /// <summary>The number of pages the printout fits down, 0 automatic; present when it fits to pages.</summary>
    [Minimum(0)]
    public int? FitToHeight { get; init; }

    /// <summary>The page margins in points, every side.</summary>
    public required CellsMargins Margins { get; init; }

    /// <summary>The center header text, with Excel codes such as &amp;P; omitted when empty.</summary>
    public string? Header { get; init; }

    /// <summary>The left header text; omitted when empty.</summary>
    public string? HeaderLeft { get; init; }

    /// <summary>The right header text; omitted when empty.</summary>
    public string? HeaderRight { get; init; }

    /// <summary>The center footer text, with Excel codes such as &amp;P and &amp;N; omitted when empty.</summary>
    public string? Footer { get; init; }

    /// <summary>The left footer text; omitted when empty.</summary>
    public string? FooterLeft { get; init; }

    /// <summary>The right footer text; omitted when empty.</summary>
    public string? FooterRight { get; init; }
}

/// <summary>A row outline group.</summary>
public sealed record CellsRowGroupInfo
{
    /// <summary>The first row of the group (1-based).</summary>
    [Minimum(1)]
    public required int First { get; init; }

    /// <summary>The last row of the group (1-based).</summary>
    [Minimum(1)]
    public required int Last { get; init; }

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
    public required string First { get; init; }

    /// <summary>The last column of the group.</summary>
    public required string Last { get; init; }

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

    /// <summary>The table style, such as <c>TableStyleMedium2</c>; omitted when the table has none.</summary>
    public string? TableStyle { get; init; }

    /// <summary>Whether the table shows a totals row.</summary>
    public required bool TotalsRow { get; init; }
}

/// <summary>A chart in a workbook.</summary>
public sealed record CellsChartInfo
{
    /// <summary>Sheet the chart is on.</summary>
    public required string Sheet { get; init; }

    /// <summary>The 1-based index of the chart on its sheet, as set_chart and delete_chart take it.</summary>
    [Minimum(1)]
    public required int Index { get; init; }

    /// <summary>The chart's name.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// The chart kind: a insert_chart type such as <c>column</c>, or the lower-camel engine name
    /// of a type outside that vocabulary, such as <c>columnStacked</c>.
    /// </summary>
    [AllowedValues(typeof(CellsChartTypes)), OpenEnum("^[a-z][A-Za-z0-9]*$")]
    public required string Kind { get; init; }

    /// <summary>The chart title; omitted when the chart shows none or an automatic one.</summary>
    public string? Title { get; init; }

    /// <summary>
    /// The range the series plot, headers included, such as <c>A1:C5</c>: the smallest range
    /// covering every series' values, categories and name. It names the sheet only when that is
    /// not the chart's own. Omitted when no series plots cells.
    /// </summary>
    public string? DataRange { get; init; }

    /// <summary>Whether the series are plotted from rows rather than columns.</summary>
    public required bool SeriesInRows { get; init; }

    /// <summary>The legend: whether it shows, and its position when it is one of the four sides.</summary>
    public required CellsChartLegend Legend { get; init; }

    /// <summary>The axis titles; omitted when the chart has no axes or no axis title.</summary>
    public CellsChartAxisTitles? AxisTitles { get; init; }

    /// <summary>The color of each series in series order; a pie's slices count as the series. Omitted when the chart has no series.</summary>
    [HexColor] public IReadOnlyList<string>? SeriesColors { get; init; }

    /// <summary>The data labels of the first series; omitted when the chart has no series.</summary>
    public CellsChartDataLabels? DataLabels { get; init; }
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

    /// <summary>The source data, headers included; it names the sheet only when that is not the pivot's own.</summary>
    public string? SourceRange { get; init; }

    /// <summary>The header names of the row fields; omitted when there are none.</summary>
    public IReadOnlyList<string>? Rows { get; init; }

    /// <summary>The header names of the column fields; omitted when there are none.</summary>
    public IReadOnlyList<string>? Columns { get; init; }

    /// <summary>The value fields with their aggregation and caption; omitted when there are none.</summary>
    public IReadOnlyList<CellsPivotValueInfo>? Values { get; init; }

    /// <summary>The caption language: <c>zh</c> when the headers carry Excel's Simplified Chinese captions, otherwise <c>en</c>.</summary>
    [AllowedValues(typeof(CellsPivotCaptionLanguages))]
    public required string Captions { get; init; }
}

/// <summary>A data validation in a workbook.</summary>
public sealed record CellsValidationInfo
{
    /// <summary>Sheet the validation is on.</summary>
    public required string Sheet { get; init; }

    /// <summary>A1 range the validation applies to.</summary>
    public required string Range { get; init; }

    /// <summary>
    /// The validation kind: a set_validation kind such as <c>list</c> or <c>whole-number</c>, or
    /// the lower-kebab engine name of a kind outside that vocabulary, such as <c>any-value</c>.
    /// </summary>
    [AllowedValues(typeof(CellsValidationTypes)), OpenEnum("^[a-z][a-z0-9]*(-[a-z0-9]+)*$")]
    public required string Kind { get; init; }

    /// <summary>The comparison of a kind that compares; omitted for list and custom.</summary>
    [AllowedValues(typeof(CellsValidationOperators))]
    public string? Operator { get; init; }

    /// <summary>
    /// The first bound (the lower bound for between), or the formula of a custom validation; a
    /// number reads without the leading = Excel stores. Omitted for a list.
    /// </summary>
    public string? Value1 { get; init; }

    /// <summary>The upper bound for between and not-between, read as value1 is.</summary>
    public string? Value2 { get; init; }

    /// <summary>The dropdown items of a list given as items.</summary>
    public IReadOnlyList<string>? ListItems { get; init; }

    /// <summary>The range a list reads its items from.</summary>
    public string? ListSource { get; init; }

    /// <summary>The prompt shown when a validated cell is selected; omitted when none.</summary>
    public string? InputMessage { get; init; }

    /// <summary>The message shown when a value is rejected; omitted when none.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Whether a validated cell may be left blank.</summary>
    public required bool AllowBlank { get; init; }
}

/// <summary>One value field of a pivot table, in insert_pivot's fields.</summary>
public sealed record CellsPivotValueInfo
{
    /// <summary>The header name of the source column.</summary>
    public required string Field { get; init; }

    /// <summary>
    /// The aggregation: an insert_pivot function such as <c>sum</c>, or the lower-camel engine
    /// name of one outside that vocabulary, such as <c>stdDev</c>.
    /// </summary>
    [AllowedValues(typeof(CellsPivotFunctions)), OpenEnum("^[a-z][A-Za-z0-9]*$")]
    public required string Function { get; init; }

    /// <summary>The number format code of the field; omitted when it has none.</summary>
    public string? NumberFormat { get; init; }

    /// <summary>The caption of the value field.</summary>
    public required string Label { get; init; }
}

/// <summary>One conditional-formatting rule, one entry per rule in the order the sheet holds them.</summary>
public sealed record CellsConditionalFormatInfo
{
    /// <summary>Sheet the rule is on.</summary>
    public required string Sheet { get; init; }

    /// <summary>The A1 range the rule formats; several areas are comma-separated.</summary>
    public required string Range { get; init; }

    /// <summary>The rule, in add_conditional_format's fields; a number reads without the leading = Excel stores.</summary>
    public required CellsConditionalRuleInfo Rule { get; init; }

    /// <summary>The style matching cells receive; omitted for the kinds that take none.</summary>
    public CellsConditionalStyle? Style { get; init; }
}

/// <summary>A conditional-formatting rule as a read reports it: add_conditional_format's fields the rule's kind uses.</summary>
public sealed record CellsConditionalRuleInfo
{
    /// <summary>
    /// The rule kind: an add_conditional_format kind such as <c>cell-value</c>, or the lower-kebab
    /// engine name of a kind outside that vocabulary, such as <c>contains-text</c>, whose other
    /// fields are not read.
    /// </summary>
    [AllowedValues(typeof(CellsConditionalRuleKinds)), OpenEnum("^[a-z][a-z0-9]*(-[a-z0-9]+)*$")]
    public required string Kind { get; init; }

    /// <summary>The comparison of a cell-value rule.</summary>
    [AllowedValues(typeof(CellsValidationOperators))] public string? Operator { get; init; }

    /// <summary>The comparison value of a cell-value rule (the lower bound for between), or the formula of a formula rule.</summary>
    public string? Value1 { get; init; }

    /// <summary>The upper bound of a between or not-between cell-value rule.</summary>
    public string? Value2 { get; init; }

    /// <summary>The low-end color of a color-scale.</summary>
    [HexColor] public string? MinColor { get; init; }

    /// <summary>The middle color of a 3-point color-scale.</summary>
    [HexColor] public string? MidColor { get; init; }

    /// <summary>The high-end color of a color-scale.</summary>
    [HexColor] public string? MaxColor { get; init; }

    /// <summary>The bar color of a data-bar.</summary>
    [HexColor] public string? BarColor { get; init; }

    /// <summary>The N of a top-bottom rule.</summary>
    public int? Rank { get; init; }

    /// <summary>Whether a top-bottom rank is a percentage of the range.</summary>
    public bool? Percent { get; init; }

    /// <summary>Whether a top-bottom rule marks the bottom rather than the top.</summary>
    public bool? Bottom { get; init; }

    /// <summary>
    /// The icons of an icon-set rule: an add_conditional_format icon set, or the lower-kebab engine
    /// name of one outside that vocabulary.
    /// </summary>
    [AllowedValues(typeof(CellsIconSetNames)), OpenEnum("^[a-z][a-z0-9]*(-[a-z0-9]+)*$")]
    public string? IconSet { get; init; }
}

/// <summary>A sparkline group.</summary>
public sealed record CellsSparklineInfo
{
    /// <summary>Sheet the sparklines are drawn on.</summary>
    public required string Sheet { get; init; }

    /// <summary>The sparkline type: an add_sparkline kind, or the lower-case engine name of one outside that vocabulary.</summary>
    [AllowedValues(typeof(CellsSparklineTypes)), OpenEnum("^[a-z][a-z0-9]*$")]
    public required string Kind { get; init; }

    /// <summary>The series color; omitted when the group has none.</summary>
    [HexColor] public string? Color { get; init; }

    /// <summary>
    /// The data the group plots, one range when the sparklines' data fill it, otherwise each
    /// sparkline's data, comma-separated; it names the sheet only when that is not the group's own.
    /// </summary>
    public required string DataRange { get; init; }

    /// <summary>
    /// The cells the sparklines are drawn in: one range when they fill it, otherwise each cell,
    /// comma-separated, such as H2,H4,H5 after a row was inserted inside the group.
    /// </summary>
    public required string Location { get; init; }
}

/// <summary>A picture on a sheet.</summary>
public sealed record CellsImageInfo
{
    /// <summary>Sheet the picture is on.</summary>
    public required string Sheet { get; init; }

    /// <summary>The 1-based index of the picture on its sheet.</summary>
    [Minimum(1)]
    public required int Index { get; init; }

    /// <summary>The top-left anchor cell.</summary>
    public required string At { get; init; }

    /// <summary>The width in points.</summary>
    [Minimum(0)]
    public required double Width { get; init; }

    /// <summary>The height in points.</summary>
    [Minimum(0)]
    public required double Height { get; init; }
}

/// <summary>A cell comment (note).</summary>
public sealed record CellsCommentInfo
{
    /// <summary>Sheet the comment is on.</summary>
    public required string Sheet { get; init; }

    /// <summary>The cell the comment annotates.</summary>
    public required string Cell { get; init; }

    /// <summary>The comment text.</summary>
    public required string Text { get; init; }

    /// <summary>The comment author; omitted when empty.</summary>
    public string? Author { get; init; }
}

/// <summary>A hyperlink, with one of url and target.</summary>
public sealed record CellsHyperlinkInfo
{
    /// <summary>Sheet the hyperlink is on.</summary>
    public required string Sheet { get; init; }

    /// <summary>The A1 range the hyperlink covers.</summary>
    public required string Range { get; init; }

    /// <summary>The external address: a URL, an e-mail address or a file.</summary>
    public string? Url { get; init; }

    /// <summary>The internal target; it names the sheet only when that is not the hyperlink's own.</summary>
    public string? Target { get; init; }

    /// <summary>The text shown in the cell; omitted when empty.</summary>
    public string? Display { get; init; }
}
