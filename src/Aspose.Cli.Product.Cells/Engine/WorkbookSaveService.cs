using Aspose.Cells;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Cells.Engine;

/// <summary>
/// Owns atomic workbook persistence, text-export normalization and warnings
/// caused by target-format limits.
/// </summary>
internal sealed class WorkbookSaveService(SafeFileWriter writer)
{
    private static readonly HashSet<string> EncryptableFormats =
        new(StringComparer.Ordinal)
        {
            "xlsx",
            "xlsm",
            "xlsb",
            "xls",
            "ods",
        };

    private readonly SafeFileWriter _writer =
        writer ?? throw new ArgumentNullException(nameof(writer));

    internal (
        OutputInfo Output,
        BackupInfo? Backup,
        Warning? Truncated,
        Warning? FormulasBroken) Save(
            Workbook workbook,
            string outputPath,
            bool overwrite,
            string? encryptPassword = null,
            string? backupPath = null,
            FileWritePrecondition? inputPrecondition = null,
            bool verifyReopen = false)
    {
        string extension = Path.GetExtension(outputPath);
        string formatId = extension.Length > 1
            ? CellsFormats.ResolveConvert(extension).Id
            : "xlsx";
        SaveFormat saveFormat = FormatMapper.ToSaveFormat(formatId);
        Warning? truncated = DetectGridTruncation(workbook, saveFormat);

        if (encryptPassword is not null)
        {
            if (!EncryptableFormats.Contains(formatId))
            {
                throw CliErrors.OptionInvalid(
                    "--encrypt",
                    $"the '{formatId}' format cannot be password-protected",
                    "Encrypt only spreadsheet outputs (xlsx, xlsm, xlsb, xls, ods).");
            }
            workbook.Settings.Password = encryptPassword;
        }

        int refsBefore = CountRefFormulas(workbook);
        SafeWriteResult write = _writer.Write(
            outputPath,
            overwrite,
            backupPath,
            inputPrecondition,
            temporaryPath =>
            {
                workbook.Save(temporaryPath, saveFormat);
                if (verifyReopen)
                {
                    using var reopened = new Workbook(
                        temporaryPath,
                        new LoadOptions { Password = encryptPassword });
                    _ = reopened.Worksheets.Count;
                }
            });
        Warning? formulasBroken = BuildBrokenFormulaWarning(
            refsBefore,
            CountRefFormulas(workbook),
            formatId);
        BackupInfo? backup = write.Backup is { } saved
            ? new BackupInfo
            {
                Path = saved.Path,
                Created = saved.Created,
                SizeBytes = saved.SizeBytes,
            }
            : null;
        return (
            new OutputInfo
            {
                Path = outputPath,
                Format = formatId,
                SizeBytes = write.SizeBytes,
                Fingerprint = FileFingerprints.Capture(outputPath),
            },
            backup,
            truncated,
            formulasBroken);
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
