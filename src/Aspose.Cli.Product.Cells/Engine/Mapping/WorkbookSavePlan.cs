using System.Text;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cells;
using Aspose.Cells.Rendering;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// The engine save format and options of one workbook output, and the password it carries
/// (<see cref="OutputProtection"/>).
/// </summary>
internal sealed record WorkbookSavePlan(string FormatId, SaveFormat Format, SaveOptions? Options, OutputProtection Protection, bool IsEvaluation)
{
    internal static WorkbookSavePlan Create(FormatDescriptor output, LicenseState licenseState,
        OutputProtection protection, IReadOnlyList<int>? selectedSheets = null, bool byteOrderMark = false)
    {
        string formatId = output.Id;
        SaveFormat format = CellsEngineFormats.Save(formatId);
        SaveOptions? options = formatId switch
        {
            "html" => new HtmlSaveOptions { SaveAsSingleFile = true, ExportImagesAsBase64 = true },
            "csv" or "tsv" => new TxtSaveOptions(format)
            {
                FormatStrategy = CellValueFormatStrategy.None, TrimLeadingBlankRowAndColumn = false,
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: byteOrderMark),
            },
            "md" => new MarkdownSaveOptions { FormatStrategy = CellValueFormatStrategy.None },
            "pdf" when selectedSheets is { } sheets => new PdfSaveOptions { SheetSet = new SheetSet([.. sheets]) },
            _ => null,
        };
        return new WorkbookSavePlan(formatId, format, options, protection, licenseState == LicenseState.Evaluation);
    }

    /// <summary>True for the text formats (csv, tsv, md), which write only the active sheet.</summary>
    internal static bool WritesActiveSheetOnlyFor(string formatId) => CellsFormats.ActiveSheetOnly.Contains(formatId);

    /// <inheritdoc cref="WritesActiveSheetOnlyFor(string)"/>
    internal bool WritesActiveSheetOnly => WritesActiveSheetOnlyFor(FormatId);

    /// <summary>Whether the output carries a password.</summary>
    internal bool Encrypts => Protection.Encrypted;

    internal Warning? DetectSheetLoss(Workbook workbook) =>
        WritesActiveSheetOnly && workbook.Worksheets.Count > 1 ? new Warning(CellsDiagnostics.SheetsDropped, $"Only worksheet '{TextSheet(workbook).Name}' was exported; the '{FormatId}' output holds one worksheet, so {workbook.Worksheets.Count - 1} other worksheet(s) were not written.")
        {
            Hint = "Use a multi-sheet format (xlsx, xlsb, ods, pdf) to retain every worksheet.",
            AffectsCompleteness = true,
        } : null;

    internal Worksheet TextSheet(Workbook workbook) =>
        workbook.Worksheets[IsEvaluation ? 0 : workbook.Worksheets.ActiveSheetIndex];

    internal void Save(Workbook workbook, string path)
    {
        workbook.Settings.Password = Protection.Password?.Reveal() ?? string.Empty;
        if (Options is not null) { workbook.Save(path, Options); }
        else { workbook.Save(path, Format); }
    }
}
