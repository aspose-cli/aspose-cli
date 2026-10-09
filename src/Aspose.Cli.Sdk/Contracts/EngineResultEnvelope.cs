using System.Text.Json.Serialization;

namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// Base of a result whose command runs a product engine, which states the license mode the
/// engine ran under. A result whose command never runs an engine, such as
/// <c>capabilities</c>, derives from <see cref="ResultEnvelope"/> and carries no license.
/// </summary>
public abstract record EngineResultEnvelope : ResultEnvelope
{
    /// <summary>Starts a result that follows the schema <paramref name="schema"/>.</summary>
    /// <param name="schema">The schema id relative to the record's owner, such as <c>render-result</c>.</param>
    /// <param name="schemaVersion">The version of the result contract.</param>
    /// <exception cref="ArgumentException"><paramref name="schema"/> is not a relative schema id.</exception>
    protected EngineResultEnvelope(string schema, int schemaVersion)
        : base(schema, schemaVersion)
    {
    }

    /// <summary>License mode the engine ran under.</summary>
    [JsonPropertyOrder(900)]
    public LicenseInfo? License { get; init; }
}
