using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Product.Cells.Contracts.Addressing;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.IO;

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
        AddCompletenessIssues(sourceWarnings, issues);
        AddCompletenessIssues(candidate.Warnings(), issues);
        DiffComparer.Result diff = DiffComparer.Compare(budgets, baseline.Workbook, candidate.Workbook,
            includeFormulas: true, MaxDiffs);
        Classify(diff, footprint, direct, formulaResults, other);
        if (diff.Truncated)
        { issues.Add(new VerificationIssue { Code = "DIFF_TRUNCATED", Message = $"The edit changed more than {MaxDiffs} cells, so verification could not list every change." }); }
        (WorkbookSummary summary, Warning? truncated) = InfoProjection.Summarize(budgets, candidate.Workbook,
            candidatePath, new InfoRequest { Details = [InfoDetails.Errors] });
        AddCompletenessIssues(truncated is null ? null : [truncated], issues);
        IReadOnlyList<CellError> errors = summary.FormulaErrors ?? [];
        if (errors.Count > 0)
        { issues.Add(new VerificationIssue { Code = "FORMULA_ERRORS", Message = $"The edited workbook contains {errors.Count} formula error(s)." }); }
        return new EditVerification
        {
            Ok = issues.Count == 0,
            RequestedTargets = footprint.Select(static target => new VerificationTarget { Sheet = target.Sheet, Range = target.Range }).ToArray(),
            DirectChanges = direct, FormulaResultChanges = formulaResults, OtherChanges = other,
            FormulaErrors = errors,
            Truncated = diff.Truncated, Issues = issues.Count == 0 ? null : issues,
        };
    }

    private static void AddCompletenessIssues(
        IReadOnlyList<Warning>? warnings, ICollection<VerificationIssue> issues)
    {
        foreach (Warning warning in warnings ?? [])
        {
            if (warning.AffectsCompleteness)
            {
                issues.Add(new VerificationIssue { Code = warning.Code, Message = warning.Message });
            }
        }
    }

    private static void Classify(
        DiffComparer.Result diff,
        IReadOnlyList<CellsEditTarget> targets,
        ICollection<VerifiedCellChange> direct,
        ICollection<VerifiedCellChange> formulaResults,
        ICollection<VerificationOtherChange> other)
    {
        foreach (SheetDiff sheet in diff.Sheets ?? [])
        {
            if (sheet.Status != "modified" || sheet.Cells is null)
            {
                other.Add(new VerificationOtherChange { Sheet = sheet.Name, Status = sheet.Status });
                continue;
            }

            foreach (CellDiff cell in sheet.Cells)
            {
                var change = new VerifiedCellChange
                {
                    Sheet = sheet.Name,
                    Cell = cell.Cell,
                    Left = cell.Left,
                    Right = cell.Right,
                };
                if (IsRequested(sheet.Name, cell.Cell, targets))
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

    private static bool HasUnchangedFormulaWithChangedResult(CellDiff cell) =>
        cell.Left?.F is { } formula &&
        string.Equals(formula, cell.Right?.F, StringComparison.Ordinal);

}
