using System.Globalization;
using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.IO.Input;
using Aspose.Cli.Sdk.Results;
using Aspose.Words;
using Aspose.Words.Layout;

namespace Aspose.Cli.Product.Words.Engine;

/// <summary>Provides stateless result, warning and text helpers shared by Words engine handlers.</summary>
internal static class WordsEngineSupport
{
    internal static IReadOnlyList<Warning>? InputWarnings(LoadedDocument loaded)
    {
        var warnings = new List<Warning>();
        if (LocalDocumentResourceLoader.OmissionWarning(loaded.OmittedResources) is { } omitted)
        {
            warnings.Add(omitted);
        }

        if (loaded.FixedPageHtml)
        {
            warnings.Add(FixedPageHtmlReflowed);
        }

        return warnings.Count == 0 ? null : warnings;
    }

    // The SDK reads no fixed-layout format: an html-fixed page rendition loads as flowing HTML,
    // and its absolutely positioned page containers break apart.
    private static Warning FixedPageHtmlReflowed { get; } = new(WarningCodes.LossyConversion, "The input is an html-fixed page rendition, which loads as flowing HTML: its pages are reflowed, so the page count, tables, headers, footers and layout differ from the delivered file.");

    /// <summary>
    /// The warnings for writing a loaded document to a format; the write pipeline adds the
    /// evaluation disclosure. A page render passes <paramref name="rendered"/>: an image shows
    /// the document as it looks and is never a copy of it, so it is not told that it drops the
    /// source's revisions or macros, only that comment balloons narrow its pages. A convert passes
    /// the <paramref name="conversion"/> warnings <see cref="PrepareConversion"/> read.
    /// </summary>
    internal static IReadOnlyList<Warning>? WrittenWarnings(LoadedDocument loaded, string format, bool rendered = false, IReadOnlyList<Warning>? conversion = null)
    {
        var extra = new List<Warning>();
        extra.AddRange(InputWarnings(loaded) ?? []);

        if (!rendered)
        {
            extra.AddRange(conversion ?? ConversionWarnings(loaded.Document, format, loaded.Evaluation));
            extra.AddRange(PdfInputWarnings(loaded) ?? []);
        }
        else if (CommentBalloons(loaded.Document, format) is { } balloons)
        {
            extra.Add(balloons);
        }

        if (KeepsRevisions(loaded.Document, format))
        {
            extra.Add(TrackedChangesPresent);
        }

        extra.AddRange(SaveWarnings(loaded, format, rendered));
        return EnvelopeParts.CombineWarnings(extra);
    }

    /// <summary>
    /// The warnings for what a saved copy of a loaded document loses or gains, unless the output
    /// is a page render: the macros the format drops, and the fonts the save embeds because the
    /// input asks for them (PDF-DOCX-EMBED-FONTS).
    /// </summary>
    internal static IEnumerable<Warning> SaveWarnings(LoadedDocument loaded, string format, bool rendered = false)
    {
        if (rendered)
        {
            yield break;
        }

        if (MacrosDropped(loaded, format) is { } macros)
        {
            yield return macros;
        }

        if (WordsSavePipeline.EmbedsFontsTheInputLacks(loaded.Document, format))
        {
            yield return FontsEmbedded;
        }
    }

    // PDF-DOCX-EMBED-FONTS: Aspose.PDF writes a docx that asks to embed its fonts but embeds
    // few or none; the SDK honours the request, which the CLI keeps and discloses rather than rewrites.
    private static Warning FontsEmbedded { get; } = new(WordsDiagnostics.FontsEmbedded, "The input's settings ask every save to embed its TrueType and system fonts, and the input does not embed every font its text uses, so this output embeds the missing fonts and can be megabytes larger than the input.");

    /// <summary>
    /// The warnings for a document saved from a PDF input, or null. PDF loading rebuilds the
    /// pages as flowing text and guesses the headers and footers (WORDS-PDF-HEADER-FOOTER), and
    /// it ends each line of Chinese, Japanese or Korean text with a space (WORDS-PDF-CJK-LINE-END).
    /// </summary>
    internal static IReadOnlyList<Warning>? PdfInputWarnings(LoadedDocument loaded) =>
        !WordsEngineFormats.Has(loaded.FormatId, static format => format.ReflowsOnLoad) ? null
        : WordsFonts.HasEastAsian(loaded.Document.GetText()) ? [PdfInputReflowed, PdfInputLineEnds]
        : [PdfInputReflowed];

    private static Warning PdfInputLineEnds { get; } = new(WarningCodes.LossyConversion, "The Chinese, Japanese or Korean text of the PDF input has a space wherever a line ended on its page, and lines of separate paragraphs, such as a heading and the clause after it, may be merged into one paragraph.");

    private static Warning PdfInputReflowed { get; } = new(WarningCodes.LossyConversion, "A PDF input is rebuilt as flowing text, and its headers and footers are guessed: they may become body text, or headers and footers in which a number, such as a version number, becomes a page-number field; the new layout may add pages.");

