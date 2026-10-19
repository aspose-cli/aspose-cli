using System.Data;
using System.Drawing;
using Aspose.Cli.Sdk.Errors;
using Aspose.Words;

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

    internal static void InsertRelative(Node anchor, ref Node cursor, Node inserted, string position)
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

    internal static OperationInvalidException Invalid(string reason) => new(reason);
}

