using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Sdk.Extensibility;

/// <summary>Immutable identity and advertised public surface of one product.</summary>
public sealed record ProductManifest
{
    /// <summary>Stable lower-case identifier such as <c>cells</c>.</summary>
    public required string Id { get; init; }

    /// <summary>Human-readable product name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Stable contract family implemented by this package.</summary>
    public string ContractVersion { get; init; } = "v1";

    /// <summary>Deterministic order used by capabilities and UI surfaces.</summary>
    public int DisplayOrder { get; init; }

    /// <summary>Whether this product is the preferred generic default when compiled.</summary>
    public bool IsDefaultCandidate { get; init; }

    /// <summary>Default engine compiled into the product.</summary>
    public required ProductEngineCapabilities Engine { get; init; }

    /// <summary>Engine ids available in this build.</summary>
    public required IReadOnlyList<string> AvailableEngines { get; init; }

    /// <summary>Product-owned domain resource budgets.</summary>
    public IReadOnlyList<ResourceBudgetCapabilities> ResourceBudgets { get; init; } = [];
}
