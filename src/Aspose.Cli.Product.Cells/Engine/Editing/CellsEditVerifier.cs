using Aspose.Cells;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Product.Cells.Contracts.Addressing;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Results;

namespace Aspose.Cli.Product.Cells.Engine.Editing;

/// <summary>Verifies the exact staged workbook against its pre-edit baseline.</summary>
internal sealed class CellsEditVerifier(CellsWorkbookLoader loader, ResourceBudgetLedger budgets)
{
    private const int MaxDiffs = 1000;

    internal EditVerification Verify(StagedOutput staged,
        string baselinePath, string originalPath, string? inputPassword, string? outputPassword,
        CellsOpsBatch batch, IReadOnlyList<Warning>? sourceWarnings) =>
        staged.Read(candidate => VerifyCandidate(candidate, baselinePath,
            originalPath, inputPassword, outputPassword, batch, sourceWarnings));

    private EditVerification VerifyCandidate(string candidatePath,
        string baselinePath, string originalPath, string? inputPassword,
        string? outputPassword, CellsOpsBatch batch, IReadOnlyList<Warning>? sourceWarnings)
    {
        using LoadedWorkbook baseline = loader.OpenPublishedCandidate(baselinePath, inputPassword, originalPath);
        using LoadedWorkbook candidate = loader.OpenPublishedCandidate(candidatePath, outputPassword);
        IReadOnlyList<CellsEditTarget> footprint = OpsFootprint.Collect(batch);
        var direct = new List<VerifiedCellChange>();
        var formulaResults = new List<VerifiedCellChange>();
        var other = new List<VerificationOtherChange>();
        var issues = new List<VerificationIssue>();
        issues.AddRange(CompletenessIssues(sourceWarnings));
        issues.AddRange(CompletenessIssues(candidate.Warnings()));
        DiffComparer.Result diff = DiffComparer.Compare(budgets, baseline.Workbook, candidate.Workbook,
            includeFormulas: true, MaxDiffs);
        // The change lists are evidence capped at MaxDiffs cells (Truncated); they decide nothing,
        // so a large edit such as a sort still passes when the checks below find no issue.
        Classify(diff, footprint, direct, formulaResults, other);
        (IReadOnlyList<CellError> errors, int errorTotal) = InfoProjection.ScanFormulaErrors(budgets, candidate.Workbook);
        errors = errors.Select(error => IsPreexisting(error, baseline.Workbook, candidate.Workbook)
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
            issues.Add(VerificationIssue.Of(CellsDiagnostics.FormulaErrors,
                (errorsCapped
                    ? $"The edited workbook contains {errorTotal} formula error(s); formulaErrors lists the first {errors.Count}."
                    : $"The edited workbook contains {errorTotal} formula error(s).")
                + (preexisting > 0 ? $" {preexisting} of the listed error(s) were already in the input." : ""),
                location: errorTotal == 1 ? Sheets.QuotedName(errors[0].Sheet) + "!" + errors[0].Cell : null,
                hint: (errorsCapped
                    ? "Fix the cells listed in formulaErrors, then edit again with --verify to list the rest."
                    : "Fix the cells listed in formulaErrors, then edit again with --verify.")
                    + (errors.Any(static error => error.Error == "#NAME?")
                        ? " #NAME? means a formula names a function or name the workbook does not know. " + CellOps.EnglishFormulaHint
                        : "")));
        }
        return new EditVerification
        {
            Ok = issues.Count == 0,
            RequestedTargets = footprint.Select(static target => new VerificationTarget { Sheet = target.Sheet, Range = target.Range }).ToArray(),
            DirectChanges = direct, FormulaResultChanges = formulaResults, OtherChanges = other,
            FormulaErrors = errors,
            Truncated = diff.Truncated, Issues = issues,
        };
    }

    /// <summary>Completeness warnings carried into verification with their location and hint.</summary>
    internal static IEnumerable<VerificationIssue> CompletenessIssues(IReadOnlyList<Warning>? warnings) =>
        (warnings ?? []).Where(static warning => warning.AffectsCompleteness).Select(VerificationIssue.From);

    private static void Classify(
        DiffComparer.Result diff,
        IReadOnlyList<CellsEditTarget> targets,
        ICollection<VerifiedCellChange> direct,
        ICollection<VerifiedCellChange> formulaResults,
        ICollection<VerificationOtherChange> other)
    {
        foreach (SheetDiff sheet in diff.Sheets ?? [])
        {
            if (sheet.Status != "modified")
            {
                other.Add(new VerificationOtherChange { Sheet = sheet.Name, Status = sheet.Status });
            }

            // A renamed sheet lists its cell changes like a modified one.
            foreach (CellDiff cell in sheet.Cells ?? [])
            {
                var change = new VerifiedCellChange
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
                else if (HasUnchangedFormulaWithChangedResult(cell))
                {
                    formulaResults.Add(change);
                }
                else
                {
                    other.Add(new VerificationOtherChange
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

    /// <summary>Whether the input cell had the same formula and the same error value.</summary>
    private static bool IsPreexisting(CellError error, Workbook baseline, Workbook candidate)
    {
        CellRef address = A1.ParseCell(error.Cell);
        Cell? before = baseline.Worksheets[error.Sheet]?.Cells.CheckCell(address.Row, address.Column);
        Cell after = candidate.Worksheets[error.Sheet].Cells.CheckCell(address.Row, address.Column);
        return before is { Type: CellValueType.IsError }
            && string.Equals(before.StringValue, error.Error, StringComparison.Ordinal)
            && string.Equals(before.Formula, after.Formula, StringComparison.Ordinal);
    }

    private static bool HasUnchangedFormulaWithChangedResult(CellDiff cell) =>
        cell.Left?.F is { } formula &&
        string.Equals(formula, cell.Right?.F, StringComparison.Ordinal);

}
