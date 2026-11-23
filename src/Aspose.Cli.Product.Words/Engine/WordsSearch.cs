using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Tables;

namespace Aspose.Cli.Product.Words.Engine;

/// <summary><c>words search</c>: the object table's search of the paragraphs' text.</summary>
internal static class WordsSearch
{
    internal static WordsSearchResult Run(WordsSession session, TableSearchRequest request) =>
        new(TableRead.Search(session.Outputs, new WordsDocuments(session.Loader), request));
}
