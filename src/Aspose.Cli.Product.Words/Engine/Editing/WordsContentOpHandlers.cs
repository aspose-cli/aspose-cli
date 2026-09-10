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
        if (op.MaxReplacements is <= 0)
        {
            throw Invalid("replace_text maxReplacements must be greater than zero");
        }

        LimitedReplacingCallback? limiter = op.MaxReplacements is int maximum
            ? new LimitedReplacingCallback(maximum)
            : null;
        var options = new FindReplaceOptions
        {
            MatchCase = op.MatchCase,
            FindWholeWordsOnly = op.WholeWord,
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
        foreach (Node node in nodes)
        {
            if (node is not Paragraph paragraph)
            {
                throw Invalid("set_text accepts paragraph blocks only; use set_table_cell for tables");
            }

            paragraph.RemoveAllChildren();
            paragraph.AppendChild(new Run(document, op.Text));
        }

        return nodes.Count;
    }

    internal static long InsertParagraphs(Document document, Node anchor, InsertParagraphsOp op)
    {
        Node cursor = anchor;
        foreach (ParagraphInput input in op.Paragraphs)
        {
            var paragraph = new Paragraph(document);
            paragraph.AppendChild(new Run(document, input.Text));
            if (input.Style is not null)
            {
                ApplyParagraphStyle(document, paragraph, input.Style);
            }

            InsertRelative(anchor, ref cursor, paragraph, op.Position);
        }

        return op.Paragraphs.Count;
    }

    internal static long InsertMarkdown(Document document, Node anchor, InsertMarkdownOp op)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(op.Markdown));
        var markdown = new Document(stream, new Aspose.Words.Loading.LoadOptions { LoadFormat = LoadFormat.Markdown });
        Node cursor = anchor;
        int count = 0;
        foreach (Node child in markdown.FirstSection.Body.GetChildNodes(NodeType.Any, false))
        {
            if (child is not Paragraph and not Table)
            {
                continue;
            }

            Node imported = document.ImportNode(child, true, ImportFormatMode.KeepSourceFormatting);
            InsertRelative(anchor, ref cursor, imported, op.Position);
            count++;
        }

        return count;
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
            var section = new Section(document);
            section.AppendChild(new Body(document));
            Section owner = (Section)anchor.GetAncestor(NodeType.Section);
            owner.ParentNode!.InsertAfter(section, owner);
            section.EnsureMinimum();
            return 1;
        }

        var paragraph = new Paragraph(document);
        paragraph.AppendChild(new Run(document, ControlChar.PageBreak));
        Node cursor = anchor;
        InsertRelative(anchor, ref cursor, paragraph, op.Position);
        return 1;
    }
}

