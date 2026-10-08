using Aspose.Words;
using Aspose.Words.Tables;

namespace Aspose.Cli.Product.Words.Engine.Mapping;

/// <summary>
/// The one place that recognizes what Aspose.Words evaluation mode adds to a document: the
/// banner paragraph before the first block, the truncation notice, and the watermark text
/// in the page layout. Callers skip such content only when the license state is evaluation, so
/// a licensed document that merely quotes these phrases never loses blocks; with a license,
/// review and the write pipeline (<see cref="WordsEvaluationProfile"/>) report the marks an
/// earlier unlicensed save left.
/// </summary>
internal static class WordsEvaluation
{
    /// <summary>Whether a paragraph is the banner evaluation mode inserts before the first block.</summary>
    internal static bool IsBanner(Paragraph paragraph)
    {
        string text = paragraph.GetText().Trim();
        return text.StartsWith("Created with an evaluation copy of Aspose.Words.", StringComparison.Ordinal)
            && text.Contains("https://products.aspose.com/words/temporary-license/", StringComparison.Ordinal);
    }

    /// <summary>
    /// Whether a paragraph is text evaluation mode writes into a document: the banner before the
    /// first block, or the sentence it adds to the primary footer of every section.
    /// </summary>
    internal static bool IsMark(Paragraph paragraph) =>
        IsBanner(paragraph)
        || paragraph.GetText().TrimStart().StartsWith("Evaluation Only. Created with Aspose.Words.", StringComparison.Ordinal);

    /// <summary>The paragraphs of a document that are <see cref="IsMark">evaluation marks</see>.</summary>
    internal static int MarkCount(Document document) =>
        document.GetChildNodes(NodeType.Paragraph, true).Cast<Paragraph>().Count(IsMark);

    /// <summary>
    /// The banner paragraphs an evaluation-saved document carries before its first block.
    /// Evaluation mode writes one at the start of every document it saves, unless one is there.
    /// </summary>
    internal static IReadOnlyList<Paragraph> LeadingBanners(Document document) =>
        document.FirstSection?.Body.GetChildNodes(NodeType.Any, false).Cast<Node>()
            .Where(static node => node is Paragraph or Table)
            .TakeWhile(static node => node is Paragraph paragraph && IsBanner(paragraph))
            .Cast<Paragraph>()
            .ToArray() ?? [];

    /// <summary>
    /// Removes the leading banners of a document about to be appended to another, where they
    /// would otherwise stand mid-document as ordinary blocks. The saved result still starts
    /// with the one banner evaluation mode writes.
    /// </summary>
    internal static void RemoveLeadingBanners(Document document)
    {
        foreach (Paragraph banner in LeadingBanners(document))
        {
            banner.Remove();
        }
    }

    /// <summary>Whether evaluation mode cut the loaded document short.</summary>
    internal static bool IsTruncated(Document document) =>
        document.GetChildNodes(NodeType.Paragraph, true)
            .Cast<Paragraph>()
            .Select(static paragraph => paragraph.GetText())
            .Any(static text =>
                text.Contains("document was truncated", StringComparison.OrdinalIgnoreCase)
                && text.Contains("evaluation", StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether laid-out text is an evaluation watermark or banner rather than content.</summary>
    internal static bool IsLayoutMark(string text) =>
        text.Contains("Evaluation Only", StringComparison.OrdinalIgnoreCase)
        || text.Contains("Created with Aspose.Words", StringComparison.OrdinalIgnoreCase)
        || text.Contains("Aspose.Words Evaluation", StringComparison.OrdinalIgnoreCase)
        || text.Contains("evaluation copy of Aspose.Words", StringComparison.OrdinalIgnoreCase);
}
