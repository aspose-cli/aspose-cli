using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Tables;

namespace Aspose.Cli.Product.Words.Engine;

/// <summary><c>words read</c>: the object table's read of a document.</summary>
internal static class WordsRead
{
    internal static WordsReadResult Run(WordsSession session, TableReadRequest request) =>
        new(TableRead.Run(session.Outputs, new WordsDocuments(session.Loader), request));
}
