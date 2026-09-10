using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Execution;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;

namespace Aspose.Cli.Host.Invocation;

/// <summary>
/// Hand-wired object graph of the CLI. The graph is small and a one-shot
/// process gains nothing from a DI container, so wiring stays explicit and
/// readable. Future hosts (an MCP server, another engine) build their own
/// composition root against the same core ports.
/// </summary>
internal static class CompositionRoot
{
    /// <summary>
    /// Binds exactly one statically registered product for its product-owned
    /// command tree. No sibling runtime is constructed or exposed.
    /// </summary>
    public static ProductCommandContext<TPort> CreateProduct<TPort>(
        ProductCatalog catalog,
        string productId,
        GlobalValues globals,
        OperationDeadline? deadline = null,
        ResourceBudgetLedger? resourceBudgets = null)
        where TPort : class
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        ArgumentNullException.ThrowIfNull(globals);

        string workDir = ResolveWorkDir(globals.WorkDir);
        OperationDeadline effectiveDeadline =
            deadline ?? OperationDeadline.Start(null);
        ResourceBudgetLedger effectiveBudgets =
            resourceBudgets ?? CreateBudgets(catalog, globals, effectiveDeadline);
        ProductActivationContext activation = CreateActivation(
            globals,
            workDir,
            effectiveBudgets);
        ProductBinding<TPort> binding =
            catalog.Activate<TPort>(
                productId,
                activation);
        return new ProductCommandContext<TPort>
        {
            Binding = binding,
            Paths = new PathResolver(workDir),
            Globals = new ProductCommandGlobals
            {
                Quiet = globals.Quiet,
                Deadline = effectiveDeadline,
                ResourceBudgets = effectiveBudgets,
            },
        };
    }

    public static CommandContext Create(
        ProductCatalog catalog,
        GlobalValues globals,
        IReadOnlyDictionary<string, string>? productLicenseOverrides = null,
        OperationDeadline? deadline = null,
        ResourceBudgetLedger? resourceBudgets = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(globals);

        string workDir = ResolveWorkDir(globals.WorkDir);
        OperationDeadline effectiveDeadline =
            deadline ?? OperationDeadline.Start(null);
        ResourceBudgetLedger effectiveBudgets =
            resourceBudgets ?? CreateBudgets(catalog, globals, effectiveDeadline);

        return new CommandContext
        {
            Globals = globals,
            Deadline = effectiveDeadline,
            ResourceBudgets = effectiveBudgets,
            Paths = new PathResolver(workDir),
            ProductActivation = CreateActivation(
                globals,
                workDir,
                effectiveBudgets,
                productLicenseOverrides),
            Catalog = catalog,
        };
    }

    private static ProductActivationContext CreateActivation(
        GlobalValues globals,
        string workDirectory,
        ResourceBudgetLedger resourceBudgets,
        IReadOnlyDictionary<string, string>? productLicenseOverrides = null)
    {
        var writer = new SafeFileWriter(resourceBudgets);
        return new ProductActivationContext
        {
            WorkDirectory = workDirectory,
            LicensePathForProduct = productId =>
            {
                string? path = productLicenseOverrides is not null
                    && productLicenseOverrides.TryGetValue(
                        productId,
                        out string? productOverride)
                        ? productOverride
                        : globals.LicensePath;
                return ResolveLicensePath(path, workDirectory);
            },
            ConfigDirectory = Aspose.Cli.Sdk.Configuration.ConfigurationPaths.UserDirectory(),
            EnvironmentVariable = name =>
                ReadEnvironment(resourceBudgets, name),
            SafeFileWriter = writer,
            ResourceBudgets = resourceBudgets,
        };
    }

    private static string ResolveWorkDir(string? workDir)
    {
        if (workDir is null)
        {
            return Directory.GetCurrentDirectory();
        }

        string full = Path.GetFullPath(workDir);
        return Directory.Exists(full)
            ? full
            : throw CliErrors.OptionInvalid(
                "--workdir",
                $"directory does not exist: {full}",
                "Pass an existing directory, or drop --workdir to use the current directory.");
    }

    private static string? ResolveLicensePath(
        string? licensePath,
        string workDirectory) =>
        licensePath is null
            ? null
            : Path.GetFullPath(licensePath, workDirectory);

    internal static ResourceBudgetLedger CreateBudgets(
        ProductCatalog catalog,
        GlobalValues globals,
        OperationDeadline deadline)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var limits = new Dictionary<string, long>(StringComparer.Ordinal)
        {
            [ResourceBudgetKinds.InputBytes] = globals.MaxInputBytes,
        };
        foreach (ResourceBudgetCapabilities budget
            in catalog.Products
                .Select(catalog.GetCapabilities)
                .SelectMany(static product => product.ResourceBudgets))
        {
            if (budget.Default <= 0 || budget.Default > budget.Maximum)
            {
                throw new InvalidOperationException(
                    $"Resource budget '{budget.Kind}' has an invalid default/maximum.");
            }
            // Command-local options keep their published default. The shared
            // ledger enforces the advertised hard ceiling so a valid explicit
            // option can raise that local default without bypassing safety.
            long activeLimit = budget.Option is null
                && budget.EnvironmentVariable is null
                ? budget.Default
                : budget.Maximum;
            if (!limits.TryAdd(budget.Kind, activeLimit))
            {
                throw new InvalidOperationException(
                    $"Resource budget '{budget.Kind}' is declared more than once.");
            }
        }
        return new ResourceBudgetLedger(deadline, limits);
    }

    private static string? ReadEnvironment(
        ResourceBudgetLedger budgets,
        string name) =>
        string.Equals(
            name,
            InputSizeGuard.BudgetVariable,
            StringComparison.Ordinal)
            ? budgets.Limit(ResourceBudgetKinds.InputBytes)
                .ToString(System.Globalization.CultureInfo.InvariantCulture)
            : Environment.GetEnvironmentVariable(name);
}
