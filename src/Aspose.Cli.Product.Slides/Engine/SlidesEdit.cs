using Aspose.Cli.Sdk.Tables;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary><c>slides edit</c>: the object table edit of a presentation.</summary>
internal static class SlidesEdit
{
    internal static SlidesEditResult Run(SlidesSession session, TableEditRequest request) =>
        new(TableEdit.Run(session.Outputs, new SlidesDocuments(session.Loader), request));
}
