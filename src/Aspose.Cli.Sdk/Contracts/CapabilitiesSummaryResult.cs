namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// Result of <c>aspose-cli capabilities --summary</c>: the short first look at what this build
/// can do, projected from the capabilities document. Each product lists its commands with
/// their descriptions, its formats and its edit operation names;
/// <c>capabilities &lt;product&gt; &lt;command&gt;</c> gives the full detail of one command.
/// </summary>
public sealed record CapabilitiesSummaryResult() : ResultEnvelope("capabilities-summary", 2)
{
    /// <summary>CLI version, the same value as capabilities <c>cliVersion</c>.</summary>
    [MinLength(1)]
    public required string CliVersion { get; init; }

    /// <summary>One entry per selected product, in display order.</summary>
    public required IReadOnlyList<ProductCapabilitiesSummary> Products { get; init; }
}

/// <summary>What one product can do, without option and schema detail.</summary>
public sealed record ProductCapabilitiesSummary
{
    /// <summary>Product id as used on the command line, e.g. <c>cells</c>.</summary>
    [MinLength(1)]
    public required string Id { get; init; }

    /// <summary>Human-readable product name.</summary>
    [MinLength(1)]
    public required string Name { get; init; }

    /// <summary>What the product's command group does.</summary>
    [MinLength(1)]
    public string? Description { get; init; }

    /// <summary>Default engine id compiled into this product.</summary>
    [MinLength(1)]
    public string? Engine { get; init; }

    /// <summary>Version of that engine, as capabilities <c>enginePins</c> reports it.</summary>
    [MinLength(1)]
    public string? EngineVersion { get; init; }

    /// <summary>Format ids accepted as input.</summary>
    [MinLength(1)]
    public required IReadOnlyList<string> LoadFormats { get; init; }

    /// <summary>Format ids accepted by <c>convert --to</c>.</summary>
    [MinLength(1)]
    public required IReadOnlyList<string> ConvertFormats { get; init; }

    /// <summary>Format ids accepted by <c>render --to</c>.</summary>
    [MinLength(1)]
    public required IReadOnlyList<string> RenderFormats { get; init; }

    /// <summary>Visible product commands in path order.</summary>
    public required IReadOnlyList<CommandCapabilitiesSummary> Commands { get; init; }

    /// <summary>Operation names each operation-document command accepts.</summary>
    public required IReadOnlyList<OperationCapabilitiesSummary> Operations { get; init; }
}

/// <summary>One product command and what it does.</summary>
public sealed record CommandCapabilitiesSummary
{
    /// <summary>Product-relative command path, e.g. <c>query range</c>.</summary>
    [MinLength(1)]
    public required string Command { get; init; }

    /// <summary>The command's one-line description.</summary>
    [MinLength(1)]
    public string? Description { get; init; }
}

/// <summary>The operation names one operation-document command accepts.</summary>
public sealed record OperationCapabilitiesSummary
{
    /// <summary>Product-relative command path, e.g. <c>edit</c>.</summary>
    [MinLength(1)]
    public required string Command { get; init; }

    /// <summary>Operation names in published order.</summary>
    [MinLength(1)]
    public required IReadOnlyList<string> Ops { get; init; }
}
