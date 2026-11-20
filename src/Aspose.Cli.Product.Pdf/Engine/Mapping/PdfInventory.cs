using Aspose.Cli.Sdk.Tables;
using Aspose.Pdf;

namespace Aspose.Cli.Product.Pdf.Engine.Mapping;

/// <summary>
/// The inventory of a PDF: what the document holds, counted through the engine's own collections.
/// The pages are Pages.Count; everything else is counted over what the engine's enumerators give,
/// which in evaluation mode are the first few of a collection only. Nothing here reads what changes
/// the document in memory: the structure tree, a search of the text, or a page's images, whose
/// collection the engine creates in the page's resources when it is read.
/// </summary>
internal static class PdfInventory
{
    internal static IReadOnlyList<InventoryKind> Kinds { get; } =
    [
        new("pages", "Document.Pages.Count.", PdfTable.PageKind),
        new("annotations", "Page.Annotations.Count over the pages, the widgets of form fields included.", PdfTable.AnnotationKind),
        new("fonts", "Page.Resources.Fonts.Count over the pages; a page that uses no font has none.", PdfTable.FontKind),
        new("layers", "Page.Layers over the pages.", PdfTable.LayerKind),
        new("bookmarks", "The outline items at every level.", PdfTable.BookmarkKind),
        new("fields", "The terminal fields of Document.Form, a field beneath an untyped node counted once.", PdfTable.FieldKind),
        new("attachments", "The files Document.EmbeddedFiles enumerates.", PdfTable.AttachmentKind),
        new("pageLabels", "Document.PageLabels.GetPages().Length: the labels and where each starts.", PdfTable.PageLabelKind),
        new("info", "The entries of Document.Info whose key is no predefined one.", PdfTable.InfoKind),
        new("xmp", "Document.Metadata.Count.", PdfTable.XmpKind),
        new("destinations", "Document.Destinations.Count.", null),
        new("javaScript", "Document.JavaScript.Keys.Count: the document-level scripts.", null),
    ];

    internal static IReadOnlyDictionary<string, long> Count(Document document)
    {
        // The pages the enumerator gives: in evaluation mode the first ones only, while Pages.Count is the document's.
        List<Page> pages = [.. PdfTable.Each(document.Pages)];
        return new Dictionary<string, long>(StringComparer.Ordinal)
        {
            ["pages"] = document.Pages.Count,
            ["annotations"] = pages.Sum(static page => (long)page.Annotations.Count),
            ["fonts"] = pages.Sum(static page => (long)(page.Resources.Fonts?.Count ?? 0)),
            ["layers"] = pages.Sum(static page => (long)(page.Layers?.Count ?? 0)),
            ["bookmarks"] = Bookmarks(PdfTable.Each(document.Outlines)),
            ["fields"] = PdfTable.Fields(document).LongCount(),
            ["attachments"] = PdfTable.Each(document.EmbeddedFiles).LongCount(),
            ["pageLabels"] = document.PageLabels.GetPages().Length,
            ["info"] = document.Info.Keys.LongCount(key => !DocumentInfo.IsPredefinedKey(key)),
            ["xmp"] = document.Metadata.Count,
            ["destinations"] = document.Destinations.Count,
            ["javaScript"] = document.JavaScript.Keys.Count,
        };
    }

    private static long Bookmarks(IEnumerable<OutlineItemCollection> items) =>
        items.Sum(static item => 1 + Bookmarks(PdfTable.Each(item)));
}
