using System.Text.Json.Serialization;

namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// Result of <c>aspose-cli fonts list</c>: the engine's font environment — the
/// default fallback font and the sources it scans. Answers "where does this
/// build look for fonts, and what does it fall back to" so a "renders wrong on
/// the server" problem (P-5) can be traced to font configuration.
/// </summary>
public sealed record FontListResult() : EngineResultEnvelope("font-list", 2)
{
    /// <summary>
    /// The font substituted for any unavailable font when rendering; omitted
    /// when the engine exposes no configured default (it then uses an internal
    /// fallback).
    /// </summary>
    [JsonPropertyOrder(-50)]
    public string? DefaultFont { get; init; }

    /// <summary>
    /// The font sources the engine scans, in configured order. Empty means the
    /// engine uses its platform defaults (typically the OS font folders).
    /// </summary>
    public required IReadOnlyList<FontSource> Sources { get; init; }
}

/// <summary>One place the engine looks for fonts.</summary>
public sealed record FontSource
{
    /// <summary>Source kind: <c>folder</c>, <c>file</c>, <c>memory</c>, <c>system</c> or <c>other</c>.</summary>
    [AllowedValues("folder", "file", "memory", "system", "other")]
    public required string Type { get; init; }

    /// <summary>Filesystem location; omitted for in-memory sources.</summary>
    public string? Location { get; init; }
}
