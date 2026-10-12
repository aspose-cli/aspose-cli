using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using static Aspose.Cli.Product.Words.Engine.WordsEngineSupport;

namespace Aspose.Cli.Product.Words.Engine;

/// <summary>Serves <c>words inspect</c>: document structure, safety state and metadata.</summary>
internal static class WordsInspect
{
    /// <summary>Returns structural metadata for a document.</summary>
    internal static WordsInspectResult Run(WordsSession session, WordsInspectRequest request)
    {
        LicenseState state = session.Outputs.License;
        using LoadedDocument loaded = session.Loader.Open(request.Input, request.Password);
        WordsInspectResult result = InfoProjection.Project(loaded, request.Input, request);
        return result with
        {
            License = EnvelopeParts.License(state),
            Warnings = EnvelopeParts.CombineWarnings(InputWarnings(loaded), result.Warnings),
        };
    }
}
