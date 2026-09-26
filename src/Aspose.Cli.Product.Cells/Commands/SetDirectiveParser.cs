using System.Globalization;
using Aspose.Cli.Product.Cells;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Product.Cells.Contracts.Addressing;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Cells.Commands;

/// <summary>
/// Compiles one <c>--set</c> directive (<c>SHEET!CELL=VALUE</c>) into the edit
/// op it is sugar for: <see cref="SetFormulaOp"/> when the value starts with
/// <c>=</c>, otherwise <see cref="SetValuesOp"/> with a typed 1x1 matrix.
/// </summary>
/// <remarks>
/// The grammar, decided here once: the directive splits at its <em>first</em>
/// <c>=</c> into target and value. The target must be a sheet-qualified single
/// cell — quoting and <c>$</c> markers follow <see cref="A1"/> exactly
/// (<c>'My Sheet'!A1</c>, a quote inside the name doubled as <c>''</c>);
/// multi-cell ranges are refused and redirected to <c>--ops</c>. A value
/// starting with <c>=</c> is a formula kept verbatim (including that <c>=</c>);
/// otherwise <c>TRUE</c>/<c>FALSE</c> in any casing is a boolean, an
/// invariant-culture decimal number is a number, and anything else — including
/// the empty string — is text. Compiled values use the same CLR shapes as ops
/// parsed from an <c>--ops</c> document (string/double/bool), so batching,
/// dry runs, recalculation and the preview hint apply to them unchanged.
/// </remarks>
internal static class SetDirectiveParser
{
    private const string Example =
        "Example: --set \"Sales!B3=42\" writes a number, --set \"Sales!G2==E2*F2\" sets a formula.";

    /// <summary>Compiles one directive; see the class remarks for the grammar.</summary>
    /// <exception cref="CliException"><c>OPTION_INVALID</c> naming what is wrong with the directive.</exception>
    public static CellsOp Parse(string directive)
    {
        ArgumentNullException.ThrowIfNull(directive);

        // The first '=' separates target and value; everything after it is the
        // value, verbatim — a formula therefore starts with a second '='.
        int separator = directive.IndexOf('=');
        if (separator < 0)
        {
            throw Invalid(directive, "there is no '=' between the target cell and the value", Example);
        }

        (string sheet, string cell) = ParseTarget(directive, directive[..separator]);
        string rawValue = directive[(separator + 1)..];

        if (rawValue.StartsWith('='))
        {
            if (rawValue.Length == 1)
            {
                throw Invalid(directive, "the formula after '=' is empty",
                    "Write the formula after the second '=', e.g. --set \"Sales!G2==E2*F2\".");
            }

            return new SetFormulaOp { Sheet = sheet, Range = cell, Formula = rawValue };
        }

        return new SetValuesOp { Sheet = sheet, Range = cell, Values = [[TypedValue(rawValue)]] };
    }

    private static (string Sheet, string Cell) ParseTarget(string directive, string target)
    {
        if (target.Trim().Length == 0)
        {
            throw Invalid(directive, "the target cell before '=' is missing", Example);
        }

        RangeSpec spec;
        try
        {
            spec = A1.ParseRange(target);
        }
        catch (CliException ex) when (ex.Code == CellsDiagnostics.RangeInvalid)
        {
            throw Invalid(directive, ex.Message,
                "The target must be a sheet-qualified single cell such as Sales!B3 " +
                "('$' markers are allowed). " + Example);
        }

        if (spec.SheetName is not { } sheet)
        {
            throw Invalid(directive, $"the target '{target}' does not name a sheet; use SHEET!CELL",
                "Prefix the cell with the sheet name and '!'; quote names that need it " +
                "('My Sheet'!A1, a quote inside the name doubled as ''). " + Example);
        }

        if (spec.Range.CellCount != 1)
        {
            throw Invalid(directive,
                $"the target '{target}' is a {spec.Range.RowCount}x{spec.Range.ColumnCount} range, not a single cell",
                "--set writes one cell per flag. To fill a range, use --ops with a " +
                "set_values matrix or a set_formula range.");
        }

        return (sheet, A1.FormatCell(spec.Range.Start));
    }

    /// <summary>
    /// Types a non-formula value the way the same literal would type in an ops
    /// JSON document. Numbers are detected with an invariant-culture
    /// <see cref="decimal"/> parse and carried as <see cref="double"/>, the one
    /// numeric shape of the ops pipeline (see <c>JsonValueMatrix</c>).
    /// </summary>
    private static object TypedValue(string rawValue)
    {
        if (bool.TryParse(rawValue, out bool flag))
        {
            return flag;
        }

        if (decimal.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal number))
        {
            return (double)number;
        }

        return rawValue;
    }

    private static CliException Invalid(string directive, string reason, string hint) =>
        CliErrors.OptionInvalid("--set", $"in '{directive}': {reason}", hint);
}
