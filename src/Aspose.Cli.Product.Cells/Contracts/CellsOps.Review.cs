using Aspose.Cli.Sdk.Operations;

namespace Aspose.Cli.Product.Cells.Contracts;

// Ops for review and collaboration: cell comments.

/// <summary>
/// Adds a note (comment) to a cell; on a cell that has one, the text is added to it as a new
/// paragraph. set_comment replaces a comment's text.
/// </summary>
[Operation("add_comment")]
public sealed record AddCommentOp : CellsSheetOp
{
    /// <summary>The cell to annotate, such as B2.</summary>
    [A1Cell] public required string Cell { get; init; }

    /// <summary>The comment text; on a cell that has a comment, the new paragraph.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/cellsCommentInfo/properties/text")]
    [MinLength(1)] public required string Text { get; init; }

    /// <summary>The comment author; the engine default when omitted.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/cellsCommentInfo/properties/author")]
    public string? Author { get; init; }
}

/// <summary>Replaces the text of a cell's comment.</summary>
[Operation("set_comment")]
[MistakenFor("edit_comment")]
public sealed record SetCommentOp : CellsSheetOp
{
    /// <summary>The cell whose comment changes.</summary>
    [A1Cell] public required string Cell { get; init; }

    /// <summary>The new comment text.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/cellsCommentInfo/properties/text")]
    [MinLength(1)] public required string Text { get; init; }

    /// <summary>The new author; unchanged when omitted.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/cellsCommentInfo/properties/author")]
    public string? Author { get; init; }
}

/// <summary>Removes a cell's comment.</summary>
[Operation("delete_comment")]
public sealed record DeleteCommentOp : CellsSheetOp
{
    /// <summary>The cell whose comment is removed.</summary>
    [A1Cell] public required string Cell { get; init; }
}
