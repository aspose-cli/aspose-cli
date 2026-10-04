using Aspose.Cli.TestKit;
using Aspose.Words;
using Aspose.Words.Saving;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>
/// The Aspose.Words defects in KNOWN-ISSUES.md, reproduced with the SDK alone. Each passes while
/// the pinned SDK still has its defect.
/// </summary>
public sealed class WordsKnownIssueTests
{
    [Fact]
    public void TextSave_WritesCommentTextIntoTheBody()
    {
        using var fixture = new WordsFixture();
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.Writeln("Clause one.");
        builder.Write("Clause two.");
        var comment = new Comment(document, "Ann", "A", new DateTime(2026, 9, 1));
        comment.AppendChild(new Paragraph(document));
        comment.FirstParagraph!.AppendChild(new Run(document, "Reviewer note"));
        document.FirstSection.Body.FirstParagraph!.AppendChild(comment);

        string text = SaveText(document, new TxtSaveOptions());
        string markdown = SaveText(document, new MarkdownSaveOptions { ExportImagesAsBase64 = true });

        KnownIssue.Reproduces(
            "WORDS-TEXT-COMMENTS",
            text.Contains("Reviewer note", StringComparison.Ordinal)
                && markdown.Contains("Reviewer note", StringComparison.Ordinal),
            $"txt: {text.Trim()}; md: {markdown.Trim()}");
    }

    [Fact]
    public void TextSave_WritesDeletedTextBesideInsertedText()
    {
        using var fixture = new WordsFixture();
        var document = new Document();
        new DocumentBuilder(document).Write("The notice period is thirty days.");
        document.StartTrackRevisions("Ann", new DateTime(2026, 9, 1));
        document.Range.Replace("thirty", "sixty");
        document.StopTrackRevisions();

        string text = SaveText(document, new TxtSaveOptions());
        string markdown = SaveText(document, new MarkdownSaveOptions { ExportImagesAsBase64 = true });

        KnownIssue.Reproduces(
            "WORDS-TEXT-DELETIONS",
            text.Contains("thirtysixty", StringComparison.Ordinal)
                && markdown.Contains("thirtysixty", StringComparison.Ordinal),
            $"txt: {text.Trim()}; md: {markdown.Trim()}");
    }

    [Fact]
    public void PageField_InsertedAfterLayoutHasNoResult()
    {
        using var fixture = new WordsFixture();
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.Writeln("Clause one.");
        _ = document.PageCount;
        Aspose.Words.Fields.Field field = builder.InsertField("NUMPAGES");
        field.Update();

        KnownIssue.Reproduces("WORDS-PAGE-FIELD-LAYOUT", field.Result.Length == 0, $"result: '{field.Result}'");
    }

    [Fact]
    public void PdfLoad_GuessesHeadersAndFootersAndTurnsTheirNumbersIntoPageFields()
    {
        using var fixture = new WordsFixture();

        // One page: the footer becomes the last body paragraph, on a page of its own.
        (int onePageSource, Document onePage) = LoadThroughPdf(clauses: 30, HeaderFooterType.FooterPrimary);
        string last = onePage.FirstSection.Body.LastParagraph!.GetText().Trim();
        // Two pages: the header stays a header, and its version number becomes a PAGE field too.
        // (A header, because evaluation mode writes a sentence of its own into the footer.)
        (int twoPageSource, Document twoPages) = LoadThroughPdf(clauses: 80, HeaderFooterType.HeaderPrimary);
        HeaderFooter header = twoPages.FirstSection.HeadersFooters[HeaderFooterType.HeaderPrimary];
        int pageFields = header?.Range.Fields.Cast<Aspose.Words.Fields.Field>()
            .Count(static field => field.Type == Aspose.Words.Fields.FieldType.FieldPage) ?? 0;

        KnownIssue.Reproduces(
            "WORDS-PDF-HEADER-FOOTER",
            onePageSource == 1 && onePage.PageCount > onePageSource && last.EndsWith('1')
                && twoPageSource == 2 && pageFields >= 2,
            $"one page: {onePage.PageCount} pages, last body paragraph '{last}'; "
                + $"two pages: header '{header?.GetText().Trim()}' with {pageFields} PAGE fields");
    }

    /// <summary>
    /// Saves a document of numbered clauses whose header or footer reads "Version 1  Page {PAGE}"
    /// as PDF and loads the PDF back; returns the source's page count and the loaded document.
    /// </summary>
    private static (int SourcePages, Document Loaded) LoadThroughPdf(int clauses, HeaderFooterType story)
    {
        var document = new Document();
        var builder = new DocumentBuilder(document);
        for (int clause = 1; clause <= clauses; clause++)
        {
            builder.Writeln($"Clause {clause}.");
        }
        builder.MoveToHeaderFooter(story);
        builder.Write("Version 1    Page ");
        builder.InsertField("PAGE");
        int sourcePages = document.PageCount;
        using var pdf = new MemoryStream();
        document.Save(pdf, SaveFormat.Pdf);
        pdf.Position = 0;
        return (sourcePages, new Document(pdf, new Aspose.Words.Loading.PdfLoadOptions()));
    }

    private static string SaveText(Document document, SaveOptions options)
    {
        using var stream = new MemoryStream();
        document.Save(stream, options);
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }
}
