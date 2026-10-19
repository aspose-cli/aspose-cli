using System.Data;
using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Text;
using Aspose.Words;
using Aspose.Words.Lists;

using static Aspose.Cli.Product.Words.Engine.Editing.WordsMutationSupport;

namespace Aspose.Cli.Product.Words.Engine.Editing;

// Text and block content.
internal sealed partial class WordsMutationHandlers
{
    /// <summary>
    /// Replaces matches in the stories of the op's scope where <c>words search</c> finds them
    /// (see <see cref="WordsTextMatches"/>): field codes and text a tracked change deletes are
    /// not text, and a match in a comment or footnote belongs to that note's scope, not to the
    /// body or header that anchors it. The matches count in the order search numbers its hits.
    /// </summary>
    public ValueTuple Apply(ReplaceTextOp operation)
    {
        TextReplacement replacement = TextReplacement.Create(operation.Find, operation.Replace, operation.Regex, operation.CaseSensitive, operation.WholeWord);
        int remaining = operation.MaxReplacementCount ?? int.MaxValue;
        long replaced = 0;
        foreach (Node unit in WordsStories.In(_document, operation.Scope).SelectMany(static story => WordsStories.Units(story.Story)))
        {
            IReadOnlyList<WordsTextMatches.TextMatch> matches = WordsTextMatches.In(unit, replacement, remaining);
            if (matches.Count == 0)
            {
                continue;
            }

            // The runs split from the text around a match are no change of their own.
            _tracking?.Stop();
            IReadOnlyList<IReadOnlyList<Run>> isolated;
            try
            {
                isolated = WordsTextMatches.Isolate(matches);
            }
            finally
            {
                _tracking?.Start();
            }

            for (int index = 0; index < matches.Count; index++)
            {
                Replace(isolated[index], matches[index].Replacement);
            }

            replaced += matches.Count;
            remaining -= matches.Count;
            if (remaining == 0)
            {
                break;
            }
        }

        Matched = replaced;
        return default;
    }

    /// <summary>
    /// Replaces the text of a match's runs, as typing over it in Word does: the new text, in the
    /// format of the text where the match starts, is written there and the matched text is
    /// removed. It follows the matched runs that sit side by side with the first, so a tracked
    /// replace reads as a deletion and then an insertion, but never enters a field result, a
    /// content control or another container the match only ends in. The new text is inserted as
    /// it is: no &amp; or ^ code, such as &amp;p, stands for a break.
    /// </summary>
    private void Replace(IReadOnlyList<Run> matched, string replacement)
    {
        if (replacement.Length > 0)
        {
            Run start = matched[0];
            // Another author's tracked change on the matched text is no part of the new text.
            Run run = IsRevised(start) ? FontLike(start, replacement) : TextLike(start, replacement);
            KeepEastAsianFont(run, start, replacement);
            Run after = start;
            for (int index = 1; index < matched.Count && ReferenceEquals(matched[index].PreviousSibling, after); index++)
            {
                after = matched[index];
            }

            after.ParentNode.InsertAfter(run, after);
        }

        foreach (Run piece in matched)
        {
            piece.Remove();
        }
    }

    /// <summary>
    /// A detached run of <paramref name="text"/> in the font of <paramref name="format"/>, its
    /// character style, typefaces, sizes, color and the settings set_style keeps, without the
    /// revision a copy of the run would carry.
    /// </summary>
    private Run FontLike(Run format, string text)
    {
        var run = new Run(_document, text);
        Aspose.Words.Font from = format.Font;
        Aspose.Words.Font to = run.Font;
        _tracking?.Stop();
        try
        {
            to.StyleName = from.StyleName;
            (to.NameAscii, to.NameOther, to.NameFarEast, to.NameBi) = (from.NameAscii, from.NameOther, from.NameFarEast, from.NameBi);
            (to.Size, to.SizeBi, to.Color) = (from.Size, from.SizeBi, from.Color);
            foreach ((Func<Aspose.Words.Font, object> get, Action<Aspose.Words.Font, object> set) in KeptSettings)
            {
                set(to, get(from));
            }
        }
        finally
        {
            _tracking?.Start();
        }

        return run;
    }

