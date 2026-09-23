using Aspose.Cli.Sdk.Serialization;

namespace Aspose.Cli.Product.Cells.Contracts.Serialization;

/// <summary>Connects the Cells operation vocabulary to the shared wire protocol.</summary>
internal sealed class OpJsonConverter()
    : OperationJsonConverter<Op>(CellsOps.Catalog);
