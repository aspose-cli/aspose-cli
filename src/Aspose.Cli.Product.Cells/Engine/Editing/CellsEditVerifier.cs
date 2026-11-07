using Aspose.Cells;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Diagnostics;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.Results;

namespace Aspose.Cli.Product.Cells.Engine.Editing;

/// <summary>
/// Verifies the exact staged workbook against its pre-edit baseline, as far as the output's format
/// holds it: a text table holds the values of one sheet (<see cref="DiffComparer.CompareTextTable"/>).
/// </summary>
internal sealed class CellsEditVerifier(CellsWorkbookLoader loader, ResourceBudgetLedger budgets)
{
    private const int MaxDiffs = 1000;

    internal CellsEditVerification Verify(string candidatePath,
        string baselinePath, string originalPath, Secret? inputPassword,
        Secret? outputPassword, CellsOpsBatch batch, string activeSheet, IReadOnlyList<BoundedOperationOutcome> applied,
        IReadOnlyList<Warning>? sourceWarnings, TextTableSheet? textTable)
    {
        using LoadedWorkbook baseline = loader.OpenPublishedCandidate(baselinePath, inputPassword, originalPath);
        using LoadedWorkbook candidate = loader.OpenPublishedCandidate(candidatePath, outputPassword);
        // A text table reads back named after its file and holds no formulas: its sheet keeps the
        // name it was written from, and its values alone are compared.
        textTable?.Rename(candidate.Workbook);
        bool valuesOnly = textTable is not null;
        // Targets as the batch found the workbook before its first operation: an operation that
        // names no sheet ran on the sheet active then.
        IReadOnlyList<CellsEditTarget> footprint = OpsFootprint.Collect(batch, activeSheet);
        IReadOnlyList<CellsEditTarget> written = OpsFootprint.Collect(batch, activeSheet, cellsOnly: true);
        var direct = new List<CellsVerifiedCellChange>();
        var formulaResults = new List<CellsVerifiedCellChange>();
        var other = new List<CellsVerificationOtherChange>();
        var issues = new List<VerificationIssue>();
        issues.AddRange(CompletenessIssues(sourceWarnings));
        issues.AddRange(CompletenessIssues(candidate.Warnings()));
        DiffComparer.Result diff = textTable is null
            ? DiffComparer.Compare(budgets, baseline.Workbook, candidate.Workbook, includeFormulas: true, MaxDiffs)
            : DiffComparer.CompareTextTable(budgets, baseline.Workbook, candidate.Workbook, textTable, MaxDiffs);
        // The change lists are evidence capped at MaxDiffs cells (Truncated); they decide nothing,
        // so a large edit such as a sort still passes when the checks below find no issue.
        Classify(diff, written, direct, formulaResults, other, valuesOnly);
        (IReadOnlyList<CellsCellError> errors, int errorTotal) = InfoProjection.ScanFormulaErrors(budgets, candidate.Workbook);
        errors = errors.Select(error => IsPreexisting(error, baseline.Workbook, candidate.Workbook, valuesOnly)
            ? error with { Preexisting = true } : error).ToArray();
        bool errorsCapped = errorTotal > errors.Count;
        if (errorsCapped)
        {
            issues.Add(VerificationIssue.From(EnvelopeParts.ListTruncated(
                "verification.formulaErrors", errors.Count, errorTotal, InfoProjection.FormulaErrorsCappedHint)));
        }
        if (errorTotal > 0)
        {
            int preexisting = errors.Count(static error => error.Preexisting == true);
            issues.Add(VerificationIssues.Of(CellsDiagnostics.FormulaErrors,
                (errorsCapped
                    ? $"The edited workbook contains {errorTotal} formula error(s); formulaErrors lists the first {errors.Count}."
                    : $"The edited workbook contains {errorTotal} formula error(s).")
                + (preexisting > 0 ? $" {preexisting} of the listed error(s) were already in the input." : ""),
                location: errorTotal == 1 ? CellsAddresses.At("cell", errors[0].Sheet, errors[0].Cell) : null,
                hint: (errorsCapped
                    ? "Fix the cells listed in formulaErrors, then edit again with --verify to list the rest."
                    : "Fix the cells listed in formulaErrors, then edit again with --verify.")
                    + (errors.Any(static error => error.Error == "#NAME?")
                        ? " #NAME? means a formula names a function or name the workbook does not know. " + CellOps.EnglishFormulaHint
                        : "")));
        }
        return new CellsEditVerification
        {
            Ok = issues.Count == 0,
            CheckedOps = OpsFootprint.Checked(batch, Held(footprint, textTable, baseline.Workbook), applied
                .Where(static outcome => outcome.Status == OpStatuses.Ok)
                .Select(static outcome => outcome.Id)
                .ToHashSet(StringComparer.Ordinal), activeSheet),
            RequestedTargets = footprint.Select(static target => new CellsVerificationTarget { Sheet = target.Sheet, Range = target.Range }).ToArray(),
            DirectChanges = direct, FormulaResultChanges = formulaResults, OtherChanges = other,
            FormulaErrors = errors,
            Truncated = diff.Truncated, Issues = issues,
        };
    }

