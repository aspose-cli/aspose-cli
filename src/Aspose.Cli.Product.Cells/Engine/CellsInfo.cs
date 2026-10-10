using Aspose.Cells;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using static Aspose.Cli.Product.Cells.Engine.CellsEngineSupport;

namespace Aspose.Cli.Product.Cells.Engine;

/// <summary><c>cells inspect</c>: the structural summary of a workbook (projection ladder step one).</summary>
internal static class CellsInfo
{
    public static WorkbookInfoResult Run(CellsSession session, InfoRequest request)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(request.Input);

        LicenseState licenseState = session.Outputs.License;
        using LoadedWorkbook loaded = session.Loader.Open(request.Input, request.Password);
        Workbook workbook = loaded.Workbook;

        (WorkbookSummary summary, Warning? errorsTruncated) =
            InfoProjection.Summarize(session.Budgets, workbook, request);

        return new WorkbookInfoResult
        {
            Source = BuildSource(request.Input, loaded) with { Encrypted = loaded.IsEncrypted },
            Workbook = summary,
            License = EnvelopeParts.License(licenseState),
            // Info is read-only, so no evaluation watermark — but an honest count
            // that outran its capped list is a condition the caller must see.
            Warnings = loaded.Warnings([errorsTruncated, .. TextTableLayout.Warnings(loaded, session.Budgets)]),
        };
    }
}
