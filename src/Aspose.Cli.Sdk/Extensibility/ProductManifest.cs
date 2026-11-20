using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Sdk.Extensibility;

/// <summary>Immutable identity and advertised public surface of one product.</summary>
public sealed record ProductManifest
{
    /// <summary>Stable lower-case identifier such as <c>cells</c>.</summary>
    public required string Id { get; init; }

    /// <summary>Human-readable product name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>The position of the product where products are listed.</summary>
    public int DisplayOrder { get; init; }

    /// <summary>The engine compiled into the product.</summary>
    public required ProductEngine Engine { get; init; }

    /// <summary>Product-owned domain resource budgets.</summary>
    public IReadOnlyList<ResourceBudget> ResourceBudgets { get; init; } = [];
}

/// <summary>The engine a product runs on: its SDK package and the version the build compiles.</summary>
public sealed record ProductEngine
{
    /// <summary>The engine's SDK package, e.g. <c>Aspose.Cells</c>.</summary>
    [MinLength(1)]
    public required string Sdk { get; init; }

    /// <summary>The engine SDK package version.</summary>
    [MinLength(1)]
    public required string SdkVersion { get; init; }
}
