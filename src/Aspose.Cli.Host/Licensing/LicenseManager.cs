using System.Security.Cryptography;
using System.Text;
using Aspose.Cli.Sdk.Execution;
using Aspose.Cli.Host.Catalog;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Sdk.Configuration;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Licensing;

namespace Aspose.Cli.Host.Licensing;

/// <summary>Owns license management and status for CLI, App and background services.</summary>
internal sealed class LicenseManager(ProductCatalog catalog, GlobalValues globals)
{
    private readonly object _gate = new();
    private Snapshot? _snapshot;

    public GlobalValues Globals => globals;
    public CommandContext CreateContext() => CompositionRoot.Create(catalog, globals,
        runtimeLicenses: id => catalog.Activate(id, Current.Activation).LicenseGate);

    public LicenseStatusResult Status() => Current.Status;
    public string? Identity => Current.Status.Identity;

    private Snapshot Current
    {
        get { lock (_gate) { return _snapshot ??= Capture(CompositionRoot.Create(catalog, globals)); } }
    }

    public IReadOnlyList<string> Install(Stream input, string? productId)
    {
        lock (_gate)
        {
            CommandContext context = CompositionRoot.Create(catalog, globals);
            _ = RequireApplicable(catalog, productId, "installation");
            var compatible = new List<string>();
            LicenseInstaller.InstallMany(context.ResourceBudgets, input, snapshot =>
            {
                IReadOnlyList<ProductLicenseStatus> statuses = LicenseValidationProcess.Inspect(context, snapshot, productId).Products;
                compatible.AddRange(statuses.Where(static status => status.Mode == LicenseModes.Licensed)
                    .Select(static status => status.Product));
                if (compatible.Count == 0)
                {
                    throw CliErrors.LicenseInvalid("file", statuses.FirstOrDefault()?.Problem
                        ?? "the file is not valid for the selected products");
                }
                ConfigurationPaths.EnsureUserDirectory();
                return compatible.Select(id => LicenseResolver.UserLicensePath(context.ProductActivation.ConfigDirectory, id));
            });
            return compatible;
        }
    }

    public bool Remove(string? productId)
    {
        lock (_gate)
        {
            return Remove(CompositionRoot.Create(catalog, globals), productId);
        }
    }

    /// <summary>Reports validation failures per product without hiding healthy products.</summary>
    internal static LicenseStatusResult Inspect(CommandContext context, string? productId = null) =>
        Capture(context, productId).Status;

    internal static string? InstanceIdentity(CommandContext context) => Capture(context).Status.Identity;

    internal static string? IsolatedInstanceIdentity(CommandContext context) =>
        LicenseValidationProcess.Inspect(context, snapshotPath: null, productId: null).Identity;

    private static Snapshot Capture(CommandContext context, string? productId = null)
    {
        ProductDefinition[] products = Select(context.Catalog, productId);
        ProductLicenseStatus[] statuses = products.Select(product => Inspect(context, product)).ToArray();
        bool applicable = statuses.Any(static status => status.Applicable);
        string? identity = null;
        if (statuses.All(static status => status.Mode != LicenseModes.Invalid))
        {
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (ProductDefinition product in products)
            {
                hash.AppendData(Encoding.UTF8.GetBytes(product.Manifest.Id + "\0" + RequireIdentity(context, product) + "\0"));
            }
            identity = Convert.ToHexStringLower(hash.GetHashAndReset());
        }
        return new Snapshot(new LicenseStatusResult
        {
            Applicable = applicable,
            Products = statuses,
            Identity = identity,
            SharedUserLicenseInstalled = applicable && WorkerOutputSession.FileExists(LicenseResolver.SharedUserLicensePath(context.ProductActivation.ConfigDirectory)),
        }, context.ProductActivation);
    }

    /// <summary>Validates before reuse and identifies the exact bytes applied to this product.</summary>
    internal static string RequireIdentity(CommandContext context, ProductDefinition product) =>
        context.Activate(product).LicenseGate.Identity;

    internal static IReadOnlyList<string> Install(CommandContext context, string sourcePath, string? productId) =>
        Install(context, productId, validate => LicenseInstaller.InstallMany(context.ResourceBudgets, sourcePath, validate));

