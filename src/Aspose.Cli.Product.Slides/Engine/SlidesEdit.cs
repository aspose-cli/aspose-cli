using Aspose.Cli.Sdk.Tables;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary><c>slides edit</c>: the object table edit of a presentation.</summary>
internal static class SlidesEdit
{
    internal static SlidesEditResult Run(SlidesSession session, SlidesEditRequest request)
    {
        TableEditOutcome outcome = TableEdit.Run(session.Outputs, new SlidesDocuments(session.Loader), request);
        return new SlidesEditResult
        {
            Input = outcome.Input,
            Output = outcome.Output,
            Ops = outcome.Ops,
            Counts = outcome.Counts,
            EngineWarnings = outcome.EngineWarnings,
            License = outcome.License,
            Warnings = outcome.Warnings,
        };
    }
}
