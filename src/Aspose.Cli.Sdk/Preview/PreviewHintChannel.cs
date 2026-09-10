using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Sdk.Preview;

/// <summary>
/// A best-effort edit hint whose targets are versioned product payloads.
/// The shared channel owns freshness and transport only.
/// </summary>
public sealed record PreviewHint(
    IReadOnlyList<ProductPreviewPayload> Targets,
    string WriteId,
    long AtUnixMs);

/// <summary>
/// Best-effort sideband from an editing command to live preview. Product
/// fields remain inside opaque, schema-identified payloads; this class never
/// grows a sheet/page/range/block union.
/// </summary>
public static class PreviewHintChannel
{
    private const int MaxPayloadBytes = 64 * 1024;

    public static void Write(string filePath, PreviewHint hint)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(hint);
        if (hint.Targets.Any(static target => !IsValidEnvelope(target)))
        {
            throw new ArgumentException(
                "Preview hints contain an invalid product payload envelope.",
                nameof(hint));
        }

        string path = HintFilePath(filePath);
        PrivateUserStorage.EnsureDirectory(Path.GetDirectoryName(path)!);
        var payload = new JsonObject
        {
            ["writeId"] = hint.WriteId,
            ["atUnixMs"] = hint.AtUnixMs,
            ["targets"] = TargetsToJson(hint.Targets),
        };
        PrivateUserStorage.WriteAllText(path, payload.ToJsonString());
    }

    /// <summary>Best-effort publication for decorative post-edit focus targets.</summary>
    public static bool TryWrite(
        string filePath,
        IReadOnlyList<ProductPreviewPayload> targets)
    {
        if (targets.Count == 0)
        {
            return false;
        }
        try
        {
            Write(filePath, new PreviewHint(
                targets,
                Guid.NewGuid().ToString("N"),
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static bool TryConsume(
        string filePath,
        TimeSpan maxAge,
        ISet<string> seenWriteIds,
        [NotNullWhen(true)] out PreviewHint? hint)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(seenWriteIds);
        hint = null;

        string json;
        try
        {
            json = PrivateUserStorage.ReadAllText(HintFilePath(filePath));
        }
        catch (Exception)
        {
            return false;
        }

        if (!TryParse(json, out PreviewHint? parsed))
        {
            return false;
        }
        long age =
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - parsed.AtUnixMs;
        if (age > (long)maxAge.TotalMilliseconds
            || !seenWriteIds.Add(parsed.WriteId))
        {
            return false;
        }
        hint = parsed;
        return true;
    }

    /// <summary>
    /// Creates the versioned product payload transport object.
    /// </summary>
    public static JsonObject CreateTransportObject(
        ProductPreviewPayload target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var item = new JsonObject
        {
            ["productId"] = target.ProductId,
            ["kind"] = target.Kind,
            ["schemaVersion"] = target.SchemaVersion,
            ["schemaId"] = target.SchemaId,
            ["payload"] = JsonNode.Parse(target.Payload.GetRawText()),
        };

        return item;
    }

    private static string HintFilePath(string filePath)
    {
        string normalized = Path.GetFullPath(filePath);
        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
        {
            normalized = normalized.ToLowerInvariant();
        }
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        string key = Convert.ToHexString(hash)[..16].ToLowerInvariant();
        return Path.Combine(
            PrivateUserStorage.TemporaryRoot(),
            "preview-hints",
            key + ".json");
    }

    private static JsonArray TargetsToJson(
        IReadOnlyList<ProductPreviewPayload> targets)
    {
        var array = new JsonArray();
        foreach (ProductPreviewPayload target in targets)
        {
            array.Add(CreateTransportObject(target));
        }
        return array;
    }

    private static bool TryParse(
        string json,
        [NotNullWhen(true)] out PreviewHint? hint)
    {
        hint = null;
        JsonObject? root;
        try
        {
            root = JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            return false;
        }
        if (root is null
            || root["writeId"] is not JsonValue idNode
            || !idNode.TryGetValue(out string? writeId)
            || string.IsNullOrEmpty(writeId)
            || root["atUnixMs"] is not JsonValue stampNode
            || !stampNode.TryGetValue(out long atUnixMs)
            || root["targets"] is not JsonArray targetNodes)
        {
            return false;
        }

        var targets = new List<ProductPreviewPayload>(targetNodes.Count);
        foreach (JsonNode? node in targetNodes)
        {
            if (node is not JsonObject item
                || !TryReadPayload(item, out ProductPreviewPayload? payload))
            {
                return false;
            }
            targets.Add(payload);
        }
        hint = new PreviewHint(targets, writeId, atUnixMs);
        return true;
    }

    private static bool TryReadPayload(
        JsonObject item,
        [NotNullWhen(true)] out ProductPreviewPayload? payload)
    {
        payload = null;
        if (!TryRequiredString(item, "productId", out string? productId)
            || !TryRequiredString(item, "kind", out string? kind)
            || !TryRequiredString(item, "schemaId", out string? schemaId)
            || item["schemaVersion"] is not JsonValue versionNode
            || !versionNode.TryGetValue(out int schemaVersion)
            || item["payload"] is not JsonNode value)
        {
            return false;
        }
        JsonElement element =
            JsonSerializer.SerializeToElement(value);
        var candidate = new ProductPreviewPayload
        {
            ProductId = productId,
            Kind = kind,
            SchemaVersion = schemaVersion,
            SchemaId = schemaId,
            Payload = element,
        };
        if (!IsValidEnvelope(candidate))
        {
            return false;
        }
        payload = candidate;
        return true;
    }

    private static bool IsValidEnvelope(ProductPreviewPayload payload) =>
        !string.IsNullOrWhiteSpace(payload.ProductId)
        && !string.IsNullOrWhiteSpace(payload.Kind)
        && payload.SchemaVersion == 2
        && payload.SchemaId.StartsWith(
            $"v2/{payload.ProductId}/",
            StringComparison.Ordinal)
        && payload.Payload.ValueKind is not (
            JsonValueKind.Undefined or JsonValueKind.Null)
        && payload.Utf8Bytes <= MaxPayloadBytes;

    private static bool TryRequiredString(
        JsonObject item,
        string key,
        [NotNullWhen(true)] out string? value)
    {
        value = null;
        return item[key] is JsonValue node
            && node.TryGetValue(out value)
            && !string.IsNullOrWhiteSpace(value);
    }

}
