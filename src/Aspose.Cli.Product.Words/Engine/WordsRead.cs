using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using static Aspose.Cli.Product.Words.Engine.WordsEngineSupport;

namespace Aspose.Cli.Product.Words.Engine;

/// <summary>Serves <c>words query blocks</c>: a bounded structural projection of a document.</summary>
internal static class WordsRead
{
    /// <summary>Reads a bounded structural projection of a document.</summary>
    internal static DocumentReadResult Run(WordsSession session, DocumentReadRequest request)
    {
        LicenseState state = session.Outputs.License;
        using LoadedDocument loaded = session.Loader.Open(request.Input, request.Password);
        return ReadProjection.Project(loaded, request.Input, request) with
        {
            License = EnvelopeParts.License(state),
            Warnings = InputWarnings(loaded),
        };
    }
}
