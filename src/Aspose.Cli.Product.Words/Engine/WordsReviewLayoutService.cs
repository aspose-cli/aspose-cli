using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Product.Words.Ports;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Words;
using Aspose.Words.Drawing;
using Aspose.Words.Layout;
using Aspose.Words.Tables;

namespace Aspose.Cli.Product.Words.Engine;

/// <summary>Projects conservative review facts from the real fixed-page layout model.</summary>
internal sealed class WordsReviewLayoutService
{
    private const double PageBoundaryTolerance = 0.5;
    private readonly ILicenseGate _licenseGate;
    private readonly WordsDocumentLoader _loader;

    internal WordsReviewLayoutService(
        ILicenseGate licenseGate,
        WordsDocumentLoader loader)
    {
        _licenseGate = licenseGate ?? throw new ArgumentNullException(nameof(licenseGate));
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
    }

    internal WordsReviewLayout Inspect(
        string filePath,
        string? password,
        int maxPages)
    {
        _ = _licenseGate.EnsureApplied();
        using LoadedDocument loaded = _loader.Open(filePath, password);
        Document document = loaded.Document;
        document.UpdatePageLayout();
        int pageCount = Math.Min(document.PageCount, maxPages);
        var pages = Enumerable.Range(1, pageCount)
            .Select(page =>
            {
                Aspose.Words.Rendering.PageInfo info = document.GetPageInfo(page - 1);
                return new MutablePage(page, info.WidthInPoints, info.HeightInPoints);
            })
            .ToArray();
        var collector = new LayoutCollector(document);
        CollectLayoutEntities(document, pages);
        CollectNodeFacts(document, collector, pages);
        IReadOnlyList<WordsReviewHeadingLayout> headings = OrphanedHeadings(
            document,
            collector,
            pageCount);
        collector.Document = null;
        return new WordsReviewLayout(
            pages.Select(static page => page.ToContract()).ToArray(),
            headings);
    }

    private static void CollectLayoutEntities(
        Document document,
        IReadOnlyList<MutablePage> pages)
    {
        if (pages.Count == 0)
        {
            return;
        }
        var enumerator = new LayoutEnumerator(document);
        enumerator.Reset();
        do
        {
            Visit(enumerator, pages, excludedStory: false);
        }
        while (enumerator.MoveNext());
    }

    private static void Visit(
        LayoutEnumerator enumerator,
        IReadOnlyList<MutablePage> pages,
        bool excludedStory)
    {
        LayoutEntityType type = enumerator.Type;
        bool excluded = excludedStory
            || type is LayoutEntityType.HeaderFooter or LayoutEntityType.Comment;
        int page = enumerator.PageIndex;
        if (!excluded && page >= 1 && page <= pages.Count)
        {
            if (type == LayoutEntityType.Span
                && !string.IsNullOrWhiteSpace(enumerator.Text)
                && !string.Equals(enumerator.Kind, "PAGE", StringComparison.Ordinal)
                && !IsEvaluationMark(enumerator.Text))
            {
                int visibleCharacters = enumerator.Text.Count(static character =>
                    !char.IsControl(character) && !char.IsWhiteSpace(character));
                if (visibleCharacters > 0)
                {
                    pages[page - 1].AddContent(
                        enumerator.Rectangle,
                        visibleCharacters);
                }
            }
            else if (type == LayoutEntityType.Row)
            {
                pages[page - 1].AddContent(enumerator.Rectangle, 0);
            }
        }

        if (!enumerator.MoveFirstChild())
        {
            return;
        }
        do
        {
            Visit(enumerator, pages, excluded);
        }
        while (enumerator.MoveNext());
        _ = enumerator.MoveParent();
    }

    private static bool IsEvaluationMark(string text) =>
        text.Contains("Evaluation Only", StringComparison.OrdinalIgnoreCase)
        || text.Contains("Created with Aspose.Words", StringComparison.OrdinalIgnoreCase)
        || text.Contains("Aspose.Words Evaluation", StringComparison.OrdinalIgnoreCase);

