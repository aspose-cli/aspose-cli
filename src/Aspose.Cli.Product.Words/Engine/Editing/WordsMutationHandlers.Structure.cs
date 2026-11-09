using System.Data;
using System.Text.RegularExpressions;
using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.IO.Input;
using Aspose.Words;
using Aspose.Words.Fields;

using static Aspose.Cli.Product.Words.Engine.Editing.WordsMutationSupport;

namespace Aspose.Cli.Product.Words.Engine.Editing;

// Sections, page setup, headers, footers and appended documents.
internal sealed partial class WordsMutationHandlers
{
    /// <summary>
    /// Inserts an empty section that starts with the page setup of its neighbour (the section
    /// <c>at</c> names, before or after which it goes), as Word does, instead of SDK defaults.
    /// Without its own headers and footers it continues its neighbour's.
    /// </summary>
    public ValueTuple Apply(InsertSectionOp operation)
    {
        Section reference = Sections.FirstOrDefault() ?? throw Invalid("insert_section requires an original section at");
        Section section = EmptyLike(reference);
        if (operation.Position == "before")
        {
            _document.InsertBefore(section, reference);
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

        return default;
    }

    /// <summary>
    /// Splits the anchor's section at a block boundary: the blocks from the boundary on move
    /// into a new section with the same page setup, which continues the headers and footers.
    /// </summary>
    private static void InsertSectionBreak(Node anchor, string position)
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

    public ValueTuple Apply(DeleteSectionOp operation)
    {
        Section section = Sections[0];
        if (_document.Sections.Count == 1)
        {
            throw Invalid("the last section cannot be deleted");
        }

        section.Remove();
        return default;
    }

    public ValueTuple Apply(SetPageSetupOp operation)
    {
        foreach (Section current in Sections)
        {
            ApplyPageSetup(current, operation.Setup);
        }

        return default;
    }

    public ValueTuple Apply(SetHeaderOp operation)
    {
        SetHeaderFooter(operation, isHeader: true);
        return default;
    }

    public ValueTuple Apply(SetFooterOp operation)
    {
        SetHeaderFooter(operation, isHeader: false);
        return default;
    }

    /// <summary>
    /// Replaces one kind of header or footer in each section with plain paragraphs or imported
    /// Markdown. Plain paragraphs keep the format of the first paragraph and run the section
    /// shows, such as a right alignment, or take Word's Header or Footer style when there is none. A
    /// first-page or even-page kind also turns on the section setting that shows it. A replaced
    /// primary header or footer that held page numbers is recorded, to report them removed when
    /// the batch leaves the section without one.
    /// </summary>
    private void SetHeaderFooter(HeaderFooterOp operation, bool isHeader)
    {
        string kind = operation.Kind;
        Document? markdown = operation.Markdown is null ? null : _loader.OpenMarkdown(operation.Markdown, _loaded);
        HeaderFooterType type = HeaderFooterTypeOf(kind, isHeader);
        // Before the first replacement: a section without its own continues an earlier one's.
        Section[] numbered = kind == WordsHeaderFooterKinds.Primary
            ? [.. Sections.Where(section => WordsHeadersFooters.HoldsPageNumbers(WordsHeadersFooters.Continued(section, type)))]
            : [];
        foreach (Section section in Sections)
        {
            Paragraph? replaced = ShownParagraph(section, type);
            section.HeadersFooters[type]?.Remove();
            var replacement = new HeaderFooter(_document, type);
            section.HeadersFooters.Add(replacement);
            IEnumerable<Node> blocks = markdown is null
                ? operation.Paragraphs!.Select(input => (Node)HeaderFooterParagraph(input, replaced, isHeader))
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

            if (numbered.Contains(section))
            {
                _pageNumbers.Replaced(operation, section, isHeader);
            }
        }
    }

    /// <summary>
    /// The first paragraph of the header or footer a section shows: its own, or the one it
    /// continues from the nearest earlier section that has one. Evaluation mode's text is no
    /// format to keep.
    /// </summary>
    private Paragraph? ShownParagraph(Section section, HeaderFooterType type)
    {
        for (Section? current = section; current is not null; current = current.PreviousSibling as Section)
        {
            if (current.HeadersFooters[type]?.Paragraphs.Cast<Paragraph>()
                    .FirstOrDefault(paragraph => !(_loaded.Evaluation && WordsEvaluation.IsMark(paragraph))) is { } shown)
            {
                return shown;
            }
        }

        return null;
    }

    /// <summary>A header or footer paragraph: its text in the format of the one it replaces, in its own style when it names one.</summary>
    private Paragraph HeaderFooterParagraph(WordsParagraphInput input, Paragraph? replaced, bool isHeader)
    {
        Paragraph paragraph = PlainParagraph(input.Text, replaced, isHeader);
        if (input.Style is { } style)
        {
            ApplyParagraphStyle(_document, paragraph, style);
        }

        return paragraph;
    }

    /// <summary>A header or footer paragraph of text in the format of the one it replaces.</summary>
    private Paragraph PlainParagraph(string text, Paragraph? replaced, bool isHeader)
    {
        Paragraph paragraph;
        Run run;
        if (replaced is null)
        {
            paragraph = new Paragraph(_document);
            paragraph.ParagraphFormat.StyleIdentifier = isHeader ? StyleIdentifier.Header : StyleIdentifier.Footer;
            run = new Run(_document);
        }
        else
        {
            paragraph = (Paragraph)replaced.Clone(isCloneChildren: false);
            run = replaced.Runs.Count > 0 ? (Run)replaced.Runs[0].Clone(isCloneChildren: true) : new Run(_document);
        }

        run.Text = text;
        paragraph.AppendChild(run);
        return paragraph;
    }

    public ValueTuple Apply(SetPageNumbersOp operation)
    {
        // A text without {n} shows no page number: nothing moves and the number stays where it is.
        bool numbers = operation.Text?.Contains(WordsReadBack.PageNumberPlaceholder, StringComparison.Ordinal) != false;
        foreach (Section section in Sections)
        {
            HeaderFooterType type = operation.Location == "header" ? HeaderFooterType.HeaderPrimary : HeaderFooterType.FooterPrimary;
            HeaderFooter? container = section.HeadersFooters[type];
            if (container is null)
            {
                container = new HeaderFooter(_document, type);
                section.HeadersFooters.Add(container);
            }

            // The page numbers move: their PAGE field leaves the other primary header or footer,
            // and with a text, which is its paragraph's whole content, so does that paragraph's.
            HeaderFooterType other = operation.Location == "header" ? HeaderFooterType.FooterPrimary : HeaderFooterType.HeaderPrimary;
            if (numbers && section.HeadersFooters[other] is { } previous)
            {
                if (operation.Text is null)
                {
                    RemovePageFields(previous);
                }
                else
                {
                    ClearPageNumberParagraphs(previous);
                }
            }

            Field? existingPage = numbers
                ? container.Range.Fields.Cast<Field>().FirstOrDefault(static field => field.Type == FieldType.FieldPage)
                : null;
            Paragraph paragraph;
            if (existingPage is null)
            {
                paragraph = new Paragraph(_document);
                container.AppendChild(paragraph);
            }
            else
            {
                paragraph = (Paragraph)existingPage.Start.GetAncestor(NodeType.Paragraph);
            }

            if (operation.Text is { } text)
            {
                WritePageNumberText(paragraph, text);
            }
            else if (existingPage is null)
            {
                var builder = new DocumentBuilder(_document);
                builder.MoveTo(paragraph);
                builder.InsertField("PAGE");
            }

            paragraph.ParagraphFormat.Alignment = AlignmentOf(operation.Alignment);
            if (operation.Start is int start)
            {
                section.PageSetup.RestartPageNumbering = true;
                section.PageSetup.PageStartingNumber = start;
            }

            if (operation.NumberStyle is not null)
            {
                section.PageSetup.PageNumberStyle = WordsReadBack.NumberStyleOf(operation.NumberStyle);
            }
        }

        _document.UpdatePageLayout();
        return default;
    }

    /// <summary>
    /// Replaces what a paragraph holds with a set_page_numbers text, its placeholders written as
    /// fields, in the font of what it replaces. A paragraph that already reads so is left as it is.
    /// </summary>
    private void WritePageNumberText(Paragraph paragraph, string text)
    {
        if (WordsReadBack.PageNumberText(paragraph) == text)
        {
            return;
        }

        RequireOnlyTextAndFields(paragraph);
        Field[] fields = [.. paragraph.Range.Fields.Cast<Field>()];
        Node[] replaced = [.. paragraph.GetChildNodes(NodeType.Any, isDeep: false).Cast<Node>()];
        var builder = new DocumentBuilder(_document);
        // Moved to the first node, the builder writes in that node's font.
        builder.MoveTo(replaced.FirstOrDefault() ?? paragraph);
        foreach (string piece in PageNumberPlaceholders().Split(text).Where(static piece => piece.Length > 0))
        {
            switch (piece)
            {
                case WordsReadBack.PageNumberPlaceholder:
                    builder.InsertField("PAGE");
                    break;
                case WordsReadBack.PageCountPlaceholder:
                    builder.InsertField("NUMPAGES");
                    break;
                default:
                    builder.Write(piece);
                    break;
            }
        }

        RemoveInlines(fields, replaced);
    }

    [GeneratedRegex(@"(\{n\}|\{N\})", RegexOptions.CultureInvariant)]
    private static partial Regex PageNumberPlaceholders();

    /// <summary>
    /// Empties each paragraph of a header or footer that holds a PAGE field, and removes it when
    /// it is not the last, so that a text moving the number elsewhere leaves none of its words.
    /// </summary>
    private void ClearPageNumberParagraphs(HeaderFooter headerFooter)
    {
        Paragraph[] paragraphs = [.. headerFooter.Range.Fields.Cast<Field>()
            .Where(static field => field.Type == FieldType.FieldPage)
            .Select(static field => (Paragraph)field.Start.GetAncestor(NodeType.Paragraph))
            .Distinct()];
        foreach (Paragraph paragraph in paragraphs)
        {
            RequireOnlyTextAndFields(paragraph);
            RemoveInlines([.. paragraph.Range.Fields.Cast<Field>()], [.. paragraph.GetChildNodes(NodeType.Any, isDeep: false).Cast<Node>()]);
            if (paragraph.ParentNode is HeaderFooter && headerFooter.Paragraphs.Count > 1)
            {
                paragraph.Remove();
            }
        }
    }

    /// <summary>
    /// Refuses to overwrite a paragraph that holds what a text cannot carry, such as an image, a
    /// logo or a shape. A bookmark shows nothing, and evaluation mode draws its watermark again at
    /// every save.
    /// </summary>
    private void RequireOnlyTextAndFields(Paragraph paragraph)
    {
        if (paragraph.GetChildNodes(NodeType.Any, isDeep: false).Cast<Node>().All(IsTextOrField))
        {
            return;
        }

        bool isHeader = ((HeaderFooter)paragraph.GetAncestor(NodeType.HeaderFooter)).IsHeader;
        string location = isHeader ? WordsHeaderFooterLocations.Header : WordsHeaderFooterLocations.Footer;
        throw new OperationInvalidException(
            $"the {location} paragraph that holds the page number also holds an image or another object that text cannot carry, and text replaces the whole paragraph",
            $"Rewrite the {location} with {(isHeader ? "set_header" : "set_footer")} first so that the page number stands in a paragraph of its own, or omit text to keep the paragraph as it is.");
    }

    private bool IsTextOrField(Node node) =>
        node is Aspose.Words.Run or FieldStart or FieldSeparator or FieldEnd or BookmarkStart or BookmarkEnd
        || (_loaded.Evaluation && node is Aspose.Words.Drawing.Shape shape && WordsEvaluation.IsWatermark(shape));

    /// <summary>Removes the fields and then the other nodes of a paragraph, each only while it is still there.</summary>
    private static void RemoveInlines(IEnumerable<Field> fields, IEnumerable<Node> nodes)
    {
        foreach (Field field in fields.Where(static field => field.Start.ParentNode is not null))
        {
            field.Remove();
        }

        foreach (Node node in nodes.Where(static node => node.ParentNode is not null))
        {
            node.Remove();
        }
    }

    /// <summary>Removes the PAGE fields of a header or footer, and each paragraph one leaves empty that is not its last.</summary>
    private static void RemovePageFields(HeaderFooter headerFooter)
    {
        foreach (Field field in headerFooter.Range.Fields.Cast<Field>().Where(static field => field.Type == FieldType.FieldPage).ToArray())
        {
            var paragraph = (Paragraph)field.Start.GetAncestor(NodeType.Paragraph);
            field.Remove();
            if (!paragraph.HasChildNodes && headerFooter.Paragraphs.Count > 1)
            {
                paragraph.Remove();
            }
        }
    }

    public ValueTuple Apply(ImportSectionsOp operation)
    {
        using LoadedDocument loaded = OpenSource(operation);
        _outputs.Neutralize(loaded.Document);

        ImportFormatMode mode = operation.Formatting == FormattingModes.UseDestination
            ? ImportFormatMode.UseDestinationStyles
            : ImportFormatMode.KeepSourceFormatting;
        _document.AppendDocument(loaded.Document, mode);
        return default;
    }

    private LoadedDocument OpenSource(ImportSectionsOp operation) =>
        _loader.Open(operation.Path, PasswordSource.OperationField(
            OperationSecrets.Resolve(_secrets, operation.SourcePasswordEnv), "sourcePasswordEnv"));
}

