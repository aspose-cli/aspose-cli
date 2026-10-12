namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>Options for structural Words inspection.</summary>
public sealed record WordsInspectRequest
{
    /// <summary>The document to open.</summary>
    public required string Input { get; init; }
    public bool IncludePreview { get; init; }
    public IReadOnlyList<string>? Details { get; init; }
    public Secret? Password { get; init; }
}

/// <summary>Accepted values of <c>words inspect --detail</c>.</summary>
public static class WordsInspectDetails
{
    /// <summary>The headings.</summary>
    public const string Outline = "outline";

    /// <summary>The sections: page setup, headers and footers.</summary>
    public const string Sections = "sections";

    /// <summary>The styles.</summary>
    public const string Styles = "styles";

    /// <summary>The fields.</summary>
    public const string Fields = "fields";

    /// <summary>The bookmarks.</summary>
    public const string Bookmarks = "bookmarks";

    /// <summary>The comments.</summary>
    public const string Comments = "comments";

    /// <summary>The tracked revisions.</summary>
    public const string Revisions = "revisions";

    /// <summary>The images.</summary>
    public const string Images = "images";

    /// <summary>The tables.</summary>
    public const string Tables = "tables";

    /// <summary>The document properties: title, author, subject and keywords.</summary>
    public const string Properties = "properties";

    /// <summary>The fonts the document uses.</summary>
    public const string Fonts = "fonts";

    /// <summary>Every detail id, in documentation order.</summary>
    public static IReadOnlyList<string> All { get; } = [Outline, Sections, Styles, Fields, Bookmarks, Comments, Revisions, Images, Tables, Properties, Fonts];
}
