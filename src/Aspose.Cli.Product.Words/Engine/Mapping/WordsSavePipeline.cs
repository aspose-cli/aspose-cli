using Aspose.Cli.Sdk.Errors;
using Aspose.Words;
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

    public static SaveOptions Options(string formatId, Secret? password = null, IReadOnlyList<int>? pages = null, int? dpi = null)
    {
        SaveFormat format = WordsFormatMapper.ToSaveFormat(formatId);
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
                break;
            // Plain text is the body text: a table keeps its rows and columns, and the page
            // headers, footers and page numbers stay out of it.
            case TxtSaveOptions text:
                text.PreserveTableLayout = true;
                text.ExportHeadersFootersMode = TxtExportHeadersFootersMode.None;
                break;
            // RTF stores each image once, in its own format such as PNG. The copy the SDK adds
            // for old readers is an uncompressed metafile, which makes a document with a few
            // photos many times larger; Word, LibreOffice and current readers read the first.
            case RtfSaveOptions rtf:
                rtf.ExportImagesForOldReaders = false;
                break;
        }

        if (pages is not null)
        {
            if (options is not FixedPageSaveOptions fixedOptions)
            {
                throw CliErrors.OptionInvalid("--pages", $"format '{formatId}' is not fixed-page", $"Use --pages only with {string.Join(", ", WordsFormats.FixedPageConvertIds)}.");
            }

            fixedOptions.PageSet = new PageSet(pages.Select(static page => page - 1).ToArray());
        }

        if (dpi is not null && options is ImageSaveOptions image)
        {
            image.Resolution = dpi.Value;
        }

        if (password?.Reveal() is { } value)
        {
            switch (options)
            {
                case OoxmlSaveOptions ooxml: ooxml.Password = value; break;
                case DocSaveOptions doc: doc.Password = value; break;
                case OdtSaveOptions odt: odt.Password = value; break;
                default: throw CliErrors.OptionInvalid(
                    "--encrypt",
                    $"the '{formatId}' format cannot be password-protected",
                    "Write a Word or OpenDocument format, or drop the password.");
            }
        }

        return options;
    }
}
