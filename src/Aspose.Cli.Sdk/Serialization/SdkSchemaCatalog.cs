using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization.Metadata;
using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Sdk.Serialization;

/// <summary>
/// The common JSON Schemas the SDK publishes under <c>v2/common</c>, written from the SDK's
/// result records (see <see cref="ResultSchemaSet"/>). The <c>aspose-cli schema</c> command
/// prints them, and product schemas reference them by URI.
/// </summary>
public static class SdkSchemaCatalog
{
    /// <summary>The SDK's result schemas.</summary>
    public static ResultSchemaSet Schemas { get; } = new("common", ResultRecordsOf(SdkJsonContext.Default), common: null);

    /// <summary>All common schema ids, e.g. <c>v2/common/error</c>, in ordinal order.</summary>
    public static IReadOnlyList<string> Ids => Schemas.Ids;

    /// <summary>Returns the schema document for an id from <see cref="Ids"/>.</summary>
    /// <exception cref="ArgumentException">The id is not in the catalog.</exception>
    public static string Read(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        return TryRead(id, out string? schema)
            ? schema
            : throw new ArgumentException($"Unknown schema id '{id}'. Known ids: {string.Join(", ", Ids)}", nameof(id));
    }

    /// <summary>Attempts to read the schema document for an id.</summary>
    public static bool TryRead(string id, [NotNullWhen(true)] out string? schema) => Schemas.TryRead(id, out schema);

    /// <summary>
    /// The result records a JSON context carries: the generator implements
    /// <see cref="IResultSchemaSource"/> on every context that lists a published record.
    /// </summary>
    public static IReadOnlyList<ResultRecord> ResultRecordsOf(IJsonTypeInfoResolver resolver) =>
        resolver is IResultSchemaSource source ? source.ResultRecords : [];
}
