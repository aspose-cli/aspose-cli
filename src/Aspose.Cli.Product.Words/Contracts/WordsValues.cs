namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>
/// The stories of a document, as <c>query search</c>, <c>replace_text</c> and inspect name them:
/// <c>body</c> is the main text without the comments and footnotes it anchors.
/// </summary>
public static class WordsStoryScopes
{
    public const string Body = "body";
    public const string HeadersFooters = "headersFooters";
    public const string Footnotes = "footnotes";
    public const string Comments = "comments";
}

/// <summary>
/// The editing restrictions reads report: <c>none</c>, or the <c>protect</c> mode that sets one.
/// </summary>
public static class WordsProtectionModes
{
    public const string None = "none";
    public const string ReadOnly = "readOnly";
    public const string Comments = "comments";
    public const string TrackedChanges = "trackedChanges";
    public const string Forms = "forms";
}

/// <summary>The kinds of tracked change inspect and compare report.</summary>
public static class WordsRevisionTypes
{
    public const string Insertion = "insertion";
    public const string Deletion = "deletion";
    public const string FormatChange = "formatChange";
    public const string StyleDefinitionChange = "styleDefinitionChange";
    public const string Moving = "moving";
}

/// <summary>Where a header or footer shows: at the top or the bottom of the page.</summary>
public static class HeaderFooterLocations
{
    public const string Header = "header";
    public const string Footer = "footer";
}

/// <summary>Which pages a header or footer shows on.</summary>
public static class HeaderFooterKinds
{
    public const string Primary = "primary";
    public const string First = "first";
    public const string Even = "even";
}

/// <summary>The orientations of a section's pages.</summary>
public static class PageOrientations
{
    public const string Portrait = "portrait";
    public const string Landscape = "landscape";
}

/// <summary>The breaks <c>insert_break</c> inserts and a read reports after a block.</summary>
public static class WordsBreakKinds
{
    public const string Page = "page";
    public const string Section = "section";
}

/// <summary>What <c>words extract --what</c> extracts.</summary>
public static class WordsExtractTargets
{
    public const string Images = "images";
    public const string Comments = "comments";
    public const string Text = "text";
    public const string Tables = "tables";

    public static IReadOnlyList<string> Names { get; } = [Images, Comments, Text, Tables];
}
