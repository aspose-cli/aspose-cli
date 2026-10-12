using Aspose.Cli.Sdk.Operations;

namespace Aspose.Cli.Product.Cells.Contracts;

// Ops that operate at the sheet level (add, rename, delete, visibility, panes, page layout).

/// <summary>Adds a sheet.</summary>
[Operation("add_sheet")]
public sealed record AddSheetOp : CellsOp
{
    /// <summary>The name of the new sheet.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/cellsSheetInfo/properties/name")]
    [Pattern(@"\S")] public required string Name { get; init; }

    /// <summary>
    /// The 1-based position in the tab order, as inspect reports a sheet's position; appended when
    /// omitted, or when past the last sheet.
    /// </summary>
    [MistakenFor("position")]
    [Minimum(1)] public int? At { get; init; }
}

/// <summary>Renames the sheet the operation's sheet field names.</summary>
[Operation("rename_sheet")]
[AtLeastOneOf("sheet")]
public sealed record RenameSheetOp : CellsOp
{
    /// <summary>The new name.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/cellsSheetInfo/properties/name")]
    [Pattern(@"\S")] public required string To { get; init; }
}

/// <summary>Deletes the sheet the operation's sheet field names.</summary>
[Operation("delete_sheet")]
[AtLeastOneOf("sheet")]
public sealed record DeleteSheetOp : CellsOp;

/// <summary>Hides or shows the sheet the operation's sheet field names.</summary>
[Operation("set_sheet_visibility")]
[AtLeastOneOf("sheet")]
public sealed record SetSheetVisibilityOp : CellsOp
{
    /// <summary>Whether the sheet is hidden.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/cellsSheetInfo/properties/hidden")]
    public required bool Hidden { get; init; }
}

/// <summary>
/// Makes the visible sheet the operation's sheet field names the active sheet. The choice
/// survives saving and is the initial tab of the HTML preview.
/// </summary>
[Operation("set_active_sheet")]
[AtLeastOneOf("sheet")]
public sealed record SetActiveSheetOp : CellsOp;

/// <summary>
/// Moves the sheet the operation's sheet field names to a position in the tab order.
/// Cross-sheet and 3-D references follow the new order as they would in Excel.
/// </summary>
[Operation("move_sheet")]
[AtLeastOneOf("sheet")]
public sealed record MoveSheetOp : CellsOp
{
    /// <summary>
    /// The 1-based destination in the tab order, as inspect reports a sheet's position; a value
    /// past the last sheet moves it to the end.
    /// </summary>
    [MistakenFor("position")]
    [Minimum(1)] public required int To { get; init; }
}

/// <summary>Freezes the panes above and left of a cell: B2 freezes row 1 and column A, and A1 unfreezes.</summary>
[Operation("freeze_panes")]
public sealed record FreezePanesOp : CellsOp
{
    /// <summary>The anchor cell.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/cellsSheetLayoutInfo/properties/freezePanes")]
    [A1Cell] public required string Cell { get; init; }
}

/// <summary>
/// Sets the page layout for printing and PDF export: at least one field, and margins set at
/// least one side. Only the fields present are applied; the rest of the page setup is preserved.
/// </summary>
[Operation("set_page_setup")]
[AtLeastOneOf("orientation", "paperSize", "fitToWidth", "fitToHeight", "scale", "margins", "header", "footer")]
public sealed record SetPageSetupOp : CellsOp
{
    [ReadBack("inspect-result.schema.json#/$defs/cellsSheetLayoutInfo/properties/orientation")]
    [AllowedValues(typeof(CellsPageOrientations))] public string? Orientation { get; init; }

    [NotReadBack]
    [AllowedValues(typeof(CellsPaperSizes))] public string? PaperSize { get; init; }

    /// <summary>The number of pages the printout fits across; 0 is automatic.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/cellsSheetLayoutInfo/properties/fitToWidth")]
    [Minimum(0)] public int? FitToWidth { get; init; }

    /// <summary>The number of pages the printout fits down; 0 is automatic.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/cellsSheetLayoutInfo/properties/fitToHeight")]
    [Minimum(0)] public int? FitToHeight { get; init; }

    /// <summary>The zoom percentage; ignored when the printout fits to pages.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/cellsSheetLayoutInfo/properties/scale")]
    [Minimum(10), Maximum(400)] public int? Scale { get; init; }

    [NotReadBack]
    public CellsMargins? Margins { get; init; }

    /// <summary>The center header text, with Excel codes such as &amp;P (page) and &amp;N (pages).</summary>
    [ReadBack("inspect-result.schema.json#/$defs/cellsSheetLayoutInfo/properties/header")]
    public string? Header { get; init; }

    /// <summary>The center footer text, with Excel codes such as &amp;P and &amp;N.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/cellsSheetLayoutInfo/properties/footer")]
    public string? Footer { get; init; }
}

/// <summary>Page margins in inches; only the sides present are applied, and at least one is set.</summary>
[MinProperties(1)]
public sealed record CellsMargins
{
    [Minimum(0)] public double? Top { get; init; }

    [Minimum(0)] public double? Bottom { get; init; }

    [Minimum(0)] public double? Left { get; init; }

    [Minimum(0)] public double? Right { get; init; }

    /// <summary>The distance from the top of the page to the header.</summary>
    [Minimum(0)] public double? Header { get; init; }

    /// <summary>The distance from the bottom of the page to the footer.</summary>
    [Minimum(0)] public double? Footer { get; init; }
}

/// <summary>Accepted values of <see cref="SetPageSetupOp.PaperSize"/>.</summary>
public static class CellsPaperSizes
{
    public const string Letter = "letter";
    public const string Legal = "legal";
    public const string A3 = "a3";
    public const string A4 = "a4";
    public const string A5 = "a5";
    public const string Tabloid = "tabloid";
}

/// <summary>
/// Sets the print area of a sheet and the rows and columns repeated on every printed page.
/// Titles alone keep the current print area; an operation with no fields clears it.
/// </summary>
[Operation("set_print_area")]
public sealed record SetPrintAreaOp : CellsOp
{
    /// <summary>The print area, such as A1:H50.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/cellsSheetLayoutInfo/properties/printArea")]
    [A1Range] public string? Range { get; init; }

    /// <summary>The rows repeated on every printed page, such as 1:2 or 1.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/cellsSheetLayoutInfo/properties/titleRows")]
    [A1RowBand] public string? TitleRows { get; init; }

    /// <summary>The columns repeated on every printed page, such as A:B or A.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/cellsSheetLayoutInfo/properties/titleColumns")]
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
