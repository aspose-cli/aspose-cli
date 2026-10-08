namespace Aspose.Cli.Sdk.Extensibility;

using System.Collections.Frozen;
using System.Collections.ObjectModel;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Diagnostics;

/// <summary>Validated, immutable catalog of build-time product definitions.</summary>
public sealed class ProductCatalog
{
    private readonly IReadOnlyDictionary<string, ProductDefinition> _byId;
    private readonly IReadOnlyDictionary<string, ProductDefinition> _defaultOwners;
    private readonly IReadOnlyDictionary<Type, ProductOutputDefinition> _outputs;
    private readonly IReadOnlyDictionary<ProductDefinition, ProductCapabilities>
        _derivedCapabilities;
    private readonly IReadOnlyDictionary<string, string> _resolvedOwners;
    private readonly IReadOnlyList<string> _defaultOwnerExtensions;
    private readonly ProductDefinition? _defaultProduct;

    internal ProductCatalog(
        IReadOnlyList<ProductDefinition> products,
        IReadOnlyDictionary<string, string> resolvedOwners,
        ProductResourceCatalog resources)
    {
        Products = Array.AsReadOnly(products.ToArray());
        _byId = Products.ToFrozenDictionary(
            static product => product.Manifest.Id,
            StringComparer.OrdinalIgnoreCase);
        var orderedOwners = new SortedDictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);
        foreach ((string extension, string productId) in resolvedOwners)
        {
            orderedOwners.Add(extension, productId);
        }
        _resolvedOwners = new ReadOnlyDictionary<string, string>(
            orderedOwners);
        _defaultOwners = _resolvedOwners
            .ToFrozenDictionary(
                static item => item.Key,
                item => _byId[item.Value],
                StringComparer.OrdinalIgnoreCase);
        _defaultOwnerExtensions = Array.AsReadOnly(
            _defaultOwners.Keys.Order(StringComparer.Ordinal).ToArray());
        _outputs = Products
            .SelectMany(static product => product.Outputs)
            .ToFrozenDictionary(static output => output.ResultType);
        _derivedCapabilities = ProductCapabilityDeriver.Derive(
                Products,
                _resolvedOwners)
            .ToFrozenDictionary(
                static item => item.Key,
                static item => item.Value);
        Resources = resources ?? throw new ArgumentNullException(nameof(resources));
        Diagnostics = DiagnosticCatalog.Build(Products, Resources);
        JsonDefinitions = Array.AsReadOnly(Products
            .Select(static product => product.Json)
            .ToArray());
        _defaultProduct = Products.SingleOrDefault(
            static product => product.Manifest.IsDefaultCandidate);
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

    /// <summary>Returns the public surface derived from the product definition.</summary>
    public Aspose.Cli.Sdk.Contracts.ProductCapabilities GetCapabilities(
        ProductDefinition product)
    {
        ArgumentNullException.ThrowIfNull(product);
        return _derivedCapabilities[product];
    }

    /// <summary>
    /// Activates one product and caches its binding in the invocation context.
    /// </summary>
    public ProductBinding<TPort> Activate<TPort>(
        string productId,
        ProductActivationContext context)
        where TPort : class
    {
        ArgumentNullException.ThrowIfNull(context);
        ProductDefinition target = Get(productId);
        if (target.PortType != typeof(TPort))
        {
            throw new InvalidOperationException(
                $"Product '{productId}' binds '{target.PortType.FullName}', not '{typeof(TPort).FullName}'.");
        }

        lock (context.SyncRoot)
        {
            return (ProductBinding<TPort>)ActivateUntyped(target, context);
        }
    }

    /// <summary>Activates a product for a product-neutral host surface.</summary>
    public ProductBinding Activate(
        string productId,
        ProductActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ProductDefinition product = Get(productId);
        lock (context.SyncRoot)
        {
            return ActivateUntyped(product, context);
        }
    }

    private static ProductBinding ActivateUntyped(
        ProductDefinition definition,
        ProductActivationContext context)
    {
        if (context.TryGetBinding(definition, out ProductBinding? existing))
        {
            return existing!;
        }
        ProductBinding binding = definition.Activate(context);
        context.AddBinding(definition, binding);
        return binding;
    }

