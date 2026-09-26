using Aspose.Cli.Sdk.Operations;

namespace Aspose.Cli.Product.Cells.Contracts;

// Ops for review and collaboration: cell comments.

/// <summary>Adds a note (comment) to a cell.</summary>
[Operation("add_comment")]
public sealed record AddCommentOp : CellsOp
{
    /// <summary>The cell to annotate, such as B2.</summary>
    [A1Cell] public required string Cell { get; init; }

    [MinLength(1)] public required string Text { get; init; }

    /// <summary>The comment author; the engine default when omitted.</summary>
    public string? Author { get; init; }
}

/// <summary>Replaces the text of a cell's comment.</summary>
[Operation("edit_comment")]
public sealed record EditCommentOp : CellsOp
{
    /// <summary>The cell whose comment changes.</summary>
    [A1Cell] public required string Cell { get; init; }

    /// <summary>The new comment text.</summary>
    [MinLength(1)] public required string Text { get; init; }

    /// <summary>The new author; unchanged when omitted.</summary>
    public string? Author { get; init; }
}

/// <summary>Removes a cell's comment.</summary>
[Operation("delete_comment")]
public sealed record DeleteCommentOp : CellsOp
{
    /// <summary>The cell whose comment is removed.</summary>
    [A1Cell] public required string Cell { get; init; }
}
