using Aspose.Cli.Sdk.Execution;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.TestKit;

/// <summary>Creates the real default invocation budgets for direct product-engine tests.</summary>
public static class ProductTestBudgets
{
    /// <summary>
    /// Starts one engine invocation; <paramref name="limits"/> replaces the default of each
    /// budget it names.
    /// </summary>
    public static TEngine StartEngine<TModule, TEngine>(
        Func<ResourceBudgetLedger, SafeFileWriter, TEngine> create,
        IReadOnlyDictionary<string, long>? limits = null)
        where TModule : IProductModule, new()
    {
        ArgumentNullException.ThrowIfNull(create);
        ProductTestInvocation invocation = Start<TModule>(limits);
        return create(invocation.ResourceBudgets, invocation.Writer);
    }

    public static ProductTestInvocation Start<TModule>(IReadOnlyDictionary<string, long>? limits = null)
        where TModule : IProductModule, new()
    {
        ResourceBudgetLedger resourceBudgets = Create(new TModule().Define(), limits);
        return new ProductTestInvocation(
            resourceBudgets,
            new SafeFileWriter(resourceBudgets));
    }

    public static ResourceBudgetLedger Create<TModule>()
        where TModule : IProductModule, new() =>
        Create(new TModule().Define());

    public static ResourceBudgetLedger Create(ProductDefinition product, IReadOnlyDictionary<string, long>? overrides = null)
    {
        ArgumentNullException.ThrowIfNull(product);
        Dictionary<string, long> limits =
            product.Manifest.ResourceBudgets.ToDictionary(
                static budget => budget.Kind,
                static budget =>
                    budget.Option is null
                    && budget.EnvironmentVariable is null
                        ? budget.Default
                        : budget.Maximum,
                StringComparer.Ordinal);
        foreach ((string kind, long limit) in overrides ?? new Dictionary<string, long>())
        {
            limits[kind] = limit;
        }

        return new ResourceBudgetLedger(
            OperationDeadline.Start(null),
            limits);
    }
}

public sealed record ProductTestInvocation(
    ResourceBudgetLedger ResourceBudgets,
    SafeFileWriter Writer);