    /// <summary>The warning for a source with macros saved to a format that drops them, or null.</summary>
    private static Warning? MacrosDropped(LoadedDocument source, string format) =>
        source.Format.HasMacros && !WordsFormats.KeepsMacros(format)
            ? new Warning(WordsDiagnostics.MacrosDropped, "The source contains macros which the target format does not preserve.")
            : null;

    /// <summary>Discloses the revisions an output that stores them still contains.</summary>
    internal static Warning TrackedChangesPresent { get; } = new(WordsDiagnostics.TrackedChangesPresent, "The input has tracked changes, and the output still contains tracked changes.");

    /// <summary>Whether a document saved in a format keeps its revisions as revisions.</summary>
    internal static bool KeepsRevisions(Document document, string format) =>
        document.Revisions.Count > 0 && WordsFormats.KeepsRevisions(format);

    /// <summary>
    /// The warnings for saving a document to a format that cannot hold every Word feature, which
    /// convert, edit and the redline of compare report: one
    /// for the format, which names no kind of content; and, each naming its kind in
    /// <c>details.content</c>, one for revisions the format cannot store, which a format laid
    /// out on pages draws as markup; one for the editing restrictions a format other than Word's
    /// cannot keep; one for the headers and footers with content a format without pages leaves
    /// out; for a format laid out on pages one for the comments it draws in balloons; and for
    /// plain text and Markdown one for each kind of text the SDK mixes into the body
    /// (WORDS-TEXT-COMMENTS, WORDS-TEXT-DELETIONS), which also stands for the revisions such an
    /// output drops. Under <paramref name="evaluation"/>, what evaluation mode wrote into the
    /// headers and footers is not their content. A <paramref name="redline"/>, compare's output,
    /// shows its revisions on purpose: it is not told that a format that marks them cannot keep
    /// them as revisions.
    /// </summary>
    internal static IEnumerable<Warning> ConversionWarnings(Document document, string format, bool evaluation, bool redline = false)
    {
        bool text = WordsEngineFormats.Has(format, static entry => entry.PlainText);
        if (WordsEngineFormats.Has(format, static entry => entry.Lossy))
        {
            yield return new Warning(WarningCodes.LossyConversion, $"Conversion to {format} cannot preserve every Word feature.");
        }

        bool revised = document.Revisions.Count > 0;
        bool mixed = text && document.Revisions.Cast<Revision>()
            .Any(static revision => revision.RevisionType is RevisionType.Deletion or RevisionType.Moving);
        if (revised && !mixed && !(redline && !text) && !WordsFormats.KeepsRevisions(format))
        {
            yield return WordsEngineFormats.LaysOutPages(format)
                ? ContentLoss.Warning([ContentKind.Revisions],
                    $"The {format} output draws the tracked changes on its pages as markup, such as struck-through deletions, and cannot keep tracked changes as revisions that can be accepted or rejected.")
                : ContentLoss.Warning([ContentKind.Revisions],
                    $"The {format} output cannot keep tracked changes as revisions that can be accepted or rejected.");
        }

        if (document.ProtectionType != ProtectionType.NoProtection && !WordsFormats.IsWord(format))
        {
            yield return ContentLoss.Warning([ContentKind.EditingRestrictions],
                $"The {format} output cannot keep the {WordsProtection.ToMode(document.ProtectionType)} editing restrictions of the document.");
        }

        if (HeadersFootersLeftOut(document, format, evaluation) is { } headersFooters)
        {
            yield return headersFooters;
        }

        if (CommentBalloons(document, format) is { } balloons)
        {
            yield return balloons;
        }

        if (!text)
        {
            yield break;
        }

        int comments = CommentCount(document);
        if (comments > 0)
        {
            yield return ContentLoss.Warning([ContentKind.Comments],
                string.Create(CultureInfo.InvariantCulture, $"The {format} output writes the text of {comments} comment(s) into the body text, where it reads as document text."));
        }

        if (mixed)
        {
            yield return ContentLoss.Warning([ContentKind.Revisions],
                $"The {format} output writes the deleted and moved-from text of tracked changes beside the text that replaces it, so it reads as neither the original nor the revised document.");
        }
    }

    /// <summary>
    /// The <see cref="ConversionWarnings"/> of a document about to be saved in a format, read
    /// first, as a revision or comment may sit on or in a drawing; then the warning naming the
    /// drawings the format leaves out, which it takes out (<see cref="WordsSavePipeline.LeaveOutDrawings"/>).
    /// Convert and compare call it right before they save.
    /// </summary>
    internal static IReadOnlyList<Warning> PrepareConversion(Document document, string format, bool evaluation, bool redline = false) =>
        [.. ConversionWarnings(document, format, evaluation, redline),
            .. EnvelopeParts.CombineWarnings(DrawingsLeftOut(WordsSavePipeline.LeaveOutDrawings(document, format), format)) ?? []];

