using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// The single error shape of the CLI, written to stderr when a command fails.
/// Exit codes are coarse and frozen; <see cref="ErrorPayload.Code"/> carries
/// the precise, machine-readable meaning.
/// </summary>
public sealed record ErrorEnvelope
{
    [JsonPropertyOrder(-100)]
    public string Schema { get; } = CommonSchemaIds.Error;

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
