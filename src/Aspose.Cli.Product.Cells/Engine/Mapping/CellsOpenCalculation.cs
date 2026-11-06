using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cells;
using Aspose.Cli.Sdk.IO.Budgets;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// Calculates a workbook that asks to be calculated when opened, as Excel does, so a read shows
/// the values a user sees in Excel. Tools that write formulas without results (openpyxl, pandas
/// and many exporters) set that request; read as stored, their formulas are empty. Saving stores
/// the calculated results, so the next read shows the same, unless a formula whose result differs
/// calls a volatile function, such as RAND or NOW, which every calculation changes.
/// </summary>
internal static partial class CellsOpenCalculation
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
        (Cell Cell, object? Value)[] changed = [.. stored.Where(static entry => !SameResult(entry.Value, entry.Cell.Value))];
        if (changed.Length == 0)
        {
            return null;
        }

        // A volatile formula such as RANDBETWEEN or NOW changes with every load, so the counts go
        // in the details and the message stays the same for every load of one workbook.
        return new Warning(CellsDiagnostics.FormulasCalculatedOnOpen, "The workbook asks to be calculated when opened, as Excel does, "
                + "and formula results shown differ from the stored ones; details counts them.")
        {
            Details = new JsonObject
            {
                ["differingCount"] = changed.Length,
                ["formulaCount"] = stored.Count,
                ["storedWithoutResultCount"] = empty,
            },
            Hint = changed.Any(static entry => Volatile(entry.Cell.Formula))
                ? "The values shown are the engine's, as Excel shows them on opening. Formulas that call a volatile function, "
                    + "such as RAND, NOW, TODAY, OFFSET or INDIRECT, are calculated again every time and can change each time, "
                    + "so saving the workbook does not settle them; run 'cells read <file> --content formulas' to see the formulas."
                : "The values shown are the engine's, as Excel shows them on opening. Save the workbook with 'cells edit' "
                    + "to store them, or run 'cells read <file> --content formulas' to see the formulas.",
        };
    }

    // Whether a formula calls a volatile function outside its string literals and quoted sheet names.
    private static bool Volatile(string? formula) =>
        formula is not null && VolatileCall().IsMatch(QuotedText().Replace(formula, string.Empty));

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

    // The functions Excel recalculates at every calculation, whatever their arguments, called by name.
    [GeneratedRegex(@"(?<![\w.])(?:_xlfn\.)?(?:RAND|RANDBETWEEN|RANDARRAY|NOW|TODAY|OFFSET|INDIRECT|CELL|INFO)\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VolatileCall();

    [GeneratedRegex(@"""[^""]*""|'[^']*'")]
    private static partial Regex QuotedText();
}
