using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Tables;

namespace Aspose.Cli.Product.Words.Engine;

/// <summary><c>words edit</c>: the object table's edit of a document.</summary>
internal static class WordsEdit
{
    internal static WordsEditResult Run(WordsSession session, TableEditRequest request) =>
        new(TableEdit.Run(session.Outputs, new WordsDocuments(session.Loader), request));
}
