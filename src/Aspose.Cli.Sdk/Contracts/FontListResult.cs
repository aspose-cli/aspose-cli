using System.Text.Json.Serialization;

namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// Result of <c>aspose-cli fonts list</c>: the font environment of each product engine — the
/// default fallback font and the sources it scans. Answers "where does this build look for
/// fonts, and what does it fall back to" so a "renders wrong on the server" problem can be
/// traced to font configuration.
/// </summary>
public sealed record FontListResult() : EngineResultEnvelope("font-list", 2)
{
    /// <summary>
    /// One entry per product, in catalog order: every product with font diagnostics, or only the
    /// one <c>--product</c> names.
    /// </summary>
    [JsonPropertyOrder(-50)]
    public required IReadOnlyList<ProductFonts> Products { get; init; }
}

/// <summary>The font environment of one product engine.</summary>
public sealed record ProductFonts
{
    /// <summary>The product whose font engine this is.</summary>
    public required string Product { get; init; }

    /// <summary>
    /// The font substituted for any unavailable font when rendering; omitted
    /// when the engine exposes no configured default (it then uses an internal
    /// fallback).
    /// </summary>
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
    public required string Kind { get; init; }

    /// <summary>Filesystem location; omitted for in-memory sources.</summary>
    public string? Location { get; init; }
}