    /// <summary>Finds the unique generic-routing owner for an extension.</summary>
    public bool TryGetDefaultOwner(
        string extension,
        out ProductDefinition? product)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);
        return _defaultOwners.TryGetValue(
            NormalizeExtension(extension),
            out product);
    }

    /// <summary>
    /// Finds the final owner for an extension when that product declares the
    /// requested generic operation.
    /// </summary>
    public bool TryGetDefaultOwner(
        string extension,
        string operation,
        out ProductDefinition? product)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        if (!TryGetDefaultOwner(extension, out product))
        {
            return false;
        }
        if (product!.Formats.Count == 0)
        {
            bool standardOperation =
                StandardFileRouteOperations.Contains(operation);
            if (!standardOperation)
            {
                product = null;
            }
            return standardOperation;
        }

        string normalized = NormalizeExtension(extension);
        bool eligible = product.Formats.Any(format =>
            format.Uses.HasFlag(FormatUse.Input)
            && format.Ownership == RouteOwnership.Default
            && format.Extensions.Any(candidate =>
                string.Equals(
                    NormalizeExtension(candidate),
                    normalized,
                    StringComparison.Ordinal))
            && (format.Operations.Count == 0
                ? StandardFileRouteOperations.Contains(operation)
                : format.Operations.Contains(
                    operation,
                    StringComparer.Ordinal)));
        if (!eligible)
        {
            product = null;
        }
        return eligible;
    }

    /// <summary>
    /// Explicitly declared default product, or null when this distribution has
    /// no implicit default. Catalog ordering is never used as a fallback.
    /// </summary>
    public ProductDefinition? DefaultProduct => _defaultProduct;

    /// <summary>Builds deterministic public routing metadata without probing files.</summary>
    public RoutingCapabilities GetRoutingCapabilities(
        FileProbeOptions? options = null)
    {
        FileProbeOptions effective = options ?? new FileProbeOptions();
        ResolvedRouteCapabilities[] routes = _defaultOwners
            .OrderBy(static item => item.Key, StringComparer.Ordinal)
            .Select(item =>
            {
                ProductDefinition product = item.Value;
                string[] operations = product.Formats.Count == 0
                    ? [.. StandardFileRouteOperations.All]
                    : product.Formats
                        .Where(format =>
                            format.Uses.HasFlag(FormatUse.Input)
                            && format.Ownership == RouteOwnership.Default
                            && format.Extensions.Any(extension =>
                                string.Equals(
                                    NormalizeExtension(extension),
                                    item.Key,
                                    StringComparison.Ordinal)))
                        .SelectMany(static format =>
                            format.Operations.Count == 0
                                ? StandardFileRouteOperations.All
                                : format.Operations)
                        .Distinct(StringComparer.Ordinal)
                        .Order(StringComparer.Ordinal)
                        .ToArray();
                FileRecognizerDescriptor descriptor =
                    product.Files.Recognizer?.Descriptor
                    ?? FileRecognizerDescriptor.Custom;
                return new ResolvedRouteCapabilities
                {
                    Extension = item.Key,
                    Product = product.Manifest.Id,
                    Operations = operations,
                    RequiresContentProbe = true,
                    RecognizerStrategy = descriptor.Strategy,
                    MaxProbeBytes = Math.Min(
                        effective.MaxPrefixBytes,
                        descriptor.MaxProbeBytes),
                };
            })
            .ToArray();
        return new RoutingCapabilities
        {
            DefaultProduct = DefaultProduct?.Manifest.Id,
            DefaultProductSource = DefaultProduct is null
                ? null
                : "product-manifest",
            TotalProbeMilliseconds =
                (int)Math.Ceiling(effective.RecognizerTimeout.TotalMilliseconds),
            TotalProbeBytes = effective.MaxPrefixBytes,
            MaxConcurrency = effective.MaxConcurrency,
            IndeterminatePolicy = "fail-closed-explicit-product-required",
            Routes = routes,
        };
    }

    /// <summary>All extensions with a unique generic-routing owner.</summary>
    public IReadOnlyList<string> DefaultOwnerExtensions =>
        _defaultOwnerExtensions;

    /// <summary>Renders a product-owned result when an exact registration exists.</summary>
    public bool TryRender(
        Aspose.Cli.Sdk.Contracts.ResultEnvelope result,
        Output.TableSurface surface)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!_outputs.TryGetValue(result.GetType(), out ProductOutputDefinition? output))
        {
            return false;
        }

        output.Render(result, surface);
        return true;
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
        var owners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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
            foreach (string extension in product.Files.DefaultOwnerExtensions)
            {
                string normalized = NormalizeExtension(extension);
                if (!owners.TryAdd(normalized, productId))
                {
                    throw new InvalidOperationException(
                        $"Extension '{normalized}' has multiple default owners: "
                        + $"'{owners[normalized]}' and '{productId}'. "
                        + "Make all but one declaration explicit-only.");
                }
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
        string[] defaults = ordered
            .Where(static product => product.Manifest.IsDefaultCandidate)
            .Select(static product => product.Manifest.Id)
            .ToArray();
        if (defaults.Length > 1)
        {
            throw new InvalidOperationException(
                "A distribution can declare at most one default product. "
                + $"Candidates: {string.Join(", ", defaults)}.");
        }
        return new ProductCatalog(
            ordered,
            owners,
            ProductResourceCatalog.Build(
                ordered.Select(product => (
                    resources[product.Manifest.Id],
                    product.Manifest))));
    }
}
