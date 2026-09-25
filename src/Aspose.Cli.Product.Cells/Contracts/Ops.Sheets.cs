using Aspose.Cli.Sdk.Operations;

namespace Aspose.Cli.Product.Cells.Contracts;

// Ops that operate at the sheet level (add, rename, delete, visibility, panes, page layout).

/// <summary>Adds a sheet.</summary>
[Operation("add_sheet")]
public sealed record AddSheetOp : Op
{
    /// <summary>The name of the new sheet.</summary>
    [Pattern(@"\S")] public required string Name { get; init; }

    /// <summary>The zero-based position; appended when omitted.</summary>
    [Minimum(0)] public int? Position { get; init; }
}

/// <summary>Renames the sheet the operation's sheet field names.</summary>
[Operation("rename_sheet")]
[AtLeastOneOf("sheet")]
public sealed record RenameSheetOp : Op
{
    /// <summary>The new name.</summary>
    [Pattern(@"\S")] public required string To { get; init; }
}

/// <summary>Deletes the sheet the operation's sheet field names.</summary>
[Operation("delete_sheet")]
[AtLeastOneOf("sheet")]
public sealed record DeleteSheetOp : Op;

/// <summary>Hides or shows the sheet the operation's sheet field names.</summary>
[Operation("set_sheet_visibility")]
[AtLeastOneOf("sheet")]
public sealed record SetSheetVisibilityOp : Op
{
    /// <summary>Whether the sheet is hidden.</summary>
    public required bool Hidden { get; init; }
}

/// <summary>
/// Makes the visible sheet the operation's sheet field names the active sheet. The choice
/// survives saving and is the initial tab of the HTML preview.
/// </summary>
[Operation("set_active_sheet")]
[AtLeastOneOf("sheet")]
public sealed record SetActiveSheetOp : Op;

/// <summary>
/// Moves the sheet the operation's sheet field names to a position in the tab order.
/// Cross-sheet and 3-D references follow the new order as they would in Excel.
/// </summary>
[Operation("move_sheet")]
[AtLeastOneOf("sheet")]
public sealed record MoveSheetOp : Op
{
    /// <summary>The zero-based destination in the tab order; a value past the last sheet moves it to the end.</summary>
    [Minimum(0)] public required int Position { get; init; }
}

/// <summary>Freezes the panes above and left of a cell: B2 freezes row 1 and column A, and A1 unfreezes.</summary>
[Operation("freeze_panes")]
public sealed record FreezePanesOp : Op
{
    /// <summary>The anchor cell.</summary>
    [A1Cell] public required string Cell { get; init; }
}

/// <summary>
/// Sets the page layout for printing and PDF export: at least one field, and margins set at
/// least one side. Only the fields present are applied; the rest of the page setup is preserved.
/// </summary>
[Operation("set_page_setup")]
[AtLeastOneOf("orientation", "paperSize", "fitToWidth", "fitToHeight", "scale", "margins", "header", "footer")]
public sealed record SetPageSetupOp : Op
{
    [AllowedValues(typeof(PageOrientations))] public string? Orientation { get; init; }

    [AllowedValues(typeof(PaperSizes))] public string? PaperSize { get; init; }

    /// <summary>The number of pages the printout fits across; 0 is automatic.</summary>
    [Minimum(0)] public int? FitToWidth { get; init; }

    /// <summary>The number of pages the printout fits down; 0 is automatic.</summary>
    [Minimum(0)] public int? FitToHeight { get; init; }

    /// <summary>The zoom percentage; ignored when the printout fits to pages.</summary>
    [Minimum(10), Maximum(400)] public int? Scale { get; init; }

    public Margins? Margins { get; init; }

    /// <summary>The center header text, with Excel codes such as &amp;P (page) and &amp;N (pages).</summary>
    public string? Header { get; init; }

    /// <summary>The center footer text, with Excel codes such as &amp;P and &amp;N.</summary>
    public string? Footer { get; init; }
}

/// <summary>Page margins in inches; only the sides present are applied, and at least one is set.</summary>
[MinProperties(1)]
public sealed record Margins
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

/// <summary>Accepted values of <see cref="SetPageSetupOp.Orientation"/>.</summary>
public static class PageOrientations
{
    public const string Portrait = "portrait";
    public const string Landscape = "landscape";
}

/// <summary>Accepted values of <see cref="SetPageSetupOp.PaperSize"/>.</summary>
public static class PaperSizes
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
public sealed record SetPrintAreaOp : Op
{
    /// <summary>The print area, such as A1:H50.</summary>
    [A1Range] public string? Range { get; init; }

    /// <summary>The rows repeated on every printed page, such as 1:2 or 1.</summary>
    [A1RowBand] public string? TitleRows { get; init; }

    /// <summary>The columns repeated on every printed page, such as A:B or A.</summary>
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
