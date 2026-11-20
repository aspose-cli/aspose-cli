namespace Aspose.Cli.Sdk.Extensibility;

using System.Collections.Frozen;

/// <summary>Validated, immutable catalog of build-time product definitions.</summary>
public sealed class ProductCatalog
{
    private readonly IReadOnlyDictionary<string, ProductDefinition> _byId;

    internal ProductCatalog(
        IReadOnlyList<ProductDefinition> products,
        ProductResourceCatalog resources)
    {
        Products = Array.AsReadOnly(products.ToArray());
        _byId = Products.ToFrozenDictionary(
            static product => product.Manifest.Id,
            StringComparer.OrdinalIgnoreCase);
        Resources = resources ?? throw new ArgumentNullException(nameof(resources));
        Diagnostics = DiagnosticCatalog.Build(Products, Resources);
        JsonDefinitions = Array.AsReadOnly(Products
            .Select(static product => product.Json)
            .ToArray());
    }

    /// <summary>Definitions in deterministic display order.</summary>
    public IReadOnlyList<ProductDefinition> Products { get; }

    /// <summary>Immutable product-owned embedded resource catalog.</summary>
    public ProductResourceCatalog Resources { get; }

    /// <summary>Validated diagnostics from common infrastructure and compiled products.</summary>
    public DiagnosticCatalog Diagnostics { get; }

    /// <summary>Product JSON contributions in deterministic product order.</summary>
    public IReadOnlyList<Serialization.ProductJsonDefinition> JsonDefinitions { get; }

    /// <summary>Builds and validates a frozen catalog.</summary>
    public static ProductCatalog Build(IEnumerable<IProductModule> modules) =>
        BuildCore(modules, registrations: null);

    /// <summary>
    /// Builds a catalog from compile-time version registrations and validates
    /// that every product targets the host SDK major version.
    /// </summary>
    public static ProductCatalog Build(
        IEnumerable<ProductModuleRegistration> registrations,
        string hostSdkVersion)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        ProductModuleRegistration[] discovered = registrations.ToArray();
        return BuildCore(
            discovered.Select(static registration => registration.Module),
            ProductDefinitionValidator.ValidateDiscovery(
                discovered,
                hostSdkVersion));
    }

    /// <summary>Finds a product by stable identifier.</summary>
    public bool TryGet(string productId, out ProductDefinition? product)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        return _byId.TryGetValue(productId, out product);
    }

    /// <summary>Returns a product by stable identifier.</summary>
    public ProductDefinition Get(string productId) =>
        TryGet(productId, out ProductDefinition? product)
            ? product!
            : throw new KeyNotFoundException($"Product '{productId}' is not registered.");

    /// <summary>Activates one product and caches its binding in the invocation context.</summary>
    public ProductBinding Activate(
        string productId,
        ProductActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ProductDefinition product = Get(productId);
        lock (context.SyncRoot)
        {
            if (context.TryGetBinding(product, out ProductBinding? existing))
            {
                return existing!;
            }
            ProductBinding binding = product.Activate(context);
            context.AddBinding(product, binding);
            return binding;
        }
    }

    internal static string NormalizeExtension(string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);
        string normalized = extension.StartsWith('.')
            ? extension.ToLowerInvariant()
            : "." + extension.ToLowerInvariant();
        if (normalized.Length < 2
            || normalized.Any(static character =>
                character != '.' && !char.IsAsciiLetterOrDigit(character)))
        {
            throw new InvalidOperationException(
                $"File extension '{extension}' is invalid.");
        }
        return normalized;
    }

    private static ProductCatalog BuildCore(
        IEnumerable<IProductModule> modules,
        IReadOnlyDictionary<IProductModule, ProductModuleRegistration>? registrations)
    {
        ArgumentNullException.ThrowIfNull(modules);
        var validator = new ProductDefinitionValidator(registrations);
        var products = new List<ProductDefinition>();
        var productIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var resources = new Dictionary<string, ProductPackageResources>(
            StringComparer.OrdinalIgnoreCase);
        foreach (IProductModule module in modules)
        {
            ProductDefinition product = validator.Prepare(module);
            string productId = product.Manifest.Id;
            if (!productIds.Add(productId))
            {
                throw new InvalidOperationException(
                    $"Product id '{productId}' is registered more than once.");
            }
            ProductPackageResources package = ProductPackageResources.Discover(
                module.GetType().Assembly,
                productId);
            validator.ValidateResources(product, package);
            products.Add(product);
            resources.Add(productId, package);
        }

        ProductDefinition[] ordered = products
            .OrderBy(static product => product.Manifest.DisplayOrder)
            .ThenBy(static product => product.Manifest.Id, StringComparer.Ordinal)
            .ToArray();
        return new ProductCatalog(
            ordered,
            ProductResourceCatalog.Build(
                ordered.Select(product => (
                    resources[product.Manifest.Id],
                    product.Operations,
                    product.Tables,
                    product.Json.ResultRecords))));
    }
}
