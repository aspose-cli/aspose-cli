using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Tables;

namespace Aspose.Cli.Product.Words.Engine;

/// <summary><c>words read</c>: the object table's read of a document.</summary>
internal static class WordsRead
{
    internal static WordsReadResult Run(WordsSession session, TableReadRequest request)
    {
        TableReadOutcome outcome = TableRead.Run(session.Outputs, new WordsDocuments(session.Loader), request);
        return new WordsReadResult
        {
            Input = outcome.Input,
            Objects = outcome.Objects,
            Inventory = outcome.Inventory,
            EngineWarnings = outcome.EngineWarnings,
            Window = outcome.Window,
            License = outcome.License,
            Warnings = outcome.Warnings,
        };
    }
}
