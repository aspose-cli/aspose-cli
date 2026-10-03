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

    private static string SaveText(Document document, SaveOptions options)
    {
        using var stream = new MemoryStream();
        document.Save(stream, options);
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }
}
