using Aspose.Cli.Sdk.Tables;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary><c>slides read</c>: a window of the presentation's objects, as the object table reads them.</summary>
internal static class SlidesRead
{
    internal static SlidesReadResult Run(SlidesSession session, TableReadRequest request) =>
        new(TableRead.Run(session.Outputs, new SlidesDocuments(session.Loader), request));
}
