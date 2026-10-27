using Aspose.Cells;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.IO.Publication;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// Owns atomic workbook persistence, text-export normalization and warnings
/// caused by target-format limits.
/// </summary>
internal sealed class CellsSavePipeline(OutputPipeline<Workbook> outputs)
{
    private readonly OutputPipeline<Workbook> _outputs =
        outputs ?? throw new ArgumentNullException(nameof(outputs));

    internal WorkbookSave Save(Workbook workbook, ResolvedOutput output, LicenseState licenseState, OutputProtection protection)
    {
        WorkbookSavePlan plan = WorkbookSavePlan.Create(output.Format, licenseState, protection);
        using OutputSet<Workbook> transaction = _outputs.BeginSet([output.Directory], "cells-save");
        WorkbookSaveLoss? loss = null;
        StagedOutput candidate = transaction.Stage(output.Path, output.Format, output.Overwrite, workbook,
            path => loss = ProduceReporting(workbook, plan, path), encrypted: plan.Encrypts);
        transaction.Commit();
        return new WorkbookSave(FileObjects.Output(candidate.Published, output.Format, plan.Encrypts), loss!);
    }

    /// <summary>
    /// Writes the workbook (<see cref="Produce"/>) and names what the target format's limits
    /// lost: the data beyond its grid, the formulas that became <c>#REF!</c> and the sheets it
    /// cannot hold.
    /// </summary>
    internal WorkbookSaveLoss ProduceReporting(Workbook workbook, WorkbookSavePlan plan, string path)
    {
        Warning? truncated = DetectGridTruncation(workbook, plan.Format);
        Warning? sheetsDropped = plan.DetectSheetLoss(workbook);
        int refsBefore = CountRefFormulas(workbook);
        Produce(workbook, plan, path);
        return new WorkbookSaveLoss(truncated,
            BuildBrokenFormulaWarning(refsBefore, CountRefFormulas(workbook), plan.FormatId), sheetsDropped);
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
    /// Publishes <paramref name="output"/> saved from <paramref name="workbook"/>, or from the one
    /// sheet of it that <paramref name="singleSheet"/> names, which carries no evaluation mark of
    /// the workbook's; <paramref name="encrypted"/> says whether the file carries a password.
    /// </summary>
    internal PublishedFile Write(
        ResolvedOutput output,
        Workbook workbook,
        bool singleSheet,
        bool encrypted,
        Action<string> save) =>
        _outputs.Write(output.Path, output.Format, output.Overwrite, singleSheet ? null : workbook, save, encrypted: encrypted);

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

/// <summary>What a save lost to its target format's limits, in the order a result reports it.</summary>
internal sealed record WorkbookSaveLoss(Warning? Truncated, Warning? FormulasBroken, Warning? SheetsDropped)
{
    internal Warning?[] Warnings => [Truncated, FormulasBroken, SheetsDropped];
}

/// <summary>A published workbook and what its format's limits lost.</summary>
internal sealed record WorkbookSave(OutputInfo Output, WorkbookSaveLoss Loss);
