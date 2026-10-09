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
    private static readonly ResultSchemaSet HostSchemas =
        new("common", SdkSchemaCatalog.ResultRecordsOf(AppJsonContext.Default), SdkSchemaCatalog.Schemas);

    private readonly Aspose.Cli.Sdk.Extensibility.ProductCatalog _catalog;

    public HostSchemaCatalog(
        Aspose.Cli.Sdk.Extensibility.ProductCatalog catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        Ids = SdkSchemaCatalog.Ids
            .Concat(HostSchemas.Ids)
            .Concat(catalog.Resources.SchemaIds)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<string> Ids { get; }

    public bool TryRead(
        string id,
        [NotNullWhen(true)] out string? document) =>
        _catalog.Resources.TryRead(id, out document)
        || SdkSchemaCatalog.TryRead(id, out document)
        || HostSchemas.TryRead(id, out document);

    public IReadOnlyList<string> GetOperations(string schemaId) =>
        _catalog.Resources.GetOperations(schemaId);

    public bool TryReadOperation(
        string schemaId,
        string operationId,
        [NotNullWhen(true)] out string? document) =>
        _catalog.Resources.TryReadOperation(schemaId, operationId, out document);
}
