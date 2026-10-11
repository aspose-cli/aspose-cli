using System.Text.Json.Serialization;

namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// Result of <c>aspose-cli fonts check &lt;file&gt;</c>: whether the fonts a document uses
/// are available on this machine and, if not, what they will be substituted
/// with. This is the rendering-fidelity diagnostic that a product listing of the used
/// fonts cannot give. A document with a font that is not available here exits 8.
/// </summary>
public sealed record FontCheckResult() : EngineResultEnvelope("font-check", 2), IPartialOutcome
{
    /// <summary>The document that was checked.</summary>
    [JsonPropertyOrder(-50)]
    public required InputInfo Input { get; init; }

    /// <summary><c>true</c> when every used font is available — no substitution.</summary>
    [JsonPropertyOrder(-49)]
    public required bool AllAvailable { get; init; }

    /// <summary>Each font the document uses, with its availability.</summary>
    public required IReadOnlyList<FontAvailability> Fonts { get; init; }

    /// <summary>Whether some font is not available here. Not serialized.</summary>
    [JsonIgnore]
    public bool HasFailures => !AllAvailable;
}

/// <summary>Availability of one font a document uses.</summary>
public sealed record FontAvailability
{
    /// <summary>The font family name as stored in the document.</summary>
    public required string Name { get; init; }

    /// <summary>Whether the engine can render with this exact font.</summary>
    public required bool Available { get; init; }

    /// <summary>The font used instead; omitted when the font is available.</summary>
    public string? SubstitutedBy { get; init; }
}
