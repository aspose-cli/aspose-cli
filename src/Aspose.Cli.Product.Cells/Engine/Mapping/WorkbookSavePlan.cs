using Aspose.Cli.Sdk.Formats;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cells;
using Aspose.Cells.Drawing;

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
        SaveOptions? options = CellsEngineFormats.Options(formatId, byteOrderMark, selectedSheets);
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

    /// <summary>
    /// Removes from the in-memory workbook the drawings of the sheet a format that
    /// <see cref="CellsEngineFormats.DropsDrawings"/> writes, and says how many of each kind it left
    /// out; null when it removed none. A group counts once, as one drawing, and takes the shapes
    /// in it along. Comments are no drawings: the output keeps them.
    /// </summary>
    internal Warning? DropDrawings(Workbook workbook)
    {
        if (!CellsEngineFormats.DropsDrawings(FormatId))
        {
            return null;
        }

        Worksheet sheet = TextSheet(workbook);
        Shape[] drawings = [.. sheet.Shapes.Cast<Shape>().Where(static shape => shape is not CommentShape && !shape.IsInGroup)];
        int charts = 0, pictures = 0, others = 0;
        foreach (Shape drawing in drawings)
        {
            switch (drawing)
            {
                case ChartShape:
                    charts++;
                    break;
                case Picture:
                    pictures++;
                    break;
                default:
                    others++;
                    break;
            }

            sheet.Shapes.Remove(drawing);
        }

        var lost = new List<string>();
        if (charts > 0) { lost.Add($"{charts} chart(s)"); }
        if (pictures > 0) { lost.Add($"{pictures} picture(s)"); }
        if (others > 0) { lost.Add($"{others} other drawing(s), such as text boxes, shapes or groups,"); }
        return lost.Count == 0 ? null : new Warning(WarningCodes.LossyConversion,
            $"The '{FormatId}' output holds the cells of worksheet '{sheet.Name}' as a table, so its {string.Join(" and ", lost)} were not written.")
        {
            Hint = "To keep them, render the sheet as an image with aspose-cli cells render, or convert to xlsx or pdf.",
            AffectsCompleteness = true,
        };
    }

    internal Worksheet TextSheet(Workbook workbook) =>
        workbook.Worksheets[IsEvaluation ? 0 : workbook.Worksheets.ActiveSheetIndex];

    internal void Save(Workbook workbook, string path)
    {
        workbook.Settings.Password = Protection.Password?.Reveal() ?? string.Empty;
        if (Options is not null) { workbook.Save(path, Options); }
        else { workbook.Save(path, Format); }
    }
}
