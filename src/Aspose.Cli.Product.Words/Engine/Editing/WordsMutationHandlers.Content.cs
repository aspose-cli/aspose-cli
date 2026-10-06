using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Text;
using Aspose.Words;
using Aspose.Words.Fields;
using Aspose.Words.Lists;
using Aspose.Words.Replacing;
using SkiaSharp;

using static Aspose.Cli.Product.Words.Engine.Editing.WordsMutationSupport;

namespace Aspose.Cli.Product.Words.Engine.Editing;

// Text and block content.
internal sealed partial class WordsMutationHandlers
{
    /// <summary>
    /// Replaces matches in the stories of the op's scope, as search reads them: field codes
    /// and text a tracked change deletes are not text, and a match in a comment or footnote
    /// belongs to that note's scope, not to the body or header that anchors it. Each replacement
    /// records its paragraph as changed.
    /// </summary>
    public long Apply(ReplaceTextOp operation)
    {
        var callback = new ScopedReplacingCallback(operation.MaxReplacementCount, _changed);
        var options = new FindReplaceOptions
        {
            MatchCase = operation.MatchCase,
            FindWholeWordsOnly = operation.WholeWord,
            // A regex replacement honors $1 and ${name}; a literal one is inserted verbatim.
            UseSubstitutions = operation.Regex,
            IgnoreFieldCodes = true,
            IgnoreDeleted = true,
            ReplacingCallback = callback,
        };
        Regex pattern = operation.Regex
            ? SafeRegex.Create(operation.Find, operation.MatchCase)
            : new Regex(Regex.Escape(operation.Find), operation.MatchCase ? RegexOptions.CultureInvariant : RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, SafeRegex.DefaultTimeout);

        long replaced = 0;
        foreach ((CompositeNode story, _) in WordsStories.In(_document, operation.Scope).ToArray())
        {
            callback.Story = story;
            replaced += story.Range.Replace(pattern, operation.Replace, options);
            if (callback.LimitReached)
            {
                break;
            }
        }

        // Like a verification issue, the warning never repeats the find text.
        if (replaced == 0)
        {
            _warnings.Add(new Warning
            {
                Code = WarningCodes.ReplaceNoMatch,
                Message = $"replace_text matched no text in scope '{operation.Scope}', so nothing was replaced.",
                Hint = "Search with 'words query search' and the same pattern and scope, or --scope all for headers, footers, "
                    + "footnotes and comments. Field codes and text a tracked change deletes are never matched.",
            });
        }

        return replaced;
    }

    private sealed class ScopedReplacingCallback(int? maximum, ICollection<Node> changed) : IReplacingCallback
    {
        private int _accepted;

        public CompositeNode? Story { get; set; }

        public bool LimitReached => _accepted >= maximum;

        public ReplaceAction Replacing(ReplacingArgs args)
        {
            ArgumentNullException.ThrowIfNull(args);
            if (_accepted >= maximum || !ReferenceEquals(WordsStories.Of(args.MatchNode), Story))
            {
                return ReplaceAction.Skip;
            }

            _accepted++;
            changed.Add(args.MatchNode.GetAncestor(NodeType.Paragraph) ?? args.MatchNode);
            if (args.MatchNode is Run run)
            {
                KeepEastAsianFont(run, args.Replacement);
            }

            return ReplaceAction.Replace;
        }

        /// <summary>
        /// The SDK writes a replacement in the format of the run where the match starts. When that
        /// run holds no East Asian text, its East Asian font may be a Latin one, as in the digits
        /// of a loaded PDF, so East Asian text of the replacement takes the East Asian font of the
        /// nearest East Asian text in the paragraph, after the match first. The run's own text is
        /// unchanged, since it has no character that font draws.
        /// </summary>
        private static void KeepEastAsianFont(Run run, string replacement)
        {
            if (!WordsFonts.HasEastAsian(replacement) || WordsFonts.HasEastAsian(run.Text) || run.ParentParagraph is not { } paragraph)
            {
                return;
            }

            Run[] runs = [.. paragraph.GetChildNodes(NodeType.Run, true).Cast<Run>()];
            int at = Array.IndexOf(runs, run);
            Run? source = runs.Skip(at + 1).Concat(runs.Take(at).Reverse())
                .FirstOrDefault(static candidate => WordsFonts.HasEastAsian(candidate.Text));
            if (source is not null)
            {
                run.Font.NameFarEast = source.Font.NameFarEast;
            }
        }
    }

