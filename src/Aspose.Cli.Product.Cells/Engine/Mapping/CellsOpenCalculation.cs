using Aspose.Cells;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// Calculates a workbook that asks to be calculated when opened, as Excel does, so a read shows
/// the values a user sees in Excel. Tools that write formulas without results (openpyxl, pandas
/// and many exporters) set that request; read as stored, their formulas are empty.
/// </summary>
internal static class CellsOpenCalculation
{
    internal static Warning? Apply(Workbook workbook, ResourceBudgetLedger budgets)
    {
        if (!workbook.Settings.FormulaSettings.CalculateOnOpen)
        {
            return null;
        }

        var stored = new List<(Cell Cell, object? Value)>();
        foreach (Worksheet sheet in workbook.Worksheets)
        {
            budgets.Deadline.ThrowIfExpired("calculate-on-open");
            foreach (Cell cell in sheet.Cells)
            {
                if (cell.IsFormula)
                {
                    stored.Add((cell, cell.Value));
                }
            }
        }

        if (stored.Count == 0)
        {
            return null;
        }

        workbook.CalculateFormula();
        int empty = stored.Count(static entry => entry.Value is null or "");
        int changed = stored.Count(static entry => !SameResult(entry.Value, entry.Cell.Value));
        if (changed == 0)
        {
            return null;
        }

        return new Warning
        {
            Code = CellsDiagnostics.FormulasCalculatedOnOpen,
            Message = $"The workbook asks to be calculated when opened, as Excel does: {changed} of {stored.Count} formula results "
                + $"shown differ from the stored ones ({empty} were stored without a result).",
            Hint = "The values shown are the engine's, as Excel shows them on opening. Save the workbook with 'cells edit' "
                + "to store them, or read with --scope formulas to see the formulas.",
        };
    }

    // A stored whole number reads back as Int32 and the calculated one as Double; compare numbers
    // by value, allowing for the last bits of floating-point rounding.
    private static bool SameResult(object? stored, object? calculated) =>
        Number(stored) is { } left && Number(calculated) is { } right
            ? Math.Abs(left - right) <= 1e-9 * Math.Max(1, Math.Max(Math.Abs(left), Math.Abs(right)))
            : Equals(stored, calculated);

    private static double? Number(object? value) => value switch
    {
        int number => number,
        long number => number,
        double number => number,
        decimal number => (double)number,
        _ => null,
    };
}
