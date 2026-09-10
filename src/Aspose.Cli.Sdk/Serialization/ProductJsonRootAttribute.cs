namespace Aspose.Cli.Sdk.Serialization;

/// <summary>
/// Marks a product-owned deserialization root that must be included in the
/// product's source-generated JSON metadata.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class ProductJsonRootAttribute : Attribute;
