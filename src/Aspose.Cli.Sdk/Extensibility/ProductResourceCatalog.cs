using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using Aspose.Cli.Sdk.Operations;

namespace Aspose.Cli.Sdk.Extensibility;

/// <summary>
/// Immutable aggregate of schemas and other product-owned embedded resources. A generated
/// operation vocabulary owns its schema id here: its schema is served from the vocabulary's
/// descriptors, not from an embedded file.
/// </summary>
public sealed class ProductResourceCatalog
{
    private const string SchemaSuffix = ".schema.json";
    private readonly IReadOnlyDictionary<string, ResourceEntry> _schemas;
    private readonly IReadOnlyDictionary<string, OperationViews>
        _operationSchemas;
    private readonly IReadOnlyDictionary<string, ProductPackageResources> _byProduct;
    private readonly ConcurrentDictionary<string, string> _fingerprints = new(StringComparer.Ordinal);

    private ProductResourceCatalog(
        IReadOnlyList<ProductPackageResources> products,
        IReadOnlyDictionary<string, ResourceEntry> schemas,
        IReadOnlyDictionary<string, OperationViews> operationSchemas)
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
                AddSchema(schemas, id, new ResourceEntry(package, name, null));
            }
        }
        // Views are built on first use: most invocations never read a schema.
        var operationSchemas = new Dictionary<string, OperationViews>(StringComparer.Ordinal);
        foreach ((ProductPackageResources package, ProductManifest manifest) in entries)
        {
            foreach (IGrouping<string, ProductOperationCommand> group in
                manifest.Operations.GroupBy(static operation => operation.Descriptor.InputSchema, StringComparer.Ordinal))
            {
                string[] names = [.. group.SelectMany(static operation => operation.Descriptor.Ops).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
                if (group.First().GeneratedSchema is { } generated)
                {
                    AddSchema(schemas, group.Key, new ResourceEntry(package, null, generated));
                    operationSchemas.Add(group.Key, new OperationViews(names, new(() => generated.Operations)));
                    continue;
                }
                if (!schemas.TryGetValue(group.Key, out ResourceEntry? entry))
                {
                    continue;
                }
                operationSchemas.Add(group.Key, new OperationViews(
                    names,
                    new(() => ProductOperationSchemaIndex.Build(group.Key, entry.Read(), names))));
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
        _operationSchemas.TryGetValue(schemaId, out OperationViews? operations) ? operations.Names : [];

    /// <summary>Attempts to read one exact, self-contained operation schema view.</summary>
    public bool TryReadOperation(
        string schemaId,
        string operationId,
        [NotNullWhen(true)] out string? document)
    {
        document = null;
        return _operationSchemas.TryGetValue(schemaId, out OperationViews? operations)
            && operations.Views.Value.TryGetValue(operationId, out document);
    }

    /// <summary>
    /// <c>sha256:</c> and the lowercase hex SHA-256 of a served schema with <c>\n</c> line
    /// endings, so the value does not depend on how the source was checked out. It changes
    /// whenever the schema text changes, descriptions included; it is computed once, on first use.
    /// </summary>
    public string ContractFingerprint(string id) =>
        _fingerprints.GetOrAdd(id, key =>
            "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Read(key).ReplaceLineEndings("\n")))));

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

    /// <summary>An operation schema's operation names and its views, built on first use.</summary>
    private sealed record OperationViews(IReadOnlyList<string> Names, Lazy<IReadOnlyDictionary<string, string>> Views);

    /// <summary>A schema served from an embedded resource or from a generated vocabulary.</summary>
    private sealed record ResourceEntry(
        ProductPackageResources Package,
        string? Name,
        GeneratedOperationSchema? Generated)
    {
        public string Read()
        {
            if (Generated is not null)
            {
                return Generated.Document;
            }

            using Stream stream = Package.ResourceAssembly.GetManifestResourceStream(Name!)
                ?? throw new InvalidOperationException(
                    $"Embedded schema resource '{Name}' is unavailable.");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
