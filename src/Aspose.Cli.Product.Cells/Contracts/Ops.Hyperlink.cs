using Aspose.Cli.Sdk.Operations;

namespace Aspose.Cli.Product.Cells.Contracts;

// Cell hyperlinks: external URLs and internal cross-sheet references.

/// <summary>Adds a hyperlink to one cell: an external URL or an internal target.</summary>
[Operation("set_hyperlink")]
[ExactlyOneOf("url", "target")]
public sealed record SetHyperlinkOp : Op
{
    /// <summary>The cell to link, such as B2.</summary>
    [A1Cell] public required string Cell { get; init; }

    /// <summary>An external URL, such as https://example.com.</summary>
    [WebLink] public string? Url { get; init; }

    /// <summary>An internal target, such as Summary!A1.</summary>
    [A1Reference] public string? Target { get; init; }

    /// <summary>The text shown in the cell; the URL or target when omitted.</summary>
    public string? Display { get; init; }
}

/// <summary>Removes the hyperlink covering a cell.</summary>
[Operation("remove_hyperlink")]
public sealed record RemoveHyperlinkOp : Op
{
    /// <summary>A cell the hyperlink covers, such as B2.</summary>
    [A1Cell] public required string Cell { get; init; }
}
