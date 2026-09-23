using Aspose.Words;
using Aspose.Words.Tables;

namespace Aspose.Cli.Product.Words.Engine.Mapping;

/// <summary>
/// The one way Markdown content enters a destination document, shared by <c>create</c>,
/// <c>insert_markdown</c> and Markdown headers and footers. Blocks take the destination's
/// styles, so the destination's template owns the look, and runs keep only the emphasis the
/// Markdown expressed.
/// </summary>
internal static class WordsMarkdownImport
{
    /// <summary>Imports the body blocks (paragraphs and tables) of a loaded Markdown document.</summary>
    internal static IReadOnlyList<Node> Blocks(Document destination, Document markdown)
    {
        KeepOnlyExpressedEmphasis(markdown);
        var importer = new NodeImporter(markdown, destination, ImportFormatMode.UseDestinationStyles);
        return markdown.Sections.Cast<Section>()
            .SelectMany(static section => section.Body.GetChildNodes(NodeType.Any, false).Cast<Node>())
            .Where(static node => node is Paragraph or Table)
            .Select(node => importer.ImportNode(node, true))
            .ToArray();
    }

    /// <summary>
    /// The Markdown reader stores "no emphasis" as explicit false bold, italic and
    /// strike-through on every run, which would override the destination's heading styles.
    /// Only emphasis the Markdown actually expressed and each run's character style (inline
    /// code, hyperlinks) are kept.
    /// </summary>
    private static void KeepOnlyExpressedEmphasis(Document markdown)
    {
        foreach (Run run in markdown.GetChildNodes(NodeType.Run, isDeep: true).OfType<Run>())
        {
            KeepOnlyExpressedEmphasis(run.Font);
        }

        foreach (Paragraph paragraph in markdown.GetChildNodes(NodeType.Paragraph, isDeep: true).OfType<Paragraph>())
        {
            KeepOnlyExpressedEmphasis(paragraph.ParagraphBreakFont);
        }
    }

    private static void KeepOnlyExpressedEmphasis(Font font)
    {
        (bool bold, bool italic, bool strike, string characterStyle) =
            (font.Bold, font.Italic, font.StrikeThrough, font.StyleName);
        font.ClearFormatting();
        font.StyleName = characterStyle;
        if (bold) { font.Bold = true; }
        if (italic) { font.Italic = true; }
        if (strike) { font.StrikeThrough = true; }
    }
}
