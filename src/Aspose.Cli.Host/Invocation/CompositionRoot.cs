using Aspose.Cli.Sdk;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.IO.Paths;
using Aspose.Cli.Sdk.IO.Publication;
using Aspose.Cli.Sdk.Licensing;

namespace Aspose.Cli.Host.Invocation;

/// <summary>
/// Hand-wired object graph of the CLI. The graph is small and a one-shot
/// process gains nothing from a DI container, so wiring stays explicit and
/// readable.
/// </summary>
internal static class CompositionRoot
{
    public static CommandContext Create(
        ProductCatalog catalog,
        GlobalValues globals,
        ResourceBudgetLedger resourceBudgets,
        UserLicenseChanges? licenseChanges = null,
        Func<string, string?>? licenseEnvironment = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(globals);
        ArgumentNullException.ThrowIfNull(resourceBudgets);

        string workDir = ResolveWorkDir(globals.WorkDir);
        return new CommandContext
        {
            ReadEnvironment = InvocationEnvironment.CreateSecretReader(resourceBudgets),
            Globals = globals,
            ResourceBudgets = resourceBudgets,
            Paths = new PathResolver(workDir),
            ProductActivation = CreateActivation(
                catalog,
                globals,
                workDir,
                resourceBudgets,
                licenseChanges,
                EnvironmentFor(catalog, licenseEnvironment)),
            Catalog = catalog,
        };
    }

    // The license variables come from the given environment alone, when there is one: a render
    // for another process resolves that process's license, never this process's.
    private static Func<string, string?> EnvironmentFor(
        ProductCatalog catalog,
        Func<string, string?>? licenseEnvironment)
    {
        if (licenseEnvironment is null)
        {
            return Environment.GetEnvironmentVariable;
        }
        var names = new HashSet<string>(InvocationEnvironment.LicenseVariables(catalog), StringComparer.OrdinalIgnoreCase);
        return name => names.Contains(name) ? licenseEnvironment(name) : Environment.GetEnvironmentVariable(name);
    }

    private static ProductActivationContext CreateActivation(
        ProductCatalog catalog,
        GlobalValues globals,
        string workDirectory,
        ResourceBudgetLedger resourceBudgets,
        UserLicenseChanges? licenseChanges,
        Func<string, string?> environmentVariable)
    {
        var writer = new SafeFileWriter(resourceBudgets);
        return new ProductActivationContext
        {
            WorkDirectory = workDirectory,
            LicensePath = ResolveLicensePath(globals.LicensePath, workDirectory),
            EvaluationRequested = globals.EvaluationRequested,
            UserLicenseChanges = licenseChanges,
            ConfigDirectory = Aspose.Cli.Sdk.Configuration.ConfigurationPaths.UserDirectory(),
            EnvironmentVariable = environmentVariable,
            SafeFileWriter = writer,
            ResourceBudgets = resourceBudgets,
            Formats = [.. catalog.Products.SelectMany(static product => product.Formats)],
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
                GlobalOptionNames.WorkDir,
                $"directory does not exist: {full}");
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
        CancellationToken cancellation,
        long? outputBytesLimit = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var limits = new Dictionary<string, long>(StringComparer.Ordinal)
        {
            [ResourceBudgetKinds.InputBytes] = globals.MaxInputBytes,
        };
        if (outputBytesLimit is { } bytes) { limits[ResourceBudgetKinds.OutputBytes] = bytes; }
        foreach (ResourceBudget budget
            in catalog.Products.SelectMany(static product => product.Manifest.ResourceBudgets))
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
        return new ResourceBudgetLedger(limits, cancellation);
    }
}
