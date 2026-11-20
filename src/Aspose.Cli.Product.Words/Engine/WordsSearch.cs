using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Tables;

namespace Aspose.Cli.Product.Words.Engine;

/// <summary><c>words search</c>: the object table's search of the paragraphs' text.</summary>
internal static class WordsSearch
{
    internal static WordsSearchResult Run(WordsSession session, WordsSearchRequest request)
    {
        TableSearchOutcome outcome = TableRead.Search(session.Outputs, new WordsDocuments(session.Loader), request);
        return new WordsSearchResult
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
