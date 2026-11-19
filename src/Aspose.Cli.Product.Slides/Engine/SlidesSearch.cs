using Aspose.Cli.Sdk.Tables;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary><c>slides search</c>: the object table's search over the presentation's text rows.</summary>
internal static class SlidesSearch
{
    internal static SlidesSearchResult Run(SlidesSession session, SlidesSearchRequest request)
    {
        TableSearchOutcome outcome = TableRead.Search(session.Outputs, new SlidesDocuments(session.Loader), request);
        return new SlidesSearchResult
        {
            Input = outcome.Input,
            Hits = outcome.Hits,
            EngineWarnings = outcome.EngineWarnings,
            Window = outcome.Window,
            License = outcome.License,
            Warnings = outcome.Warnings,
        };
    }
}
