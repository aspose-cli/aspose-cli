using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>
/// Windowed block projection returned by <c>words read</c>. Its <c>window</c> counts
/// blocks: the total is the blocks the range, section and content select. <c>window.next</c>
/// starts again at a block whose content was truncated and doubles <c>--max-chars</c> when that
/// block alone exceeded the budget.
/// </summary>
public sealed record WordsReadResult() : WindowedResultEnvelope("read-result", 2)
{
    /// <summary>The document read.</summary>
    [JsonPropertyOrder(-49)]
    public required InputInfo Input { get; init; }

    /// <summary>How much of each block the read returns, as <c>--content</c> chose it.</summary>
    [AllowedValues(typeof(WordsReadContents))]
    public required string Content { get; init; }

    /// <summary>The document's block count, whatever the range, section and content select.</summary>
    [Minimum(0)]
    public required int BlockCount { get; init; }

    /// <summary>The blocks of this window, in document order.</summary>
    public required IReadOnlyList<WordsBlockData> Blocks { get; init; }
}

/// <summary>A paragraph or table directly owned by a section body.</summary>
public sealed record WordsBlockData
{
    /// <summary>The 1-based block number that operation block addresses accept.</summary>
    [Minimum(1)]
    public required int Block { get; init; }

    /// <summary>Whether the block is a paragraph or a table.</summary>
    [AllowedValues("paragraph", "table")]
    public required string Kind { get; init; }

    /// <summary>The 1-based section that holds the block.</summary>
    [Minimum(1)]
    public required int Section { get; init; }

    /// <summary>
    /// The number Word draws before a numbered list paragraph, such as 6.2; bullets are not
    /// reported. The paragraph's text leaves it out; search snippets, find and heading addresses,
    /// extracted text and table cells read it before the text, followed by a space.
    /// </summary>
    public string? ListLabel { get; init; }

    /// <summary>A paragraph's visible text.</summary>
    public string? Text { get; init; }

    /// <summary>The name of a paragraph's style.</summary>
    public string? Style { get; init; }

    /// <summary>A heading paragraph's level, 1 for Heading 1.</summary>
    [Minimum(1)]
    public int? HeadingLevel { get; init; }

    /// <summary>
    /// The runs of the visible text, in order: their text joins to the paragraph's text, without
    /// field codes, deleted text or anchored comments and footnotes. Returned with <c>--content full</c>.
    /// </summary>
    public IReadOnlyList<WordsRunData>? Runs { get; init; }

    /// <summary>The paragraph's indents, spacing and alignment, returned with <c>--content full</c>.</summary>
    public WordsParagraphFormatData? ParagraphFormat { get; init; }

    /// <summary>The images in a paragraph.</summary>
    public IReadOnlyList<WordsBlockImageData>? Images { get; init; }

    /// <summary>A list paragraph's list: bullets or numbers, and its 1-based level.</summary>
    public WordsListData? List { get; init; }

    /// <summary>
    /// The break that follows this block, as insert_break inserts it: a page break, including one
    /// that starts the next paragraph, or a section break after the last block of a section.
    /// </summary>
    public WordsBreakData? Break { get; init; }

    /// <summary>A table's number of rows.</summary>
    [Minimum(0)]
    public int? RowCount { get; init; }

    /// <summary>The number of cells in a table's widest row.</summary>
    [Minimum(0)]
    public int? ColumnCount { get; init; }

    /// <summary>
    /// The text of each cell by row; a numbered list paragraph in a cell starts with its number
    /// and a space.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<string>>? Values { get; init; }

    /// <summary>
    /// Returned document text shares the max-chars budget, including repeated projections.
    /// Re-read this item with a larger budget for omitted content.
    /// </summary>
    public bool ContentTruncated { get; init; }
}

/// <summary>One image in a paragraph block; its size is in points.</summary>
public sealed record WordsBlockImageData
{
    /// <summary>The 1-based block that holds the image.</summary>
    [Minimum(1)]
    public required int Block { get; init; }

    /// <summary>The name of the shape that holds the image.</summary>
    public string? Name { get; init; }

    /// <summary>The width in points.</summary>
    [Minimum(0)]
    public required double Width { get; init; }

    /// <summary>The height in points.</summary>
    [Minimum(0)]
    public required double Height { get; init; }

    /// <summary>Whether the image sits in the text line rather than floating.</summary>
    public required bool Inline { get; init; }
}

/// <summary>A break after a block.</summary>
public sealed record WordsBreakData
{
    /// <summary>Whether it is a page break or a section break.</summary>
    [AllowedValues(typeof(WordsBreakKinds))]
    public required string Kind { get; init; }
}

/// <summary>
/// The format a paragraph has, from its own settings and its style, in points; returned with
/// <c>--content full</c>.
/// </summary>
public sealed record WordsParagraphFormatData
{
    /// <summary>Such as left, center, right, justify or distributed.</summary>
    public required string Alignment { get; init; }

    /// <summary>The left indent.</summary>
    public required double LeftIndent { get; init; }

    /// <summary>The right indent.</summary>
    public required double RightIndent { get; init; }

    /// <summary>The first line's indent; negative for a hanging indent.</summary>
    public required double FirstLineIndent { get; init; }

    /// <summary>The space before the paragraph.</summary>
    public required double SpaceBefore { get; init; }

    /// <summary>The space after the paragraph.</summary>
    public required double SpaceAfter { get; init; }

    /// <summary>How <c>lineSpacing</c> is measured.</summary>
    [AllowedValues("at-least", "exactly", "multiple")]
    public required string LineSpacingRule { get; init; }

    /// <summary>Lines for the multiple rule, where 1 is single spacing; points otherwise.</summary>
    public required double LineSpacing { get; init; }
}

/// <summary>One run of a paragraph's visible text and its formatting, returned with <c>--content full</c>.</summary>
public sealed record WordsRunData
{
    /// <summary>The run's text.</summary>
    public required string Text { get; init; }

    /// <summary>
    /// The East Asian font when the run starts with a Chinese, Japanese or Korean character,
    /// otherwise the Latin font.
    /// </summary>
    public string? Font { get; init; }

    /// <summary>The font of the run's Latin text.</summary>
    public string? LatinFont { get; init; }

    /// <summary>The font of the run's Chinese, Japanese and Korean text.</summary>
    public string? EastAsianFont { get; init; }

    /// <summary>The font size in points.</summary>
    public double? FontSize { get; init; }

    /// <summary>Whether the run is bold.</summary>
    public bool? Bold { get; init; }

    /// <summary>Whether the run is italic.</summary>
    public bool? Italic { get; init; }

    /// <summary>A #RRGGBB color.</summary>
    public string? Color { get; init; }

    /// <summary>Whether the run is underlined.</summary>
    public bool? Underline { get; init; }

    /// <summary>The highlight color, as #RRGGBB; absent without a highlight.</summary>
    public string? Highlight { get; init; }

    /// <summary>The address of the hyperlink whose text the run is; absent elsewhere.</summary>
    public string? Url { get; init; }
}
