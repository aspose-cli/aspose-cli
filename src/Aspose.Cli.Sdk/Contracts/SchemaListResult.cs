namespace Aspose.Cli.Sdk.Contracts;

/// <summary>Result of <c>aspose-cli schema</c> without a schema id.</summary>
public sealed record SchemaListResult() : ResultEnvelope(CommonSchemaIds.SchemaList, 2)
{
    /// <summary>Canonical schema ids available in this compiled distribution.</summary>
    public required IReadOnlyList<string> Schemas { get; init; }
}
