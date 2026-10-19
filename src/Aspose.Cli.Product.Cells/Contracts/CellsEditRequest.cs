using System.Diagnostics.CodeAnalysis;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>
/// Options of <c>cells edit</c>. The edit command admits <c>Verify</c> only for a published,
/// recalculated edit; a null <c>EncryptPassword</c> preserves the source encryption where the
/// target format supports it.
/// </summary>
public sealed record CellsEditRequest : BoundedEditRequest<CellsOpsBatch>
{
    /// <summary>Creates a request whose fields an object initializer sets.</summary>
    public CellsEditRequest()
    {
    }

    /// <summary>Creates the request of one resolved edit invocation.</summary>
    [SetsRequiredMembers]
    public CellsEditRequest(BoundedEditInvocation<CellsOpsBatch> edit)
        : base(edit)
    {
    }

    /// <summary>Recalculate formulas after applying the ops.</summary>
    public bool Recalculate { get; init; } = true;
}
