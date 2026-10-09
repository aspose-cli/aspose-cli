using Aspose.Words;
using Aspose.Words.Notes;

namespace Aspose.Cli.Product.Words.Engine.Mapping;

/// <summary>
/// Resolves a <see cref="WordsTextScopes"/> name to the stories that search and replace_text
/// read. A story is a section body, a header or footer, a footnote or endnote, or a comment;
/// the comments and footnotes a body or header anchors are stories of their own, never part
/// of the text around them.
/// </summary>
internal static class WordsStories
{
    /// <summary>The stories in a scope, in document order, each with its own scope name.</summary>
    internal static IEnumerable<(CompositeNode Story, string Scope)> In(Document document, string scope)
    {
        bool all = scope == WordsTextScopes.All;
        if (!all && !WordsTextScopes.Names.Contains(scope, StringComparer.Ordinal))
        {
            throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown Words text scope.");
        }

        if (all || scope == WordsTextScopes.Body)
        {
            foreach (Section section in document.Sections)
            {
                yield return (section.Body, WordsTextScopes.Body);
            }
        }

        if (all || scope == WordsTextScopes.HeadersFooters)
        {
            foreach (Section section in document.Sections)
            {
                foreach (HeaderFooter headerFooter in section.HeadersFooters)
                {
                    yield return (headerFooter, WordsTextScopes.HeadersFooters);
                }
            }
        }

        if (all || scope == WordsTextScopes.Footnotes)
        {
            foreach (Footnote footnote in document.GetChildNodes(NodeType.Footnote, true))
            {
                yield return (footnote, WordsTextScopes.Footnotes);
            }
        }

        if (all || scope == WordsTextScopes.Comments)
        {
            foreach (Comment comment in document.GetChildNodes(NodeType.Comment, true))
            {
                yield return (comment, WordsTextScopes.Comments);
            }
        }
    }

    /// <summary>The story a node belongs to: its nearest comment, footnote, header, footer or body.</summary>
    internal static CompositeNode? Of(Node node)
    {
        for (Node? current = node; current is not null; current = current.ParentNode)
        {
            if (current is Comment or Footnote or HeaderFooter or Body)
            {
                return (CompositeNode)current;
            }
        }

        return null;
    }

    /// <summary>The scope name of the story a node belongs to, or null outside every story.</summary>
    internal static string? ScopeOf(Node? node) => node is null ? null : Of(node) switch
    {
        Comment => WordsTextScopes.Comments,
        Footnote => WordsTextScopes.Footnotes,
        HeaderFooter => WordsTextScopes.HeadersFooters,
        Body => WordsTextScopes.Body,
        _ => null,
    };

    /// <summary>The scope name of the story that holds a node of the document's sections, which every such node has.</summary>
    internal static string StoryOf(Node node) =>
        ScopeOf(node) ?? throw new InvalidOperationException($"A {node.NodeType} node lies outside every story of the document.");

    /// <summary>
    /// Where a header or footer shows, in the set_header and set_page_numbers vocabulary: its
    /// location, <c>header</c> or <c>footer</c>, and its kind, <c>primary</c>, <c>first</c> or
    /// <c>even</c>.
    /// </summary>
    internal static (string Location, string Kind) PlaceOf(HeaderFooter headerFooter) => (
        headerFooter.IsHeader ? HeaderFooterLocations.Header : HeaderFooterLocations.Footer,
        headerFooter.HeaderFooterType switch
        {
            HeaderFooterType.HeaderFirst or HeaderFooterType.FooterFirst => HeaderFooterKinds.First,
            HeaderFooterType.HeaderEven or HeaderFooterType.FooterEven => HeaderFooterKinds.Even,
            _ => HeaderFooterKinds.Primary,
        });

    /// <summary>The 1-based number of the section that holds a node.</summary>
    internal static int SectionOf(Node node) =>
        node.Document.GetChildNodes(NodeType.Section, false).IndexOf(node.GetAncestor(NodeType.Section)) + 1;

    /// <summary>
    /// A story's search units: a note as a whole, otherwise each paragraph not nested in another
    /// paragraph. A text box's paragraphs are read with the paragraph that anchors the box.
    /// </summary>
    internal static IEnumerable<Node> Units(CompositeNode story) => story is Comment or Footnote
        ? [story]
        : story.GetChildNodes(NodeType.Paragraph, true).Cast<Paragraph>()
            .Where(static paragraph => paragraph.GetAncestor(NodeType.Paragraph) is null);
}
