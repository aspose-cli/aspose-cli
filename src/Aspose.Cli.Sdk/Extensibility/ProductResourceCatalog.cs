using System.Diagnostics.CodeAnalysis;
using System.Collections.Frozen;
using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Sdk.Extensibility;

/// <summary>
/// Immutable aggregate of schemas and other product-owned embedded resources.
/// </summary>
public sealed class ProductResourceCatalog
{
    private const string SchemaSuffix = ".schema.json";
    private readonly IReadOnlyDictionary<string, ResourceEntry> _schemas;
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>
        _operationSchemas;
    private readonly IReadOnlyDictionary<string, ProductPackageResources> _byProduct;

    private ProductResourceCatalog(
        IReadOnlyList<ProductPackageResources> products,
        IReadOnlyDictionary<string, ResourceEntry> schemas,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> operationSchemas)
    {
        Products = products;
        _byProduct = Products.ToFrozenDictionary(
            static product => product.ProductId,
            StringComparer.OrdinalIgnoreCase);
        _schemas = schemas.ToFrozenDictionary(
            static item => item.Key,
            static item => item.Value,
            StringComparer.Ordinal);
        _operationSchemas = operationSchemas.ToFrozenDictionary(
            static item => item.Key,
            static item => item.Value,
            StringComparer.Ordinal);
        SchemaIds = Array.AsReadOnly(
            _schemas.Keys.Order(StringComparer.Ordinal).ToArray());
    }

    /// <summary>All registered schema identifiers in deterministic order.</summary>
    public IReadOnlyList<string> SchemaIds { get; }

    /// <summary>Product resource indexes in deterministic catalog order.</summary>
    public IReadOnlyList<ProductPackageResources> Products { get; }

    internal static ProductResourceCatalog Build(
        IEnumerable<(ProductPackageResources Package, ProductManifest Manifest)> products)
    {
        ArgumentNullException.ThrowIfNull(products);
        (ProductPackageResources Package, ProductManifest Manifest)[] entries = products.ToArray();
        ProductPackageResources[] packages = entries.Select(static entry => entry.Package).ToArray();
        var schemas = new Dictionary<string, ResourceEntry>(StringComparer.Ordinal);
        foreach (ProductPackageResources package in packages)
        {
            foreach (string name in package.ResourceNames)
            {
                string? id = TryGetSchemaId(name);
                if (id is null)
                {
                    continue;
                }
                if (!schemas.TryAdd(
                        id,
                        new ResourceEntry(package, name)))
                {
                    throw new InvalidOperationException(
                        $"Schema '{id}' has multiple product resource owners.");
                }
            }
        }
        var operationSchemas = new Dictionary<string, IReadOnlyDictionary<string, string>>(
            StringComparer.Ordinal);
        foreach ((ProductPackageResources package, ProductManifest manifest) in entries)
        {
            foreach (IGrouping<string, ProductOperationDescriptor> group in
                manifest.Operations.GroupBy(static operation => operation.InputSchema, StringComparer.Ordinal))
            {
                if (!schemas.TryGetValue(group.Key, out ResourceEntry? entry))
                {
                    continue;
                }
                operationSchemas.Add(
                    group.Key,
                    ProductOperationSchemaIndex.Build(
                        group.Key,
                        Read(entry),
                        group.SelectMany(static operation => operation.Ops).ToArray()));
            }
        }
        return new ProductResourceCatalog(
            Array.AsReadOnly(packages),
            schemas,
            operationSchemas);
    }

    /// <summary>Returns the resources indexed for one product.</summary>
    public ProductPackageResources GetProduct(string productId) =>
        _byProduct.TryGetValue(productId, out ProductPackageResources? product)
            ? product
            : throw new KeyNotFoundException(
                $"Product resources for '{productId}' are not registered.");

    /// <summary>Attempts to read a registered schema document.</summary>
    public bool TryRead(
        string id,
        [NotNullWhen(true)] out string? document)
    {
        document = null;
        if (string.IsNullOrWhiteSpace(id)
            || !_schemas.TryGetValue(id, out ResourceEntry? entry))
        {
            return false;
        }

        using Stream stream = entry.Package.ResourceAssembly.GetManifestResourceStream(
            entry.Name)
            ?? throw new InvalidOperationException(
                $"Embedded schema resource '{entry.Name}' is unavailable.");
        using var reader = new StreamReader(stream);
        document = reader.ReadToEnd();
        return true;
    }

    /// <summary>Reads a registered schema document.</summary>
    public string Read(string id) =>
        TryRead(id, out string? document)
            ? document
            : throw new ArgumentException(
                $"Unknown schema id '{id}'. Known ids: {string.Join(", ", SchemaIds)}",
                nameof(id));

    /// <summary>Returns operation IDs indexed for one schema.</summary>
    public IReadOnlyList<string> GetOperations(string schemaId) =>
        _operationSchemas.TryGetValue(schemaId, out IReadOnlyDictionary<string, string>? operations)
            ? operations.Keys.Order(StringComparer.Ordinal).ToArray()
            : [];

    /// <summary>Attempts to read one exact, self-contained operation schema view.</summary>
    public bool TryReadOperation(
        string schemaId,
        string operationId,
        [NotNullWhen(true)] out string? document)
    {
        document = null;
        return _operationSchemas.TryGetValue(
                schemaId,
                out IReadOnlyDictionary<string, string>? operations)
            && operations.TryGetValue(operationId, out document);
    }

    internal static string? TryGetSchemaId(string resourceName)
    {
        const string productPrefix = "Schemas/";
        string normalized = resourceName.Replace('\\', '/');
        string? remainder = normalized.StartsWith(
            productPrefix,
            StringComparison.Ordinal)
            ? normalized[productPrefix.Length..]
            : null;
        if (remainder is null
            || !remainder.EndsWith(SchemaSuffix, StringComparison.Ordinal))
        {
            return null;
        }

        string id = remainder[..^SchemaSuffix.Length];
        return id.Length > 0 ? id : null;
    }

    private sealed record ResourceEntry(
        ProductPackageResources Package,
        string Name);

    private static string Read(ResourceEntry entry)
    {
        using Stream stream = entry.Package.ResourceAssembly.GetManifestResourceStream(entry.Name)
            ?? throw new InvalidOperationException(
                $"Embedded schema resource '{entry.Name}' is unavailable.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
