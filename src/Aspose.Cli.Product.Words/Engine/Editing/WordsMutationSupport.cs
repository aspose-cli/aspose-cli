using System.Data;
using System.Drawing;
using Aspose.Cli.Sdk.Errors;
using Aspose.Words;
using Aspose.Words.Tables;

namespace Aspose.Cli.Product.Words.Engine.Editing;

/// <summary>Provides stateless SDK adaptation helpers shared by Words operation families.</summary>
internal static class WordsMutationSupport
{
    internal static void ApplyPageSetup(Section section, WordsPageSetupInput setup)
    {
        // The orientation first: the SDK swaps the page's width and height when it changes it.
        if (setup.Orientation is not null)
        {
            section.PageSetup.Orientation = setup.Orientation == WordsPageOrientations.Landscape ? Orientation.Landscape : Orientation.Portrait;
        }

        // The orientation decides which side is long: a landscape page is wider than it is high,
        // so the pageSize and orientation a read reports write back the same page.
        if (setup.PageSize is { } size)
        {
            (double width, double height) = size.Dimensions();
            (double shorter, double longer) = (Math.Min(width, height), Math.Max(width, height));
            bool landscape = section.PageSetup.Orientation == Orientation.Landscape;
            section.PageSetup.PageWidth = landscape ? longer : shorter;
            section.PageSetup.PageHeight = landscape ? shorter : longer;
        }

        if (setup.Margins is not null)
        {
            section.PageSetup.TopMargin = setup.Margins.Top ?? section.PageSetup.TopMargin;
            section.PageSetup.RightMargin = setup.Margins.Right ?? section.PageSetup.RightMargin;
            section.PageSetup.BottomMargin = setup.Margins.Bottom ?? section.PageSetup.BottomMargin;
            section.PageSetup.LeftMargin = setup.Margins.Left ?? section.PageSetup.LeftMargin;
        }

        if (setup.ColumnCount is int columns)
        {
            section.PageSetup.TextColumns.SetCount(columns);
        }
    }

    internal static void ApplyParagraphStyle(Document document, Paragraph paragraph, string styleName)
    {
        paragraph.ParagraphFormat.Style = GetStyle(document, styleName);
    }

    internal static Paragraph InsertBuilderParagraph(Document document, Node anchor, string position)
    {
        var paragraph = new Paragraph(document);
        Node cursor = anchor;
        InsertRelative(anchor, ref cursor, paragraph, position);
        return paragraph;
    }

    /// <summary>
    /// Places a block before the anchor, or after the cursor, which then moves to it. A table
    /// placed beside a table is parted from it (<see cref="Part"/>): from the block after it only
    /// when it is the <paramref name="last"/> block an operation places, as the next one lands
    /// right after it.
    /// </summary>
    internal static void InsertRelative(Node anchor, ref Node cursor, Node inserted, string position, bool last = true)
    {
        if (position == "before")
        {
            anchor.ParentNode!.InsertBefore(inserted, anchor);
        }
        else
        {
            cursor.ParentNode!.InsertAfter(inserted, cursor);
            cursor = inserted;
        }

        Part(inserted.PreviousSibling, inserted);
        if (last)
        {
            Part(inserted, inserted.NextSibling);
        }
    }

    // Two tables with nothing between them read as one table when the document is opened again,
    // so an empty paragraph parts them, as the engine parts the tables of Markdown it loads.
    private static void Part(Node? before, Node? after)
    {
        if (before is Table && after is Table)
        {
            before.ParentNode!.InsertAfter(new Paragraph(before.Document), before);
        }
    }

    internal static IEnumerable<Paragraph> Paragraphs(Node node) =>
        node is Paragraph paragraph ? [paragraph] : Descendants<Paragraph>(node);

    internal static IEnumerable<T> Descendants<T>(Node node)
        where T : Node =>
        node is CompositeNode composite
            ? composite.GetChildNodes(NodeType.Any, true).Cast<Node>().OfType<T>()
            : [];

    internal static HeaderFooterType HeaderFooterTypeOf(string kind, bool isHeader) => (kind, isHeader) switch
    {
        ("primary", true) => HeaderFooterType.HeaderPrimary,
        ("first", true) => HeaderFooterType.HeaderFirst,
        ("even", true) => HeaderFooterType.HeaderEven,
        ("primary", false) => HeaderFooterType.FooterPrimary,
        ("first", false) => HeaderFooterType.FooterFirst,
        ("even", false) => HeaderFooterType.FooterEven,
        _ => throw Invalid($"unknown header/footer kind '{kind}'"),
    };

    internal static ParagraphAlignment AlignmentOf(string alignment) => alignment switch
    {
        "left" => ParagraphAlignment.Left,
        "right" => ParagraphAlignment.Right,
        "center" => ParagraphAlignment.Center,
        _ => throw Invalid($"unknown alignment '{alignment}'"),
    };

    /// <summary>A #RRGGBB color, as the operation schema accepts it.</summary>
    internal static Color ParseColor(string value) => ColorTranslator.FromHtml(value);

    /// <summary>A style of the document by name; a missing name is STYLE_NOT_FOUND listing the document's styles.</summary>
    internal static Style GetStyle(Document document, string name) =>
        document.Styles[name] ?? throw CliErrors.NotFound(
            ErrorCodes.StyleNotFound, "style", name, document.Styles.Select(static style => style.Name).ToArray());

    /// <summary>
    /// A table style by name: one the document has, or a built-in one of Word's, which the engine
    /// adds to the document when it is named. A name that is missing or belongs to a style of
    /// another type is STYLE_NOT_FOUND listing the document's table styles.
    /// </summary>
    internal static Style GetTableStyle(Document document, string name) =>
        document.Styles[name] is { Type: StyleType.Table } style
            ? style
            : throw CliErrors.NotFound(
                ErrorCodes.StyleNotFound,
                "table style",
                name,
                document.Styles.Where(static candidate => candidate.Type == StyleType.Table).Select(static candidate => candidate.Name).ToArray(),
                hint: "Word's built-in table styles, such as 'Table Grid' and 'Grid Table 4', also work by name.");

    internal static OperationInvalidException Invalid(string reason, string? hint = null) => new(reason, hint);
}

