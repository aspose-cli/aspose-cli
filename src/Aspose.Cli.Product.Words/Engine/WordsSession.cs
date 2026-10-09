using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Results;
using Aspose.Words;

namespace Aspose.Cli.Product.Words.Engine;

/// <summary>
/// What every Words handler of one invocation shares: the write pipeline (license state and
/// publishing), the resource budgets and the document loader.
/// </summary>
internal sealed record WordsSession(
    OutputPipeline<Document> Outputs,
    ResourceBudgetLedger Budgets,
    WordsDocumentLoader Loader);
