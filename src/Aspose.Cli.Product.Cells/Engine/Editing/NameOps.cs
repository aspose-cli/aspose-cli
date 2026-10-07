using Aspose.Cells;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Cells.Engine.Editing;

/// <summary>Workbook-scoped defined names (named ranges).</summary>
internal static class NameOps
{
    public static long? DefineName(Workbook workbook, DefineNameOp op)
    {
        int index = workbook.Worksheets.Names.Add(op.Name);
        Name name = workbook.Worksheets.Names[index];
        name.RefersTo = op.RefersTo.StartsWith('=') ? op.RefersTo : "=" + op.RefersTo;
        return null;
    }

    public static long? DeleteName(Workbook workbook, DeleteNameOp op)
    {
        if (workbook.Worksheets.Names[op.Name] is null)
        {
            throw CliErrors.NotFound(CellsDiagnostics.NameNotFound, "defined name", op.Name, VisibleNames(workbook));
        }

        workbook.Worksheets.Names.Remove(op.Name);
        return null;
    }

    /// <summary>
    /// The names a caller can address, in collection order: the full text, which carries the
    /// sheet prefix of a sheet-scoped name, and never the hidden or _xl names Excel keeps for itself.
    /// </summary>
    private static List<string> VisibleNames(Workbook workbook) =>
        [.. DefinedNames.Of(workbook).Where(static name => name.IsVisible).Select(static name => name.FullText)];
}
