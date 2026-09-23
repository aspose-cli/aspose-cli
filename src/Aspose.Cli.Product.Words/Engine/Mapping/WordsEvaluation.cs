using Aspose.Words;

namespace Aspose.Cli.Product.Words.Engine.Mapping;

/// <summary>
/// The one place that recognizes what Aspose.Words evaluation mode adds to a document: the
/// banner paragraph before the first block, the truncation notice, and the watermark text
/// in the page layout. Callers consult it only when the license state is evaluation, so a
/// licensed document that merely quotes these phrases is never misread.
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