    private static void CollectNodeFacts(
        Document document,
        LayoutCollector collector,
        IReadOnlyList<MutablePage> pages)
    {
        CollectFontFacts(document, collector, pages);
        CollectPageBreakFacts(document, collector, pages);
        CollectShapeFacts(document, collector, pages);
    }

    private static void CollectFontFacts(
        Document document,
        LayoutCollector collector,
        IReadOnlyList<MutablePage> pages)
    {
        foreach (Run run in document.GetChildNodes(NodeType.Run, true).Cast<Run>())
        {
            if (ShouldSkipRun(run))
            {
                continue;
            }
            int page = collector.GetStartPageIndex(run);
            if (IsIncludedPage(page, pages.Count) && run.Font.Size > 0)
            {
                pages[page - 1].AddFont(run.Font.Size);
            }
        }
    }

    private static bool ShouldSkipRun(Run run) =>
        run.GetAncestor(NodeType.HeaderFooter) is not null
        || string.IsNullOrWhiteSpace(run.Text)
        || run.GetAncestor(NodeType.Paragraph) is Paragraph paragraph
            && IsEvaluationMark(InfoProjection.Clean(paragraph.GetText()));

    private static void CollectPageBreakFacts(
        Document document,
        LayoutCollector collector,
        IReadOnlyList<MutablePage> pages)
    {
        foreach (Paragraph paragraph in document.GetChildNodes(NodeType.Paragraph, true).Cast<Paragraph>())
        {
            if (paragraph.GetAncestor(NodeType.HeaderFooter) is not null)
            {
                continue;
            }
            int page = collector.GetStartPageIndex(paragraph);
            if (IsIncludedPage(page, pages.Count))
            {
                pages[page - 1].ExplicitPageBreaks += CountExplicitPageBreaks(paragraph);
            }
        }
    }

    private static int CountExplicitPageBreaks(Paragraph paragraph) =>
        (paragraph.ParagraphFormat.PageBreakBefore ? 1 : 0)
        + paragraph.Runs.Cast<Run>().Sum(static run =>
            run.Text.Count(static character => character == ControlChar.PageBreakChar));

    private static void CollectShapeFacts(
        Document document,
        LayoutCollector collector,
        IReadOnlyList<MutablePage> pages)
    {
        var nodeEnumerator = new LayoutEnumerator(document);
        foreach (Shape shape in document.GetChildNodes(NodeType.Shape, true).Cast<Shape>())
        {
            if (ShouldSkipShape(shape))
            {
                continue;
            }
            object? entity = collector.GetEntity(shape);
            if (entity is null)
            {
                continue;
            }
            nodeEnumerator.Current = entity;
            int page = nodeEnumerator.PageIndex;
            if (!IsIncludedPage(page, pages.Count))
            {
                continue;
            }
            System.Drawing.RectangleF rectangle = nodeEnumerator.Rectangle;
            MutablePage facts = pages[page - 1];
            facts.AddObject(rectangle);
            if (IsOutsidePage(rectangle, facts))
            {
                facts.AddOutsideObject(shape.Name);
            }
        }
    }

    private static bool ShouldSkipShape(Shape shape)
    {
        if (shape.GetAncestor(NodeType.HeaderFooter) is not null)
        {
            return true;
        }
        string shapeText = InfoProjection.Clean(shape.GetText());
        return !string.IsNullOrEmpty(shapeText) && IsEvaluationMark(shapeText);
    }

    private static bool IsIncludedPage(int page, int pageCount) => page >= 1 && page <= pageCount;

    private static bool IsOutsidePage(System.Drawing.RectangleF rectangle, MutablePage page) =>
        rectangle.Left < -PageBoundaryTolerance
        || rectangle.Top < -PageBoundaryTolerance
        || rectangle.Right > page.WidthPoints + PageBoundaryTolerance
        || rectangle.Bottom > page.HeightPoints + PageBoundaryTolerance;

