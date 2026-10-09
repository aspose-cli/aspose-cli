using System.Text.Json.Serialization;

namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// Result of <c>aspose-cli skill install</c>: the agent skill bundled inside the
/// binary was extracted to a target directory, ready for an agent framework to
/// load.
/// </summary>
public sealed record SkillInstallResult() : ResultEnvelope("skill-install", 2)
{
    /// <summary>The installed skill's name, e.g. <c>aspose-cli-cells</c>.</summary>
    [JsonPropertyOrder(-50)]
    public required string Skill { get; init; }

    /// <summary>Absolute directory the skill was written to.</summary>
    [JsonPropertyOrder(-49)]
    public required string Target { get; init; }

    /// <summary>Number of files written.</summary>
    [Minimum(0)]
    public required int FileCount { get; init; }
}
