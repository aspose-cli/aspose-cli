using Aspose.Cells;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// Owns atomic workbook persistence, text-export normalization and warnings
/// caused by target-format limits.
/// </summary>
internal sealed class CellsSavePipeline(SafeFileWriter writer, CellsWorkbookLoader loader)
{
    private readonly SafeFileWriter _writer =
        writer ?? throw new ArgumentNullException(nameof(writer));

    internal AtomicOutputSetWriter CreateOutputSet(IEnumerable<string> directories, string operation, string? backupPath = null) =>
        new(_writer, backupPath is null ? directories : directories.Append(Path.GetDirectoryName(backupPath)!), operation);

    internal (OutputInfo Output, BackupInfo? Backup, Warning? Truncated, Warning? FormulasBroken, Warning? SheetsDropped) Save(
        Workbook workbook, string outputPath, bool overwrite, LicenseState licenseState, string? encryptPassword = null,
        string? backupPath = null, FileWritePrecondition? inputPrecondition = null,
        bool verifyReopen = false, string? inputPassword = null)
    {
        WorkbookSavePlan plan = WorkbookSavePlan.Create(CellsFormats.ForOutputPath(outputPath), outputPath, licenseState, encryptPassword, inputPassword);
        using AtomicOutputSetWriter transaction = CreateOutputSet([Path.GetDirectoryName(outputPath)!], "cells-save", backupPath);
        WorkbookStagedSave saved = Stage(transaction, workbook, plan, outputPath, overwrite, backupPath, inputPrecondition, verifyReopen);
        transaction.Commit();
        return (saved.Output, saved.Backup, saved.Truncated, saved.FormulasBroken, saved.SheetsDropped);
    }

    internal WorkbookStagedSave Stage(AtomicOutputSetWriter transaction, Workbook workbook,
        WorkbookSavePlan plan, string outputPath, bool overwrite, string? backupPath,
        FileWritePrecondition? inputPrecondition, bool verifyReopen)
    {
        Warning? truncated = DetectGridTruncation(workbook, plan.Format);
        Warning? sheetsDropped = plan.DetectSheetLoss(workbook);
        int refsBefore = CountRefFormulas(workbook);
        StagedOutput candidate = transaction.Stage(outputPath, overwrite, backupPath, inputPrecondition,
            path => Produce(workbook, plan, path),
            verify: verifyReopen ? path =>
            {
                using LoadedWorkbook reopened = loader.OpenPublishedCandidate(path, plan.OutputPassword);
                _ = reopened.Workbook.Worksheets.Count;
            } : null);
        return new WorkbookStagedSave(candidate, plan.FormatId, truncated,
            BuildBrokenFormulaWarning(refsBefore, CountRefFormulas(workbook), plan.FormatId), sheetsDropped);
    }

    internal void Produce(Workbook workbook, WorkbookSavePlan plan, string path)
    {
        if (plan.FormatId is "csv" or "tsv" or "md")
        { NormalizeDatesForTextExport(plan.TextSheet(workbook)); }
        plan.Save(workbook, path);
    }

    internal long Write(
        string outputPath,
        bool overwrite,
        Action<string> save) =>
        _writer.Write(outputPath, overwrite, save);

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
        return new Warning
        {
            Code = CellsDiagnostics.DataTruncated,
            AffectsCompleteness = true,
            Message =
                $"The target format's grid holds at most {maxRows} rows × {maxColumns} columns, "
                + $"so data beyond it was discarded: {string.Join("; ", overflowed)}.",
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
        return new Warning
        {
            Code = CellsDiagnostics.FormulasBroken,
            AffectsCompleteness = true,
            Message =
                $"{newlyBroken} formula(s) referenced cells beyond the {formatId} grid and became #REF! "
                + "(for example a whole-column total like =SUM(A5:A1048576) downconverted to the smaller "
                + "65,536-row xls grid). The cached values are kept, but the live formulas are broken.",
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
    internal OutputInfo Output => new()
    { Path = Candidate.TargetPath, Format = Format, SizeBytes = Candidate.SizeBytes, Fingerprint = Candidate.Fingerprint };
    internal BackupInfo? Backup => Candidate.Backup is { } backup
        ? new BackupInfo { Path = backup.Path, Created = backup.Created, SizeBytes = backup.SizeBytes } : null;
}
