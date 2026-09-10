using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Aspose.Cli.Sdk.Preview;

/// <summary>Stable kinds carried by the product-neutral preview envelope.</summary>
public static class ProductPreviewPayloadKinds
{
    public const string Selector = "selector";
    public const string Hint = "hint";
    public const string State = "state";
}

/// <summary>
/// One product-owned, versioned preview value. The shared runtime transports
/// the JSON payload but never interprets its domain fields.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProductPreviewPayload
{
    /// <summary>Product id that owns the payload schema and semantics.</summary>
    public required string ProductId { get; init; }

    /// <summary>Payload role, such as selector, hint, or state.</summary>
    public required string Kind { get; init; }

    /// <summary>Positive product payload contract version.</summary>
    public required int SchemaVersion { get; init; }

    /// <summary>Product-owned schema id registered by the product resource catalog.</summary>
    public required string SchemaId { get; init; }

    /// <summary>Opaque JSON value interpreted only by the owning product.</summary>
    public required JsonElement Payload { get; init; }

    /// <summary>
    /// Creates an envelope using source-generated product JSON metadata.
    /// Polymorphic CLR type names are never written.
    /// </summary>
    public static ProductPreviewPayload Create<T>(
        string productId,
        string kind,
        int schemaVersion,
        string schemaId,
        T value,
        JsonTypeInfo<T> jsonTypeInfo)
    {
        ArgumentNullException.ThrowIfNull(jsonTypeInfo);
        return new ProductPreviewPayload
        {
            ProductId = productId,
            Kind = kind,
            SchemaVersion = schemaVersion,
            SchemaId = schemaId,
            Payload = JsonSerializer.SerializeToElement(value, jsonTypeInfo),
        };
    }

    /// <summary>Reads the opaque value with its owning product serializer.</summary>
    public T Read<T>(JsonTypeInfo<T> jsonTypeInfo)
    {
        ArgumentNullException.ThrowIfNull(jsonTypeInfo);
        return Payload.Deserialize(jsonTypeInfo)
            ?? throw new JsonException(
                $"Preview payload '{SchemaId}' deserialized to null.");
    }

    internal int Utf8Bytes =>
        Encoding.UTF8.GetByteCount(Payload.GetRawText());
}

/// <summary>One product-declared preview payload contract.</summary>
public sealed record ProductPreviewPayloadContract
{
    public required string Kind { get; init; }

    public required int SchemaVersion { get; init; }

    public required string SchemaId { get; init; }

    public int MaxBytes { get; init; } = 16 * 1024;
}
