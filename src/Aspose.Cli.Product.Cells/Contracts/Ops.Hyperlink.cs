namespace Aspose.Cli.Product.Cells.Contracts;

// Cell hyperlinks: external URLs and internal cross-sheet references.

/// <summary>Adds a hyperlink to a single cell. Give either a URL or an internal target.</summary>
public sealed record SetHyperlinkOp() : Op
{
    /// <summary>The cell to link, e.g. <c>B2</c>.</summary>
    public required string Cell { get; init; }

    /// <summary>An external URL, e.g. <c>https://example.com</c>. Give this or <see cref="Target"/>.</summary>
    public string? Url { get; init; }

    /// <summary>An internal target, e.g. <c>Summary!A1</c>. Give this or <see cref="Url"/>.</summary>
    public string? Target { get; init; }

    /// <summary>Text shown in the cell; the URL or target when omitted.</summary>
    public string? Display { get; init; }
}

/// <summary>Removes the hyperlink covering a cell.</summary>
public sealed record RemoveHyperlinkOp() : Op
{
    /// <summary>A cell the hyperlink covers, e.g. <c>B2</c>.</summary>
    public required string Cell { get; init; }
}
