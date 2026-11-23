using Aspose.Cli.Sdk.Tables;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary><c>slides search</c>: the object table's search over the presentation's text rows.</summary>
internal static class SlidesSearch
{
    internal static SlidesSearchResult Run(SlidesSession session, TableSearchRequest request) =>
        new(TableRead.Search(session.Outputs, new SlidesDocuments(session.Loader), request));
}
