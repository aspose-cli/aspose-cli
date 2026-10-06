using System.Text;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cells;
using Aspose.Cells.Rendering;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>Product-owned format and encryption decisions for one workbook output.</summary>
internal sealed record WorkbookSavePlan(string FormatId, SaveFormat Format, SaveOptions? Options, string? OutputPassword, Warning? EncryptionWarning, bool IsEvaluation)
{
    /// <remarks>
    /// The commands reject an output password for a format that cannot carry one, naming the
    /// option the caller passed (<see cref="Sdk.Extensibility.Commanding.StandardInvocation.EncryptPassword"/>).
    /// </remarks>
    internal static WorkbookSavePlan Create(string formatId, LicenseState licenseState,
        string? encryptPassword = null, string? inputPassword = null, int? selectedSheet = null, bool byteOrderMark = false)
    {
        bool encryptable = CellsFormats.EncryptableIds.Contains(formatId, StringComparer.Ordinal);
        if (encryptPassword is not null && !encryptable)
        {
            throw new InvalidOperationException($"An output password reached the '{formatId}' format, which cannot carry one.");
        }

        SaveFormat format = FormatMapper.ToSaveFormat(formatId);
        SaveOptions? options = formatId switch
        {
            "html" => new HtmlSaveOptions { SaveAsSingleFile = true, ExportImagesAsBase64 = true },
            "csv" or "tsv" => new TxtSaveOptions(format)
            {
                FormatStrategy = CellValueFormatStrategy.None, TrimLeadingBlankRowAndColumn = false,
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: byteOrderMark),
            },
            "md" => new MarkdownSaveOptions { FormatStrategy = CellValueFormatStrategy.None },
            "pdf" when selectedSheet is { } sheet => new PdfSaveOptions { SheetSet = new SheetSet([sheet]) },
            _ => null,
        };
        string? password = encryptPassword ?? (encryptable ? inputPassword : null);
        Warning? warning = inputPassword is not null && password is null ? new Warning
        {
            Code = CellsDiagnostics.EncryptionRemoved,
            Message = $"The '{formatId}' output cannot retain the source workbook encryption.",
            Hint = "Use an encryption-capable spreadsheet output to keep password protection.",
        } : null;
        return new WorkbookSavePlan(formatId, format, options, password, warning, licenseState == LicenseState.Evaluation);
    }

    /// <summary>True for the text formats (csv, tsv, md), which write only the active sheet.</summary>
    internal static bool WritesActiveSheetOnlyFor(string formatId) => formatId is "csv" or "tsv" or "md";

    /// <inheritdoc cref="WritesActiveSheetOnlyFor(string)"/>
    internal bool WritesActiveSheetOnly => WritesActiveSheetOnlyFor(FormatId);

    /// <summary>Whether the output carries a password.</summary>
    internal bool Encrypts => OutputPassword is not null;

    internal Warning? DetectSheetLoss(Workbook workbook) =>
        WritesActiveSheetOnly && workbook.Worksheets.Count > 1 ? new Warning
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
