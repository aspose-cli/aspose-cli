namespace Aspose.Cli.Product.Cells.Contracts;

// Ops that operate at the sheet level (add, rename, delete, visibility, panes).

/// <summary>Adds a sheet.</summary>
public sealed record AddSheetOp() : Op(OpNames.AddSheet)
{
    /// <summary>Name of the new sheet.</summary>
    public required string Name { get; init; }

    /// <summary>Zero-based position; appended when omitted.</summary>
    public int? Position { get; init; }
}

/// <summary>Renames a sheet (the op's <c>sheet</c> field is the current name).</summary>
public sealed record RenameSheetOp() : Op(OpNames.RenameSheet)
{
    /// <summary>The new name.</summary>
    public required string To { get; init; }
}

/// <summary>Deletes a sheet (the op's <c>sheet</c> field names it).</summary>
public sealed record DeleteSheetOp() : Op(OpNames.DeleteSheet);

/// <summary>Hides or shows a sheet (the op's <c>sheet</c> field names it).</summary>
public sealed record SetSheetVisibilityOp() : Op(OpNames.SetSheetVisibility)
{
    /// <summary><c>true</c> hides the sheet; <c>false</c> shows it.</summary>
    public required bool Hidden { get; init; }
}

/// <summary>
/// Makes the explicitly named visible sheet the workbook's active sheet.
/// The choice survives save/reopen and becomes the initial HTML preview tab.
/// </summary>
public sealed record SetActiveSheetOp() : Op(OpNames.SetActiveSheet);

/// <summary>
/// Moves a sheet to a new position in the tab order (the op's <c>sheet</c>
/// field names it). Cross-sheet and 3-D references re-scope to the new order
/// exactly as they would in Excel.
/// </summary>
public sealed record MoveSheetOp() : Op(OpNames.MoveSheet)
{
    /// <summary>
    /// Zero-based destination index in the tab order; a value past the last
    /// sheet moves it to the end.
    /// </summary>
    public required int Position { get; init; }
}

/// <summary>
/// Freezes panes above and left of a cell (<c>B2</c> freezes row 1 and
/// column A); <c>A1</c> unfreezes.
/// </summary>
public sealed record FreezePanesOp() : Op(OpNames.FreezePanes)
{
    /// <summary>The anchor cell.</summary>
    public required string Cell { get; init; }
}

/// <summary>
/// Sets page layout for printing and PDF export. Only the fields present are
/// applied; the rest of the sheet's page setup is preserved.
/// </summary>
public sealed record SetPageSetupOp() : Op(OpNames.SetPageSetup)
{
    /// <summary>Page orientation; one of <see cref="PageOrientations"/>.</summary>
    public string? Orientation { get; init; }

    /// <summary>Paper size; one of <see cref="PaperSizes"/>.</summary>
    public string? PaperSize { get; init; }

    /// <summary>Fit the printout to this many pages wide (0 = automatic).</summary>
    public int? FitToWidth { get; init; }

    /// <summary>Fit the printout to this many pages tall (0 = automatic).</summary>
    public int? FitToHeight { get; init; }

    /// <summary>Zoom percentage (10-400); ignored when fit-to-page is set.</summary>
    public int? Scale { get; init; }

    /// <summary>Page margins in inches.</summary>
    public MarginsData? Margins { get; init; }

    /// <summary>Center header text, with Excel codes like <c>&amp;P</c> (page) and <c>&amp;N</c> (pages).</summary>
    public string? Header { get; init; }

    /// <summary>Center footer text, with Excel codes like <c>&amp;P</c> and <c>&amp;N</c>.</summary>
    public string? Footer { get; init; }
}

/// <summary>Page margins in inches; only the sides present are applied.</summary>
public sealed record MarginsData
{
    /// <summary>Top margin in inches.</summary>
    public double? Top { get; init; }

    /// <summary>Bottom margin in inches.</summary>
    public double? Bottom { get; init; }

    /// <summary>Left margin in inches.</summary>
    public double? Left { get; init; }

    /// <summary>Right margin in inches.</summary>
    public double? Right { get; init; }

    /// <summary>Distance from the top of the page to the header, in inches.</summary>
    public double? Header { get; init; }

    /// <summary>Distance from the bottom of the page to the footer, in inches.</summary>
    public double? Footer { get; init; }
}

/// <summary>Accepted values of <see cref="SetPageSetupOp.Orientation"/>.</summary>
public static class PageOrientations
{
    public const string Portrait = "portrait";
    public const string Landscape = "landscape";

    /// <summary>Every orientation, in documentation order.</summary>
    public static IReadOnlyList<string> All { get; } = [Portrait, Landscape];
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

    /// <summary>Every paper size, in documentation order.</summary>
    public static IReadOnlyList<string> All { get; } = [Letter, Legal, A3, A4, A5, Tabloid];
}

/// <summary>Sets or clears the print area of a sheet, with optional repeating titles.</summary>
public sealed record SetPrintAreaOp() : Op(OpNames.SetPrintArea)
{
    /// <summary>The print area, e.g. <c>A1:H50</c>; omit to clear it.</summary>
    public string? Range { get; init; }

    /// <summary>Rows repeated on every printed page, e.g. <c>1:1</c>.</summary>
    public string? TitleRows { get; init; }

    /// <summary>Columns repeated on every printed page, e.g. <c>A:A</c>.</summary>
    public string? TitleColumns { get; init; }
}
