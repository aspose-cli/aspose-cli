namespace Aspose.Cli.TestKit.Scenarios;

/// <summary>
/// The inputs a product opens only with <c>convert</c>, by product and extension, with the options
/// that convert reads them with: its read refuses them as FORMAT_UNSUPPORTED, so a check that
/// reopens such a file converts it to the product's own format instead.
/// </summary>
public static class ConvertOnlyInputs
{
    private static readonly string[] Txt =
        ["--encoding", "utf-8", "--auto-numbering-detection", "false", "--detect-numbering-with-whitespaces", "false", "--detect-hyperlinks", "false", "--leading-spaces", "Preserve"];

    private static readonly Dictionary<(string Product, string Extension), string[]> All = new()
    {
        [("words", ".txt")] = Txt,
        [("words", ".md")] = ["--encoding", "utf-8"],
        [("words", ".markdown")] = ["--encoding", "utf-8"],
        [("words", ".htm")] = [],
        [("words", ".html")] = [],
        [("words", ".mhtml")] = [],
        [("words", ".epub")] = [],
        [("words", ".mobi")] = [],
        [("words", ".azw3")] = [],
        [("words", ".chm")] = [],
        [("words", ".pdf")] = [],
        [("pdf", ".htm")] = [],
        [("pdf", ".html")] = [],
    };

    /// <summary>The options convert reads <paramref name="path"/> with when only convert opens it in <paramref name="product"/>, or null.</summary>
    public static string[]? Options(string product, string path) =>
        All.TryGetValue((product, Path.GetExtension(path).ToLowerInvariant()), out string[]? options) ? options : null;
}
