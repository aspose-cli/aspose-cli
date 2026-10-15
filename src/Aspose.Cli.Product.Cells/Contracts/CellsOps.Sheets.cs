using Aspose.Cli.Sdk.Operations;

namespace Aspose.Cli.Product.Cells.Contracts;

// Ops that operate at the sheet level (add, rename, delete, visibility, panes, page layout).

/// <summary>Adds a sheet.</summary>
[Operation("insert_sheet")]
[MistakenFor("add_sheet")]
public sealed record InsertSheetOp : CellsOp
{
    /// <summary>The name of the new sheet, as Excel allows it (see <see cref="CellsSheetNames"/>).</summary>
    [ReadBack("inspect-result.schema.json#/$defs/cellsSheetInfo/properties/name")]
    [Pattern(@"\S"), MaxLength(CellsSheetNames.MaxLength)] public required string Name { get; init; }

    /// <summary>
    /// The 1-based position in the tab order, as inspect reports a sheet's position; appended when
    /// omitted, or when past the last sheet.
    /// </summary>
    [MistakenFor("position")]
    [Minimum(1)] public int? At { get; init; }

    /// <inheritdoc />
    protected override BoundedOperation Validated()
    {
        CellsSheetNames.Require(Name);
        return this;
    }
}

/// <summary>Renames the sheet the operation's sheet field names.</summary>
[Operation("rename_sheet")]
[AtLeastOneOf("sheet")]
public sealed record RenameSheetOp : CellsSheetOp
{
    /// <summary>The new name, as Excel allows it (see <see cref="CellsSheetNames"/>).</summary>
    [MistakenFor("to")]
    [ReadBack("inspect-result.schema.json#/$defs/cellsSheetInfo/properties/name")]
    [Pattern(@"\S"), MaxLength(CellsSheetNames.MaxLength)] public required string Name { get; init; }

    /// <inheritdoc />
    protected override BoundedOperation Validated()
    {
        CellsSheetNames.Require(Name);
        return this;
    }
}

/// <summary>
/// The sheet names Excel allows: at most 31 characters, none of : \ / ? * [ ], not starting or
/// ending with an apostrophe, and not History, which Excel reserves.
/// </summary>
public static class CellsSheetNames
{
    /// <summary>The longest name Excel allows.</summary>
    public const int MaxLength = 31;

    private static readonly char[] Forbidden = [':', '\\', '/', '?', '*', '[', ']'];

    /// <summary>Refuses a name Excel would not open, with what to change in it.</summary>
    internal static void Require(string name)
    {
        const string Hint = "Excel refuses : \\ / ? * [ ] in a sheet name, an apostrophe at its start or end, the name History, and more than 31 characters; replace or drop them, e.g. 华东-销售.";
        int forbidden = name.IndexOfAny(Forbidden);
        OperationInvalidException.Require(forbidden < 0,
            $"sheet name '{name}' holds '{(forbidden < 0 ? ' ' : name[forbidden])}', which Excel does not allow in a sheet name", Hint);
        OperationInvalidException.Require(!name.StartsWith('\'') && !name.EndsWith('\''),
            $"sheet name '{name}' starts or ends with an apostrophe, which Excel does not allow", Hint);
        OperationInvalidException.Require(!string.Equals(name, "History", StringComparison.OrdinalIgnoreCase),
            "Excel reserves the sheet name History", Hint);
    }
}

/// <summary>Deletes the sheet the operation's sheet field names.</summary>
[Operation("delete_sheet")]
[AtLeastOneOf("sheet")]
public sealed record DeleteSheetOp : CellsSheetOp;

/// <summary>
/// Shows or hides the sheet the operation's sheet field names. Showing a sheet leaves the active
/// sheet as it was; hiding the active sheet makes the next visible sheet active, and the last
/// visible sheet cannot be hidden.
/// </summary>
[Operation("set_sheet_visibility")]
[AtLeastOneOf("sheet")]
public sealed record SetSheetVisibilityOp : CellsSheetOp
{
    /// <summary>
    /// visible shows the tab; hidden hides it, and Excel's Unhide shows it again; very-hidden hides
    /// it from Excel's Unhide too.
    /// </summary>
    [MistakenFor("hidden", "visible")]
    [ReadBack("inspect-result.schema.json#/$defs/cellsSheetInfo/properties/visibility")]
    [AllowedValues(typeof(CellsSheetVisibilities))] public required string Visibility { get; init; }
}

/// <summary>Accepted values of <see cref="SetSheetVisibilityOp.Visibility"/>, as inspect reports a sheet's visibility.</summary>
public static class CellsSheetVisibilities
{
    public const string Visible = "visible";
    public const string Hidden = "hidden";
    public const string VeryHidden = "very-hidden";
}

/// <summary>
/// Makes the visible sheet the operation's sheet field names the active sheet. The choice
/// survives saving and is the initial tab of the HTML preview.
/// </summary>
[Operation("set_active_sheet")]
[AtLeastOneOf("sheet")]
public sealed record SetActiveSheetOp : CellsSheetOp;

/// <summary>
/// Moves the sheet the operation's sheet field names to a position in the tab order.
/// Cross-sheet and 3-D references follow the new order as they would in Excel.
/// </summary>
[Operation("move_sheet")]
[AtLeastOneOf("sheet")]
public sealed record MoveSheetOp : CellsSheetOp
{
    /// <summary>
    /// The 1-based destination in the tab order, as inspect reports a sheet's position; a value
    /// past the last sheet moves it to the end.
    /// </summary>
    [MistakenFor("position")]
    [Minimum(1)] public required int To { get; init; }
}