    /// <summary>Completeness warnings carried into verification with their location and hint.</summary>
    internal static IEnumerable<VerificationIssue> CompletenessIssues(IReadOnlyList<Warning>? warnings) =>
        (warnings ?? []).Where(static warning => warning.AffectsCompleteness).Select(VerificationIssue.From);

    /// <summary>
    /// The targets on the sheets the output holds: every target, or for a text table those on its
    /// one sheet, by the name it has or had before the edit. The diff reads no other sheet back.
    /// </summary>
    private static IReadOnlyList<CellsEditTarget> Held(
        IReadOnlyList<CellsEditTarget> footprint, TextTableSheet? textTable, Workbook baseline)
    {
        if (textTable is null)
        {
            return footprint;
        }

        string?[] names = [textTable.Name, textTable.Before(baseline)?.Name];
        return [.. footprint.Where(target => names.Contains(target.Sheet, StringComparer.OrdinalIgnoreCase))];
    }

    private static void Classify(
        DiffComparer.Result diff,
        IReadOnlyList<CellsEditTarget> targets,
        ICollection<CellsVerifiedCellChange> direct,
        ICollection<CellsVerifiedCellChange> formulaResults,
        ICollection<CellsVerificationOtherChange> other,
        bool valuesOnly)
    {
        foreach (CellsSheetDiff sheet in diff.Sheets ?? [])
        {
            if (sheet.Status != CellsSheetChangeStatuses.Modified)
            {
                other.Add(new CellsVerificationOtherChange { Sheet = sheet.Name, Status = sheet.Status });
            }

            // A renamed sheet lists its cell changes like a modified one.
            foreach (CellsCellDiff cell in sheet.Cells ?? [])
            {
                var change = new CellsVerifiedCellChange
                {
                    Sheet = sheet.Name,
                    Cell = cell.Cell,
                    Left = cell.Left,
                    Right = cell.Right,
                };
                if (IsRequested(sheet.Name, cell.Cell, targets)
                    || (sheet.From is not null && IsRequested(sheet.From, cell.Cell, targets)))
                {
                    direct.Add(change);
                }
                else if (IsFormulaResult(cell, valuesOnly))
                {
                    formulaResults.Add(change);
                }
                else
                {
                    other.Add(new CellsVerificationOtherChange
                    {
                        Sheet = sheet.Name,
                        Status = sheet.Status,
                        Cell = cell.Cell,
                        Left = cell.Left,
                        Right = cell.Right,
                    });
                }
            }
        }
    }

    private static bool IsRequested(string sheet, string cell, IReadOnlyList<CellsEditTarget> targets)
    {
        CellRef address = A1.ParseCell(cell);
        foreach (CellsEditTarget target in targets)
        {
            // A null sheet means "active sheet". Since diff deliberately does
            // not expose that workbook state, classifying it as direct would
            // be a guess; it remains in otherChanges instead.
            if (target.Sheet is null || !string.Equals(target.Sheet, sheet, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (target.Range is null)
            {
                return true;
            }

            RangeRef range = A1.ParseRange(target.Range).Range;
            if (address.Row >= range.Start.Row && address.Row <= range.End.Row &&
                address.Column >= range.Start.Column && address.Column <= range.End.Column)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether the input cell had the same error value and the same formula, or, in an output that
    /// holds values only, held the same error value.
    /// </summary>
    private static bool IsPreexisting(CellsCellError error, Workbook baseline, Workbook candidate, bool valuesOnly)
    {
        CellRef address = A1.ParseCell(error.Cell);
        Cell? before = baseline.Worksheets[error.Sheet]?.Cells.CheckCell(address.Row, address.Column);
        Cell after = candidate.Worksheets[error.Sheet].Cells.CheckCell(address.Row, address.Column);
        return before is { Type: CellValueType.IsError }
            && string.Equals(before.StringValue, error.Error, StringComparison.Ordinal)
            && (valuesOnly || string.Equals(before.Formula, after.Formula, StringComparison.Ordinal));
    }

    /// <summary>
    /// Whether a changed cell is a formula's new result: the cell kept its formula or, in an output
    /// that holds values only, held one in the input.
    /// </summary>
    private static bool IsFormulaResult(CellsCellDiff cell, bool valuesOnly) =>
        cell.Left?.F is { } formula &&
        (valuesOnly || string.Equals(formula, cell.Right?.F, StringComparison.Ordinal));

}
