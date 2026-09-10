using Aspose.Cli.Host.Catalog;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.Licensing;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Licensing;

namespace Aspose.Cli.Host.App;

/// <summary>
/// Tolerant multi-product license management for the local App. Repair
/// surfaces remain available even when one product's configured source is
/// missing or invalid.
/// </summary>
internal sealed class AppLicenseState
{
    private readonly object _gate = new();
    private readonly ProductCatalog _catalog;
    private readonly GlobalValues _globals;
    private readonly string _workDirectory;
    private readonly string _configDirectory;
    private readonly Dictionary<string, string> _activeLicensePaths =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AppLicenseView> _statusCache =
        new(StringComparer.OrdinalIgnoreCase);

    public AppLicenseState(ProductCatalog catalog, GlobalValues globals)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _globals = globals;
        _workDirectory = globals.WorkDir is null
            ? Directory.GetCurrentDirectory()
            : Path.GetFullPath(globals.WorkDir);
        _configDirectory = Aspose.Cli.Sdk.Configuration.ConfigurationPaths.UserDirectory();
    }

    public GlobalValues Globals => _globals;

    public AppLicenseView Status(string currentProductId)
    {
        lock (_gate)
        {
            if (_statusCache.TryGetValue(
                    currentProductId,
                    out AppLicenseView? cached))
            {
                return cached;
            }

            IReadOnlyList<ProductLicenseInspection> inspections =
                ProductLicenseInspector.Inspect(CreateContext());
            AppProductLicenseView[] products = inspections
                .Select(BuildProductView)
                .ToArray();
            bool anyApplicable = products.Any(
                static product => product.Applicable);
            bool sharedInstalled = anyApplicable
                && File.Exists(LicenseResolver.UserLicensePath());
            _statusCache.Clear();
            foreach (AppProductLicenseView product in products)
            {
                _statusCache[product.Product] = new AppLicenseView(
                    product.Applicable,
                    product.Mode,
                    product.Source,
                    product.Path,
                    product.Problem,
                    product.Hint,
                    product.UserLicenseInstalled,
                    sharedInstalled,
                    products);
            }

            return _statusCache.TryGetValue(
                    currentProductId,
                    out AppLicenseView? status)
                ? status
                : throw new InvalidOperationException(
                    $"Product '{currentProductId}' was not inspected.");
        }
    }

    public IReadOnlyList<string> Install(
        string sourcePath,
        string? requestedProductId)
    {
        string full = Path.GetFullPath(sourcePath);
        IReadOnlyList<ProductLicenseInstallation> installed =
            ProductLicenseProvisioning.Install(
                _catalog,
                full,
                _globals,
                requestedProductId);
        lock (_gate)
        {
            foreach (ProductLicenseInstallation install in installed)
            {
                _activeLicensePaths[install.Product.Manifest.Id] = install.Path;
            }

            InvalidateCache();
        }

        return installed
            .Select(static install => install.Product.Manifest.Id)
            .ToArray();
    }

    public bool RemoveUserLicense(string productId)
    {
        bool removed;
        lock (_gate)
        {
            if (string.Equals(productId, "shared", StringComparison.OrdinalIgnoreCase))
            {
                removed = ProductLicenseProvisioning.RemoveShared(_catalog);
                _activeLicensePaths.Clear();
            }
            else
            {
                removed = ProductLicenseProvisioning.RemoveProduct(
                    _catalog,
                    productId);
                _activeLicensePaths.Remove(productId);
            }

            InvalidateCache();
        }

        return removed;
    }

    public CommandContext CreatePreviewContext()
    {
        lock (_gate)
        {
            return CreateContext();
        }
    }

    private CommandContext CreateContext() =>
        CompositionRoot.Create(_catalog, _globals, _activeLicensePaths);

    private void InvalidateCache()
    {
        _statusCache.Clear();
    }

    private AppProductLicenseView BuildProductView(
        ProductLicenseInspection inspection)
    {
        string productId = inspection.Product.Manifest.Id;
        if (!inspection.Applicable)
        {
            return new AppProductLicenseView(
                productId,
                inspection.Product.Manifest.DisplayName,
                false,
                inspection.State.ToContractName(),
                null,
                null,
                null,
                null,
                false);
        }

        string userPath = LicenseResolver.UserLicensePath(_configDirectory, productId);
        bool userInstalled = File.Exists(userPath);
        string? source = inspection.Resolution.SourceLabel;
        string? path = inspection.Resolution.Path;
        string? problem = inspection.Error?.Message;
        string? hint = inspection.Error?.Hint;

        if (_activeLicensePaths.TryGetValue(productId, out string? active))
        {
            source = "user:" + productId;
            path = active;
            AddCliPrecedenceWarning(productId, ref problem, ref hint);
        }

        return new AppProductLicenseView(
            productId,
            inspection.Product.Manifest.DisplayName,
            true,
            inspection.State.ToContractName(),
            source,
            path,
            problem,
            hint,
            userInstalled);
    }

    private void AddCliPrecedenceWarning(
        string productId,
        ref string? problem,
        ref string? hint)
    {
        try
        {
            LicenseResolution normal = LicenseResolver.Resolve(
                _globals.LicensePath,
                productId,
                Environment.GetEnvironmentVariable,
                _workDirectory,
                _configDirectory);
            if (normal.Kind is not LicenseSourceKind.ProductUserFile)
            {
                problem =
                    $"The saved {productId} license is active in this App, but "
                    + $"'{normal.SourceLabel}' has higher priority for new CLI commands.";
                hint =
                    "Unset or fix the higher-priority source so the App and CLI use the same license.";
            }
        }
        catch (CliException exception)
        {
            problem =
                $"The saved {productId} license is active in this App, but a "
                + "higher-priority CLI source is broken.";
            hint = exception.Hint
                ?? "Unset or fix the higher-priority license source.";
        }
    }
}
