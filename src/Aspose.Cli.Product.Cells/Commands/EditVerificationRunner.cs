using Aspose.Cli.Product.Cells.Addressing;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Preview;

namespace Aspose.Cli.Product.Cells.Commands;

/// <summary>Runs the bounded post-edit evidence loop behind <c>cells edit --verify</c>.</summary>
internal static class EditVerificationRunner
{
    private const int MaxDiffs = 1000;

    public static string CaptureBaseline(string inputPath)
    {
        string directory = Path.Combine(Path.GetTempPath(), "aspose-cli", "verify-baselines");
        string path = Path.Combine(directory, Guid.NewGuid().ToString("N") + Path.GetExtension(inputPath));
        try
        {
            Directory.CreateDirectory(directory);
            File.Copy(inputPath, path, overwrite: false);
            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw CliErrors.OutputUnwritable(
                inputPath,
                "could not capture the temporary verification baseline",
                ex,
                phase: "backup");
        }
    }

    public static void DeleteBaseline(string? path)
    {
        if (path is null)
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (Exception)
        {
            // The baseline is internal scratch data; cleanup must not replace
            // a successful edit result with a new failure.
        }
    }

    public static EditVerification Run(
        ProductCommandContext<IWorkbookEngine> context,
        string baselinePath,
        string outputPath,
        OpsBatch successfulBatch,
        string verificationDirectory,
        string? leftPassword,
        string? rightPassword)
    {
        IReadOnlyList<CellsPreviewHint> footprint = OpsFootprint.Collect(successfulBatch);
        IReadOnlyList<VerificationTarget> targets = footprint
            .Select(static target => new VerificationTarget { Sheet = target.Sheet, Range = target.Range })
            .ToArray();
        var direct = new List<VerifiedCellChange>();
        var formulaResults = new List<VerifiedCellChange>();
        var other = new List<VerificationOtherChange>();
        var formulaErrors = new List<CellError>();
        var renders = new List<SheetRenderOutput>();
        var issues = new List<VerificationIssue>();
        bool truncated = RunDiff(
            context, baselinePath, outputPath, leftPassword, rightPassword,
            footprint, direct, formulaResults, other, issues);
        ScanFormulaErrors(context, outputPath, rightPassword, formulaErrors, issues);
        RenderEvidence(context, outputPath, verificationDirectory, rightPassword, renders, issues);

        return new EditVerification
        {
            Ok = issues.Count == 0,
            RequestedTargets = targets,
            DirectChanges = direct,
            FormulaResultChanges = formulaResults,
            OtherChanges = other,
            FormulaErrors = formulaErrors,
            Renders = renders,
            VisualReviewRequired = renders.Count > 0,
            Truncated = truncated,
            Issues = issues.Count == 0 ? null : issues,
        };
    }

    private static bool RunDiff(
        ProductCommandContext<IWorkbookEngine> context,
        string baselinePath,
        string outputPath,
        string? leftPassword,
        string? rightPassword,
        IReadOnlyList<CellsPreviewHint> footprint,
        ICollection<VerifiedCellChange> direct,
        ICollection<VerifiedCellChange> formulaResults,
        ICollection<VerificationOtherChange> other,
        ICollection<VerificationIssue> issues)
    {
        try
        {
            DiffResult diff = context.Port.Diff(baselinePath, outputPath, new DiffRequest
            {
                Scope = DiffScope.Formulas,
                MaxDiffs = MaxDiffs,
                LeftPassword = leftPassword,
                RightPassword = rightPassword,
            });
            Classify(diff, footprint, direct, formulaResults, other);
            if (diff.Truncated)
            {
                issues.Add(new VerificationIssue
                {
                    Code = "DIFF_TRUNCATED",
                    Message = $"The edit changed more than {MaxDiffs} cells, so verification could not list every change.",
                });
            }
            return diff.Truncated;
        }
        catch (Exception ex)
        {
            issues.Add(Issue("VERIFY_DIFF_FAILED", "Workbook diff failed", ex));
            return false;
        }
    }

    private static void ScanFormulaErrors(
        ProductCommandContext<IWorkbookEngine> context,
        string outputPath,
        string? password,
        ICollection<CellError> errors,
        ICollection<VerificationIssue> issues)
    {
        try
        {
            WorkbookInfoResult info = context.Port.GetInfo(outputPath, new InfoRequest
            {
                Details = [InfoDetails.Errors],
                Password = password,
            });
            foreach (CellError error in info.Workbook.FormulaErrors ?? [])
            {
                errors.Add(error);
            }
            if (errors.Count > 0)
            {
                issues.Add(new VerificationIssue
                {
                    Code = "FORMULA_ERRORS",
                    Message = $"The edited workbook contains {errors.Count} formula error(s).",
                });
            }
        }
        catch (Exception ex)
        {
            issues.Add(Issue("VERIFY_FORMULA_SCAN_FAILED", "Formula-error scanning failed", ex));
        }
    }

    private static void RenderEvidence(
        ProductCommandContext<IWorkbookEngine> context,
        string outputPath,
        string verificationDirectory,
        string? password,
        ICollection<SheetRenderOutput> renders,
        ICollection<VerificationIssue> issues)
    {
        try
        {
            Directory.CreateDirectory(verificationDirectory);
            string renderBase = Path.Combine(
                verificationDirectory,
                Path.GetFileNameWithoutExtension(outputPath) + ".png");
            RenderResult render = context.Port.Render(outputPath, new RenderRequest
            {
                TargetFormatId = "png",
                OutputPath = renderBase,
                Overwrite = true,
                AllSheets = true,
                Dpi = 192,
                Password = password,
            });
            AddRenderOutputs(render, renders);
            foreach (Warning warning in render.Warnings ?? [])
            {
                if (warning.Code == CellsDiagnostics.SheetsSkipped)
                {
                    issues.Add(new VerificationIssue { Code = warning.Code, Message = warning.Message });
                }
            }
        }
        catch (Exception ex)
        {
            issues.Add(Issue("VERIFY_RENDER_FAILED", "Visible-sheet rendering failed", ex));
        }
    }

    private static void AddRenderOutputs(RenderResult render, ICollection<SheetRenderOutput> renders)
    {
        if (render.Outputs is { } outputs)
        {
            foreach (SheetRenderOutput output in outputs)
            {
                renders.Add(output);
            }
            return;
        }

        renders.Add(new SheetRenderOutput
        {
            Sheet = render.Sheet,
            Path = render.Output.Path,
            SizeBytes = render.Output.SizeBytes,
        });
    }

    private static void Classify(
        DiffResult diff,
        IReadOnlyList<CellsPreviewHint> targets,
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

    private static bool IsRequested(string sheet, string cell, IReadOnlyList<CellsPreviewHint> targets)
    {
        CellRef address = A1.ParseCell(cell);
        foreach (CellsPreviewHint target in targets)
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
        string.Equals(formula, cell.Right?.F, StringComparison.Ordinal) &&
        !Equals(cell.Left?.V, cell.Right?.V);

    private static VerificationIssue Issue(string fallbackCode, string stage, Exception ex) => ex switch
    {
        CliException cli => new VerificationIssue { Code = cli.Code.Name, Message = $"{stage}: {cli.Message}" },
        _ => new VerificationIssue { Code = fallbackCode, Message = $"{stage}: {ex.Message}" },
    };
}
