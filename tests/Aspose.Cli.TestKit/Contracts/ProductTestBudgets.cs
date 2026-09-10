using Aspose.Cli.Sdk.Execution;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.TestKit;

/// <summary>Creates the real default invocation budgets for direct product-engine tests.</summary>
public static class ProductTestBudgets
{
    public static TEngine StartEngine<TModule, TEngine>(
        Func<ResourceBudgetLedger, SafeFileWriter, TEngine> create)
        where TModule : IProductModule, new()
    {
        ArgumentNullException.ThrowIfNull(create);
        ProductTestInvocation invocation = Start<TModule>();
        return create(invocation.ResourceBudgets, invocation.Writer);
    }

    public static ProductTestInvocation Start<TModule>()
        where TModule : IProductModule, new()
    {
        ResourceBudgetLedger resourceBudgets = Create<TModule>();
        return new ProductTestInvocation(
            resourceBudgets,
            new SafeFileWriter(resourceBudgets));
    }

    public static ResourceBudgetLedger Create<TModule>()
        where TModule : IProductModule, new() =>
        Create(new TModule().Define());

    public static ResourceBudgetLedger Create(ProductDefinition product)
    {
        ArgumentNullException.ThrowIfNull(product);
        IReadOnlyDictionary<string, long> limits =
            product.Manifest.ResourceBudgets.ToDictionary(
                static budget => budget.Kind,
                static budget =>
                    budget.Option is null
                    && budget.EnvironmentVariable is null
                        ? budget.Default
                        : budget.Maximum,
                StringComparer.Ordinal);
        return new ResourceBudgetLedger(
            OperationDeadline.Start(null),
            limits);
    }
}

public sealed record ProductTestInvocation(
    ResourceBudgetLedger ResourceBudgets,
    SafeFileWriter Writer);
