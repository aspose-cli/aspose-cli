using System.Diagnostics.CodeAnalysis;
using System.Collections.Frozen;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Operations;
using Aspose.Cli.Sdk.Serialization;

namespace Aspose.Cli.Sdk.Extensibility;

/// <summary>
/// Immutable aggregate of the schemas products publish and their other embedded resources. Every
/// schema is written from records: an operation vocabulary's schema and per-operation views from
/// its operation records, and a product's result schemas from the result records its contract
/// generator described.
/// </summary>
public sealed class ProductResourceCatalog
{
    private readonly IReadOnlyDictionary<string, Func<string>> _schemas;
    private readonly IReadOnlyDictionary<string, GeneratedOperationSchema> _operationSchemas;
    private readonly IReadOnlyDictionary<string, Lazy<Tables.TableSchema>> _tableSchemas;
    private readonly IReadOnlyDictionary<string, ProductPackageResources> _byProduct;

    private ProductResourceCatalog(
        IReadOnlyList<ProductPackageResources> products,
        IReadOnlyDictionary<string, Func<string>> schemas,
        IReadOnlyDictionary<string, GeneratedOperationSchema> operationSchemas,
        IReadOnlyDictionary<string, Lazy<Tables.TableSchema>> tableSchemas)
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
        _tableSchemas = tableSchemas.ToFrozenDictionary(StringComparer.Ordinal);
        SchemaIds = Array.AsReadOnly(
            _schemas.Keys.Order(StringComparer.Ordinal).ToArray());
    }

    /// <summary>All registered schema identifiers in deterministic order.</summary>
    public IReadOnlyList<string> SchemaIds { get; }

    /// <summary>Product resource indexes in deterministic catalog order.</summary>
    public IReadOnlyList<ProductPackageResources> Products { get; }

    internal static ProductResourceCatalog Build(
        IEnumerable<(ProductPackageResources Package, IReadOnlyList<ProductOperationCommand> Operations, IReadOnlyList<Func<Tables.DocumentTable>> Tables, IReadOnlyList<ResultRecord> Results)> products)
    {
        ArgumentNullException.ThrowIfNull(products);
        var packages = new List<ProductPackageResources>();
        // Schemas and their views are written on first use: most invocations never read one.
        var schemas = new Dictionary<string, Func<string>>(StringComparer.Ordinal);
        var operationSchemas = new Dictionary<string, GeneratedOperationSchema>(StringComparer.Ordinal);
        var tableSchemas = new Dictionary<string, Lazy<Tables.TableSchema>>(StringComparer.Ordinal);
        foreach ((ProductPackageResources package, IReadOnlyList<ProductOperationCommand> operations, IReadOnlyList<Func<Tables.DocumentTable>> tables, IReadOnlyList<ResultRecord> results) in products)
        {
            var published = new List<string>();
            foreach (ProductOperationCommand command in operations)
            {
                string id = command.Descriptor.InputSchema;
                if (operationSchemas.TryAdd(id, command.Schema))
                {
                    GeneratedOperationSchema schema = command.Schema;
                    AddSchema(schemas, id, () => schema.Document);
                    published.Add(id);
                }
            }

            foreach (Func<Tables.DocumentTable> table in tables.Take(1))
            {
                // A table's operation document schema is written from the table on first use,
                // which loads the engine's types only then.
                string id = ResultEnvelope.CatalogId(package.ProductId, Tables.DocumentTable.SchemaId);
                string product = package.ProductId;
                var schema = new Lazy<Tables.TableSchema>(() => Tables.TableSchema.Of(product, table()));
                tableSchemas.Add(id, schema);
                AddSchema(schemas, id, () => schema.Value.Document);
                published.Add(id);
            }

            var resultSchemas = new ResultSchemaSet(package.ProductId, results, SdkSchemaCatalog.Schemas);
            foreach (string id in resultSchemas.Ids)
            {
                AddSchema(schemas, id, () => resultSchemas.TryRead(id, out string? document)
                    ? document
                    : throw new InvalidOperationException($"Result schema '{id}' is unavailable."));
                published.Add(id);
            }

            packages.Add(package.Publishing(published));
        }

        return new ProductResourceCatalog(
            packages.AsReadOnly(),
            schemas,
            operationSchemas,
            tableSchemas);
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
            || !_schemas.TryGetValue(id, out Func<string>? read))
        {
            return false;
        }

        document = read();
        return true;
    }

    /// <summary>Whether a schema is registered under <paramref name="id"/>, without writing it.</summary>
    internal bool Contains(string? id) => id is not null && _schemas.ContainsKey(id);

    /// <summary>Reads a registered schema document.</summary>
    public string Read(string id) =>
        TryRead(id, out string? document)
            ? document
            : throw new ArgumentException(
                $"Unknown schema id '{id}'. Known ids: {string.Join(", ", SchemaIds)}",
                nameof(id));

    /// <summary>Returns operation IDs indexed for one schema.</summary>
    public IReadOnlyList<string> GetOperations(string schemaId) =>
        _operationSchemas.TryGetValue(schemaId, out GeneratedOperationSchema? schema) ? schema.Names
        : _tableSchemas.TryGetValue(schemaId, out Lazy<Tables.TableSchema>? table) ? table.Value.Names
        : [];

    /// <summary>Attempts to read one exact, self-contained operation schema view.</summary>
    public bool TryReadOperation(
        string schemaId,
        string operationId,
        [NotNullWhen(true)] out string? document)
    {
        document = null;
        return _operationSchemas.TryGetValue(schemaId, out GeneratedOperationSchema? schema)
            ? schema.Operations.TryGetValue(operationId, out document)
            : _tableSchemas.TryGetValue(schemaId, out Lazy<Tables.TableSchema>? table) && table.Value.Views.TryGetValue(operationId, out document);
    }

    private static void AddSchema(Dictionary<string, Func<string>> schemas, string id, Func<string> read)
    {
        if (!schemas.TryAdd(id, read))
        {
            throw new InvalidOperationException(
                $"Schema '{id}' has multiple product resource owners.");
        }
    }
}