    private static IReadOnlyList<WordsReviewHeadingLayout> OrphanedHeadings(
        Document document,
        LayoutCollector collector,
        int maxPage)
    {
        var index = new DocumentBlockIndex(document);
        var findings = new List<WordsReviewHeadingLayout>();
        for (int position = 0; position < index.Entries.Count - 1; position++)
        {
            BlockEntry entry = index.Entries[position];
            if (entry.Node is not Paragraph heading
                || InfoProjection.HeadingLevel(heading) is not int level
                || level < 2
                || string.IsNullOrWhiteSpace(InfoProjection.Clean(heading.GetText())))
            {
                continue;
            }
            BlockEntry? following = index.Entries.Skip(position + 1).FirstOrDefault(HasVisibleBodyContent);
            BlockEntry? preceding = index.Entries.Take(position).LastOrDefault(HasVisibleBodyContent);
            if (following is null || preceding is null)
            {
                continue;
            }
            int page = collector.GetEndPageIndex(heading);
            int followingPage = collector.GetStartPageIndex(following.Node);
            int precedingPage = collector.GetEndPageIndex(preceding.Node);
            bool followingStartsExplicitPage = following.Node is Paragraph paragraph
                && paragraph.ParagraphFormat.PageBreakBefore;
            if (page >= 1
                && page <= maxPage
                && precedingPage == page
                && followingPage > page
                && !followingStartsExplicitPage)
            {
                findings.Add(new WordsReviewHeadingLayout(
                    entry.Index,
                    page,
                    level,
                    InfoProjection.Clean(heading.GetText())));
            }
        }
        return findings;
    }

    private static bool HasVisibleBodyContent(BlockEntry entry) => entry.Node switch
    {
        Paragraph paragraph => !string.IsNullOrWhiteSpace(InfoProjection.Clean(paragraph.GetText())),
        Table table => table.Rows.Count > 0,
        _ => false,
    };

    private sealed class MutablePage(int page, double width, double height)
    {
        private double? _left;
        private double? _top;
        private double? _right;
        private double? _bottom;
        private double? _minimumFont;
        private double? _maximumFont;
        private readonly List<string> _outsideNames = [];

        public int Page { get; } = page;
        public double WidthPoints { get; } = width;
        public double HeightPoints { get; } = height;
        public int VisibleCharacters { get; private set; }
        public int VisualObjects { get; private set; }
        public int ExplicitPageBreaks { get; set; }
        public int OutsideObjects { get; private set; }

        public void AddContent(System.Drawing.RectangleF rectangle, int characters)
        {
            VisibleCharacters += characters;
            Include(rectangle);
        }

        public void AddObject(System.Drawing.RectangleF rectangle)
        {
            VisualObjects++;
            Include(rectangle);
        }

        public void AddOutsideObject(string? name)
        {
            OutsideObjects++;
            if (_outsideNames.Count < 10)
            {
                _outsideNames.Add(string.IsNullOrWhiteSpace(name) ? "unnamed shape" : name);
            }
        }

        public void AddFont(double size)
        {
            _minimumFont = Math.Min(_minimumFont ?? size, size);
            _maximumFont = Math.Max(_maximumFont ?? size, size);
        }

        public WordsReviewPageLayout ToContract() => new()
        {
            Page = Page,
            WidthPoints = WidthPoints,
            HeightPoints = HeightPoints,
            VisibleCharacters = VisibleCharacters,
            VisualObjects = VisualObjects,
            ExplicitPageBreaks = ExplicitPageBreaks,
            OutsideObjects = OutsideObjects,
            OutsideObjectNames = _outsideNames.ToArray(),
            ContentLeft = _left,
            ContentTop = _top,
            ContentRight = _right,
            ContentBottom = _bottom,
            MinimumFontSize = _minimumFont,
            MaximumFontSize = _maximumFont,
        };

        private void Include(System.Drawing.RectangleF rectangle)
        {
            if (rectangle.Width <= 0 || rectangle.Height <= 0)
            {
                return;
            }
            _left = Math.Min(_left ?? rectangle.Left, rectangle.Left);
            _top = Math.Min(_top ?? rectangle.Top, rectangle.Top);
            _right = Math.Max(_right ?? rectangle.Right, rectangle.Right);
            _bottom = Math.Max(_bottom ?? rectangle.Bottom, rectangle.Bottom);
        }
    }
}
