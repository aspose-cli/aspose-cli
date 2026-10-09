using Aspose.Cells;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// Owns atomic workbook persistence, text-export normalization and warnings
/// caused by target-format limits.
/// </summary>
internal sealed class CellsSavePipeline(OutputPipeline<Workbook> outputs, CellsWorkbookLoader loader)
{
    private readonly OutputPipeline<Workbook> _outputs =
        outputs ?? throw new ArgumentNullException(nameof(outputs));

    internal OutputSet<Workbook> CreateOutputSet(IEnumerable<string> directories, string operation, string? backupPath = null) =>
        _outputs.BeginSet(backupPath is null ? directories : directories.Append(Path.GetDirectoryName(backupPath)!), operation);

    internal WorkbookStagedSave Save(
        Workbook workbook, ResolvedOutput output, LicenseState licenseState, Secret? encryptPassword = null,
        FileWritePrecondition? inputPrecondition = null, bool verifyReopen = false, Secret? inputPassword = null)
    {
        WorkbookSavePlan plan = WorkbookSavePlan.Create(output.Format, licenseState, encryptPassword, inputPassword);
        using OutputSet<Workbook> transaction = CreateOutputSet([output.Directory], "cells-save", output.BackupPath);
        WorkbookStagedSave saved = Stage(transaction, workbook, plan, output, inputPrecondition, verifyReopen);
        transaction.Commit();
        return saved;
    }

    internal WorkbookStagedSave Stage(OutputSet<Workbook> transaction, Workbook workbook,
        WorkbookSavePlan plan, ResolvedOutput output, FileWritePrecondition? inputPrecondition, bool verifyReopen)
    {
        Warning? truncated = DetectGridTruncation(workbook, plan.Format);
        Warning? sheetsDropped = plan.DetectSheetLoss(workbook);
        int refsBefore = CountRefFormulas(workbook);
        StagedOutput candidate = transaction.Stage(output.Path, output.Overwrite, workbook,
            path => Produce(workbook, plan, path),
            backupPath: output.BackupPath,
            inputPrecondition: inputPrecondition,
            verify: verifyReopen ? path =>
            {
                using LoadedWorkbook reopened = loader.OpenPublishedCandidate(path, plan.OutputPassword);
                _ = reopened.Workbook.Worksheets.Count;
            } : null);
        return new WorkbookStagedSave(candidate, plan.FormatId, truncated,
            BuildBrokenFormulaWarning(refsBefore, CountRefFormulas(workbook), plan.FormatId), sheetsDropped)
        { Encrypted = plan.Encrypts };
    }

    /// <summary>
    /// Writes the workbook. An evaluation save adds its warning sheet to the in-memory workbook
    /// too and activates it, which the write pipeline reports (<see cref="CellsEvaluationProfile"/>).
    /// </summary>
    internal void Produce(Workbook workbook, WorkbookSavePlan plan, string path)
    {
        if (plan.WritesActiveSheetOnly)
        { NormalizeDatesForTextExport(plan.TextSheet(workbook)); }
        plan.Save(workbook, path);
    }

    /// <summary>
    /// Publishes a file saved from <paramref name="workbook"/>, or from the one sheet of it that
    /// <paramref name="singleSheet"/> names, which carries no evaluation mark of the workbook's.
    /// </summary>
    internal long Write(
        string outputPath,
        bool overwrite,
        Workbook workbook,
        bool singleSheet,
        Action<string> save) =>
        _outputs.Write(outputPath, overwrite, singleSheet ? null : workbook, save);

    internal void NormalizeDatesForTextExport(Worksheet sheet)
    {
        var dateCells = new List<Cell>();
        foreach (Cell cell in sheet.Cells)
        {
            if (cell.Type == CellValueType.IsDateTime)
            {
                dateCells.Add(cell);
            }
        }
        foreach (Cell cell in dateCells)
        {
            cell.PutValue(CellMapper.FormatDateTime(cell.DateTimeValue));
        }
    }

    internal Warning? DetectGridTruncation(
        Workbook workbook,
        SaveFormat saveFormat)
    {
        if (GridLimit(saveFormat) is not { } limit)
        {
            return null;
        }

        (int maxRows, int maxColumns) = limit;
        var overflowed = new List<string>();
        foreach (Worksheet sheet in workbook.Worksheets)
        {
            int rows = sheet.Cells.MaxDataRow + 1;
            int columns = sheet.Cells.MaxDataColumn + 1;
            if (rows <= maxRows && columns <= maxColumns)
            {
                continue;
            }

            var lost = new List<string>();
            if (rows > maxRows)
            {
                lost.Add($"{rows - maxRows} row(s)");
            }
            if (columns > maxColumns)
            {
                lost.Add($"{columns - maxColumns} column(s)");
            }
            overflowed.Add(
                $"'{sheet.Name}' ({rows}×{columns}) loses {string.Join(" and ", lost)}");
        }

        if (overflowed.Count == 0)
        {
            return null;
        }
        return new Warning(CellsDiagnostics.DataTruncated, $"The target format's grid holds at most {maxRows} rows × {maxColumns} columns, "
                + $"so data beyond it was discarded: {string.Join("; ", overflowed)}.")
        {
            AffectsCompleteness = true,
            Hint = "Convert to a modern format (xlsx, xlsb, ods) to keep every row and column.",
        };
    }

    internal int CountRefFormulas(Workbook workbook)
    {
        int count = 0;
        foreach (Worksheet sheet in workbook.Worksheets)
        {
            foreach (Cell cell in sheet.Cells)
            {
                if (cell.IsFormula
                    && cell.Formula.Contains("#REF!", StringComparison.Ordinal))
                {
                    count++;
                }
            }
        }
        return count;
    }

    internal Warning? BuildBrokenFormulaWarning(
        int refsBefore,
        int refsAfter,
        string formatId)
    {
        int newlyBroken = refsAfter - refsBefore;
        if (newlyBroken <= 0)
        {
            return null;
        }
        return new Warning(CellsDiagnostics.FormulasBroken, $"{newlyBroken} formula(s) referenced cells beyond the {formatId} grid and became #REF! "
                + "(for example a whole-column total like =SUM(A5:A1048576) downconverted to the smaller "
                + "65,536-row xls grid). The cached values are kept, but the live formulas are broken.")
        {
            AffectsCompleteness = true,
            Hint = "Convert to a modern format (xlsx, xlsb) to preserve every formula.",
        };
    }

    private static (int MaxRows, int MaxColumns)? GridLimit(SaveFormat format) =>
        format switch
        {
            SaveFormat.Excel97To2003 => (65536, 256),
            _ => null,
        };
}

internal sealed record WorkbookStagedSave(StagedOutput Candidate, string Format,
    Warning? Truncated, Warning? FormulasBroken, Warning? SheetsDropped)
{
    /// <summary>Whether the save encrypted the output with a password.</summary>
    internal bool Encrypted { get; init; }

    internal OutputInfo Output => new()
    { Path = Candidate.TargetPath, Format = Format, SizeBytes = Candidate.SizeBytes, Fingerprint = Candidate.Fingerprint, Encrypted = Encrypted };
    internal BackupInfo? Backup => Candidate.Backup;
}
