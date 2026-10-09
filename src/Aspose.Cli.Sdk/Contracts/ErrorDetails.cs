using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// Base schema for product-owned diagnostic details: an object whose members a product may
/// narrow without changing the common envelope.
/// </summary>
[SchemaId(Id)]
public sealed record DiagnosticDetails
{
    /// <summary>The relative id the record's schema is published under.</summary>
    public const string Id = "diagnostic-details";

    /// <summary>The schema id the diagnostic catalog names the record's schema by.</summary>
    public static string CatalogId { get; } = ResultEnvelope.CatalogId("common", Id);

    /// <summary>The details' members.</summary>
    [JsonExtensionData]
    public JsonObject? Members { get; init; }
}

/// <summary>
/// Details of an error whose code names a target the document does not contain. They state what
/// was requested and what exists, so the caller can correct the request without another
/// inspection. Operation errors add the operation's index and op.
/// </summary>
[SchemaId(Id)]
public sealed record NotFoundDetails
{
    /// <summary>The relative id the record's schema is published under.</summary>
    public const string Id = "not-found-details";

    /// <summary>The schema id the diagnostic catalog names the record's schema by.</summary>
    public static string CatalogId { get; } = ResultEnvelope.CatalogId("common", Id);

    /// <summary>What was looked up, e.g. sheet, slide, bookmark or page.</summary>
    public required string Subject { get; init; }

    /// <summary>The name, number or range as the caller wrote it.</summary>
    public required string Requested { get; init; }

    /// <summary>How many targets of this kind the document contains.</summary>
    public required uint AvailableCount { get; init; }

    /// <summary>
    /// The names that exist, in document order; present for named targets and truncated to 50
    /// when availableCount is larger.
    /// </summary>
    [MaxItems(50)]
    public IReadOnlyList<string>? Available { get; init; }

    /// <summary>The existing names closest to the request, best first; present only when some name is close.</summary>
    [MaxItems(3)]
    public IReadOnlyList<string>? Suggestions { get; init; }

    /// <summary>The zero-based position of the failing operation in its batch; present only for an operation error.</summary>
    public uint? Index { get; init; }

    /// <summary>The failing operation's op name; present only for an operation error that states one.</summary>
    public string? Op { get; init; }
}
