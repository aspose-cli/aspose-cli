using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Text;
using Aspose.Words;
using Aspose.Words.Fields;
using Aspose.Words.Replacing;
using Aspose.Words.Tables;
using SkiaSharp;

using static Aspose.Cli.Product.Words.Engine.Editing.WordsMutationSupport;

namespace Aspose.Cli.Product.Words.Engine.Editing;

/// <summary>Owns section, page, header and document-structure mutations.</summary>
internal static class WordsStructureOpHandlers
{
    /// <summary>
    /// Adds an empty section that starts with the page setup of its neighbour (the section it
    /// follows, or the first section for <c>start</c>), as Word does, instead of SDK defaults.
    /// Without its own headers and footers it continues its neighbour's.
    /// </summary>
    internal static long AddSection(Document document, AddSectionOp op, Section? after)
    {
        Section reference = op.Position switch
        {
            "after" => after ?? throw Invalid("add_section position 'after' requires an original section target"),
            "start" => document.FirstSection,
            _ => document.LastSection,
        };
        Section section = EmptyLike(reference);
        if (op.Position == "start")
        {
            document.PrependChild(section);
        }
        else
        {
            document.InsertAfter(section, reference);
        }

        section.EnsureMinimum();

        if (op.PageSetup is not null)
        {
            ApplyPageSetup(section, op.PageSetup);
        }

        return 1;
    }

    /// <summary>
    /// Splits the anchor's section at a block boundary: the blocks from the boundary on move
    /// into a new section with the same page setup, which continues the headers and footers.
    /// </summary>
    internal static long InsertSectionBreak(Node anchor, string position)
    {
        if (anchor.ParentNode is not Body)
        {
            throw Invalid("a section break cannot split a content control; target a block before or after the control");
        }

        var owner = (Section)anchor.GetAncestor(NodeType.Section);
        Section section = EmptyLike(owner);
        owner.ParentNode!.InsertAfter(section, owner);
        for (Node? node = position == "before" ? anchor : anchor.NextSibling; node is not null;)
        {
            Node? next = node.NextSibling;
            section.Body.AppendChild(node);
            node = next;
        }

        EndWithParagraph(owner.Body);
        EndWithParagraph(section.Body);
        return 1;
    }

    private static Section EmptyLike(Section reference)
    {
        // A shallow clone copies the section properties (page setup, columns, numbering) only.
        var section = (Section)reference.Clone(false);
        section.AppendChild(new Body(reference.Document));
        return section;
    }

    // A body must end with a paragraph, which also carries the section break.
    private static void EndWithParagraph(Body body)
    {
        if (body.LastChild is not Paragraph)
        {
            body.AppendChild(new Paragraph(body.Document));
        }
    }

    internal static long DeleteSection(Document document, Section section)
    {
        if (document.Sections.Count == 1)
        {
            throw Invalid("the last section cannot be deleted");
        }

        section.Remove();
        return 1;
    }

    internal static long SetPageSetup(IReadOnlyList<Section> sections, SetPageSetupOp op)
    {
        foreach (Section current in sections)
        {
            ApplyPageSetup(current, op.Setup);
        }

        return sections.Count;
    }

    /// <summary>
    /// Replaces one kind of header or footer in each section with plain paragraphs or imported
    /// Markdown. A first-page or even-page kind also turns on the section setting that shows it.
    /// </summary>
    internal static long SetHeaderFooter(
        Document document,
        IReadOnlyList<Section> sections,
        string kind,
        IReadOnlyList<string>? paragraphs,
        Document? markdown,
        bool isHeader)
    {
        HeaderFooterType type = HeaderFooterTypeOf(kind, isHeader);
        foreach (Section section in sections)
        {
            section.HeadersFooters[type]?.Remove();
            var replacement = new HeaderFooter(document, type);
            section.HeadersFooters.Add(replacement);
            IEnumerable<Node> blocks = markdown is null
                ? paragraphs!.Select(text =>
                {
                    var paragraph = new Paragraph(document);
                    paragraph.AppendChild(new Run(document, text));
                    return (Node)paragraph;
                })
                : WordsMarkdownImport.Blocks(document, markdown);
            foreach (Node block in blocks)
            {
                replacement.AppendChild(block);
            }

            if (!replacement.HasChildNodes)
            {
                replacement.AppendChild(new Paragraph(document));
            }

            if (kind == "first")
            {
                section.PageSetup.DifferentFirstPageHeaderFooter = true;
            }
            else if (kind == "even")
            {
                section.PageSetup.OddAndEvenPagesHeaderFooter = true;
            }
        }

        return sections.Count;
    }

    internal static long SetPageNumbers(Document document, IReadOnlyList<Section> sections, SetPageNumbersOp op)
    {
        foreach (Section section in sections)
        {
            HeaderFooterType type = op.Location == "header" ? HeaderFooterType.HeaderPrimary : HeaderFooterType.FooterPrimary;
            HeaderFooter? container = section.HeadersFooters[type];
            if (container is null)
            {
                container = new HeaderFooter(document, type);
                section.HeadersFooters.Add(container);
            }

            Field? existingPage = container.Range.Fields.Cast<Field>()
                .FirstOrDefault(static field => field.Type == FieldType.FieldPage);
            Paragraph paragraph;
            if (existingPage is null)
            {
                paragraph = new Paragraph(document);
                container.AppendChild(paragraph);
                var builder = new DocumentBuilder(document);
                builder.MoveTo(paragraph);
                builder.InsertField("PAGE");
            }
            else
            {
                paragraph = (Paragraph)existingPage.Start.GetAncestor(NodeType.Paragraph);
            }

            paragraph.ParagraphFormat.Alignment = AlignmentOf(op.Alignment);
            if (op.Start is int start)
            {
                section.PageSetup.RestartPageNumbering = true;
                section.PageSetup.PageStartingNumber = start;
            }

            if (op.Format is not null)
            {
                section.PageSetup.PageNumberStyle = op.Format switch
                {
                    "decimal" => NumberStyle.Arabic,
                    "upperRoman" => NumberStyle.UppercaseRoman,
                    "lowerRoman" => NumberStyle.LowercaseRoman,
                    "upperLetter" => NumberStyle.UppercaseLetter,
                    "lowerLetter" => NumberStyle.LowercaseLetter,
                    _ => throw Invalid($"unknown page number format '{op.Format}'"),
                };
            }
        }

        document.UpdatePageLayout();
        return sections.Count;
    }

    internal static long AppendDocument(
        LoadedDocument destination,
        AppendDocumentOp op,
        WordsDocumentLoader loader)
    {
        Document document = destination.Document;
        using LoadedDocument loaded = loader.Open(op.Path, null);
        destination.Imported(loaded);
        if (loaded.Evaluation)
        {
            WordsEvaluation.RemoveLeadingBanners(loaded.Document);
        }

        ImportFormatMode mode = op.ImportFormatMode == "useDestination"
            ? ImportFormatMode.UseDestinationStyles
            : ImportFormatMode.KeepSourceFormatting;
        document.AppendDocument(loaded.Document, mode);
        return 1;
    }
}

