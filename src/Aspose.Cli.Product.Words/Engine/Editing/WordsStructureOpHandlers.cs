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

// Sections, page setup, headers, footers and appended documents.
internal sealed partial class WordsOpHandlers
{
    /// <summary>
    /// Adds an empty section that starts with the page setup of its neighbour (the section it
    /// follows, or the first section for <c>start</c>), as Word does, instead of SDK defaults.
    /// Without its own headers and footers it continues its neighbour's.
    /// </summary>
    public long Apply(AddSectionOp operation)
    {
        Section? after = Sections.FirstOrDefault();
        Section reference = operation.Position switch
        {
            "after" => after ?? throw Invalid("add_section position 'after' requires an original section target"),
            "start" => _document.FirstSection,
            _ => _document.LastSection,
        };
        Section section = EmptyLike(reference);
        if (operation.Position == "start")
        {
            _document.PrependChild(section);
        }
        else
        {
            _document.InsertAfter(section, reference);
        }

        section.EnsureMinimum();

        if (operation.PageSetup is not null)
        {
            ApplyPageSetup(section, operation.PageSetup);
        }

        return 1;
    }

    /// <summary>
    /// Splits the anchor's section at a block boundary: the blocks from the boundary on move
    /// into a new section with the same page setup, which continues the headers and footers.
    /// </summary>
    private static long InsertSectionBreak(Node anchor, string position)
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

    public long Apply(DeleteSectionOp operation)
    {
        Section section = Sections[0];
        if (_document.Sections.Count == 1)
        {
            throw Invalid("the last section cannot be deleted");
        }

        section.Remove();
        return 1;
    }

    public long Apply(SetPageSetupOp operation)
    {
        foreach (Section current in Sections)
        {
            ApplyPageSetup(current, operation.Setup);
        }

        return Sections.Count;
    }

    public long Apply(SetHeaderOp operation) => SetHeaderFooter(operation, isHeader: true);

    public long Apply(SetFooterOp operation) => SetHeaderFooter(operation, isHeader: false);

    /// <summary>
    /// Replaces one kind of header or footer in each section with plain paragraphs or imported
    /// Markdown. A first-page or even-page kind also turns on the section setting that shows it.
    /// </summary>
    private long SetHeaderFooter(HeaderFooterOp operation, bool isHeader)
    {
        string kind = operation.Kind;
        Document? markdown = operation.Markdown is null ? null : _loader.OpenMarkdown(operation.Markdown, _loaded);
        HeaderFooterType type = HeaderFooterTypeOf(kind, isHeader);
        foreach (Section section in Sections)
        {
            section.HeadersFooters[type]?.Remove();
            var replacement = new HeaderFooter(_document, type);
            section.HeadersFooters.Add(replacement);
            IEnumerable<Node> blocks = markdown is null
                ? operation.Paragraphs!.Select(text =>
                {
                    var paragraph = new Paragraph(_document);
                    paragraph.AppendChild(new Run(_document, text));
                    return (Node)paragraph;
                })
                : WordsMarkdownImport.Blocks(_document, markdown);
            foreach (Node block in blocks)
            {
                replacement.AppendChild(block);
            }

            if (!replacement.HasChildNodes)
            {
                replacement.AppendChild(new Paragraph(_document));
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

        return Sections.Count;
    }

    public long Apply(SetPageNumbersOp operation)
    {
        foreach (Section section in Sections)
        {
            HeaderFooterType type = operation.Location == "header" ? HeaderFooterType.HeaderPrimary : HeaderFooterType.FooterPrimary;
            HeaderFooter? container = section.HeadersFooters[type];
            if (container is null)
            {
                container = new HeaderFooter(_document, type);
                section.HeadersFooters.Add(container);
            }

            Field? existingPage = container.Range.Fields.Cast<Field>()
                .FirstOrDefault(static field => field.Type == FieldType.FieldPage);
            Paragraph paragraph;
            if (existingPage is null)
            {
                paragraph = new Paragraph(_document);
                container.AppendChild(paragraph);
                var builder = new DocumentBuilder(_document);
                builder.MoveTo(paragraph);
                builder.InsertField("PAGE");
            }
            else
            {
                paragraph = (Paragraph)existingPage.Start.GetAncestor(NodeType.Paragraph);
            }

            paragraph.ParagraphFormat.Alignment = AlignmentOf(operation.Alignment);
            if (operation.Start is int start)
            {
                section.PageSetup.RestartPageNumbering = true;
                section.PageSetup.PageStartingNumber = start;
            }

            if (operation.Format is not null)
            {
                section.PageSetup.PageNumberStyle = operation.Format switch
                {
                    "decimal" => NumberStyle.Arabic,
                    "upperRoman" => NumberStyle.UppercaseRoman,
                    "lowerRoman" => NumberStyle.LowercaseRoman,
                    "upperLetter" => NumberStyle.UppercaseLetter,
                    "lowerLetter" => NumberStyle.LowercaseLetter,
                    _ => throw Invalid($"unknown page number format '{operation.Format}'"),
                };
            }
        }

        _document.UpdatePageLayout();
        return Sections.Count;
    }

    public long Apply(AppendDocumentOp operation)
    {
        using LoadedDocument loaded = _loader.Open(operation.Path, null);
        _loaded.Imported(loaded);
        if (loaded.Evaluation)
        {
            WordsEvaluation.RemoveLeadingBanners(loaded.Document);
        }

        ImportFormatMode mode = operation.ImportFormatMode == "useDestination"
            ? ImportFormatMode.UseDestinationStyles
            : ImportFormatMode.KeepSourceFormatting;
        _document.AppendDocument(loaded.Document, mode);
        return 1;
    }
}

