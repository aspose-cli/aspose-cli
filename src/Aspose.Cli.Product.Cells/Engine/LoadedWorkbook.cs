using Aspose.Cells;
using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Product.Cells.Engine;

/// <summary>Owns the workbook and the external-resource lifetime used to load it.</summary>
internal sealed record LoadedWorkbook(Workbook Workbook, WorkbookResources Resources) : IDisposable
{
    internal IReadOnlyList<Warning>? Warnings(params Warning?[] additional)
    {
        Warning[] warnings = new[] { Resources.CoverageWarning }.Concat(additional).OfType<Warning>().ToArray();
        return warnings.Length == 0 ? null : warnings;
    }

    public void Dispose()
    {
        try { Workbook.Dispose(); }
        finally { Resources.Dispose(); }
    }
}
