using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Words;
using Aspose.Words.Drawing;
using Aspose.Words.Layout;
using Aspose.Words.Tables;

namespace Aspose.Cli.Product.Words.Engine;

/// <summary>Projects conservative review facts from the real fixed-page layout model.</summary>
internal static class WordsReviewLayoutReader
{
    private const double PageBoundaryTolerance = 0.5;

    /// <summary>The layout facts of the first <paramref name="maxPages"/> pages of a document.</summary>
    internal static WordsReviewLayout Read(
        WordsSession session,
        string filePath,
        Secret? password,
        int maxPages)
    {
        _ = session.Outputs.License;
        using LoadedDocument loaded = session.Loader.Open(filePath, password);
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
        bool evaluation = loaded.Evaluation;
        CollectLayoutEntities(document, pages, evaluation);
        CollectNodeFacts(document, collector, pages, evaluation);
        IReadOnlyList<WordsReviewHeadingLayout> headings = OrphanedHeadings(
            document,
            evaluation,
            collector,
            pageCount);
        collector.Document = null;
        // Without a license, opening the document adds these marks itself.
        int evaluationMarks = evaluation
            ? 0
            : WordsEvaluation.MarkCount(document);
        return new WordsReviewLayout(
            pages.Select(static page => page.ToContract()).ToArray(),
            headings,
            evaluationMarks,
            !evaluation && WordsEvaluation.IsTruncated(document));
    }

    private static void CollectLayoutEntities(
        Document document,
        IReadOnlyList<MutablePage> pages,
        bool evaluation)
    {
        if (pages.Count == 0)
        {
            return;
        }
        var enumerator = new LayoutEnumerator(document);
        enumerator.Reset();
        do
        {
            Visit(enumerator, pages, excludedStory: false, evaluation);
        }
        while (enumerator.MoveNext());
    }

    private static void Visit(
        LayoutEnumerator enumerator,
        IReadOnlyList<MutablePage> pages,
        bool excludedStory,
        bool evaluation)
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
                && !(evaluation && WordsEvaluation.IsLayoutMark(enumerator.Text)))
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
            Visit(enumerator, pages, excluded, evaluation);
        }
        while (enumerator.MoveNext());
        _ = enumerator.MoveParent();
    }

    private static void CollectNodeFacts(
        Document document,
        LayoutCollector collector,
        IReadOnlyList<MutablePage> pages,
        bool evaluation)
    {
        CollectFontFacts(document, collector, pages, evaluation);
        CollectPageBreakFacts(document, collector, pages);
        CollectShapeFacts(document, collector, pages, evaluation);
    }

    private static void CollectFontFacts(
        Document document,
        LayoutCollector collector,
        IReadOnlyList<MutablePage> pages,
        bool evaluation)
    {
        foreach (Run run in document.GetChildNodes(NodeType.Run, true).Cast<Run>())
        {
            if (ShouldSkipRun(run, evaluation))
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

    private static bool ShouldSkipRun(Run run, bool evaluation) =>
        run.GetAncestor(NodeType.HeaderFooter) is not null
        || string.IsNullOrWhiteSpace(run.Text)
        || evaluation
            && run.GetAncestor(NodeType.Paragraph) is Paragraph paragraph
            && WordsEvaluation.IsLayoutMark(paragraph.GetText());

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
        IReadOnlyList<MutablePage> pages,
        bool evaluation)
    {
        var nodeEnumerator = new LayoutEnumerator(document);
        foreach (Shape shape in document.GetChildNodes(NodeType.Shape, true).Cast<Shape>())
        {
            if (ShouldSkipShape(shape, evaluation))
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

    private static bool ShouldSkipShape(Shape shape, bool evaluation)
    {
        if (shape.GetAncestor(NodeType.HeaderFooter) is not null)
        {
            return true;
        }
        string shapeText = shape.GetText();
        return evaluation && !string.IsNullOrEmpty(shapeText) && WordsEvaluation.IsLayoutMark(shapeText);
    }

    private static bool IsIncludedPage(int page, int pageCount) => page >= 1 && page <= pageCount;

    private static bool IsOutsidePage(System.Drawing.RectangleF rectangle, MutablePage page) =>
        rectangle.Left < -PageBoundaryTolerance
        || rectangle.Top < -PageBoundaryTolerance
        || rectangle.Right > page.WidthPoints + PageBoundaryTolerance
        || rectangle.Bottom > page.HeightPoints + PageBoundaryTolerance;

    private static IReadOnlyList<WordsReviewHeadingLayout> OrphanedHeadings(
        Document document,
        bool evaluation,
        LayoutCollector collector,
        int maxPage)
    {
        var index = new DocumentBlockIndex(document, evaluation);
        var findings = new List<WordsReviewHeadingLayout>();
        IReadOnlyList<BlockEntry> entries = index.Entries;

        // The nearest block with visible content before and after each position, in two passes.
        var preceding = new BlockEntry?[entries.Count];
        var following = new BlockEntry?[entries.Count];
        for (int position = 1; position < entries.Count; position++)
        {
            BlockEntry previous = entries[position - 1];
            preceding[position] = HasVisibleBodyContent(previous) ? previous : preceding[position - 1];
        }

        for (int position = entries.Count - 2; position >= 0; position--)
        {
            BlockEntry next = entries[position + 1];
            following[position] = HasVisibleBodyContent(next) ? next : following[position + 1];
        }

        for (int position = 0; position < entries.Count - 1; position++)
        {
            BlockEntry entry = index.Entries[position];
            if (entry.Node is not Paragraph heading
                || InfoProjection.HeadingLevel(heading) is not int level
                || level < 2
                || string.IsNullOrWhiteSpace(WordsText.Of(heading)))
            {
                continue;
            }
            if (following[position] is not BlockEntry next || preceding[position] is not BlockEntry previous)
            {
                continue;
            }
            int page = collector.GetEndPageIndex(heading);
            int followingPage = collector.GetStartPageIndex(next.Node);
            int precedingPage = collector.GetEndPageIndex(previous.Node);
            bool followingStartsExplicitPage = next.Node is Paragraph paragraph
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
                    WordsText.Of(heading)));
            }
        }
        return findings;
    }

    private static bool HasVisibleBodyContent(BlockEntry entry) => entry.Node switch
    {
        Paragraph paragraph => !string.IsNullOrWhiteSpace(WordsText.Of(paragraph)),
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
