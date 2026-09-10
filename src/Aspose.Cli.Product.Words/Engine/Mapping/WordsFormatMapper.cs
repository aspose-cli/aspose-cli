using Aspose.Cli.Product.Words.Contracts;
using Aspose.Words;

namespace Aspose.Cli.Product.Words.Engine.Mapping;

internal static class WordsFormatMapper
{
    private static readonly IReadOnlyDictionary<LoadFormat, string> LoadIds =
        new Dictionary<LoadFormat, string>
        {
            [LoadFormat.Doc] = "doc",
            [LoadFormat.Dot] = "dot",
            [LoadFormat.Docx] = "docx",
            [LoadFormat.Docm] = "docm",
            [LoadFormat.Dotx] = "dotx",
            [LoadFormat.Dotm] = "dotm",
            [LoadFormat.FlatOpc] = "flatopc",
            [LoadFormat.Rtf] = "rtf",
            [LoadFormat.WordML] = "wordml",
            [LoadFormat.Html] = "html",
            [LoadFormat.Mhtml] = "mhtml",
            [LoadFormat.Odt] = "odt",
            [LoadFormat.Ott] = "ott",
            [LoadFormat.Text] = "txt",
            [LoadFormat.Markdown] = "md",
            [LoadFormat.Pdf] = "pdf",
            [LoadFormat.Epub] = "epub",
            [LoadFormat.Mobi] = "mobi",
            [LoadFormat.Azw3] = "azw3",
            [LoadFormat.Chm] = "chm",
        };

    private static readonly IReadOnlyDictionary<string, SaveFormat> SaveFormats =
        new Dictionary<string, SaveFormat>(StringComparer.Ordinal)
        {
            ["doc"] = SaveFormat.Doc,
            ["dot"] = SaveFormat.Dot,
            ["docx"] = SaveFormat.Docx,
            ["docm"] = SaveFormat.Docm,
            ["dotx"] = SaveFormat.Dotx,
            ["dotm"] = SaveFormat.Dotm,
            ["flatopc"] = SaveFormat.FlatOpc,
            ["rtf"] = SaveFormat.Rtf,
            ["wordml"] = SaveFormat.WordML,
            ["pdf"] = SaveFormat.Pdf,
            ["xps"] = SaveFormat.Xps,
            ["openxps"] = SaveFormat.OpenXps,
            ["ps"] = SaveFormat.Ps,
            ["pcl"] = SaveFormat.Pcl,
            ["html"] = SaveFormat.Html,
            ["html-fixed"] = SaveFormat.HtmlFixed,
            ["mhtml"] = SaveFormat.Mhtml,
            ["epub"] = SaveFormat.Epub,
            ["mobi"] = SaveFormat.Mobi,
            ["azw3"] = SaveFormat.Azw3,
            ["odt"] = SaveFormat.Odt,
            ["ott"] = SaveFormat.Ott,
            ["txt"] = SaveFormat.Text,
            ["md"] = SaveFormat.Markdown,
            ["png"] = SaveFormat.Png,
            ["jpeg"] = SaveFormat.Jpeg,
            ["svg"] = SaveFormat.Svg,
        };

    public static string ToId(LoadFormat format) =>
        LoadIds.TryGetValue(format, out string? id) ? id : "unknown";

    public static SaveFormat ToSaveFormat(string id) =>
        SaveFormats.TryGetValue(id, out SaveFormat format)
            ? format
            : throw Sdk.Errors.CliErrors.FormatUnsupported(
                id,
                [.. WordsFormats.ConvertIds, .. WordsFormats.RenderIds]);

    public static string Extension(string id) => id switch
    {
        "html-fixed" => ".html",
        "wordml" or "flatopc" => ".xml",
        "jpeg" => ".jpg",
        _ => "." + id,
    };
}