    /// <summary>
    /// New text takes the format of the text where the match starts. When that text holds no East
    /// Asian text, its East Asian font may be a Latin one, as in the digits of a loaded PDF, so
    /// East Asian text of the replacement takes the East Asian font of the nearest East Asian text
    /// in the paragraph, after the match first.
    /// </summary>
    private static void KeepEastAsianFont(Run run, Run start, string replacement)
    {
        if (!WordsFonts.HasEastAsian(replacement) || WordsFonts.HasEastAsian(start.Text) || start.ParentParagraph is not { } paragraph)
        {
            return;
        }

        Run[] runs = [.. paragraph.GetChildNodes(NodeType.Run, true).Cast<Run>()];
        int at = Array.IndexOf(runs, start);
        Run? source = runs.Skip(at + 1).Concat(runs.Take(at).Reverse())
            .FirstOrDefault(static candidate => WordsFonts.HasEastAsian(candidate.Text));
        if (source is not null)
        {
            run.Font.NameFarEast = source.Font.NameFarEast;
        }
    }

    public ValueTuple Apply(SetTextOp operation)
    {
        // A bookmark owns exactly its enclosed range, wherever it sits (mid-paragraph or in a
        // table cell). Only that range changes; the bookmark and its surroundings remain.
        if (operation.Target.Bookmark is { } name)
        {
            Bookmark bookmark = _document.Range.Bookmarks[name]
                ?? throw Invalid($"bookmark '{name}' was removed by an earlier operation");
            bookmark.Text = operation.Text;
            return default;
        }

        // Check every target before the first change: a rejected operation changes nothing.
        if (Nodes.Any(static node => node is not Paragraph))
        {
            throw Invalid("set_text accepts paragraph blocks only; use set_table_cell for tables");
        }

        // As typing over a paragraph's text in Word does, the new text takes the font of the
        // first text it replaces; the paragraph keeps its format.
        foreach (Paragraph paragraph in Nodes.Cast<Paragraph>())
        {
            Run? first = WordsText.VisibleRuns(paragraph, fieldResults: false).FirstOrDefault(static run => !string.IsNullOrWhiteSpace(run.Text));
            Run run = first is null || IsRevised(first) ? new Run(_document, operation.Text) : TextLike(first, operation.Text);
            paragraph.RemoveAllChildren();
            paragraph.AppendChild(run);
        }

        return default;
    }

    /// <summary>
    /// Inserts paragraphs at a block boundary. A paragraph without <c>style</c> continues the
    /// paragraph before the insertion point (see <see cref="Continue"/>). A paragraph with
    /// <c>listLevel</c> after or before a list item joins the list that numbers its level there
    /// (see <see cref="ListAt"/>) and, without <c>style</c>, continues that list's item at its
    /// level, taking its indents when the item sets its own; otherwise it joins one bullet list
    /// shared by the operation's list paragraphs. A later item that finds one the operation
    /// inserted joins its list but continues the item that one continued, since an inserted
    /// item is a tracked insertion when changes are tracked and gives no format to continue.
    /// </summary>
    public ValueTuple Apply(InsertParagraphsOp operation)
    {
        Node cursor = Anchor;
        Paragraph? before = (operation.Position == "after" ? Anchor : Anchor.PreviousSibling) as Paragraph;
        var created = new Dictionary<string, Aspose.Words.Lists.List>(StringComparer.Ordinal);
        var continued = new Dictionary<Paragraph, Paragraph?>();
        foreach (WordsParagraphInput input in operation.Paragraphs)
        {
            Aspose.Words.Lists.List? list = null;
            Paragraph? peer = null;
            // The input level is 1-based; the SDK numbers levels from 0.
            if (input.List?.Level is int listLevel)
            {
                Node? previous = operation.Position == "after" ? cursor : Anchor.PreviousSibling;
                (list, peer) = ListAt(listLevel - 1, previous, operation.Position == "after" ? cursor.NextSibling : Anchor);
                if (peer is not null && continued.TryGetValue(peer, out Paragraph? source))
                {
                    peer = source;
                }
            }

            Paragraph paragraph;
            if (input.Style is null)
            {
                paragraph = Continue(peer ?? before, input.Text);
            }
            else
            {
                paragraph = new Paragraph(_document);
                paragraph.AppendChild(new Run(_document, input.Text));
                ApplyParagraphStyle(_document, paragraph, input.Style);
            }

            if (input.List is { Level: int level } wanted)
            {
                string kind = wanted.Kind ?? "bullet";
                paragraph.ListFormat.List = list ?? (created.TryGetValue(kind, out Aspose.Words.Lists.List? started)
                    ? started
                    : created[kind] = _document.Lists.Add(kind == "number" ? ListTemplate.NumberDefault : ListTemplate.BulletDefault));
                paragraph.ListFormat.ListLevelNumber = level - 1;
                if (peer is not null && HasOwnIndent(peer))
                {
                    paragraph.ParagraphFormat.LeftIndent = peer.ParagraphFormat.LeftIndent;
                    paragraph.ParagraphFormat.FirstLineIndent = peer.ParagraphFormat.FirstLineIndent;
                }

                continued[paragraph] = peer;
            }

            InsertRelative(Anchor, ref cursor, paragraph, operation.Position);
        }

        return default;
    }

