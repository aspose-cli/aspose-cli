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

/// <summary>Owns section, page, header and document-structure mutations.</summary>
internal static class WordsStructureOpHandlers
{
    internal static long AddSection(Document document, AddSectionOp op)
    {
        var section = new Section(document);
        section.AppendChild(new Body(document));
        section.EnsureMinimum();
        if (op.Position == "after")
        {
            int after = op.After
                ?? throw Invalid("add_section position 'after' requires an 'after' section number");
            Section existing = GetSection(document, after);
            existing.ParentNode!.InsertAfter(section, existing);
        }
        else if (op.Position == "start")
        {
            document.PrependChild(section);
        }
        else
        {
            document.AppendChild(section);
        }

        if (op.PageSetup is not null)
        {
            ApplyPageSetup(section, op.PageSetup);
        }

        return 1;
    }

    internal static long DeleteSection(Document document, DeleteSectionOp op)
    {
        if (document.Sections.Count == 1)
        {
            throw Invalid("the last section cannot be deleted");
        }

        GetSection(document, op.Section).Remove();
        return 1;
    }

    internal static long SetPageSetup(Document document, SetPageSetupOp op)
    {
        Section[] sections = op.Section is int section ? [GetSection(document, section)] : document.Sections.Cast<Section>().ToArray();
        foreach (Section current in sections)
        {
            ApplyPageSetup(current, op.Setup);
        }

        return sections.Length;
    }

    internal static long SetHeader(Document document, SetHeaderOp op) =>
        SetHeaderFooter(document, op.Section, op.Kind, op.Paragraphs, op.Markdown, isHeader: true);

    internal static long SetFooter(Document document, SetFooterOp op) =>
        SetHeaderFooter(document, op.Section, op.Kind, op.Paragraphs, op.Markdown, isHeader: false);

    internal static long SetHeaderFooter(
        Document document,
        int? sectionNumber,
        string kind,
        IReadOnlyList<string>? paragraphs,
        string? markdown,
        bool isHeader)
    {
        Section[] sections = sectionNumber is int number ? [GetSection(document, number)] : document.Sections.Cast<Section>().ToArray();
        HeaderFooterType type = HeaderFooterTypeOf(kind, isHeader);
        foreach (Section section in sections)
        {
            HeaderFooter? current = section.HeadersFooters[type];
            current?.Remove();
            var replacement = new HeaderFooter(document, type);
            section.HeadersFooters.Add(replacement);
            foreach (string text in paragraphs ?? MarkdownLines(markdown!))
            {
                var paragraph = new Paragraph(document);
                paragraph.AppendChild(new Run(document, text));
                replacement.AppendChild(paragraph);
            }

            if (!replacement.HasChildNodes)
            {
                replacement.AppendChild(new Paragraph(document));
            }
        }

        return sections.Length;
    }

    internal static long SetPageNumbers(Document document, SetPageNumbersOp op)
    {
        Section[] sections = op.Section is int number ? [GetSection(document, number)] : document.Sections.Cast<Section>().ToArray();
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
        return sections.Length;
    }

    internal static long AppendDocument(
        Document document,
        AppendDocumentOp op,
        WordsDocumentLoader loader)
    {
        using LoadedDocument loaded = loader.Open(op.Path, null);
        ImportFormatMode mode = op.ImportFormatMode == "useDestination"
            ? ImportFormatMode.UseDestinationStyles
            : ImportFormatMode.KeepSourceFormatting;
        document.AppendDocument(loaded.Document, mode);
        return 1;
    }
}

