namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>Which sheets a convert output holds, and so what <c>--sheets</c> can select for it.</summary>
internal enum CellsSheetScope
{
    /// <summary>Every sheet of the workbook; <c>--sheets</c> is refused.</summary>
    Workbook,

    /// <summary>One worksheet by nature, the active one; <c>--sheets</c> selects which.</summary>
    ActiveSheet,

    /// <summary>Every sheet, or the set <c>--sheets</c> selects.</summary>
    SheetSet,
}
