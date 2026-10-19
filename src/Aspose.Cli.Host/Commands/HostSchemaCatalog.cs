using System.Diagnostics.CodeAnalysis;
using Aspose.Cli.Host.App;
using Aspose.Cli.Sdk.Serialization;

namespace Aspose.Cli.Host.Commands;

/// <summary>
/// Immutable host schema view composed from the common, host-owned and product-owned schemas.
/// </summary>
internal sealed class HostSchemaCatalog
{
    /// <summary>The schemas the host itself publishes under <c>v2/common</c>, such as the App status.</summary>
    private static readonly ResultSchemaSet HostSchemas = new(
        "common",
        [
            .. SdkSchemaCatalog.ResultRecordsOf(HostResultJsonContext.Default),
            .. SdkSchemaCatalog.ResultRecordsOf(AppJsonContext.Default),
        ],
        SdkSchemaCatalog.Schemas);

    /// <summary>Every <c>v2/common</c> schema id, the SDK's and the host's, in ordinal order.</summary>
    internal static IReadOnlyList<string> CommonIds { get; } =
        [.. SdkSchemaCatalog.Ids.Concat(HostSchemas.Ids).Order(StringComparer.Ordinal)];

    private readonly Aspose.Cli.Sdk.Extensibility.ProductCatalog _catalog;

    public HostSchemaCatalog(
        Aspose.Cli.Sdk.Extensibility.ProductCatalog catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        string[] ids = [.. CommonIds.Concat(catalog.Resources.SchemaIds).Order(StringComparer.Ordinal)];
        // The SDK, the host and the products publish disjoint ids, so no schema shadows another.
        if (ids.Zip(ids.Skip(1)).FirstOrDefault(static pair => pair.First == pair.Second).First is { } shared)
        {
            throw new InvalidOperationException($"Schema '{shared}' is published twice, by the SDK, the host or a product.");
        }

        Ids = ids;
    }

    public IReadOnlyList<string> Ids { get; }

    public bool TryRead(
        string id,
        [NotNullWhen(true)] out string? document) =>
        _catalog.Resources.TryRead(id, out document) || TryReadCommon(id, out document);

    /// <summary>Reads a <c>v2/common</c> schema, the SDK's or the host's.</summary>
    internal static bool TryReadCommon(
        string id,
        [NotNullWhen(true)] out string? document) =>
        SdkSchemaCatalog.TryRead(id, out document) || HostSchemas.TryRead(id, out document);

    public IReadOnlyList<string> GetOperations(string schemaId) =>
        _catalog.Resources.GetOperations(schemaId);

    public bool TryReadOperation(
        string schemaId,
        string operationId,
        [NotNullWhen(true)] out string? document) =>
        _catalog.Resources.TryReadOperation(schemaId, operationId, out document);
}
