using System.Text.Json.Serialization;

namespace Aspose.Cli.Sdk.Contracts;

/// <summary>Result of <c>aspose-cli skill list</c>: skills bundled in this build.</summary>
public sealed record SkillListResult() : ResultEnvelope(CommonSchemaIds.SkillList, 2)
{
    /// <summary>Bundled skill packages available for installation.</summary>
    [JsonPropertyOrder(-50)]
    public required IReadOnlyList<SkillPackageInfo> Skills { get; init; }
}

/// <summary>One bundled Agent Skill and the supported host integrations.</summary>
public sealed record SkillPackageInfo
{
    /// <summary>Stable package name.</summary>
    public required string Name { get; init; }

    /// <summary>Short human-readable purpose.</summary>
    public required string Description { get; init; }

    /// <summary>Agent hosts with a known skills-directory convention.</summary>
    public required IReadOnlyList<string> Hosts { get; init; }
}
