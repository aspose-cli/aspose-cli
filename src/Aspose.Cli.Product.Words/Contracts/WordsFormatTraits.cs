namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>What one declared Words format keeps and how the commands treat it.</summary>
/// <param name="Id">The public format id.</param>
internal sealed record WordsFormatTraits(string Id)
{
    /// <summary>A Microsoft Word format, which keeps fields, revisions and protection.</summary>
    public bool Word { get; init; }

    /// <summary>Stores tracked changes as revisions a reviewer can accept or reject.</summary>
    public bool KeepsRevisions { get; init; }

    /// <summary>Keeps a document's macros (its VBA project).</summary>
    public bool KeepsMacros { get; init; }

    /// <summary>A fixed-page convert format, which can hold a selection of the pages.</summary>
    public bool FixedPage { get; init; }

    /// <summary>
    /// Create and edit write it: it reopens as the document it saved, so a later edit works on
    /// the same document. Only a convert format the product also loads can be editable.
    /// </summary>
    public bool Editable { get; init; }
}
