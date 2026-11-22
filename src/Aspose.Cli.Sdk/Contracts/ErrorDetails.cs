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
/// Details of an error whose code names a target that does not exist: a part the document does
/// not contain, or a file. They state what was requested and what exists, so the caller can
/// correct the request without another inspection. An object table's errors add the operation's
/// position and the address.
/// </summary>
[SchemaId(SchemaName)]
public sealed record NotFoundDetails
{
    /// <summary>The relative id the record's schema is published under.</summary>
    public const string SchemaName = "not-found-details";

    /// <summary>The schema id the diagnostic catalog names the record's schema by.</summary>
    public static string CatalogId { get; } = ResultEnvelope.CatalogId("common", SchemaName);

    /// <summary>What was looked up, e.g. sheet, slide, bookmark or page.</summary>
    public required string Subject { get; init; }

    /// <summary>The name, number, range or path as the caller wrote it.</summary>
    public required string Requested { get; init; }

    /// <summary>How many targets of this kind the document contains; present for a target in a document.</summary>
    public uint? AvailableCount { get; init; }

    /// <summary>The absolute path of the missing file; present for a file.</summary>
    public string? Path { get; init; }

    /// <summary>Where the missing file was configured, such as an environment variable; present when a setting names it.</summary>
    public string? Source { get; init; }

    /// <summary>
    /// The names that exist, in document order; present for named targets and truncated to 50
    /// when availableCount is larger.
    /// </summary>
    [MaxItems(50)]
    public IReadOnlyList<string>? Available { get; init; }

    /// <summary>The position of the failing operation in an object table batch, from 0; present only for such an operation.</summary>
    [Minimum(0)]
    public int? Index { get; init; }

    /// <summary>The object table address that was looked up; present only for an object table address.</summary>
    public string? At { get; init; }
}
