namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>Deny-by-default Words format registry.</summary>
public static class WordsFormats
{
    public static IReadOnlyList<string> LoadIds { get; } =
        ["doc", "dot", "docx", "docm", "dotx", "dotm", "flatopc", "rtf", "wordml",
         "html", "mhtml", "odt", "ott", "txt", "md", "pdf", "epub", "mobi", "azw3", "chm"];

    public static IReadOnlyList<string> ConvertIds { get; } =
        ["doc", "dot", "docx", "docm", "dotx", "dotm", "flatopc", "rtf", "wordml",
         "pdf", "xps", "openxps", "ps", "pcl", "html", "html-fixed", "mhtml",
         "epub", "mobi", "azw3", "odt", "ott", "txt", "md"];

    public static IReadOnlyList<string> RenderIds { get; } = ["png", "jpeg", "svg"];
    public static IReadOnlyList<string> FixedPageConvertIds { get; } = ["pdf", "xps", "openxps", "ps", "pcl"];
    public static IReadOnlyList<string> EncryptIds { get; } =
        ["doc", "dot", "docx", "docm", "dotx", "dotm", "flatopc", "odt", "ott"];

    public static bool IsLoad(string id) => LoadIds.Contains(id, StringComparer.Ordinal);
}