    /// <summary>
    /// The list a new item at <paramref name="level"/> joins when the anchor is a list item, and
    /// that list's item at the level whose format the new item takes. The anchor's list is
    /// joined when it numbers or indents the level (see <see cref="ListPeer"/>). Otherwise, as
    /// in Markdown, where each nesting level is a list of its own, the new item joins the list of
    /// the nearest item at the level around the insertion point, between <paramref name="previous"/>
    /// and <paramref name="next"/>, within the items of the same parent. Without either, or when
    /// the anchor is no list item, the result is no list, and the caller starts a bullet list.
    /// </summary>
    private (Aspose.Words.Lists.List? List, Paragraph? Peer) ListAt(int level, Node? previous, Node? next)
    {
        if (Anchor is not Paragraph { IsListItem: true } anchor)
        {
            return (null, null);
        }

        Aspose.Words.Lists.List list = anchor.ListFormat.List;
        Aspose.Words.Lists.ListLevel defined = list.ListLevels[level];
        if (defined.NumberFormat.Length > 0 || defined.TextPosition != 0 || defined.NumberPosition != 0)
        {
            return (list, ListPeer(list, level));
        }

        // Items at a deeper level are skipped; a non-list block or an item at a shallower level
        // ends the parent whose items the new one joins.
        Paragraph? Sibling(Node? start, Func<Node, Node?> step)
        {
            foreach (Node node in Siblings(start, step))
            {
                if (node is not Paragraph { IsListItem: true } item)
                {
                    return null;
                }

                int itemLevel = item.ListFormat.ListLevelNumber;
                if (itemLevel == level)
                {
                    return item;
                }

                if (itemLevel < level)
                {
                    return null;
                }
            }

            return null;
        }

        Paragraph? sibling = Sibling(previous, static node => node.PreviousSibling) ?? Sibling(next, static node => node.NextSibling);
        return sibling is null ? (null, null) : (sibling.ListFormat.List, sibling);
    }

    /// <summary>
    /// Whether a list item's indents differ from its list level's, as when they are set on the
    /// paragraph. An item that follows its level gives a new item nothing to copy, so the new
    /// item keeps following the level too. Indents are stored in twentieths of a point.
    /// </summary>
    private static bool HasOwnIndent(Paragraph item)
    {
        Aspose.Words.Lists.ListLevel level = item.ListFormat.ListLevel;
        return Math.Abs(item.ParagraphFormat.LeftIndent - level.TextPosition) >= 0.05
            || Math.Abs(item.ParagraphFormat.FirstLineIndent - (level.NumberPosition - level.TextPosition)) >= 0.05;
    }

    /// <summary>
    /// The item of <paramref name="list"/> at <paramref name="level"/> whose format a new item
    /// takes: the anchor, else the nearest such paragraph before it, else after it, among the
    /// anchor's siblings. Documents converted from RTF often hold an item's indent on the
    /// paragraph rather than on its list level, so the level alone would misalign the new item.
    /// </summary>
    private Paragraph? ListPeer(Aspose.Words.Lists.List list, int level)
    {
        bool IsPeer(Node? node) =>
            node is Paragraph { IsListItem: true } paragraph
            && paragraph.ListFormat.List?.ListId == list.ListId
            && paragraph.ListFormat.ListLevelNumber == level;

        return (Paragraph?)Siblings(Anchor, static node => node.PreviousSibling)
            .Concat(Siblings(Anchor.NextSibling, static node => node.NextSibling))
            .FirstOrDefault(IsPeer);
    }

