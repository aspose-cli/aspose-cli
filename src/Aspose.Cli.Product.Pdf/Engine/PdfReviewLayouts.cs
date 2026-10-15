using Aspose.Cli.Sdk.Licensing;
using Aspose.Pdf;
using Aspose.Pdf.Operators;
using Aspose.Pdf.Text;
using Aspose.Pdf.Vector;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>Projects the displayed page sizes and conservative text-boundary facts from the real PDF layout.</summary>
internal static class PdfReviewLayouts
{
    private const double PageBoundaryTolerance = 0.5;

    internal static PdfReviewLayout Inspect(
        PdfSession session,
        string filePath,
        Secret? password,
        int maxPages)
    {
        // Only an evaluation-mode review leaves evaluation notices out of its text checks.
        bool evaluation = session.Outputs.License == LicenseState.Evaluation;
        using LoadedPdf loaded = session.Loader.Open(filePath, password);
        int inspectedPages = Math.Min(loaded.Document.Pages.Count, maxPages);
        var pages = new List<PdfReviewPageLayout>(inspectedPages);
        for (int pageNumber = 1; pageNumber <= inspectedPages; pageNumber++)
        {
            pages.Add(InspectPage(loaded.Document.Pages[pageNumber], pageNumber, evaluation));
        }
        return new PdfReviewLayout(pages);
    }

    private static PdfReviewPageLayout InspectPage(Page page, int pageNumber, bool evaluation)
    {
        var absorber = new TextFragmentAbsorber();
        page.Accept(absorber);
        TextFragment[] fragments = [.. absorber.TextFragments.Where(static fragment => !string.IsNullOrWhiteSpace(fragment.Text))];
        Rectangle[] text = [.. fragments.Select(static fragment => fragment.Rectangle)];
        Rectangle displayed = page.GetPageRect(considerRotation: true);
        (string? product, bool[] notice) = EvaluationNotices(fragments);
        bool[] excluded = evaluation ? notice : new bool[notice.Length];
        return new PdfReviewPageLayout(
            pageNumber,
            displayed.Width,
            displayed.Height,
            // In evaluation mode the evaluation finding covers a notice an engine prints beyond
            // the page edge, as Aspose.Cells prints it across the evaluation warning sheet it adds.
            text.Where((_, index) => !excluded[index]).Count(fragment => IsOutsidePage(PdfEngineSupport.ToContractTextRect(page, fragment), displayed)),
            ImageCoverage(page),
            text.Length == 0 ? 0 : CoveredFragments(page),
            product,
            excluded.Count(static left => left));
    }

    /// <summary>
    /// The product whose evaluation notice the page's text fragments hold, and which fragments
    /// belong to a notice, read in the order they are drawn: a notice drawn over other text, as
    /// Aspose.Slides draws it over a slide title, interleaves with that text when the page is
    /// read by position.
    /// </summary>
    private static (string? Product, bool[] Notice) EvaluationNotices(IReadOnlyList<TextFragment> fragments)
    {
        int[] starts = new int[fragments.Count];
        var joined = new System.Text.StringBuilder();
        for (int index = 0; index < fragments.Count; index++)
        {
            if (index > 0)
            {
                joined.Append('\n');
            }

            starts[index] = joined.Length;
            joined.Append(fragments[index].Text);
        }

        bool[] notice = new bool[fragments.Count];
        string? product = null;
        foreach (System.Text.RegularExpressions.Match match in PdfEvaluation.Notice.Matches(joined.ToString()))
        {
            product ??= match.Groups["product"].Value;
            for (int index = 0; index < fragments.Count; index++)
            {
                notice[index] |= starts[index] < match.Index + match.Length
                    && starts[index] + fragments[index].Text.Length > match.Index;
            }
        }

        return (product, notice);
    }

