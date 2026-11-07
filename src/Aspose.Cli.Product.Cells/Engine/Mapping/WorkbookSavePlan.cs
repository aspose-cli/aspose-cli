using System.Globalization;
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
    /// <summary>The most worksheets a <c>SHEETS_DROPPED</c> names one by one.</summary>
    private const int NamedSheets = 10;

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

    /// <summary>
    /// The <c>SHEETS_DROPPED</c> of a text format, which writes only <see cref="TextSheet"/>: it
    /// names the other worksheets, the first <see cref="NamedSheets"/> by name and the rest by
    /// count; null when the workbook has no other. The evaluation warning sheets are no content of
    /// the workbook (<see cref="CellsEvaluation.IsWarningSheet"/>) and are neither named nor counted.
    /// </summary>
    internal Warning? DetectSheetLoss(Workbook workbook)
    {
        if (!WritesActiveSheetOnly)
        {
            return null;
        }

        Worksheet written = TextSheet(workbook);
        string[] dropped = [.. workbook.Worksheets.Cast<Worksheet>()
            .Where(sheet => sheet.Index != written.Index && !CellsEvaluation.IsWarningSheet(sheet))
            .Select(static sheet => $"'{sheet.Name}'")];
        if (dropped.Length == 0)
        {
            return null;
        }

        string named = dropped.Length <= NamedSheets
            ? string.Join(", ", dropped)
            : string.Create(CultureInfo.InvariantCulture, $"{string.Join(", ", dropped.Take(NamedSheets))} and {dropped.Length - NamedSheets} more");
        return new Warning(CellsDiagnostics.SheetsDropped, string.Create(CultureInfo.InvariantCulture,
            $"Only worksheet '{written.Name}' was exported; the '{FormatId}' output holds one worksheet, so {dropped.Length} other worksheet(s) were not written: {named}."))
        {
            Hint = "Use a multi-sheet format (xlsx, xlsb, ods, pdf) to retain every worksheet.",
            AffectsCompleteness = true,
        };
    }

    /// <summary>
    /// Removes from the in-memory workbook the drawings of the sheet a format that
    /// <see cref="CellsEngineFormats.DropsDrawings"/> writes, and says how many of each kind it left
    /// out, as the charts, pictures and shapes of <c>details.content</c>; null when it removed none.
    /// A group counts once, as one drawing, and takes the shapes in it along. Comments are no
    /// drawings: they are neither removed nor counted.
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
        var kinds = new List<ContentKind>();
        if (charts > 0) { lost.Add($"{charts} chart(s)"); kinds.Add(ContentKind.Charts); }
        if (pictures > 0) { lost.Add($"{pictures} picture(s)"); kinds.Add(ContentKind.Pictures); }
        if (others > 0) { lost.Add($"{others} other drawing(s), such as text boxes, shapes or groups,"); kinds.Add(ContentKind.Shapes); }
        return lost.Count == 0 ? null : ContentLoss.Warning(kinds,
            $"The '{FormatId}' output holds the cells of worksheet '{sheet.Name}' as a table, so its {string.Join(" and ", lost)} were not written.",
            "To keep them, render the sheet as an image with aspose-cli cells render, or convert to xlsx or pdf.",
            affectsCompleteness: true);
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
