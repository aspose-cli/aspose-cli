using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Host.Contracts;

/// <summary>Result of <c>aspose-cli schema</c> without a schema id.</summary>
public sealed record SchemaListResult() : ResultEnvelope("schema-list")
{
    /// <summary>Canonical schema ids available in this compiled distribution.</summary>
    [UniqueItems]
    [MinLength(1)]
    public required IReadOnlyList<string> Schemas { get; init; }
}
