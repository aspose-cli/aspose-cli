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
            return ReplaceAction.Replace;
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

        foreach (Paragraph paragraph in Nodes.Cast<Paragraph>())
        {
            paragraph.RemoveAllChildren();
            paragraph.AppendChild(new Run(_document, operation.Text));
        }

        return Nodes.Count;
    }

    /// <summary>
    /// Inserts paragraphs at a block boundary. A paragraph with <c>listLevel</c> joins the
    /// anchor's list when the anchor is a list paragraph, otherwise one bullet list shared by
    /// the operation's list paragraphs.
    /// </summary>
    public long Apply(InsertParagraphsOp operation)
    {
        Node cursor = Anchor;
        Aspose.Words.Lists.List? list = Anchor is Paragraph { IsListItem: true } item ? item.ListFormat.List : null;
        foreach (ParagraphInput input in operation.Paragraphs)
        {
            var paragraph = new Paragraph(_document);
            paragraph.AppendChild(new Run(_document, input.Text));
            if (input.Style is not null)
            {
                ApplyParagraphStyle(_document, paragraph, input.Style);
            }

            if (input.ListLevel is int level)
            {
                list ??= _document.Lists.Add(ListTemplate.BulletDefault);
                paragraph.ListFormat.List = list;
                paragraph.ListFormat.ListLevelNumber = level;
            }

            InsertRelative(Anchor, ref cursor, paragraph, operation.Position);
        }

        return operation.Paragraphs.Count;
    }

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