    /// <summary><paramref name="start"/> and the nodes <paramref name="step"/> leads to from it, in order.</summary>
    private static IEnumerable<Node> Siblings(Node? start, Func<Node, Node?> step)
    {
        for (Node? node = start; node is not null; node = step(node))
        {
            yield return node;
        }
    }

    /// <summary>
    /// A paragraph of <paramref name="text"/> that continues <paramref name="before"/> as Word's
    /// Enter does: in its paragraph format and the direct font of its last text outside fields,
    /// or, after a paragraph whose style names another style to follow it, such as a heading, in
    /// that style alone. A list and a page break before are not continued, nor is a tracked
    /// change, which would make the new text another author's revision. Without a paragraph
    /// before, it takes Normal.
    /// </summary>
    private Paragraph Continue(Paragraph? before, string text)
    {
        var paragraph = new Paragraph(_document);
        var run = new Run(_document, text);
        if (before is not null && !IsRevised(before))
        {
            if (before.ParagraphFormat.Style is { NextParagraphStyleName: { Length: > 0 } next } style && next != style.Name)
            {
                paragraph.ParagraphFormat.StyleName = next;
            }
            else
            {
                paragraph = (Paragraph)before.Clone(false);
                paragraph.ListFormat.RemoveNumbers();
                paragraph.ParagraphFormat.PageBreakBefore = false;
                // The font continues without a character style, and never from a field result,
                // such as a hyperlink's text, whose formatting belongs to the field.
                if (WordsText.VisibleRuns(before, fieldResults: false).LastOrDefault(static candidate => !string.IsNullOrWhiteSpace(candidate.Text)) is { } last
                    && !IsRevised(last))
                {
                    run = TextLike(last, text);
                    run.Font.StyleIdentifier = StyleIdentifier.DefaultParagraphFont;
                }
            }
        }

        paragraph.AppendChild(run);
        return paragraph;
    }

    /// <summary>A detached run of <paramref name="text"/> in the format of <paramref name="format"/>.</summary>
    private Run TextLike(Run format, string text)
    {
        var run = (Run)format.Clone(false);
        // The copy is detached, and setting its text while revisions are tracked fails
        // (WORDS-TRACKED-DETACHED-TEXT); its insertion is tracked instead.
        _tracking?.Stop();
        try
        {
            run.Text = text;
        }
        finally
        {
            _tracking?.Start();
        }

        return run;
    }

    private static bool IsRevised(Paragraph paragraph) =>
        paragraph.IsInsertRevision || paragraph.IsDeleteRevision || paragraph.IsFormatRevision
        || paragraph.IsMoveFromRevision || paragraph.IsMoveToRevision;

    private static bool IsRevised(Run run) =>
        run.IsInsertRevision || run.IsDeleteRevision || run.IsFormatRevision
        || run.IsMoveFromRevision || run.IsMoveToRevision;

    public ValueTuple Apply(InsertMarkdownOp operation)
    {
        Document markdown = _loader.OpenMarkdown(operation.Markdown, _loaded);
        Node cursor = Anchor;
        IReadOnlyList<Node> blocks = WordsMarkdownImport.Blocks(_document, markdown);
        foreach (Node block in blocks)
        {
            InsertRelative(Anchor, ref cursor, block, operation.Position);
        }

        return default;
    }

    public ValueTuple Apply(DeleteBlocksOp operation)
    {
        foreach (Node node in Nodes)
        {
            DocumentBlockIndex.Remove(node);
        }

        return default;
    }

    public ValueTuple Apply(InsertBreakOp operation)
    {
        if (operation.Kind == "section")
        {
            InsertSectionBreak(Anchor, operation.Position);
            return default;
        }

        var paragraph = new Paragraph(_document);
        paragraph.AppendChild(new Run(_document, ControlChar.PageBreak));
        Node cursor = Anchor;
        InsertRelative(Anchor, ref cursor, paragraph, operation.Position);
        return default;
    }
}

