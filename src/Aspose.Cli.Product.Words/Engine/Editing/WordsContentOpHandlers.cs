using System.Data;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Text;
using Aspose.Words;
using Aspose.Words.Drawing;
using Aspose.Words.Fields;
using Aspose.Words.Lists;
using Aspose.Words.Replacing;
using Aspose.Words.Tables;
using SkiaSharp;

using static Aspose.Cli.Product.Words.Engine.Editing.WordsMutationSupport;

namespace Aspose.Cli.Product.Words.Engine.Editing;

/// <summary>Owns text and block-content mutations.</summary>
internal static class WordsContentOpHandlers
{
    internal static long ReplaceText(Document document, ReplaceTextOp op)
    {
        LimitedReplacingCallback? limiter = op.MaxReplacements is int maximum
            ? new LimitedReplacingCallback(maximum)
            : null;
        var options = new FindReplaceOptions
        {
            MatchCase = op.MatchCase,
            FindWholeWordsOnly = op.WholeWord,
            // A regex replacement honors $1 and ${name}; a literal one is inserted verbatim.
            UseSubstitutions = op.Regex,
            ReplacingCallback = limiter,
        };
        Regex pattern = op.Regex
            ? SafeRegex.Create(op.Find, op.MatchCase)
            : new Regex(Regex.Escape(op.Find), op.MatchCase ? RegexOptions.CultureInvariant : RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, SafeRegex.DefaultTimeout);

        long replaced = 0;
        foreach (Aspose.Words.Range range in ReplacementRanges(document, op.Scope))
        {
            replaced += range.Replace(pattern, op.Replace, options);
            if (limiter?.LimitReached == true)
            {
                break;
            }
        }

        return replaced;
    }

    internal static IEnumerable<Aspose.Words.Range> ReplacementRanges(Document document, string scope)
    {
        switch (scope)
        {
            case "body":
                return document.Sections.Cast<Section>().Select(static section => section.Body.Range);
            case "headersFooters":
                return document.Sections.Cast<Section>()
                    .SelectMany(static section => section.HeadersFooters.Cast<HeaderFooter>())
                    .Select(static headerFooter => headerFooter.Range);
            case "comments":
                return document.GetChildNodes(NodeType.Comment, true)
                    .Cast<Comment>()
                    .Select(static comment => comment.Range);
            case "all":
                return [document.Range];
            default:
                throw Invalid("replace_text scope must be body, headersFooters, comments, or all");
        }
    }

    private sealed class LimitedReplacingCallback(int maximum) : IReplacingCallback
    {
        private int _accepted;

        public bool LimitReached => _accepted >= maximum;

        public ReplaceAction Replacing(ReplacingArgs args)
        {
            ArgumentNullException.ThrowIfNull(args);
            if (_accepted >= maximum)
            {
                return ReplaceAction.Skip;
            }

            _accepted++;
            return ReplaceAction.Replace;
        }
    }

    internal static long SetText(Document document, IReadOnlyList<Node> nodes, SetTextOp op)
    {
        // A bookmark owns exactly its enclosed range, wherever it sits (mid-paragraph or in a
        // table cell). Only that range changes; the bookmark and its surroundings remain.
        if (op.At.Bookmark is { } name)
        {
            Bookmark bookmark = document.Range.Bookmarks[name]
                ?? throw Invalid($"bookmark '{name}' was removed by an earlier operation");
            bookmark.Text = op.Text;
            return 1;
        }

        // Check every target before the first change: a rejected operation changes nothing.
        if (nodes.Any(static node => node is not Paragraph))
        {
            throw Invalid("set_text accepts paragraph blocks only; use set_table_cell for tables");
        }

        foreach (Paragraph paragraph in nodes.Cast<Paragraph>())
        {
            paragraph.RemoveAllChildren();
            paragraph.AppendChild(new Run(document, op.Text));
        }

        return nodes.Count;
    }

    /// <summary>
    /// Inserts paragraphs at a block boundary. A paragraph with <c>listLevel</c> joins the
    /// anchor's list when the anchor is a list paragraph, otherwise one bullet list shared by
    /// the operation's list paragraphs.
    /// </summary>
    internal static long InsertParagraphs(Document document, Node anchor, InsertParagraphsOp op)
    {
        Node cursor = anchor;
        Aspose.Words.Lists.List? list = anchor is Paragraph { IsListItem: true } item ? item.ListFormat.List : null;
        foreach (ParagraphInput input in op.Paragraphs)
        {
            var paragraph = new Paragraph(document);
            paragraph.AppendChild(new Run(document, input.Text));
            if (input.Style is not null)
            {
                ApplyParagraphStyle(document, paragraph, input.Style);
            }

            if (input.ListLevel is int level)
            {
                list ??= document.Lists.Add(ListTemplate.BulletDefault);
                paragraph.ListFormat.List = list;
                paragraph.ListFormat.ListLevelNumber = level;
            }

            InsertRelative(anchor, ref cursor, paragraph, op.Position);
        }

        return op.Paragraphs.Count;
    }

    internal static long InsertMarkdown(Document document, Node anchor, InsertMarkdownOp op, Document markdown)
    {
        Node cursor = anchor;
        IReadOnlyList<Node> blocks = WordsMarkdownImport.Blocks(document, markdown);
        foreach (Node block in blocks)
        {
            InsertRelative(anchor, ref cursor, block, op.Position);
        }

        return blocks.Count;
    }

    internal static long Delete(IReadOnlyList<Node> nodes)
    {
        foreach (Node node in nodes)
        {
            node.Remove();
        }

        return nodes.Count;
    }

    internal static long InsertBreak(Document document, Node anchor, InsertBreakOp op)
    {
        if (op.Kind == "section")
        {
            return WordsStructureOpHandlers.InsertSectionBreak(anchor, op.Position);
        }

        var paragraph = new Paragraph(document);
        paragraph.AppendChild(new Run(document, ControlChar.PageBreak));
        Node cursor = anchor;
        InsertRelative(anchor, ref cursor, paragraph, op.Position);
        return 1;
    }
}

