using System.Diagnostics.CodeAnalysis;
using System.Collections.Frozen;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Operations;
using Aspose.Cli.Sdk.Serialization;

namespace Aspose.Cli.Sdk.Extensibility;

/// <summary>
/// Immutable aggregate of schemas and other product-owned embedded resources. An operation
/// vocabulary owns its schema id here: its schema and per-operation views are served from the
/// vocabulary's records, not from an embedded file. A product's result schemas are written from
/// the result records its contract generator described; a product that has not moved to them
/// yet still serves its embedded schema files.
/// </summary>
public sealed class ProductResourceCatalog
{
    private const string SchemaSuffix = ".schema.json";
    private readonly IReadOnlyDictionary<string, ResourceEntry> _schemas;
    private readonly IReadOnlyDictionary<string, GeneratedOperationSchema> _operationSchemas;
    private readonly IReadOnlyDictionary<string, ProductPackageResources> _byProduct;

    private ProductResourceCatalog(
        IReadOnlyList<ProductPackageResources> products,
        IReadOnlyDictionary<string, ResourceEntry> schemas,
        IReadOnlyDictionary<string, GeneratedOperationSchema> operationSchemas)
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
        IEnumerable<(ProductPackageResources Package, ProductManifest Manifest, IReadOnlyList<ResultRecord> Results)> products)
    {
        ArgumentNullException.ThrowIfNull(products);
        (ProductPackageResources Package, ProductManifest Manifest, IReadOnlyList<ResultRecord> Results)[] entries = products.ToArray();
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
                AddSchema(schemas, id, new ResourceEntry(package, name, null));
            }
        }
        // Schemas and their views are written on first use: most invocations never read one.
        var operationSchemas = new Dictionary<string, GeneratedOperationSchema>(StringComparer.Ordinal);
        foreach ((ProductPackageResources package, ProductManifest manifest, IReadOnlyList<ResultRecord> results) in entries)
        {
            foreach (ProductOperationCommand command in manifest.Operations)
            {
                string id = command.Descriptor.InputSchema;
                if (operationSchemas.TryAdd(id, command.Schema))
                {
                    GeneratedOperationSchema schema = command.Schema;
                    AddSchema(schemas, id, new ResourceEntry(package, null, () => schema.Document));
                }
            }

            var resultSchemas = new ResultSchemaSet(package.ProductId, results, SdkSchemaCatalog.Schemas);
            foreach (string id in resultSchemas.Ids)
            {
                AddSchema(schemas, id, new ResourceEntry(package, null, () => resultSchemas.TryRead(id, out string? document)
                    ? document
                    : throw new InvalidOperationException($"Result schema '{id}' is unavailable.")));
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

        document = entry.Read();
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
        _operationSchemas.TryGetValue(schemaId, out GeneratedOperationSchema? schema) ? schema.Names : [];

    /// <summary>Attempts to read one exact, self-contained operation schema view.</summary>
    public bool TryReadOperation(
        string schemaId,
        string operationId,
        [NotNullWhen(true)] out string? document)
    {
        document = null;
        return _operationSchemas.TryGetValue(schemaId, out GeneratedOperationSchema? schema)
            && schema.Operations.TryGetValue(operationId, out document);
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

    private static void AddSchema(Dictionary<string, ResourceEntry> schemas, string id, ResourceEntry entry)
    {
        if (!schemas.TryAdd(id, entry))
        {
            throw new InvalidOperationException(
                $"Schema '{id}' has multiple product resource owners.");
        }
    }

    /// <summary>A schema served from an embedded resource, or written from an operation vocabulary or result records.</summary>
    private sealed record ResourceEntry(
        ProductPackageResources Package,
        string? Name,
        Func<string>? Generated)
    {
        public string Read()
        {
            if (Generated is not null)
            {
                return Generated();
            }

            using Stream stream = Package.ResourceAssembly.GetManifestResourceStream(Name!)
                ?? throw new InvalidOperationException(
                    $"Embedded schema resource '{Name}' is unavailable.");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
