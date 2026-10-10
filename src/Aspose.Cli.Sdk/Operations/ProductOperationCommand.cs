using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Sdk.Operations;

/// <summary>
/// One product command that applies operation documents, from its edit definition: its published
/// description and the schema its vocabulary's records produce, which the product resources
/// serve. <see cref="OperationCatalog{TOp}.Describe"/> creates it.
/// </summary>
public sealed class ProductOperationCommand
{
    internal ProductOperationCommand(ProductOperationDescriptor descriptor, GeneratedOperationSchema schema)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(schema);
        Descriptor = descriptor with { Fingerprint = schema.LazyFingerprint };
        Schema = schema;
    }

    /// <summary>The command's published description.</summary>
    public ProductOperationDescriptor Descriptor { get; }

    /// <summary>The vocabulary's schema and its per-operation views.</summary>
    internal GeneratedOperationSchema Schema { get; }
}
