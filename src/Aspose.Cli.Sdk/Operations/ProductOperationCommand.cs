using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Sdk.Operations;

/// <summary>
/// One command of a product manifest that applies operation documents: its published
/// description and the schema its vocabulary's records produce, which the product resources
/// serve. <see cref="OperationCatalog{TOp}.Describe"/> creates it.
/// </summary>
public sealed class ProductOperationCommand
{
    internal ProductOperationCommand(ProductOperationDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        Descriptor = descriptor;
    }

    /// <summary>The command's published description.</summary>
    public ProductOperationDescriptor Descriptor { get; }

    /// <summary>The vocabulary's schema and its per-operation views.</summary>
    internal GeneratedOperationSchema Schema => Descriptor.Schema;
}
