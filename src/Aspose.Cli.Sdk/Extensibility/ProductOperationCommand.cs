using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Operations;

namespace Aspose.Cli.Sdk.Extensibility;

/// <summary>
/// One command of a product manifest that applies operation documents: its published
/// description and, for a generated vocabulary, the schema its records produce, which the
/// product resources serve. <see cref="OperationCatalog{TOp}.Describe"/> creates it.
/// </summary>
public sealed class ProductOperationCommand
{
    /// <summary>Declares a command whose schema is a hand-written embedded resource.</summary>
    public ProductOperationCommand(ProductOperationDescriptor descriptor)
        : this(descriptor, null)
    {
    }

    internal ProductOperationCommand(ProductOperationDescriptor descriptor, GeneratedOperationSchema? generatedSchema)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        Descriptor = descriptor;
        GeneratedSchema = generatedSchema;
    }

    /// <summary>The command's published description.</summary>
    public ProductOperationDescriptor Descriptor { get; }

    /// <summary>The generated schema, or null for a hand-written one.</summary>
    internal GeneratedOperationSchema? GeneratedSchema { get; }
}
