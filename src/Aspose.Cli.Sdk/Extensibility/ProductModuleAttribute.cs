namespace Aspose.Cli.Sdk.Extensibility;

/// <summary>
/// Declares the single product module exported by a build-time product assembly.
/// The host source generator reads this metadata without loading or reflecting
/// over the product assembly at runtime.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class ProductModuleAttribute : Attribute
{
    /// <summary>Creates one build-time product-module declaration.</summary>
    /// <param name="productId">Stable, lower-case product identifier.</param>
    /// <param name="moduleType">Public, non-abstract <see cref="IProductModule"/> implementation.</param>
    public ProductModuleAttribute(string productId, Type moduleType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        ArgumentNullException.ThrowIfNull(moduleType);
        ProductId = productId;
        ModuleType = moduleType;
    }

    /// <summary>Stable product identifier used for compile-time duplicate detection.</summary>
    public string ProductId { get; }

    /// <summary>Module implementation instantiated by generated static registration code.</summary>
    public Type ModuleType { get; }
}
