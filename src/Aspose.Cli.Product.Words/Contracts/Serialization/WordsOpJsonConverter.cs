using Aspose.Cli.Sdk.Serialization;

namespace Aspose.Cli.Product.Words.Contracts.Serialization;

/// <summary>Connects the Words operation vocabulary to the shared wire protocol.</summary>
internal sealed class WordsOpJsonConverter()
    : OperationJsonConverter<WordsOp>(WordsOps.Registry, static operation => operation.OpName);