/// <summary>
/// Freezes the panes above and left of a cell: A2 freezes row 1, B2 row 1 and column A.
/// unfreeze_panes removes frozen panes.
/// </summary>
[Operation("freeze_panes", Hint = "To remove frozen panes, use unfreeze_panes.")]
public sealed record FreezePanesOp : CellsSheetOp
{
    /// <summary>The top-left cell of the scrolling pane; not A1, which freezes nothing.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/cellsFreezePanesInfo/properties/cell")]
    [A1Cell] public required string Cell { get; init; }

    /// <inheritdoc />
    protected override BoundedOperation Validated()
    {
        OperationInvalidException.Require(A1.ParseCell(Cell) != new CellRef(0, 0),
            "'A1' freezes nothing", "Freeze below or right of A1, such as A2 or B2; remove frozen panes with unfreeze_panes.");
        return this;
    }
}

/// <summary>Removes a sheet's frozen panes.</summary>
[Operation("unfreeze_panes")]
public sealed record UnfreezePanesOp : CellsSheetOp;

/// <summary>
/// Sets the page layout for printing and PDF export: at least one field, and margins set at
/// least one side. Only the fields present are applied; the rest of the page setup is preserved.
/// </summary>
[Operation("set_page_setup", Hint = "The header and footer distances are headerMargin and footerMargin, beside margins.")]
[AtLeastOneOf("orientation", "pageSize", "fitToWidth", "fitToHeight", "scalePercent", "margins", "headerMargin", "footerMargin", "headerCenter", "footerCenter")]
public sealed record SetPageSetupOp : CellsSheetOp
{
    [ReadBack("inspect-result.schema.json#/$defs/cellsPageSetupInfo/properties/orientation")]
    [AllowedValues(typeof(CellsPageOrientations))] public string? Orientation { get; init; }

    /// <summary>
    /// The paper size: a preset, turned by the orientation, or a width and height in points whose
    /// longer side is the width in landscape and the height in portrait, as a read reports it.
    /// </summary>
    [MistakenFor("paperSize")]
    [ReadBack("inspect-result.schema.json#/$defs/cellsPageSetupInfo/properties/pageSize")]
    public PageSizeInput? PageSize { get; init; }

    /// <summary>The number of pages the printout fits across; 0 is automatic.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/cellsPageSetupInfo/properties/fitToWidth")]
    [Minimum(0)] public int? FitToWidth { get; init; }

    /// <summary>The number of pages the printout fits down; 0 is automatic.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/cellsPageSetupInfo/properties/fitToHeight")]
    [Minimum(0)] public int? FitToHeight { get; init; }

    /// <summary>The zoom percentage; ignored when the printout fits to pages.</summary>
    [MistakenFor("scale")]
    [ReadBack("inspect-result.schema.json#/$defs/cellsPageSetupInfo/properties/scalePercent")]
    [Minimum(10), Maximum(400)] public int? ScalePercent { get; init; }

    [ReadBack("inspect-result.schema.json#/$defs/cellsPageSetupInfo/properties/margins")]
    public CellsMargins? Margins { get; init; }

    /// <summary>The distance in points from the top of the page to the header.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/cellsPageSetupInfo/properties/headerMargin")]
    [Minimum(0)] public double? HeaderMargin { get; init; }

    /// <summary>The distance in points from the bottom of the page to the footer.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/cellsPageSetupInfo/properties/footerMargin")]
    [Minimum(0)] public double? FooterMargin { get; init; }

    /// <summary>The center header text, with Excel codes such as &amp;P (page) and &amp;N (pages).</summary>
    [MistakenFor("header")]
    [ReadBack("inspect-result.schema.json#/$defs/cellsPageSetupInfo/properties/headerCenter")]
    public string? HeaderCenter { get; init; }

    /// <summary>The center footer text, with Excel codes such as &amp;P and &amp;N.</summary>
    [MistakenFor("footer")]
    [ReadBack("inspect-result.schema.json#/$defs/cellsPageSetupInfo/properties/footerCenter")]
    public string? FooterCenter { get; init; }

    /// <inheritdoc />
    protected override BoundedOperation Validated()
    {
        // A preset given with a width and height that measure another paper is refused here, before anything runs.
        _ = PageSize?.Dimensions();
        return this;
    }
}

/// <summary>
/// Sets the print area of a sheet and the rows and columns repeated on every printed page;
/// titles omitted are removed. clear_print_area removes all three.
/// </summary>
[Operation("set_print_area", Hint = "To remove the print area and titles, use clear_print_area.")]
public sealed record SetPrintAreaOp : CellsSheetOp
{
    /// <summary>The print area, such as A1:H50, or several areas separated by commas, such as A1:B5,D1:E5, each printed on pages of its own.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/cellsPrintAreaInfo/properties/range")]
    [A1RangeList] public required string Range { get; init; }

    /// <summary>The rows repeated on every printed page, such as 1:2 or 1.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/cellsPrintAreaInfo/properties/titleRows")]
    [A1RowBand] public string? TitleRows { get; init; }

    /// <summary>The columns repeated on every printed page, such as A:B or A.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/cellsPrintAreaInfo/properties/titleColumns")]
    [A1ColumnBand] public string? TitleColumns { get; init; }

    /// <inheritdoc />
    protected override BoundedOperation Validated() =>
        // Titles take Excel's absolute band form ($1:$2, $A:$B), exactly what the engine stores.
        this with
        {
            TitleRows = TitleRows is { } rows ? A1.ParseRowBand(rows) : null,
            TitleColumns = TitleColumns is { } columns ? A1.ParseColumnBand(columns) : null,
        };
}

/// <summary>Removes a sheet's print area and its print titles.</summary>
[Operation("clear_print_area")]
public sealed record ClearPrintAreaOp : CellsSheetOp;
