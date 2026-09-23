using System.Text.Json.Serialization;
using Aspose.Cli.Sdk.Serialization;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>
/// A batch of edit operations, the payload of <c>cells edit --ops</c>. One
/// vocabulary serves every surface (CLI today, an MCP wrapper tomorrow).
/// Batches are atomic by default: all ops apply, or the file is untouched.
/// </summary>
/// <remarks>
/// Authoring rules kept deliberately agent-friendly: <c>schema</c> is
/// optional on input and validated when present; an op's <c>sheet</c>
/// defaults to the active sheet; rows are 1-based numbers and columns are
/// letters, exactly as in A1 notation.
/// </remarks>
[ProductJsonRoot]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OpsBatch : BoundedOperationEnvelope<Op>;

/// <summary>Base of all edit operations; <c>op</c> is the discriminator.</summary>
[JsonConverter(typeof(Serialization.OpJsonConverter))]
public abstract record Op : BoundedOperation
{
    /// <summary>Target sheet; the active sheet when omitted.</summary>
    public string? Sheet { get; init; }
}
