using Aspose.Cells;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>Owns the workbook and the external-resource lifetime used to load it.</summary>
internal sealed record LoadedWorkbook(Workbook Workbook, WorkbookResources Resources, bool IsEncrypted = false) : IDisposable
{
    /// <summary>
    /// Discloses formula results that differ from the stored ones because the workbook asks to be
    /// calculated when opened; null when the load calculated nothing or changed no result.
    /// </summary>
    internal Warning? CalculatedOnOpen { get; init; }

    /// <summary>
    /// The evaluation warning sheet that was the active sheet and the sheet activated in memory
    /// in its place; null otherwise. Read its warning through <see cref="SkippedSheetWarning"/>,
    /// since it is true only of a command that defaulted to the active sheet.
    /// </summary>
    internal CellsEvaluation.SkippedWarningSheet? EvaluationSheetSkipped { private get; init; }

    /// <summary>
    /// The honest on-disk format of the input, from the detection that planned the load.
    /// Detection is authoritative: <see cref="Workbook.FileFormat"/> reports the in-memory
    /// model, which the engine "upgrades" to <c>Xlsx</c> for the ancient BIFF family (an
    /// <c>Excel2</c> file would otherwise be reported as <c>xlsx</c>). The loaded workbook's
    /// format stands only where detection cannot see the true format: an <b>encrypted</b>
    /// container detects as its wrapper, whereas the decrypted workbook knows it is <c>xlsx</c>,
    /// and a defeated sniff returns <c>Unknown</c>, as for an HTML export that opens with a line
    /// of text before its markup and loaded as <c>Html</c> through the content fallback.
    /// </summary>
    internal required FileFormatType SourceFormat { get; init; }

    /// <summary>True when the input was imported as delimited text (CSV, TSV), not opened as a workbook.</summary>
    internal bool IsDelimitedText { get; init; }

    /// <summary>
    /// The <c>ACTIVE_SHEET_SKIPPED</c> warning when <paramref name="defaultedToActiveSheet"/>,
    /// that is when the command read or wrote the active sheet because no sheet was named;
    /// null for a command that named its sheet or covers every sheet.
    /// </summary>
    internal Warning? SkippedSheetWarning(bool defaultedToActiveSheet) =>
        defaultedToActiveSheet ? EvaluationSheetSkipped?.Warning : null;

    /// <summary>
    /// Before a save that writes the whole workbook, activates the evaluation warning sheet the
    /// load skipped again, so the save keeps the input's active sheet; a text output, which
    /// writes only the active sheet, keeps the sheet used in its place. A command that chose the
    /// active sheet itself does not call it.
    /// </summary>
    internal void RestoreActiveSheet(WorkbookSavePlan plan)
    {
        if (!plan.WritesActiveSheetOnly)
        {
            EvaluationSheetSkipped?.Restore(Workbook);
        }
    }

    /// <summary>
    /// The load's warnings and <paramref name="additional"/>; a command that defaulted to the
    /// active sheet adds <see cref="SkippedSheetWarning"/> itself.
    /// </summary>
    internal IReadOnlyList<Warning>? Warnings(params Warning?[] additional)
    {
        Warning[] warnings = new[] { Resources.CoverageWarning, CalculatedOnOpen }.Concat(additional).OfType<Warning>().ToArray();
        return warnings.Length == 0 ? null : warnings;
    }

    public void Dispose()
    {
        try { Workbook.Dispose(); }
        finally { Resources.Dispose(); }
    }
}
