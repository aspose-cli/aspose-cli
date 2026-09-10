namespace Aspose.Cli.Product.Cells.Contracts;

// Ops for review and collaboration: cell comments.

/// <summary>Adds a note (comment) to a cell.</summary>
public sealed record AddCommentOp() : Op(OpNames.AddComment)
{
    /// <summary>The cell to annotate, e.g. <c>B2</c>.</summary>
    public required string Cell { get; init; }

    /// <summary>The comment text.</summary>
    public required string Text { get; init; }

    /// <summary>Comment author; the engine default when omitted.</summary>
    public string? Author { get; init; }
}

/// <summary>Replaces the text of an existing cell comment.</summary>
public sealed record EditCommentOp() : Op(OpNames.EditComment)
{
    /// <summary>The cell whose comment to change.</summary>
    public required string Cell { get; init; }

    /// <summary>The new comment text.</summary>
    public required string Text { get; init; }

    /// <summary>New author; unchanged when omitted.</summary>
    public string? Author { get; init; }
}

/// <summary>Removes a cell comment.</summary>
public sealed record DeleteCommentOp() : Op(OpNames.DeleteComment)
{
    /// <summary>The cell whose comment to remove.</summary>
    public required string Cell { get; init; }
}
