using Aspose.Cells;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Text;
using static Aspose.Cli.Product.Cells.Engine.CellsEngineSupport;

namespace Aspose.Cli.Product.Cells.Engine;

/// <summary><c>cells query search</c>: the budgeted cells whose value or formula matches a pattern.</summary>
internal static class CellsSearch
{
    public static CellsSearchResult Run(CellsSession session, CellsSearchRequest request)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(request.Input);

        LicenseState licenseState = session.Outputs.License;
        using LoadedWorkbook loaded = session.Loader.Open(request.Input, request.Password);
        Workbook workbook = loaded.Workbook;
        InputInfo source = BuildSource(request.Input, loaded);

        int? sheetIndex = request.SheetName is { } name ? Sheets.Resolve(workbook, name).Index : null;
        SearchHits<CellsSearchHit> hits = SearchMatcher.Find(session.Budgets, workbook, request, sheetIndex);

        return new CellsSearchResult
        {
            Input = source,
            Pattern = request.Query.Text.Pattern,
            Hits = hits.Hits,
            // The window's next is left unset: the search command spells the follow-up.
            Window = hits.Window(),
            License = EnvelopeParts.License(licenseState),
            Warnings = loaded.Warnings(),
        };
    }
}
