using Aspose.Cells;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>The defined names a workbook holds, the one rule every name listing uses.</summary>
internal static class DefinedNames
{
    /// <summary>
    /// The workbook's names in collection order, hidden ones included, without the _xl names the
    /// engine and Excel keep for themselves, such as the _xlfn.XLOOKUP placeholder the engine
    /// keeps for each newer function a formula calls.
    /// </summary>
    public static IEnumerable<Name> Of(Workbook workbook) =>
        workbook.Worksheets.Names.Cast<Name>().Where(static name =>
            !name.Text.StartsWith("_xl", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// A name as a read reports it. Once a name's target sheet is deleted the engine returns a
    /// null RefersTo (26.9.0, not nullable-annotated); RefersTo is required, so a null would
    /// vanish from the payload. Excel shows #REF! for exactly this broken state, so the name
    /// reports that.
    /// </summary>
    public static CellsDefinedNameInfo Info(Name name) => new() { Name = name.Text, RefersTo = name.RefersTo ?? "#REF!" };
}
