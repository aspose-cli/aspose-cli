using System.Text.RegularExpressions;
using Aspose.Cells;
using Aspose.Cli.Product.Cells.Engine.Mapping;

namespace Aspose.Cli.Product.Cells.Engine.Editing;

/// <summary>
/// Names the functions the batch's formulas call that Aspose.Cells does not know, so they
/// evaluate to #NAME?, with the known function one edit away from each. The engine itself
/// decides what a function is: it parses an unknown name as a custom function of any arity,
/// and a known one only with an arity it accepts.
/// </summary>
internal static partial class UnknownFunctions
{
    private const int ListedCells = 5;

    // The most arguments the probe passes, enough for the argument count nearly every known
    // function accepts; a known function that requires more is reported as unknown.
    private const int MaxProbedArity = 5;

    /// <summary>
    /// Checks the anchor cells set_formula wrote, where later row, column and sheet operations
    /// moved them; what a later operation wrote over an anchor is checked instead.
    /// </summary>
    internal static Warning? Warning(Workbook workbook, IEnumerable<Cell> anchors)
    {
        Worksheet[] sheets = [.. workbook.Worksheets.Cast<Worksheet>()];
        Cell[] cells = [.. anchors.Where(anchor => sheets.Contains(anchor.Worksheet))
            .Select(static anchor => anchor.Worksheet.Cells.CheckCell(anchor.Row, anchor.Column)).OfType<Cell>()
            .Where(static cell => cell.HasCustomFunction).DistinctBy(static cell => (cell.Worksheet.Index, cell.Row, cell.Column))];
        if (cells.Length == 0)
        {
            return null;
        }

        // A defined name, such as one holding a LAMBDA, is called like a function. The engine
        // also lists each unknown function it parsed as a name, which refers to nothing.
        var defined = workbook.Worksheets.Names.Cast<Name>().Where(static name => !string.IsNullOrEmpty(name.RefersTo))
            .Select(static name => name.Text).ToHashSet(StringComparer.OrdinalIgnoreCase);
        using var scratch = new Workbook();
        Cell probe = scratch.Worksheets[0].Cells[0, 0];
        string[] findings = [.. cells.Select(cell => (Cell: cell, Names: Called(cell.Formula)
                .Where(name => !defined.Contains(name) && !IsFunction(probe, name))
                .ToArray()))
            .Where(static entry => entry.Names.Length > 0)
            .Select(static entry => $"{Sheets.QuotedName(entry.Cell.Worksheet.Name)}!{entry.Cell.Name}: {string.Join(", ", entry.Names)}")];
        return findings.Length == 0 ? null : new Warning(CellsDiagnostics.FormulaFunctionUnknown, $"Aspose.Cells does not know the function(s) in {string.Join("; ", findings.Take(ListedCells))}"
                + (findings.Length > ListedCells ? $"; and {findings.Length - ListedCells} more cell(s)" : string.Empty)
                + ". Those formulas evaluate to #NAME?.")
        {
            Docs = "cells/editing",
        };
    }

    // The names a formula calls, outside string literals and quoted sheet names, less LET and
    // LAMBDA, which take parameter names the probe cannot pass, and the parameters they
    // declare, which a formula calls when they hold a LAMBDA.
    private static IEnumerable<string> Called(string formula)
    {
        string code = QuotedText().Replace(formula, string.Empty);
        HashSet<string> skipped = Parameter().Matches(code).Select(static match => match.Groups[1].Value)
            .Append("LET").Append("LAMBDA").ToHashSet(StringComparer.OrdinalIgnoreCase);
        return FunctionCall().Matches(code).Select(static match => match.Groups[1].Value)
            .Where(name => !skipped.Contains(name)).Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsFunction(Cell probe, string name)
    {
        for (int arity = 0; arity <= MaxProbedArity; arity++)
        {
            try
            {
                probe.Formula = $"={name}({string.Join(",", Enumerable.Repeat("0", arity))})";
                return !probe.HasCustomFunction;
            }
            catch (CellsException)
            {
                // A known function refuses an arity it does not take; try the next one.
            }
        }

        return false;
    }

    // A name of any script, so that a localized name such as 求和 or SUMME is named too.
    [GeneratedRegex(@"(?<![\w.])([\p{L}_][\w.]*)\s*\(")]
    private static partial Regex FunctionCall();

    // A bare name followed by another argument, as LET and LAMBDA declare their parameters.
    [GeneratedRegex(@"(?:\b(?:LET|LAMBDA)\s*\(|,)\s*([\p{L}_][\w.]*)\s*(?=,)", RegexOptions.IgnoreCase)]
    private static partial Regex Parameter();

    [GeneratedRegex(@"""[^""]*""|'[^']*'")]
    private static partial Regex QuotedText();
}