    /// <summary>
    /// The warning naming the drawings a save left out (<see cref="WordsSavePipeline.LeaveOutDrawings"/>)
    /// as it counted them, saying whether their text stayed, or null for none.
    /// </summary>
    private static Warning? DrawingsLeftOut(WordsDrawingsLeftOut drawings, string format)
    {
        (ContentKind Kind, int Count, string Noun)[] left = [.. new[] { (ContentKind.Pictures, drawings.Pictures, "picture(s)"), (ContentKind.Charts, drawings.Charts, "chart(s)"),
            (ContentKind.Shapes, drawings.Others, "other drawing(s), such as shapes, text boxes, WordArt and SmartArt") }.Where(static kind => kind.Item2 > 0)];
        if (left.Length == 0)
        {
            return null;
        }

        string[] counts = [.. left.Select(static kind => $"{kind.Count} {kind.Noun}")];
        return ContentLoss.Warning([.. left.Select(static kind => kind.Kind)],
            $"The {format} output holds the text alone and leaves out {string.Join(", ", counts[..^1])}{(counts.Length > 1 ? " and " : "")}{counts[^1]}"
                + (drawings.KeptText ? "; the text of shapes and text boxes stays in the body." : "."));
    }

    /// <summary>
    /// The warning naming the headers and footers with content that a format without pages leaves
    /// out, with the reason for the format, or null.
    /// </summary>
    private static Warning? HeadersFootersLeftOut(Document document, string format, bool evaluation)
    {
        if (!WordsEngineFormats.Has(format, static entry => entry.LeavesOutHeadersFooters)
            || HeadersFootersWithContent(document, evaluation) is not { } leftOut)
        {
            return null;
        }

        string reason = WordsEngineFormats.Has(format, static entry => entry.PlainText)
            ? "a text file holds the body text alone, without pages"
            : "an e-book flows its text without pages";
        return ContentLoss.Warning([ContentKind.HeadersFooters],
            $"The {format} output leaves out {leftOut}: {reason}, so it has no headers or footers.");
    }

    /// <summary>
    /// The comments a format laid out on pages draws in balloons in a margin beside the text,
    /// which the engine makes room for by narrowing the pages, or null.
    /// </summary>
    private static Warning? CommentBalloons(Document document, string format)
    {
        int comments = CommentCount(document);
        return comments > 0 && WordsEngineFormats.LaysOutPages(format)
            && document.LayoutOptions.CommentDisplayMode == CommentDisplayMode.ShowInBalloons
            ? ContentLoss.Warning([ContentKind.Comments],
                string.Create(CultureInfo.InvariantCulture, $"The {format} output draws {comments} comment(s) in balloons in a margin beside the text, which narrows its pages."))
            : null;
    }

    /// <summary>The comments of a document, replies included.</summary>
    private static int CommentCount(Document document) => document.GetChildNodes(NodeType.Comment, true).Count;

    /// <summary>
    /// The headers and footers that hold content and that some page shows, by the sections that
    /// define them, such as <c>the headers and footers of section(s) 1</c> or <c>the headers of
    /// section(s) 1, 2 and the footers of section(s) 2</c>; null when every one is empty or no page
    /// shows it, as a first-page header of a section without a different first page. Content is
    /// text, a tracked deletion's struck-through text included, or a drawing. A page number or
    /// page count is not: an output without pages has none to number. Under
    /// <paramref name="evaluation"/>, neither are the banner, footer sentence and watermark
    /// evaluation mode writes.
    /// </summary>
    private static string? HeadersFootersWithContent(Document document, bool evaluation)
    {
        Section[] sections = [.. document.Sections.Cast<Section>()];
        IReadOnlySet<HeaderFooter> shown = WordsHeadersFooters.ShownStories(document);
        string headers = Numbers(isHeader: true);
        string footers = Numbers(isHeader: false);
        return (headers.Length, footers.Length) switch
        {
            (0, 0) => null,
            (_, 0) => $"the headers of section(s) {headers}",
            (0, _) => $"the footers of section(s) {footers}",
            _ when headers == footers => $"the headers and footers of section(s) {headers}",
            _ => $"the headers of section(s) {headers} and the footers of section(s) {footers}",
        };

        string Numbers(bool isHeader) => string.Join(", ", sections
            .Select(static (section, index) => (Section: section, Number: index + 1))
            .Where(item => item.Section.HeadersFooters.Cast<HeaderFooter>().Any(story => story.IsHeader == isHeader && shown.Contains(story) && HoldsContent(story)))
            .Select(static item => item.Number.ToString(CultureInfo.InvariantCulture)));

        bool HoldsContent(HeaderFooter story) =>
            story.GetChildNodes(NodeType.Paragraph, true).Cast<Paragraph>()
                .Any(paragraph => !(evaluation && WordsEvaluation.IsMark(paragraph))
                    && WordsText.VisibleRuns(paragraph, deleted: true).Any(static run => !string.IsNullOrWhiteSpace(run.Text)))
            || story.GetChildNodes(NodeType.Shape, true).Cast<Aspose.Words.Drawing.Shape>()
                .Any(shape => !(evaluation && WordsEvaluation.IsWatermark(shape)));
    }

    internal static IReadOnlyList<Warning>? CompareWarnings(LoadedDocument left, LoadedDocument right) =>
        EnvelopeParts.CombineWarnings(InputWarnings(left), InputWarnings(right));

}
