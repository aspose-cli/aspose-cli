using Aspose.Words;
using Aspose.Words.Drawing;
using Aspose.Words.Fonts;
using Aspose.Words.Saving;

namespace Aspose.Cli.Product.Words.Engine.Mapping;

internal static class WordsSavePipeline
{
    /// <summary>
    /// Removes the macros of a document about to be saved in a format that cannot keep them.
    /// The SDK refuses to write a macro-free Office Open XML format while a document has a VBA
    /// project, and asks for <see cref="Document.RemoveMacros"/>; every other such format would
    /// drop them anyway. Callers disclose the loss as MACROS_DROPPED.
    /// </summary>
    public static void RemoveMacrosUnlessKept(Document document, string formatId)
    {
        if (document.HasMacros && !WordsFormats.KeepsMacros(formatId))
        {
            document.RemoveMacros();
        }
    }

    /// <summary>
    /// Takes out of a document about to be saved in a format that leaves out its drawings each
    /// one the save would write as a picture: every drawing outside a group and outside the
    /// headers and footers such a format leaves out, but a horizontal rule, which the save writes
    /// as a line. The save writes a text box, WordArt and SmartArt as a picture alone; the
    /// paragraphs and tables in a drawing move into the body instead.
    /// </summary>
    public static WordsDrawingsLeftOut LeaveOutDrawings(Document document, string formatId)
    {
        var left = new WordsDrawingsLeftOut();
        Array.ForEach<ShapeBase>(WordsEngineFormats.Has(formatId, static entry => entry.LeavesOutDrawings)
            ? [.. document.GetChildNodes(NodeType.Any, true).OfType<ShapeBase>()
                .Where(static drawing => drawing.IsTopLevel && !drawing.IsHorizontalRule && drawing.GetAncestor(NodeType.HeaderFooter) is null)]
            : [], left.Take);
        return left;
    }

    /// <summary>
    /// Whether saving a document in a format embeds fonts its input did not carry: the document
    /// asks every save to embed its TrueType fonts, system fonts included; a font its text uses
    /// is not embedded yet; and the format is one the SDK embeds fonts in (DOC, DOCX and RTF and
    /// their variants). Aspose.PDF writes such a docx (PDF-DOCX-EMBED-FONTS), at most with the
    /// fonts the PDF itself embedded, and the next save embeds the rest. With system fonts left
    /// out, a save may embed nothing, so only both flags together are taken as a promise of
    /// growth.
    /// </summary>
    public static bool EmbedsFontsTheInputLacks(Document document, string formatId)
    {
        FontInfoCollection fonts = document.FontInfos;
        return fonts.EmbedTrueTypeFonts && fonts.EmbedSystemFonts
            && WordsEngineFormats.Save(formatId) is SaveFormat.Doc or SaveFormat.Dot or SaveFormat.Docx or SaveFormat.Docm
                or SaveFormat.Dotx or SaveFormat.Dotm or SaveFormat.FlatOpc or SaveFormat.Rtf
            && WordsFonts.Used(document).Any(name => fonts[name] is not { } font || !Embeds(font));
    }

    private static bool Embeds(FontInfo font) =>
        EmbeddedFontStyles.Any(style =>
            font.GetEmbeddedFont(EmbeddedFontFormat.OpenType, style) is not null
            || font.GetEmbeddedFont(EmbeddedFontFormat.EmbeddedOpenType, style) is not null);

    private static readonly EmbeddedFontStyle[] EmbeddedFontStyles =
        [EmbeddedFontStyle.Regular, EmbeddedFontStyle.Bold, EmbeddedFontStyle.Italic, EmbeddedFontStyle.BoldItalic];

