using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Execution;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Host.Invocation;

/// <summary>Everything a command handler needs for one invocation.</summary>
internal sealed class CommandContext
{
    public required GlobalValues Globals { get; init; }

    public required OperationDeadline Deadline { get; init; }

    public required ResourceBudgetLedger ResourceBudgets { get; init; }

    /// <summary>Resolves user paths against the working directory.</summary>
    public required PathResolver Paths { get; init; }

    public required ProductActivationContext ProductActivation { get; init; }

    public required ProductCatalog Catalog { get; init; }

    /// <summary>Activates and caches one product for this invocation.</summary>
    public ProductBinding Activate(ProductDefinition product) =>
        Catalog.Activate(
            product.Manifest.Id,
            ProductActivation);
}
