namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>
/// The stories of a document, as <c>words search</c>, <c>replace_text</c> and inspect name them:
/// <c>body</c> is the main text without the comments and footnotes it anchors.
/// </summary>
public static class WordsStoryScopes
{
    public const string Body = "body";
    public const string HeadersFooters = "headers-footers";
    public const string Footnotes = "footnotes";
    public const string Comments = "comments";
}
