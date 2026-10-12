namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>
/// The text scopes <c>words search</c> and <c>replace_text</c> share: <c>body</c> is the main
/// text without the comments and footnotes it anchors, <c>headers-footers</c> every header and
/// footer, <c>footnotes</c> every footnote and endnote, <c>comments</c> every comment, and
/// <c>all</c> all of them.
/// </summary>
public static class WordsTextScopes
{
    public const string Body = WordsStoryScopes.Body;
    public const string HeadersFooters = WordsStoryScopes.HeadersFooters;
    public const string Footnotes = WordsStoryScopes.Footnotes;
    public const string Comments = WordsStoryScopes.Comments;
    public const string All = "all";

    public static IReadOnlyList<string> Names { get; } = [Body, HeadersFooters, Footnotes, Comments, All];
}
