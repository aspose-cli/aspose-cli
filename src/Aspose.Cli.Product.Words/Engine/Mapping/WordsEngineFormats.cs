using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.Tables;
using Aspose.Words;
using Aspose.Words.Saving;

namespace Aspose.Cli.Product.Words.Engine.Mapping;

/// <summary>
/// Each format id's engine load and save format, and the save options an output is written with:
/// the engine's, except that an html, html-fixed, svg or md output holds its images, fonts and
/// styles itself, so the output is one file (the publication publishes one).
/// </summary>
internal static class WordsEngineFormats
{
    private static readonly IReadOnlyList<(string Id, LoadFormat? Load, SaveFormat? Save)> All =
    [
        ("doc", LoadFormat.Doc, SaveFormat.Doc),
        ("dot", LoadFormat.Dot, SaveFormat.Dot),
        ("docx", LoadFormat.Docx, SaveFormat.Docx),
        ("docm", LoadFormat.Docm, SaveFormat.Docm),
        ("dotx", LoadFormat.Dotx, SaveFormat.Dotx),
        ("dotm", LoadFormat.Dotm, SaveFormat.Dotm),
        ("flatopc", LoadFormat.FlatOpc, SaveFormat.FlatOpc),
        ("rtf", LoadFormat.Rtf, SaveFormat.Rtf),
        ("wordml", LoadFormat.WordML, SaveFormat.WordML),
        ("odt", LoadFormat.Odt, SaveFormat.Odt),
        ("ott", LoadFormat.Ott, SaveFormat.Ott),
        ("html", LoadFormat.Html, SaveFormat.Html),
        ("mhtml", LoadFormat.Mhtml, SaveFormat.Mhtml),
        ("txt", LoadFormat.Text, SaveFormat.Text),
        ("md", LoadFormat.Markdown, SaveFormat.Markdown),
        ("pdf", LoadFormat.Pdf, SaveFormat.Pdf),
        ("epub", LoadFormat.Epub, SaveFormat.Epub),
        ("mobi", LoadFormat.Mobi, SaveFormat.Mobi),
        ("azw3", LoadFormat.Azw3, SaveFormat.Azw3),
        ("chm", LoadFormat.Chm, null),
        ("xps", null, SaveFormat.Xps),
        ("openxps", null, SaveFormat.OpenXps),
        ("ps", null, SaveFormat.Ps),
        ("pcl", null, SaveFormat.Pcl),
        ("html-fixed", null, SaveFormat.HtmlFixed),
        ("png", null, SaveFormat.Png),
        ("jpeg", null, SaveFormat.Jpeg),
        ("svg", null, SaveFormat.Svg),
    ];

    /// <summary>The format id of a format the engine detected, or null for one no command reads.</summary>
    internal static string? IdOf(LoadFormat format) =>
        All.FirstOrDefault(entry => entry.Load == format).Id;

    internal static SaveFormat Save(string formatId) =>
        All.FirstOrDefault(entry => entry.Id == formatId).Save
            ?? throw new InvalidOperationException($"'{formatId}' has no save format.");

    /// <summary>The save options of <paramref name="formatId"/>: the engine's, made one file, with the password and pages given.</summary>
    internal static SaveOptions Options(string formatId, Secret? password = null, IReadOnlyList<int>? pages = null, int? dpi = null)
    {
        SaveOptions options = SaveOptions.CreateSaveOptions(Save(formatId));
        switch (options)
        {
            case HtmlSaveOptions html when html.SaveFormat == SaveFormat.Html:
                html.ExportImagesAsBase64 = true;
                html.ExportFontsAsBase64 = true;
                html.CssStyleSheetType = CssStyleSheetType.Embedded;
                break;
            case HtmlFixedSaveOptions fixedHtml:
                fixedHtml.ExportEmbeddedImages = true;
                fixedHtml.ExportEmbeddedFonts = true;
                fixedHtml.ExportEmbeddedCss = true;
                fixedHtml.ExportEmbeddedSvg = true;
                break;
            case SvgSaveOptions svg:
                svg.ExportEmbeddedImages = true;
                break;
            case MarkdownSaveOptions markdown:
                markdown.ExportImagesAsBase64 = true;
                break;
        }

        if (pages is not null)
        {
            ((FixedPageSaveOptions)options).PageSet = new PageSet([.. pages.Select(static page => page - 1)]);
        }

        if (dpi is not null && options is ImageSaveOptions image)
        {
            image.Resolution = dpi.Value;
        }

        if (password is not null)
        {
            string value = password.Reveal();
            switch (options)
            {
                case OoxmlSaveOptions ooxml: ooxml.Password = value; break;
                case DocSaveOptions doc: doc.Password = value; break;
                case OdtSaveOptions odt: odt.Password = value; break;
                default: throw new InvalidOperationException($"An output password reached the '{formatId}' format, which carries none.");
            }
        }

        return options;
    }

    /// <summary>Saves a document in <paramref name="formatId"/>, with the password <paramref name="protection"/> gives it, or none.</summary>
    internal static void Save(Document document, string path, string formatId, OutputProtection protection) =>
        document.Save(path, Options(formatId, protection.Password));
}

/// <summary>
/// How the object table opens and saves a document: the loader's open, and a save in the format
/// the document was read in, with the password protection decides.
/// </summary>
internal sealed class WordsDocuments(WordsDocumentLoader loader) : ITableDocuments<Document>
{
    /// <inheritdoc />
    public DocumentTable<Document> Table => WordsTable.Definition;

    /// <inheritdoc />
    public IReadOnlyList<Sdk.Formats.FormatDescriptor> Formats => WordsFormats.Editable;

    /// <inheritdoc />
    public IOpenedDocument<Document> Open(string path, Secret? password, EngineWarningLog warnings) => loader.Open(path, password, warnings);

    /// <inheritdoc />
    public IOpenedDocument<Document> Reopen(string path, Secret? password, EngineWarningLog warnings) => loader.Reopen(path, password, warnings);

    /// <inheritdoc />
    public void Save(IOpenedDocument<Document> document, string path, string format, OutputProtection protection, EngineWarningLog warnings)
    {
        WordsEngineFormats.Save(document.Document, path, format, protection);
        ((LoadedDocument)document).Resources.ThrowIfFailed();
    }
}
