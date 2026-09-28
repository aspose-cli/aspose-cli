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
