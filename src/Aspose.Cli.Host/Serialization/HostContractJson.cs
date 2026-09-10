using Aspose.Cli.Sdk.Serialization;

namespace Aspose.Cli.Host.Serialization;

/// <summary>
/// Single immutable host serializer composed from common and registered product
/// source-generated contexts.
/// </summary>
internal sealed class HostContractJson
{
    public HostContractJson(Aspose.Cli.Sdk.Extensibility.ProductCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        Serializer = new ContractJsonSerializer(catalog.JsonDefinitions);
    }

    public ContractJsonSerializer Serializer { get; }
}
