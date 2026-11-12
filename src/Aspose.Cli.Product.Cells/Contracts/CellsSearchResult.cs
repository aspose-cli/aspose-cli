using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>
/// Result of <c>aspose-cli cells search</c>: one window of hits for a pattern. Hits are ordered by
/// sheet, its cells row-major then its other parts, so output is deterministic; the envelope's
/// window counts the returned hits and, when more match, carries the command that returns the next
/// ones.
/// </summary>
public sealed record CellsSearchResult() : WindowedResultEnvelope("search-result")
{
    /// <summary>The searched file.</summary>
    [JsonPropertyOrder(-50)]
    public required InputInfo Input { get; init; }

    /// <summary>The pattern that was searched for (echoed back).</summary>
    [JsonPropertyOrder(-49)]
    public required string Pattern { get; init; }

    /// <summary>Matches, in cells and in the other parts of the sheets.</summary>
    [JsonPropertyOrder(-48)]
    public required IReadOnlyList<CellsSearchHit> Hits { get; init; }
}

/// <summary>
/// One match: in a cell, or in a part of a sheet outside its cells, such as a comment, a header,
/// a chart's title or a text box. Values search what a reader sees, the cells and every such part;
/// formulas, the text of formulas. Not searched: data validation messages and list items, hyperlink
/// screen tips, sheet and defined names, the text of ActiveX controls, and pivot table captions
/// that no cell shows.
/// </summary>
public sealed record CellsSearchHit
{
    /// <summary>Sheet the match is on.</summary>
    public required string Sheet { get; init; }

    /// <summary>Cell address, e.g. <c>B7</c>, of a cell or of the cell a comment belongs to; omitted for other parts.</summary>
    public string? Cell { get; init; }

    /// <summary>
    /// Where the match is when it is not in a cell that holds text, which replace_text writes, as
    /// TEXT_NOT_REPLACED names it in <c>details.part</c>: <c>formula</c>, <c>typed-value</c> (a cell that
    /// shows it but holds a number, date, boolean or error), <c>comment</c>, <c>header-footer</c>,
    /// <c>chart-title</c>, <c>axis-title</c>, <c>chart-data</c>, <c>data-label</c>, <c>text-box</c> or
    /// <c>alt-text</c>; omitted for a text cell.
    /// </summary>
    public string? Part { get; init; }

    /// <summary>The target address of a part outside the cells, such as <c>chart/Sales/1</c>, as TEXT_NOT_REPLACED gives it; omitted for a cell.</summary>
    public string? Location { get; init; }

    /// <summary>
    /// For a title, series name, data label or text box that shows the text of a cell on a sheet
    /// the search does not reach, that cell, such as <c>Data!B1</c>; a search of every sheet finds the cell itself instead.
    /// </summary>
    public string? LinkedCell { get; init; }

    /// <summary>
    /// The match itself: the first one in a cell's value as read reports it, else in its display
    /// text, else in its formula; in a part, in its text.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// The text that holds the match around it, up to 40 characters on each side, with line
    /// breaks as spaces and an ellipsis where it is cut.
    /// </summary>
    public required string Context { get; init; }

    /// <summary>Formula, when the cell has one.</summary>
    public string? Formula { get; init; }
}
