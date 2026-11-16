using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using static Aspose.Cli.Product.Words.Engine.WordsEngineSupport;

namespace Aspose.Cli.Product.Words.Engine;

/// <summary>Serves <c>words read</c>: a bounded structural projection of a document.</summary>
internal static class WordsRead
{
    /// <summary>Reads a bounded structural projection of a document.</summary>
    internal static WordsReadResult Run(WordsSession session, WordsReadRequest request)
    {
        LicenseState state = session.Outputs.License;
        using LoadedDocument loaded = session.Loader.Open(request.Input, request.Password);
        WordsReadResult read = ReadProjection.Project(loaded, request.Input, request);
        return read with
        {
            License = EnvelopeParts.License(state),
            Warnings = EnvelopeParts.CombineWarnings(InputWarnings(loaded), read.Warnings),
        };
    }
}
