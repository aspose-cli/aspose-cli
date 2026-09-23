using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cells;
using Aspose.Cells.Rendering;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Cells.Engine;

/// <summary>Product-owned format and encryption decisions for one workbook output.</summary>
internal sealed record WorkbookSavePlan(string FormatId, SaveFormat Format, SaveOptions? Options, string? OutputPassword, Warning? EncryptionWarning, bool IsEvaluation)
{
    private static readonly HashSet<string> EncryptableFormats = new(StringComparer.Ordinal)
    { "xlsx", "xlsm", "xlsb", "xls", "ods" };

    internal static WorkbookSavePlan Create(string formatId, string outputPath, LicenseState licenseState,
        string? encryptPassword = null, string? inputPassword = null, int? selectedSheet = null)
    {
        if (encryptPassword is not null && !EncryptableFormats.Contains(formatId))
        { throw CliErrors.OptionInvalid("--encrypt", $"the '{formatId}' format cannot be password-protected", "Encrypt only spreadsheet outputs (xlsx, xlsm, xlsb, xls, ods)."); }
        SaveFormat format = FormatMapper.ToSaveFormat(formatId, outputPath);
        SaveOptions? options = formatId switch
        {
            "html" => new HtmlSaveOptions { SaveAsSingleFile = true, ExportImagesAsBase64 = true },
            "csv" or "tsv" => new TxtSaveOptions(format)
            { FormatStrategy = CellValueFormatStrategy.None, TrimLeadingBlankRowAndColumn = false },
            "md" => new MarkdownSaveOptions { FormatStrategy = CellValueFormatStrategy.None },
            "pdf" when selectedSheet is { } sheet => new PdfSaveOptions { SheetSet = new SheetSet([sheet]) },
            _ => null,
        };
        string? password = encryptPassword ?? (EncryptableFormats.Contains(formatId) ? inputPassword : null);
        Warning? warning = inputPassword is not null && password is null ? new Warning
        {
            Code = CellsDiagnostics.EncryptionRemoved,
            Message = $"The '{formatId}' output cannot retain the source workbook encryption.",
            Hint = "Use an encryption-capable spreadsheet output to keep password protection.",
        } : null;
        return new WorkbookSavePlan(formatId, format, options, password, warning, licenseState == LicenseState.Evaluation);
    }

    /// <summary>The convert format whose id, alias or declared extension the path carries; xlsx without one.</summary>
    internal static string FormatForPath(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Length > 1 ? CellsFormats.ResolveConvert(extension).Id : "xlsx";
    }

    internal Warning? DetectSheetLoss(Workbook workbook) =>
        FormatId is "csv" or "tsv" or "md" && workbook.Worksheets.Count > 1 ? new Warning
        {
            Code = CellsDiagnostics.SheetsDropped,
            Message = $"Only worksheet '{TextSheet(workbook).Name}' was exported; the '{FormatId}' output holds one worksheet, so {workbook.Worksheets.Count - 1} other worksheet(s) were not written.",
            Hint = "Use a multi-sheet format (xlsx, xlsb, ods, pdf) to retain every worksheet.",
            AffectsCompleteness = true,
        } : null;

    internal Worksheet TextSheet(Workbook workbook) =>
        workbook.Worksheets[IsEvaluation ? 0 : workbook.Worksheets.ActiveSheetIndex];

    internal void Save(Workbook workbook, string path)
    {
        workbook.Settings.Password = OutputPassword ?? string.Empty;
        if (Options is not null) { workbook.Save(path, Options); }
        else { workbook.Save(path, Format); }
    }
}
