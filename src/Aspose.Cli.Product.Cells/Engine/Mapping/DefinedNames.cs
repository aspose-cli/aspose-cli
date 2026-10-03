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
}
