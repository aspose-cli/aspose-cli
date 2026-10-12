using Aspose.Cli.Sdk.Operations;

namespace Aspose.Cli.Product.Cells.Contracts;

// Cell hyperlinks: external URLs and internal cross-sheet references.

/// <summary>Sets the hyperlink of one cell, an external URL or an internal target, replacing a link that covered it.</summary>
[Operation("set_hyperlink")]
[ExactlyOneOf("url", "target")]
public sealed record SetHyperlinkOp : CellsSheetOp
{
    /// <summary>The cell to link, such as B2.</summary>
    [A1Cell] public required string Cell { get; init; }

    /// <summary>An external URL, such as https://example.com.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/cellsHyperlinkInfo/properties/url")]
    [WebLink] public string? Url { get; init; }

    /// <summary>An internal target, such as Summary!A1.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/cellsHyperlinkInfo/properties/target")]
    [A1Reference] public string? Target { get; init; }

    /// <summary>The text shown in the cell; the URL or target when omitted.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/cellsHyperlinkInfo/properties/display")]
    public string? Display { get; init; }
}

/// <summary>Removes the hyperlink covering a cell.</summary>
[Operation("delete_hyperlink")]
[MistakenFor("remove_hyperlink")]
public sealed record DeleteHyperlinkOp : CellsSheetOp
{
    /// <summary>A cell the hyperlink covers, such as B2.</summary>
    [A1Cell] public required string Cell { get; init; }
}
