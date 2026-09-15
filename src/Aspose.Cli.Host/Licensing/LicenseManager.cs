using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text;
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
    private readonly Lazy<Snapshot> _snapshot = new(() => Capture(CompositionRoot.Create(catalog, globals)));

    public GlobalValues Globals => globals;
    public ILicenseGate RuntimeLicense(string productId) => Current.Gates[productId];

    public LicenseStatusResult Status() => Current.Status;
    public string? Identity => Current.Status.Identity;

    private Snapshot Current => _snapshot.Value;

    public IReadOnlyList<string> Install(Stream input, string? productId)
    {
        CommandContext context = CompositionRoot.Create(catalog, globals);
        _ = RequireApplicable(catalog, productId, "installation");
        string[] compatible = [];
        LicenseInstaller.InstallMany(context.ResourceBudgets, input, snapshot =>
        {
            LicenseStatusResult validation = LicenseValidationProcess.Inspect(context, snapshot, productId);
            (compatible, string[] destinations) = SelectInstallation(context, validation);
            return destinations;
        });
        return compatible;
    }

    public bool Remove(string? productId) => Remove(CompositionRoot.Create(catalog, globals), productId);

    /// <summary>Reports validation failures per product without hiding healthy products.</summary>
    internal static LicenseStatusResult Inspect(CommandContext context, string? productId = null) =>
        Capture(context, productId).Status;

    internal static string? InstanceIdentity(CommandContext context) => Capture(context).Status.Identity;

    internal static string? IsolatedInstanceIdentity(CommandContext context) =>
        LicenseValidationProcess.Inspect(context, snapshotPath: null, productId: null).Identity;

    private static Snapshot Capture(CommandContext context, string? productId = null)
    {
        ProductDefinition[] products = Select(context.Catalog, productId);
        FrozenDictionary<string, ILicenseGate> gates = products.ToFrozenDictionary(
            product => product.Manifest.Id, product => Freeze(context.Activate(product).LicenseGate), StringComparer.Ordinal);
        ProductLicenseStatus[] statuses = products.Select(product => Inspect(context, product, gates[product.Manifest.Id])).ToArray();
        bool applicable = statuses.Any(static status => status.Applicable);
        string? identity = null;
        if (statuses.All(static status => status.Mode != LicenseModes.Invalid))
        {
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (ProductDefinition product in products)
            {
                hash.AppendData(Encoding.UTF8.GetBytes(product.Manifest.Id + "\0" + gates[product.Manifest.Id].Identity + "\0"));
            }
            identity = Convert.ToHexStringLower(hash.GetHashAndReset());
        }
        return new Snapshot(new LicenseStatusResult
        {
            Applicable = applicable,
            Products = Array.AsReadOnly(statuses),
            Identity = identity,
            SharedUserLicenseInstalled = applicable && (context.ProductActivation.UserLicenseChanges?.IsSharedInstalled(context.ProductActivation.ConfigDirectory)
                ?? File.Exists(LicenseResolver.SharedUserLicensePath(context.ProductActivation.ConfigDirectory))),
        }, gates);
    }

    /// <summary>Validates before reuse and identifies the exact bytes applied to this product.</summary>
    internal static string RequireIdentity(CommandContext context, ProductDefinition product) =>
        context.Activate(product).LicenseGate.Identity;

    internal static LicenseStatusResult Install(CommandContext context, string sourcePath, string? productId)
    {
        _ = RequireApplicable(context.Catalog, productId, "installation");
        LicenseStatusResult? result = null;
        LicenseInstaller.InstallMany(context.ResourceBudgets, sourcePath, snapshot =>
        {
            CommandContext validation = CompositionRoot.Create(
                context.Catalog, context.Globals with { LicensePath = snapshot }, resourceBudgets: context.ResourceBudgets);
            (string[] compatible, string[] destinations) = SelectInstallation(context, Inspect(validation, productId));
            result = Inspect(WithChanges(context, new UserLicenseChanges(
                compatible.Select(id => KeyValuePair.Create<string, string?>(id, snapshot)))));
            return destinations;
        });
        return result!;
    }

    private static (string[] Products, string[] Destinations) SelectInstallation(CommandContext context, LicenseStatusResult validation)
    {
        string[] compatible = validation.Products.Where(static product => product.Mode == LicenseModes.Licensed)
            .Select(static product => product.Product).ToArray();
        if (compatible.Length == 0)
        {
            throw CliErrors.LicenseInvalid("file", validation.Products.FirstOrDefault()?.Problem
                ?? "the file is not valid for the selected products");
        }
        ConfigurationPaths.EnsureUserDirectory();
        return (compatible, compatible.Select(id =>
            LicenseResolver.UserLicensePath(context.ProductActivation.ConfigDirectory, id)).ToArray());
    }

    internal static LicenseStatusResult RemoveAndReport(CommandContext context, string? productId)
    {
        bool sharedOnly = productId == "shared";
        ProductDefinition[] products = RequireApplicable(context.Catalog, sharedOnly ? null : productId, "removal");
        var changes = new UserLicenseChanges(sharedOnly ? [] : products.Select(product =>
            KeyValuePair.Create<string, string?>(product.Manifest.Id, null)), productId is null || sharedOnly);
        LicenseStatusResult result = Inspect(WithChanges(context, changes));
        Remove(context, productId);
        return result;
    }

    private static CommandContext WithChanges(CommandContext context, UserLicenseChanges changes) =>
        CompositionRoot.Create(context.Catalog, context.Globals, resourceBudgets: context.ResourceBudgets,
            licenseChanges: changes);

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

    private static ProductLicenseStatus Inspect(CommandContext context, ProductDefinition product, ILicenseGate gate)
    {
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
            UserLicenseInstalled = gate.IsApplicable && (context.ProductActivation.UserLicenseChanges?.IsProductInstalled(context.ProductActivation.ConfigDirectory, product.Manifest.Id)
                ?? File.Exists(LicenseResolver.UserLicensePath(context.ProductActivation.ConfigDirectory, product.Manifest.Id))),
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

    private sealed record Snapshot(LicenseStatusResult Status, FrozenDictionary<string, ILicenseGate> Gates);

    private static ILicenseGate Freeze(ILicenseGate gate)
    {
        try { return new RuntimeLicenseGate(gate.IsApplicable, gate.Resolution, gate.EnsureApplied(), gate.Identity, null); }
        catch (CliException error) { return new RuntimeLicenseGate(gate.IsApplicable, gate.Resolution, default, null, error); }
    }

    private sealed record RuntimeLicenseGate(bool IsApplicable, LicenseResolution Resolution,
        LicenseState State, string? ValidatedIdentity, CliException? Error) : ILicenseGate
    {
        public string Identity => Error is null ? ValidatedIdentity! : throw Error;
        public LicenseState EnsureApplied() => Error is null ? State : throw Error;
    }

    private static ProductDefinition[] Select(ProductCatalog catalog, string? productId) =>
        productId is null ? catalog.Products.ToArray() : [catalog.ResolveById(productId)];
}
