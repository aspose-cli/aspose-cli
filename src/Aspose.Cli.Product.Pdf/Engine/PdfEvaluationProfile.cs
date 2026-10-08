using Aspose.Cli.Sdk.Licensing;
using Aspose.Pdf;
using Aspose.Pdf.Text;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>
/// Recognizes the evaluation notice an Aspose product printed on the pages of a PDF it saved
/// without a license (<see cref="PdfEvaluation.Notice"/>), whichever product it was, licensed
/// or not. Every evaluation save stamps every page, so only the first
/// <see cref="PdfEvaluation.VisiblePages"/> pages are read, of the document or of the pages a
/// rendering shows: reading the text of every page of a
/// long document would cost seconds, and evaluation mode cannot read the later ones anyway. A
/// notice only on a later page, as in a PDF merged from a clean and a marked one, is not found;
/// review reports every page. In evaluation mode a document with more pages than it can read
/// counts as cut short.
/// </summary>
internal sealed class PdfEvaluationProfile : IEvaluationProfile<Document>
{
    public EvaluationMarks Inspect(Document document)
    {
        int pages = document.Pages.Count;
        return Notices(document, Enumerable.Range(1, Math.Min(pages, PdfEvaluation.VisiblePages)))
            with { IsTruncated = pages > PdfEvaluation.VisiblePages && LaterPagesHidden(document) };
    }

    /// <summary>
    /// The marks of an output: the notices on its pages. Evaluation mode limits what the engine
    /// reads, not what it saves, so an output is never cut short by it.
    /// </summary>
    public EvaluationMarks Inspect(Document document, string format) => Inspect(document) with { IsTruncated = false };

    /// <summary>
    /// The marks of a rendering of some pages, such as page images or page text: the notices on
    /// the first <see cref="PdfEvaluation.VisiblePages"/> of the pages it shows.
    /// </summary>
    public EvaluationMarks Inspect(Document document, string format, IReadOnlyCollection<int> pages) =>
        Notices(document, pages.Order().Take(PdfEvaluation.VisiblePages));

    private static EvaluationMarks Notices(Document document, IEnumerable<int> pages)
    {
        var products = new SortedSet<string>(StringComparer.Ordinal);
        foreach (int number in pages)
        {
            var absorber = new TextAbsorber();
            document.Pages[number].Accept(absorber);
            foreach (System.Text.RegularExpressions.Match match in PdfEvaluation.Notice.Matches(absorber.Text))
            {
                products.Add(match.Groups["product"].Value);
            }
        }

        return new EvaluationMarks([.. products.Select(static product => $"the evaluation notice '... Created with {product} ...' printed on its pages")]);
    }

    /// <summary>Whether evaluation mode refuses to read the page after the ones it shows.</summary>
    private static bool LaterPagesHidden(Document document)
    {
        try
        {
            _ = document.Pages[PdfEvaluation.VisiblePages + 1];
            return false;
        }
        catch (Exception exception) when (PdfEvaluation.IsCollectionLimit(exception))
        {
            return true;
        }
    }
}