    public long Apply(SetTextOp operation)
    {
        // A bookmark owns exactly its enclosed range, wherever it sits (mid-paragraph or in a
        // table cell). Only that range changes; the bookmark and its surroundings remain.
        if (operation.At.Bookmark is { } name)
        {
            Bookmark bookmark = _document.Range.Bookmarks[name]
                ?? throw Invalid($"bookmark '{name}' was removed by an earlier operation");
            bookmark.Text = operation.Text;
            return 1;
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

        return Nodes.Count;
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
    public long Apply(InsertParagraphsOp operation)
    {
        Node cursor = Anchor;
        Paragraph? before = (operation.Position == "after" ? Anchor : Anchor.PreviousSibling) as Paragraph;
        Aspose.Words.Lists.List? created = null;
        var continued = new Dictionary<Paragraph, Paragraph?>();
        foreach (ParagraphInput input in operation.Paragraphs)
        {
            Aspose.Words.Lists.List? list = null;
            Paragraph? peer = null;
            if (input.ListLevel is int listLevel)
            {
                Node? previous = operation.Position == "after" ? cursor : Anchor.PreviousSibling;
                (list, peer) = ListAt(listLevel, previous, operation.Position == "after" ? cursor.NextSibling : Anchor);
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

            if (input.ListLevel is int level)
            {
                paragraph.ListFormat.List = list ?? (created ??= _document.Lists.Add(ListTemplate.BulletDefault));
                paragraph.ListFormat.ListLevelNumber = level;
                if (peer is not null && HasOwnIndent(peer))
                {
                    paragraph.ParagraphFormat.LeftIndent = peer.ParagraphFormat.LeftIndent;
                    paragraph.ParagraphFormat.FirstLineIndent = peer.ParagraphFormat.FirstLineIndent;
                }

                continued[paragraph] = peer;
            }

            InsertRelative(Anchor, ref cursor, paragraph, operation.Position);
        }

        return operation.Paragraphs.Count;
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
            for (Node? node = start; node is Paragraph { IsListItem: true } item; node = step(node))
            {
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

        if (IsPeer(Anchor))
        {
            return (Paragraph)Anchor;
        }

        for (Node? node = Anchor.PreviousSibling; node is not null; node = node.PreviousSibling)
        {
            if (IsPeer(node))
            {
                return (Paragraph)node;
            }
        }

        for (Node? node = Anchor.NextSibling; node is not null; node = node.NextSibling)
        {
            if (IsPeer(node))
            {
                return (Paragraph)node;
            }
        }

        return null;
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

    public long Apply(InsertMarkdownOp operation)
    {
        Document markdown = _loader.OpenMarkdown(operation.Markdown, _loaded);
        Node cursor = Anchor;
        IReadOnlyList<Node> blocks = WordsMarkdownImport.Blocks(_document, markdown);
        foreach (Node block in blocks)
        {
            InsertRelative(Anchor, ref cursor, block, operation.Position);
        }

        return blocks.Count;
    }

    public long Apply(DeleteBlocksOp operation)
    {
        foreach (Node node in Nodes)
        {
            DocumentBlockIndex.Remove(node);
        }

        return Nodes.Count;
    }

    public long Apply(InsertBreakOp operation)
    {
        if (operation.Kind == "section")
        {
            return InsertSectionBreak(Anchor, operation.Position);
        }

        var paragraph = new Paragraph(_document);
        paragraph.AppendChild(new Run(_document, ControlChar.PageBreak));
        Node cursor = Anchor;
        InsertRelative(Anchor, ref cursor, paragraph, operation.Position);
        return 1;
    }
}