    /// <summary>
    /// The text fragments whose centre lies under an opaque box drawn after them: a form XObject
    /// that only fills one rectangle, as a redaction leaves, or one rectangle the page fills
    /// itself. The text drawn before a box is the text of the page with its content cut off at
    /// the box, so a copy of the page is cut from the last box to the first.
    /// </summary>
    private static int CoveredFragments(Page page)
    {
        if (!page.Resources.Forms.Any(IsBox) && !FillsARectangle(page.Contents))
        {
            return 0;
        }

        using var copy = new Document();
        Page cut = copy.Pages.Add(page);
        (int Index, Rectangle Area)[] boxes;
        using (var graphics = new GraphicsAbsorber())
        {
            graphics.Visit(cut);
            boxes = [.. graphics.Elements
                .Where(static element => element.Parent is null && element switch
                {
                    XFormPlacement placement => IsBox(placement.XForm),
                    SubPath path => IsBox(path.Operators),
                    _ => false,
                })
                .Select(static element => (element.Operators[0].Index, element.Rectangle))
                .OrderByDescending(static box => box.Index)];
        }

        var covered = new HashSet<(string, double, double)>();
        foreach ((int index, Rectangle area) in boxes)
        {
            for (int op = cut.Contents.Count; op >= index; op--)
            {
                cut.Contents.Delete(op);
            }

            var before = new TextFragmentAbsorber();
            cut.Accept(before);
            foreach (TextFragment fragment in before.TextFragments)
            {
                if (!string.IsNullOrWhiteSpace(fragment.Text) && Contains(area, fragment.Rectangle))
                {
                    covered.Add((fragment.Text, fragment.Rectangle.LLX, fragment.Rectangle.LLY));
                }
            }
        }

        return covered.Count;
    }

    /// <summary>Whether the content fills a rectangle right after it draws one.</summary>
    private static bool FillsARectangle(OperatorCollection contents)
    {
        bool rectangle = false;
        foreach (Operator op in contents)
        {
            if (rectangle && IsFill(op))
            {
                return true;
            }

            rectangle = op is Re;
        }

        return false;
    }

    private static bool IsFill(Operator op) =>
        op is Fill or EOFill or FillStroke or EOFillStroke or ClosePathFillStroke or ClosePathEOFillStroke;

    private static bool IsBox(XForm form) => IsBox(form.Contents);

    private static bool IsBox(IEnumerable<Operator> operators)
    {
        int rectangles = 0;
        int fills = 0;
        foreach (Operator op in operators)
        {
            switch (op)
            {
                case Re:
                    rectangles++;
                    break;
                case var fill when IsFill(fill):
                    fills++;
                    break;
                case GSave or GRestore or SetColorOperator or SetLineWidth:
                    break;
                default:
                    return false;
            }
        }

        return rectangles == 1 && fills == 1;
    }

    private static bool Contains(Rectangle box, Rectangle text)
    {
        double x = (text.LLX + text.URX) / 2;
        double y = (text.LLY + text.URY) / 2;
        return x > box.LLX && x < box.URX && y > box.LLY && y < box.URY;
    }

    /// <summary>The share of the page its image placements cover, overlaps counted twice, at most 1.</summary>
    private static double ImageCoverage(Page page)
    {
        var absorber = new ImagePlacementAbsorber { IsReadOnlyMode = true };
        page.Accept(absorber);
        double area = absorber.ImagePlacements.Sum(static placement =>
            placement.Rectangle.Width * placement.Rectangle.Height);
        return Math.Min(1d, area / Math.Max(1d, page.Rect.Width * page.Rect.Height));
    }

    /// <summary>
    /// Whether text, measured from the top-left corner of the visible page as a search reports
    /// it on every rotation, extends beyond that page.
    /// </summary>
    private static bool IsOutsidePage(Contracts.PdfRect text, Rectangle displayed) =>
        text.X < -PageBoundaryTolerance
        || text.Y < -PageBoundaryTolerance
        || text.X + text.Width > displayed.Width + PageBoundaryTolerance
        || text.Y + text.Height > displayed.Height + PageBoundaryTolerance;
}
