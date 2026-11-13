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
    /// first-page or even-page kind also turns on the section setting that shows it. Each section
    /// that shows a replaced primary header or footer, a later one that continues it too, is
    /// recorded, to report its page numbers removed when the batch leaves it without one.
    /// </summary>
    private void SetHeaderFooter(HeaderFooterOp operation, bool isHeader)
    {
        string kind = operation.Kind;
        Document? markdown = operation.Markdown is null ? null : _loader.OpenMarkdown(operation.Markdown, _loaded);
        HeaderFooterType type = HeaderFooterTypeOf(kind, isHeader);
        // Before the first replacement: each section that will show a replaced story, and whether the
        // story it shows now holds page numbers. After it, a section shows the story of the nearest
        // section, itself or an earlier one, that is replaced or keeps its own.
        Section[] targets = [.. Sections];
        (Section Section, HeaderFooter? Story)[] shown = kind == WordsHeaderFooterKinds.Primary
            ? [.. _document.Sections.Cast<Section>().Where(section => Shows(section) is { } owner && targets.Contains(owner))
                .Select(section => (section, WordsHeadersFooters.Continued(section, type)))]
            : [];
        Section? Shows(Section section)
        {
            Section? current = section;
            while (current is not null && !targets.Contains(current) && current.HeadersFooters[type] is null)
            {
                current = current.PreviousSibling as Section;
            }

            return current;
        }

        foreach ((Section section, HeaderFooter? story) in shown)
        {
            _pageNumbers.Replaced(operation, section, isHeader, WordsHeadersFooters.HoldsPageNumbers(story), owner: targets.Contains(section));
        }

        foreach (Section section in targets)
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
        HeaderFooterType other = operation.Location == "header" ? HeaderFooterType.FooterPrimary : HeaderFooterType.HeaderPrimary;
        // The page numbers move: their PAGE field leaves the other primary header or footer with
        // the words of its paragraph, which are the number's text. A text replaces them; without
        // one they go along, read before any are cleared, since a section can show an earlier one's.
        Section[] targets = [.. Sections];
        string?[] shown = [.. targets.Select(section => PageNumberWords(WordsHeadersFooters.Continued(section, other)))];
        foreach ((Section section, string? words) in targets.Zip(shown))
        {
            if (numbers && section.HeadersFooters[other] is { } previous)
            {
                if (operation.Text is null && words is null)
                {
                    RemovePageFields(previous);
                }
                else
                {
                    ClearPageNumberParagraphs(previous);
                }
            }

            // A section that shows an earlier section's story is numbered with it when the operation
            // numbers that section too, and else numbers a copy of it, as Word copies a story whose
            // link to the previous section is undone, so that it still shows what it showed.
            HeaderFooterType type = operation.Location == "header" ? HeaderFooterType.HeaderPrimary : HeaderFooterType.FooterPrimary;
            HeaderFooter? container = section.HeadersFooters[type];
            HeaderFooter? continued = container is null ? WordsHeadersFooters.Continued(section, type) : null;
            if (continued is not null && targets.Contains(continued.ParentSection))
            {
                SetPageNumbering(section, operation);
                continue;
            }

            if (container is null)
            {
                container = continued is null ? new HeaderFooter(_document, type) : (HeaderFooter)continued.Clone(isCloneChildren: true);
                // A bookmark's name marks one place in the document, which the original keeps.
                foreach (Node mark in container.GetChildNodes(NodeType.BookmarkStart, true).Cast<Node>().Concat(container.GetChildNodes(NodeType.BookmarkEnd, true).Cast<Node>()).ToArray())
                {
                    mark.Remove();
                }

                section.HeadersFooters.Add(container);
            }

            Field? existingPage = numbers
                ? container.Range.Fields.Cast<Field>().FirstOrDefault(static field => field.Type == FieldType.FieldPage)
                : null;
            // Without a text, a number the new location already holds keeps its own words.
            string? text = operation.Text ?? (existingPage is null ? words : null);
            if (operation.Text is null && text is not null)
            {
                _pageNumbers.Moved(operation, _document.Sections.IndexOf(section), text);
            }

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

            if (text is not null)
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
            SetPageNumbering(section, operation);
        }

        _document.UpdatePageLayout();
        return default;
    }

    // A section's own numbering: where it starts and how its numbers are written.
    private static void SetPageNumbering(Section section, SetPageNumbersOp operation)
    {
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
    /// The text of the paragraph that holds a header's or footer's PAGE field, as set_page_numbers
    /// writes it, or null when it holds none or the number alone.
    /// </summary>
    private static string? PageNumberWords(HeaderFooter? headerFooter) =>
        headerFooter?.Range.Fields.Cast<Field>().FirstOrDefault(static field => field.Type == FieldType.FieldPage)?.Start.GetAncestor(NodeType.Paragraph) is Paragraph paragraph
        && WordsReadBack.PageNumberText(paragraph) is var text && text != WordsReadBack.PageNumberPlaceholder
            ? text
            : null;

    /// <summary>
    /// Empties each paragraph of a header or footer that holds a PAGE field, and removes it when
    /// it is not the last, so that a number moving elsewhere leaves none of its words.
    /// </summary>
    private void ClearPageNumberParagraphs(HeaderFooter headerFooter)
    {
        Paragraph[] paragraphs = [.. headerFooter.Range.Fields.Cast<Field>()
            .Where(static field => field.Type == FieldType.FieldPage)
            .Select(static field => (Paragraph)field.Start.GetAncestor(NodeType.Paragraph))
            .Distinct()];
        foreach (Paragraph paragraph in paragraphs)
        {
            RequireOnlyTextAndFields(paragraph, moving: true);
            RemoveInlines([.. paragraph.Range.Fields.Cast<Field>()], [.. paragraph.GetChildNodes(NodeType.Any, isDeep: false).Cast<Node>()]);
            if (paragraph.ParentNode is HeaderFooter && headerFooter.Paragraphs.Count > 1)
            {
                paragraph.Remove();
            }
        }
    }

    /// <summary>
    /// Refuses to overwrite a paragraph with text, or clear it when the number is
    /// <paramref name="moving"/> away, when it holds what a text cannot carry: an image, a logo or
    /// a shape, or a field other than the page number and count. A bookmark shows nothing, and
    /// evaluation mode draws its watermark again at every save.
    /// </summary>
    private void RequireOnlyTextAndFields(Paragraph paragraph, bool moving = false)
    {
        bool isHeader = ((HeaderFooter)paragraph.GetAncestor(NodeType.HeaderFooter)).IsHeader;
        string location = isHeader ? WordsHeaderFooterLocations.Header : WordsHeaderFooterLocations.Footer;
        if (WordsReadBack.UnwrittenField(paragraph) is { } name)
        {
            string aligned = WordsReadBack.AlignmentName(paragraph) is { } alignment ? $",\"alignment\":\"{alignment}\"" : string.Empty;
            throw new OperationInvalidException(
                $"the {location} paragraph that holds the page number also holds a {name} field, which {(moving ? "moving the number would clear" : "text would replace")} with that paragraph: a page number's text, given or taken along, writes no field but {{n}} and {{N}}",
                $"To {(moving ? "move the number" : "write the number's words")}, rewrite the {location} with {(isHeader ? "set_header" : "set_footer")} first, which writes no {name} field, and then number the pages; "
                    + $"{{\"op\":\"set_page_numbers\",\"location\":\"{location}\"{aligned}}} keeps that paragraph, its words and its {name} field as they are.");
        }

        if (paragraph.GetChildNodes(NodeType.Any, isDeep: false).Cast<Node>().All(IsTextOrField))
        {
            return;
        }

        throw new OperationInvalidException(
            $"the {location} paragraph that holds the page number also holds an image or another object that text cannot carry, and "
                + (moving ? "moving the number clears that whole paragraph" : "text replaces the whole paragraph"),
            $"Rewrite the {location} with {(isHeader ? "set_header" : "set_footer")} first so that the page number stands in a paragraph of its own"
                + (moving ? "." : ", or omit text to keep the paragraph as it is."));
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

