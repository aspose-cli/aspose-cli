using System.Globalization;
using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk;
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
    private static Warning FixedPageHtmlReflowed { get; } = new(WarningCodes.LossyConversion, "The input is an html-fixed page rendition, which loads as flowing HTML: its pages are reflowed, so the page count, tables, headers, footers and layout differ from the delivered file.")
    {
        Hint = "Review and edit the source document the html-fixed file was converted from, such as its .docx, instead of the html-fixed file.",
    };

    /// <summary>
    /// The warnings for writing a loaded document to a format; the write pipeline adds the
    /// evaluation disclosure. A page render passes <paramref name="rendered"/>: an image shows
    /// the document as it looks and is never a copy of it, so it is not told that it drops the
    /// source's revisions or macros, only that comment balloons narrow its pages. A convert passes
    /// the input it read and the file it wrote as <paramref name="converted"/>.
    /// </summary>
    internal static IReadOnlyList<Warning>? WrittenWarnings(LoadedDocument loaded, string format, bool rendered = false, (ContinuationInput Input, string Output)? converted = null)
    {
        var extra = new List<Warning>();
        extra.AddRange(InputWarnings(loaded) ?? []);

        if (!rendered)
        {
            extra.AddRange(ConversionWarnings(loaded.Document, format, loaded.Evaluation, converted));
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
    private static Warning FontsEmbedded { get; } = new(WordsDiagnostics.FontsEmbedded, "The input's settings ask every save to embed its TrueType and system fonts, and the input does not embed every font its text uses, so this output embeds the missing fonts and can be megabytes larger than the input.")
    {
        Hint = "No option turns the setting off, so a saved copy stays this large and shows its fonts the same on every computer. To keep it small, deliver the input as it is; when 'pdf convert --to docx' wrote it, write the docx with 'aspose-cli words convert <pdf> --to docx' instead, which asks for no embedded fonts.",
    };

    /// <summary>
    /// The warnings for a document saved from a PDF input, or null. PDF loading rebuilds the
    /// pages as flowing text and guesses the headers and footers (WORDS-PDF-HEADER-FOOTER), and
    /// it ends each line of Chinese, Japanese or Korean text with a space (WORDS-PDF-CJK-LINE-END).
    /// </summary>
    internal static IReadOnlyList<Warning>? PdfInputWarnings(LoadedDocument loaded) =>
        !WordsEngineFormats.Has(loaded.FormatId, static format => format.ReflowsOnLoad) ? null
        : WordsFonts.HasEastAsian(loaded.Document.GetText()) ? [PdfInputReflowed, PdfInputLineEnds]
        : [PdfInputReflowed];

    private static Warning PdfInputLineEnds { get; } = new(WarningCodes.LossyConversion, "The Chinese, Japanese or Korean text of the PDF input has a space wherever a line ended on its page, and lines of separate paragraphs, such as a heading and the clause after it, may be merged into one paragraph.")
    {
        Hint = "Read the blocks before editing, and find a phrase with a regex that allows a space where a line may have ended, such as '甲 ?乙双方'; remove a space only where you read it.",
    };

    private static Warning PdfInputReflowed { get; } = new(WarningCodes.LossyConversion, "A PDF input is rebuilt as flowing text, and its headers and footers are guessed: they may become body text, or headers and footers in which a number, such as a version number, becomes a page-number field; the new layout may add pages.")
    {
        Hint = "Check the headers, footers and fields with 'aspose-cli words inspect <file> --detail sections --detail fields' and the body with 'words read'; correct them with 'words edit' and set_header, set_footer, set_page_numbers or delete_blocks.",
    };

    /// <summary>The warning for a source with macros saved to a format that drops them, or null.</summary>
    private static Warning? MacrosDropped(LoadedDocument source, string format) =>
        source.Format.HasMacros && !WordsFormats.KeepsMacros(format)
            ? new Warning(WordsDiagnostics.MacrosDropped, "The source contains macros which the target format does not preserve.") { Hint = "Save to docm or dotm to preserve macros." }
            : null;

    /// <summary>Discloses the revisions an output that stores them still contains.</summary>
    internal static Warning TrackedChangesPresent { get; } = new(WordsDiagnostics.TrackedChangesPresent, "The input has tracked changes, and the output still contains tracked changes.")
    {
        Hint = "Disclose them and accept or reject only when explicitly requested.",
    };

    /// <summary>Whether a document saved in a format keeps its revisions as revisions.</summary>
    internal static bool KeepsRevisions(Document document, string format) =>
        document.Revisions.Count > 0 && WordsFormats.KeepsRevisions(format);

    /// <summary>
    /// The warnings for saving a document to a format that cannot hold every Word feature: one
    /// for the format, which names no kind of content; and, each naming its kind in
    /// <c>details.content</c>, one for revisions the format cannot store, which a format laid
    /// out on pages draws as markup; one for the editing restrictions a format other than Word's
    /// cannot keep; one for the headers and footers with content a format without pages leaves
    /// out; for a format laid out on pages one for the comments it draws in balloons; and for
    /// plain text and Markdown one for each kind of text the SDK mixes into the body
    /// (WORDS-TEXT-COMMENTS, WORDS-TEXT-DELETIONS), which also stands for the revisions such an
    /// output drops. Their hints give the commands that leave it out of the file
    /// <paramref name="converted"/> names, a convert's output, or without it say the same in words;
    /// for a document with both kinds the revisions' commands delete the comments too, since two
    /// edits of the input would each put back what the other left out. Under
    /// <paramref name="evaluation"/>, what evaluation mode wrote into the headers and footers is
    /// not their content.
    /// </summary>
    internal static IEnumerable<Warning> ConversionWarnings(Document document, string format, bool evaluation, (ContinuationInput Input, string Output)? converted = null)
    {
        bool text = WordsEngineFormats.Has(format, static entry => entry.PlainText);
        if (WordsEngineFormats.Has(format, static entry => entry.Lossy))
        {
            yield return new Warning(WarningCodes.LossyConversion, $"Conversion to {format} cannot preserve every Word feature.") { Hint = "Keep a DOCX copy when styles, headers, fields or revisions matter." };
        }

        bool revised = document.Revisions.Count > 0;
        bool mixed = text && document.Revisions.Cast<Revision>()
            .Any(static revision => revision.RevisionType is RevisionType.Deletion or RevisionType.Moving);
        if (revised && !mixed && !WordsFormats.KeepsRevisions(format))
        {
            yield return WordsEngineFormats.LaysOutPages(format)
                ? ContentLoss.Warning([ContentKind.Revisions],
                    $"The {format} output draws the tracked changes on its pages as markup, such as struck-through deletions, and cannot keep tracked changes as revisions that can be accepted or rejected.",
                    "Disclose the tracked changes; when the user decides, accept or reject them with 'aspose-cli words edit' before converting, so the pages show one version, or keep a DOCX, RTF or ODT copy to keep them reviewable.")
                : ContentLoss.Warning([ContentKind.Revisions],
                    $"The {format} output cannot keep tracked changes as revisions that can be accepted or rejected.",
                    "Disclose the tracked changes; keep a DOCX, RTF or ODT copy to keep them reviewable.");
        }

        if (document.ProtectionType != ProtectionType.NoProtection && !WordsFormats.IsWord(format))
        {
            yield return ContentLoss.Warning([ContentKind.EditingRestrictions],
                $"The {format} output cannot keep the {WordsProtection.ToMode(document.ProtectionType)} editing restrictions of the document.",
                "Keep a DOCX copy to keep them; restrict a PDF with the encrypt operation of 'aspose-cli pdf edit'.");
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
        bool both = comments > 0 && mixed;
        if (comments > 0)
        {
            yield return ContentLoss.Warning([ContentKind.Comments],
                string.Create(CultureInfo.InvariantCulture, $"The {format} output writes the text of {comments} comment(s) into the body text, where it reads as document text."),
                converted is not { } from
                    ? $"To leave the comments out, apply delete_comments in an edit that writes a Word format such as docx, then convert that file to {format}; keep a DOCX copy to keep them."
                    : both ? "To leave the comments out, run the commands of the warning about the tracked changes, whose edit deletes the comments too; keep a DOCX copy to keep them."
                    : $"To leave the comments out, run {Rewritten(from, format, "delete_comments")}; keep a DOCX copy to keep them.");
        }

        if (mixed)
        {
            string[] deleting = both ? ["delete_comments"] : [];
            yield return ContentLoss.Warning([ContentKind.Revisions],
                $"The {format} output writes the deleted and moved-from text of tracked changes beside the text that replaces it, so it reads as neither the original nor the revised document.",
                converted is not { } from
                    ? $"Disclose the revisions; when the user decides, accept or reject them in an edit that writes a Word format such as docx, then convert that file to {format}, or keep a DOCX copy."
                    : $"Disclose the revisions; when the user decides, run {Rewritten(from, format, [.. deleting, "accept_revisions"])} to accept them or "
                        + $"{Rewritten(from, format, [.. deleting, "reject_revisions"])} to reject them, or keep a DOCX copy."
                        + (both ? " Either edit deletes the comments too." : string.Empty));
        }
    }

    /// <summary>
    /// The commands that leave out of a converted file what <paramref name="ops"/> remove: the edit
    /// of the input that applies them and writes a Word copy beside that file, since an edit writes
    /// no format a convert alone writes, then the convert of the copy, opened as the input is, that
    /// replaces the file.
    /// </summary>
    private static string Rewritten((ContinuationInput Input, string Output) from, string format, params string[] ops)
    {
        string copy = Path.Combine(Path.GetDirectoryName(from.Output) ?? string.Empty,
            Path.GetFileNameWithoutExtension(from.Input.Document) + ".out" + WordsSplitWriter.Docx.Extensions[0]);
        string operations = "[" + string.Join(",", ops.Select(static op => $$"""{"op":"{{op}}"}""")) + "]";
        return ContinuationCommand.Steps(
        [
            from.Input.ContinuationAt("words", "edit").Document("--ops", operations).Option("--out", copy),
            from.Input.WrittenAs(copy).ContinuationAt("words", "convert").Option("--to", format).Option("--out", from.Output).Flag("--overwrite"),
        ]);
    }

    /// <summary>
    /// The warning naming the headers and footers with content that a format without pages leaves
    /// out, with the reason for the format, or null: convert and edit report it with the other
    /// conversion warnings, compare for its redline.
    /// </summary>
    internal static Warning? HeadersFootersLeftOut(Document document, string format, bool evaluation)
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
            $"The {format} output leaves out {leftOut}: {reason}, so it has no headers or footers.",
            "Read them from the source with 'aspose-cli words inspect <file> --detail sections'; keep a DOCX or PDF copy to keep them.");
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
                string.Create(CultureInfo.InvariantCulture, $"The {format} output draws {comments} comment(s) in balloons in a margin beside the text, which narrows its pages."),
                "When the user has decided on them, apply delete_comments with 'aspose-cli words edit' first, with accept_revisions or reject_revisions for tracked changes, so the pages keep their full width; keep a DOCX copy to keep the comments.")
            : null;
    }

    /// <summary>The comments of a document, replies included.</summary>
    private static int CommentCount(Document document) => document.GetChildNodes(NodeType.Comment, true).Count;

    /// <summary>
    /// The headers and footers that hold content and that some page shows, by the sections that
    /// define them, such as <c>the headers and footers of section(s) 1</c> or <c>the headers of
    /// section(s) 1, 2 and the footers of section(s) 2</c>; null when every one is empty or no page
    /// shows it, as a first-page header of a section without a different first page. Content is
    /// text or a drawing. A page number or page count is not: an output without pages has none to
    /// number. Under <paramref name="evaluation"/>, neither are the banner, footer sentence and
    /// watermark evaluation mode writes.
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
                    && WordsText.VisibleRuns(paragraph).Any(static run => !string.IsNullOrWhiteSpace(run.Text)))
            || story.GetChildNodes(NodeType.Shape, true).Cast<Aspose.Words.Drawing.Shape>()
                .Any(shape => !(evaluation && WordsEvaluation.IsWatermark(shape)));
    }

    internal static IReadOnlyList<Warning>? CompareWarnings(LoadedDocument left, LoadedDocument right) =>
        EnvelopeParts.CombineWarnings(InputWarnings(left), InputWarnings(right));

}
