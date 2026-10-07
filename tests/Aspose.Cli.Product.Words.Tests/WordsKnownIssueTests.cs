using System.Globalization;
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
    public void TrackedRevisions_FailToSetTheTextOfADetachedRun()
    {
        using var fixture = new WordsFixture();
        var document = new Document();
        new DocumentBuilder(document).Write("Clause one.");
        var copy = (Run)document.FirstSection.Body.FirstParagraph!.Runs[0].Clone(false);
        document.StartTrackRevisions("Ann", new DateTime(2026, 9, 1));

        Exception? failure = Record.Exception(() => copy.Text = "Clause two.");

        KnownIssue.Reproduces("WORDS-TRACKED-DETACHED-TEXT", failure is NullReferenceException, $"exception: {failure?.GetType().Name ?? "none"}");
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

    [Fact]
    public void PdfLoad_TurnsLineEndsInChineseTextIntoSpaces()
    {
        using var fixture = new WordsFixture();
        const string clause = "甲乙双方经平等协商自愿签订本合同共同遵守本合同所列条款";
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.Font.NameFarEast = "SimSun";
        builder.Writeln("第一条 合同期限");
        builder.Writeln(string.Concat(Enumerable.Repeat(clause, 4)));
        builder.Writeln("第二条 工作内容");
        using var pdf = new MemoryStream();
        document.Save(pdf, SaveFormat.Pdf);
        pdf.Position = 0;

        var loaded = new Document(pdf, new Aspose.Words.Loading.PdfLoadOptions());
        string text = loaded.FirstSection.Body.Paragraphs.Cast<Paragraph>()
            .Select(static paragraph => paragraph.GetText().Trim())
            .FirstOrDefault(static paragraph => paragraph.Contains(clause[..4], StringComparison.Ordinal)) ?? string.Empty;
        // The clauses have no spaces; only a heading merged into their paragraph may bring one.
        string clauses = text.Replace("第一条 合同期限", string.Empty, StringComparison.Ordinal)
            .Replace("第二条 工作内容", string.Empty, StringComparison.Ordinal);

        KnownIssue.Reproduces(
            "WORDS-PDF-CJK-LINE-END",
            clauses.Trim().Contains(' ', StringComparison.Ordinal),
            $"paragraph of the clauses: '{text}'");
    }

    [Fact]
    public void Layout_BreaksEastAsianLinesAgainstTheRules()
    {
        using var fixture = new WordsFixture();
        const string clause = "乙方向甲方供应数控加工中心 2 台，合同总价人民币 1,920,000.00 元（大写：壹佰玖拾贰万元整）。";
        string clauses = string.Concat(Enumerable.Repeat(clause, 4));
        // The English case breaks against the rules only while the process culture is Chinese, as
        // on a Chinese Windows; under an English culture the layout follows them.
        CultureInfo previous = CultureInfo.CurrentCulture;
        CultureInfo previousUi = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("zh-CN");

            // Calibri with Microsoft YaHei for East Asian text: whose East Asian language is
            // English, a line may start with a comma or full stop.
            IReadOnlyList<string> english = WordsFixture.LayoutLines(EastAsianText(clauses, "Calibri", 1033, modern: true));
            IReadOnlyList<string> chinese = WordsFixture.LayoutLines(EastAsianText(clauses, "Calibri", 2052, modern: true));
            static bool Punctuated(IReadOnlyList<string> lines) =>
                lines.Any(static line => line.StartsWith('，') || line.StartsWith('。'));
            // Microsoft YaHei for all text, as PDF loading writes it: outside Word 2013
            // compatibility mode, the amount in words after a space breaks only at that space, so
            // the line ends early.
            IReadOnlyList<string> legacy = WordsFixture.LayoutLines(EastAsianText(clause, "Microsoft YaHei", 2052, modern: false));
            IReadOnlyList<string> modern = WordsFixture.LayoutLines(EastAsianText(clause, "Microsoft YaHei", 2052, modern: true));
            static bool Early(IReadOnlyList<string> lines) =>
                lines.Any(static line => line.StartsWith("元（大写", StringComparison.Ordinal));

            KnownIssue.Reproduces(
                "WORDS-CJK-LINE-BREAK",
                Punctuated(english) && !Punctuated(chinese) && Early(legacy) && !Early(modern),
                $"English: {string.Join(" | ", english)}; Chinese: {string.Join(" | ", chinese)}; "
                    + $"legacy mode: {string.Join(" | ", legacy)}; Word 2013 mode: {string.Join(" | ", modern)}");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
            CultureInfo.CurrentUICulture = previousUi;
        }
    }

    /// <summary>
    /// A paragraph of Chinese text formatted directly in the given Latin font with Microsoft YaHei
    /// for East Asian text, whose East Asian language is the given one, in a new document without a
    /// compatibility mode or in Word 2019 mode. The paragraph turns the East Asian
    /// line-breaking rules on.
    /// </summary>
    private static Document EastAsianText(string text, string latinFont, int localeIdFarEast, bool modern)
    {
        var document = new Document();
        if (modern)
        {
            document.CompatibilityOptions.OptimizeFor(Aspose.Words.Settings.MsWordVersion.Word2019);
        }
        var builder = new DocumentBuilder(document);
        builder.Font.Name = latinFont;
        builder.Font.NameFarEast = "Microsoft YaHei";
        builder.Font.LocaleIdFarEast = localeIdFarEast;
        Assert.True(builder.ParagraphFormat.FarEastLineBreakControl);
        builder.Write(text);
        return document;
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
