using Aspose.Cells;
using Aspose.Cli.Sdk.Contracts;

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
    /// Discloses that the active sheet is the evaluation warning sheet and that another sheet was
    /// activated in memory in its place; null otherwise. Read it through
    /// <see cref="SkippedSheetWarning"/>, since it is true only of a command that defaulted to the
    /// active sheet.
    /// </summary>
    internal Warning? EvaluationSheetSkipped { private get; init; }

    /// <summary>True when the input was imported as delimited text (CSV, TSV), not opened as a workbook.</summary>
    internal bool IsDelimitedText { get; init; }

    /// <summary>
    /// The <c>EVALUATION_SHEET_SKIPPED</c> warning when <paramref name="defaultedToActiveSheet"/>,
    /// that is when the command read or wrote the active sheet because no sheet was named;
    /// null for a command that named its sheet or covers every sheet.
    /// </summary>
    internal Warning? SkippedSheetWarning(bool defaultedToActiveSheet) =>
        defaultedToActiveSheet ? EvaluationSheetSkipped : null;

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