    internal static IReadOnlyList<string> Install(CommandContext context, Stream input, string? productId) =>
        Install(context, productId, validate => LicenseInstaller.InstallMany(context.ResourceBudgets, input, validate));

    private static IReadOnlyList<string> Install(CommandContext context, string? productId,
        Func<Func<string, IEnumerable<string>>, IReadOnlyList<string>> publish)
    {
        ProductDefinition[] products = RequireApplicable(context.Catalog, productId, "installation");
        var compatible = new List<string>();
        publish(snapshot =>
        {
            CommandContext validation = CompositionRoot.Create(
                context.Catalog, context.Globals with { LicensePath = snapshot },
                deadline: context.Deadline, resourceBudgets: context.ResourceBudgets);
            CliException? rejected = null;
            foreach (ProductDefinition product in products)
            {
                try
                {
                    if (validation.Activate(product).LicenseGate.EnsureApplied() == LicenseState.Licensed)
                    {
                        compatible.Add(product.Manifest.Id);
                    }
                }
                catch (CliException exception)
                {
                    if (productId is not null) { throw; }
                    rejected ??= exception;
                }
            }
            if (compatible.Count == 0)
            {
                throw rejected ?? CliErrors.LicenseInvalid("file", "the file is not valid for the selected products");
            }
            ConfigurationPaths.EnsureUserDirectory();
            return compatible.Select(id => LicenseResolver.UserLicensePath(context.ProductActivation.ConfigDirectory, id));
        });
        return compatible;
    }

    internal static bool Remove(CommandContext context, string? productId)
    {
        bool sharedOnly = string.Equals(productId, "shared", StringComparison.Ordinal);
        ProductDefinition[] products = RequireApplicable(context.Catalog, sharedOnly ? null : productId, "removal");
        IEnumerable<string> paths = sharedOnly
            ? []
            : products.Select(product => LicenseResolver.UserLicensePath(
                context.ProductActivation.ConfigDirectory, product.Manifest.Id));
        if (productId is null || sharedOnly)
        {
            paths = paths.Prepend(LicenseResolver.SharedUserLicensePath(context.ProductActivation.ConfigDirectory));
        }
        return LicenseInstaller.RemoveMany(context.ResourceBudgets, paths).Count > 0;
    }

    internal static void EnsureApplicable(ProductCatalog catalog, string? productId, string operation) =>
        _ = RequireApplicable(catalog, productId, operation);

    private static ProductLicenseStatus Inspect(CommandContext context, ProductDefinition product)
    {
        ILicenseGate gate = context.Activate(product).LicenseGate;
        CliException? error = null;
        string mode;
        try { mode = gate.EnsureApplied().ToContractName(); }
        catch (CliException exception) { error = exception; mode = LicenseModes.Invalid; }
        return new ProductLicenseStatus
        {
            Product = product.Manifest.Id,
            Name = product.Manifest.DisplayName,
            Applicable = gate.IsApplicable,
            Mode = mode,
            Source = gate.Resolution.SourceLabel,
            Path = gate.Resolution.Path,
            Problem = error?.Message,
            Hint = error?.Hint,
            UserLicenseInstalled = gate.IsApplicable && WorkerOutputSession.FileExists(LicenseResolver.UserLicensePath(
                context.ProductActivation.ConfigDirectory, product.Manifest.Id)),
        };
    }

    private static ProductDefinition[] RequireApplicable(ProductCatalog catalog, string? productId, string operation)
    {
        ProductDefinition[] selected = Select(catalog, productId);
        ProductDefinition[] applicable = selected.Where(static product => product.Manifest.Engine.LicenseApplicable).ToArray();
        if (applicable.Length == 0)
        {
            throw CliErrors.LicenseNotApplicable(operation, productId,
                catalog.Products.Where(static product => product.Manifest.Engine.LicenseApplicable)
                    .Select(static product => product.Manifest.Id).ToArray());
        }
        return applicable;
    }

    private sealed record Snapshot(LicenseStatusResult Status, ProductActivationContext Activation);

    private static ProductDefinition[] Select(ProductCatalog catalog, string? productId) =>
        productId is null ? catalog.Products.ToArray() : [catalog.ResolveById(productId)];
}
