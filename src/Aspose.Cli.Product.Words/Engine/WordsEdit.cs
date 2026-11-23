using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Tables;

namespace Aspose.Cli.Product.Words.Engine;

/// <summary><c>words edit</c>: the object table's edit of a document.</summary>
internal static class WordsEdit
{
    internal static WordsEditResult Run(WordsSession session, TableEditRequest request)
    {
        TableEditOutcome outcome = TableEdit.Run(session.Outputs, new WordsDocuments(session.Loader), request);
        return new WordsEditResult
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