    public static SaveOptions Options(string formatId, Secret? password = null, IReadOnlyList<int>? pages = null, int? dpi = null)
    {
        SaveFormat format = WordsEngineFormats.Save(formatId);
        SaveOptions options = SaveOptions.CreateSaveOptions(format);
        options.UpdateLastSavedTimeProperty = false;
        switch (options)
        {
            case HtmlSaveOptions html when format == SaveFormat.Html:
                html.ExportImagesAsBase64 = true;
                html.ExportFontsAsBase64 = true;
                html.CssStyleSheetType = CssStyleSheetType.Embedded;
                break;
            case HtmlFixedSaveOptions html:
                html.ExportEmbeddedImages = true;
                html.ExportEmbeddedFonts = true;
                html.ExportEmbeddedCss = true;
                html.ExportEmbeddedSvg = true;
                break;
            case SvgSaveOptions svg:
                svg.ExportEmbeddedImages = true;
                break;
            case MarkdownSaveOptions markdown:
                markdown.ExportImagesAsBase64 = true;
                markdown.OfficeMathExportMode = MarkdownOfficeMathExportMode.Text;
                break;
            // Plain text is the body text: a table keeps its rows and columns.
            case TxtSaveOptions text:
                text.PreserveTableLayout = true;
                break;
            // RTF stores each image once, in its own format such as PNG. The copy the SDK adds
            // for old readers is an uncompressed metafile, which makes a document with a few
            // photos many times larger; Word, LibreOffice and current readers read the first.
            case RtfSaveOptions rtf:
                rtf.ExportImagesForOldReaders = false;
                break;
        }

        // The page headers, footers and page numbers stay out of a format without pages.
        if (WordsEngineFormats.Has(formatId, static entry => entry.LeavesOutHeadersFooters))
        {
            switch (options)
            {
                case TxtSaveOptionsBase text: text.ExportHeadersFootersMode = TxtExportHeadersFootersMode.None; break;
                case HtmlSaveOptions html: html.ExportHeadersFootersMode = ExportHeadersFootersMode.None; break;
                default: throw new InvalidOperationException($"The '{formatId}' format leaves out headers and footers, but its save options cannot.");
            }
        }

        if (pages is not null)
        {
            if (options is not FixedPageSaveOptions fixedOptions)
            {
                throw new InvalidOperationException(
                    $"--pages reached the engine for '{formatId}', which is not fixed-page and ConvertCommand refuses.");
            }

            fixedOptions.PageSet = new PageSet(pages.Select(static page => page - 1).ToArray());
        }

        if (dpi is not null && options is ImageSaveOptions image)
        {
            image.Resolution = dpi.Value;
        }

        if (password is not null)
        {
            // The command line refuses --encrypt for a format that cannot carry a password, and
            // OutputProtection passes no input password on to one, so a password here is a defect.
            if (WordsFormats.Definitions.FirstOrDefault(format => format.Id == formatId) is not { Protectable: true })
            {
                throw new InvalidOperationException($"An output password reached the '{formatId}' format, which cannot carry one.");
            }

            string value = password.Reveal();
            switch (options)
            {
                case OoxmlSaveOptions ooxml: ooxml.Password = value; break;
                case DocSaveOptions doc: doc.Password = value; break;
                case OdtSaveOptions odt: odt.Password = value; break;
                default: throw new InvalidOperationException($"The '{formatId}' format carries a password, but its save options take none.");
            }
        }

        return options;
    }
}

/// <summary>
/// What <see cref="WordsSavePipeline.LeaveOutDrawings"/> took out, counted as it took each out:
/// each shape by what it is, a group by the shapes in it, and whether any held text that stayed.
/// </summary>
internal sealed class WordsDrawingsLeftOut
{
    public int Pictures { get; private set; }

    public int Charts { get; private set; }

    public int Others { get; private set; }

    public bool KeptText { get; private set; }

    /// <summary>
    /// Counts a drawing, moves the paragraphs and tables of its shapes as they are, with their
    /// revisions, lists and comments, after the paragraph that holds it, and removes it. A drawing
    /// in a text box is one of its own, counted and moved when it is taken out.
    /// </summary>
    internal void Take(ShapeBase drawing)
    {
        KeptText |= WordsText.Of(drawing).Length > 0;
        Node after = drawing.ParentParagraph;
        IEnumerable<Shape> shapes = drawing is Shape own ? [own] : [.. drawing.GetChildNodes(NodeType.Shape, true).Cast<Shape>().Where(static shape => !shape.IsTopLevel)];
        foreach (Shape shape in shapes)
        {
            (Pictures, Charts, Others) = shape.HasImage ? (Pictures + 1, Charts, Others) : shape.HasChart ? (Pictures, Charts + 1, Others) : (Pictures, Charts, Others + 1);
            foreach (Node block in shape.GetChildNodes(NodeType.Any, false).ToArray())
            {
                after = after.ParentNode.InsertAfter(block, after);
            }
        }

        drawing.Remove();
    }
}
