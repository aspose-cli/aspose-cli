using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// The single error shape of the CLI, written to stderr when a command fails.
/// Exit codes are coarse and frozen; <see cref="ErrorPayload.Code"/> carries
/// the precise, machine-readable meaning.
/// </summary>
[SchemaId(Id)]
public sealed record ErrorEnvelope
{
    /// <summary>The relative id the error schema is published under.</summary>
    public const string Id = "error";

    /// <summary>
    /// The URI of the error schema, a constant so a startup failure can state it before anything
    /// runs; the schema catalog refuses to publish the error schema when it differs.
    /// </summary>
    public const string SchemaUri = DistributionInfo.SchemaBaseUri + "common/" + Id + ".schema.json";

    /// <summary>URI of the JSON schema this error conforms to.</summary>
    [JsonPropertyOrder(-100)]
    public string Schema { get; } = SchemaUri;

    /// <summary>Version of the error contract.</summary>
    [JsonPropertyOrder(-99)]
    public int SchemaVersion { get; } = 2;

    /// <summary>The error details.</summary>
    public required ErrorPayload Error { get; init; }
}

/// <summary>
/// Error details designed for agent self-correction: the <see cref="Message"/>
/// states the fact, <see cref="Details"/> carries structured context (often
/// including the valid alternatives), and <see cref="Hint"/> tells the caller
/// the most likely fix.
/// </summary>
public sealed record ErrorPayload
{
    /// <summary>Stable SCREAMING_SNAKE_CASE error code, e.g. <c>SHEET_NOT_FOUND</c>.</summary>
    [Pattern("^[A-Z][A-Z0-9_]*$")]
    public required string Code { get; init; }

    /// <summary>Human-readable description of what went wrong.</summary>
    public required string Message { get; init; }

    /// <summary>Structured context, e.g. <c>{"requested": "Salez", "available": ["Sales"]}</c>.</summary>
    public JsonObject? Details { get; init; }

    /// <summary>Recommended next action for the caller.</summary>
    public string? Hint { get; init; }

    /// <summary>Topic name for <c>aspose-cli docs &lt;topic&gt;</c> with background details.</summary>
    public string? Docs { get; init; }
}
