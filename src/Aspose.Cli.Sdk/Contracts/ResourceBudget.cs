namespace Aspose.Cli.Sdk.Contracts;

/// <summary>One discoverable default and hard maximum for a resource.</summary>
[SchemaId("resource-budget")]
public sealed record ResourceBudget
{
    /// <summary>What the budget limits, such as <c>input-bytes</c>.</summary>
    [MinLength(1)]
    public required string Kind { get; init; }

    /// <summary>The limit applied when nothing overrides it.</summary>
    [Minimum(1)]
    public required long Default { get; init; }

    /// <summary>The hard safety maximum no override may exceed.</summary>
    [Minimum(1)]
    public required long Maximum { get; init; }

    /// <summary>The unit of <c>default</c> and <c>maximum</c>, such as <c>bytes</c>.</summary>
    [MinLength(1)]
    public required string Unit { get; init; }

    /// <summary>The command-line option that overrides the budget, when there is one.</summary>
    public string? Option { get; init; }

    /// <summary>The environment variable that overrides the budget, when there is one.</summary>
    public string? EnvironmentVariable { get; init; }

    /// <summary>When the budget is enforced, such as before or while the input is read.</summary>
    [MinLength(1)]
    public required string EnforcementStage { get; init; }

    public static ResourceBudget Domain(
        string kind,
        long defaultValue,
        long maximum,
        string unit,
        string enforcementStage,
        string? option = null,
        string? environmentVariable = null) =>
        new()
        {
            Kind = kind,
            Default = defaultValue,
            Maximum = maximum,
            Unit = unit,
            EnforcementStage = enforcementStage,
            Option = option,
            EnvironmentVariable = environmentVariable,
        };
}
