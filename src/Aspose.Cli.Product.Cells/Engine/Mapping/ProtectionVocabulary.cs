using Aspose.Cells;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// The protect_sheet actions as the engine's protection flags, which a read reports and
/// protect_sheet writes.
/// </summary>
internal static class ProtectionVocabulary
{
    /// <summary>
    /// Each protect_sheet action with the protection flag that allows it, in the order the
    /// vocabulary lists them: protect_sheet sets the flags, a read lists the actions set.
    /// </summary>
    internal static readonly (string Action, Func<Protection, bool> Get, Action<Protection, bool> Set)[] Actions =
    [
        (CellsProtectActions.FormatCells, static p => p.AllowFormattingCell, static (p, allowed) => p.AllowFormattingCell = allowed),
        (CellsProtectActions.InsertRows, static p => p.AllowInsertingRow, static (p, allowed) => p.AllowInsertingRow = allowed),
        (CellsProtectActions.InsertColumns, static p => p.AllowInsertingColumn, static (p, allowed) => p.AllowInsertingColumn = allowed),
        (CellsProtectActions.DeleteRows, static p => p.AllowDeletingRow, static (p, allowed) => p.AllowDeletingRow = allowed),
        (CellsProtectActions.DeleteColumns, static p => p.AllowDeletingColumn, static (p, allowed) => p.AllowDeletingColumn = allowed),
        (CellsProtectActions.Sort, static p => p.AllowSorting, static (p, allowed) => p.AllowSorting = allowed),
        (CellsProtectActions.AutoFilter, static p => p.AllowFiltering, static (p, allowed) => p.AllowFiltering = allowed),
    ];

    /// <summary>The protect_sheet actions a protection allows, in the order the vocabulary lists them.</summary>
    internal static IReadOnlyList<string> Allowed(Protection protection) =>
        [.. Actions.Where(item => item.Get(protection)).Select(static item => item.Action)];
}
