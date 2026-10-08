using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Words;

namespace Aspose.Cli.Product.Words.Engine;

/// <summary>
/// Recognizes what Aspose.Words evaluation mode writes into a document (<see cref="WordsEvaluation"/>):
/// the banner before the first block, the sentence in the footers and the notice that ends a
/// document it cut short. Evaluation mode adds the banner and the sentence when it opens a
/// document, so a document appended to another loses its banner first.
/// </summary>
internal sealed class WordsEvaluationProfile : IEvaluationProfile<Document>
{
    private const string Banner = "the evaluation banner 'Created with an evaluation copy of Aspose.Words.'";
    private const string FooterSentence = "the footer sentence 'Evaluation Only. Created with Aspose.Words.'";

    public EvaluationMarks Inspect(Document document)
    {
        Paragraph[] marks = [.. document.GetChildNodes(NodeType.Paragraph, true).Cast<Paragraph>().Where(WordsEvaluation.IsMark)];
        return new EvaluationMarks(
            [
                .. marks.Any(WordsEvaluation.IsBanner) ? [Banner] : Array.Empty<string>(),
                .. marks.Any(static paragraph => !WordsEvaluation.IsBanner(paragraph)) ? [FooterSentence] : Array.Empty<string>(),
            ],
            WordsEvaluation.IsTruncated(document));
    }

    /// <summary>
    /// The marks images of some pages show: the banner stands on the first page, the footer
    /// sentence on every page and the truncation notice on the last.
    /// </summary>
    public EvaluationMarks Inspect(Document document, string format, IReadOnlyCollection<int> pages)
    {
        EvaluationMarks marks = Inspect(document);
        return new EvaluationMarks(
            [.. marks.Marks.Where(mark => mark != Banner || pages.Contains(1))],
            marks.IsTruncated && pages.Contains(document.PageCount));
    }

    public void Neutralize(Document document) => WordsEvaluation.RemoveLeadingBanners(document);
}
