using Aspose.Cells;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

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
            // The executor attaches the op index to this domain error.
            throw CellsErrors.OpsInvalid($"no defined name '{op.Name}' in the workbook");
        }

        workbook.Worksheets.Names.Remove(op.Name);
        return null;
    }
}
