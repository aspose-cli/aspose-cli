using Aspose.Cells;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Diagnostics;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.Operations;
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
        Secret? outputPassword, CellsOpsBatch batch, string activeSheet, AppliedBatch applied,
        IReadOnlyList<Warning>? warnings, TextTableSheet? textTable)
    {
        using LoadedWorkbook baseline = loader.OpenPublishedCandidate(baselinePath, inputPassword, originalPath);
        using LoadedWorkbook candidate = loader.OpenPublishedCandidate(candidatePath, outputPassword);
        // A text table reads back named after its file and holds no formulas: its sheet keeps the
        // name it was written from, and its values alone are compared.
        textTable?.Rename(candidate.Workbook);
        bool valuesOnly = textTable is not null;
        if (textTable is not null)
        {
            CellsTextImport.TypeErrors(candidate.Workbook.Worksheets[0], textTable.Before(baseline.Workbook));
        }
        // Targets as the batch found the workbook before its first operation: an operation that
        // names no sheet ran on the sheet active then. The changes are classified, and the
        // operations checked, by the same targets of the operations the diff compares.
        IReadOnlyList<CellsEditTarget> footprint = OpsFootprint.Collect(batch, activeSheet);
        IReadOnlyList<CellsEditTarget> written = OpsFootprint.Collect(batch, activeSheet, cellsOnly: true);
        IReadOnlyList<CellsEditTarget> rewritten = OpsFootprint.Rewritten(batch, written, activeSheet);
        var direct = new List<CellsVerifiedCellChange>();
        var formulaResults = new List<CellsVerifiedCellChange>();
        var other = new List<CellsVerificationOtherChange>();
        var issues = new List<VerificationIssue>();
        DiffComparer.Result diff = textTable is null
            ? DiffComparer.Compare(budgets, baseline.Workbook, candidate.Workbook, includeFormulas: true, MaxDiffs)
            : DiffComparer.CompareTextTable(budgets, baseline.Workbook, candidate.Workbook, textTable, MaxDiffs);
        // The change lists are evidence capped at MaxDiffs cells (Truncated); they decide nothing,
        // so a large edit such as a sort still passes when the checks below find no issue.
        Classify(diff, written, rewritten, direct, formulaResults, other, valuesOnly);
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
        IReadOnlyList<CellsEditTarget> held = Held(written, textTable, baseline.Workbook);
        VerifiedBatch verified = applied.Verified(new VerificationFacts(issues, OpsFootprint.Checked(batch, held, activeSheet))
        {
            Warnings = EnvelopeParts.CombineWarnings(warnings, candidate.Warnings()),
            UncheckedReasons = OpsFootprint.UncheckedReasons(batch, written, held, activeSheet),
        });
        return new CellsEditVerification
        {
            Ok = verified.Ok,
            Issues = verified.Issues,
            CheckedOps = verified.CheckedOps,
            UncheckedOps = verified.UncheckedOps,
            RequestedTargets = footprint.Select(static target => new CellsVerificationTarget { Sheet = target.Sheet, Range = target.Range }).ToArray(),
            DirectChanges = direct, FormulaResultChanges = formulaResults, OtherChanges = other,
            FormulaErrors = errors,
            Truncated = diff.Truncated,
        };
    }

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

    /// <summary>
    /// Sorts each changed cell into the direct changes, inside a target an operation wrote, the
    /// formula results, and the other changes. A cell that kept its formula, or held one in an
    /// output that holds values only, is a direct change only inside a target whose every cell an
    /// operation writes (<paramref name="rewritten"/>): a replace_text that found nothing in it, or
    /// a rename of its sheet, did not write its new result.
    /// </summary>
    private static void Classify(
        DiffComparer.Result diff,
        IReadOnlyList<CellsEditTarget> targets,
        IReadOnlyList<CellsEditTarget> rewritten,
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
                IReadOnlyList<CellsEditTarget> writers = IsFormulaResult(cell, valuesOnly) ? rewritten : targets;
                if (IsRequested(sheet.Name, cell.Cell, writers)
                    || (sheet.From is not null && IsRequested(sheet.From, cell.Cell, writers)))
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
