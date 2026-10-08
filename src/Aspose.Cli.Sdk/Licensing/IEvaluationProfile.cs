namespace Aspose.Cli.Sdk.Licensing;

/// <summary>
/// How a product recognizes the marks its engine's evaluation mode writes into a document: a
/// watermark, a banner, an added sheet or slide box, a notice in place of content. The product
/// supplies only this recognizer; the SDK write pipeline inspects every document it publishes,
/// once it is saved, through it and derives the evaluation disclosure from what it finds, so every
/// product discloses evaluation marks the same way.
/// </summary>
/// <typeparam name="TDocument">The engine's document type.</typeparam>
public interface IEvaluationProfile<in TDocument>
    where TDocument : class
{
    /// <summary>
    /// The evaluation marks a document carries, whoever wrote them: the marks evaluation mode
    /// adds when it opens or saves a document, and those an earlier save without a license
    /// left in the file. Inspecting never changes the document.
    /// </summary>
    EvaluationMarks Inspect(TDocument document);

    /// <summary>
    /// The evaluation marks an output of the document in <paramref name="format"/> carries:
    /// by default the document's own. A product whose evaluation mode writes its notice into an
    /// output's content, or whose format leaves some marks out, says so here.
    /// </summary>
    /// <param name="document">The document just saved to the output.</param>
    /// <param name="format">The output format id, such as <c>csv</c>.</param>
    EvaluationMarks Inspect(TDocument document, string format) => Inspect(document);

    /// <summary>
    /// The evaluation marks a rendering of some pages of the document shows, such as page images
    /// or the text of a page range: by default those of an output in <paramref name="format"/>. A
    /// product whose marks belong to pages or slides inspects only those.
    /// </summary>
    /// <param name="document">The document the rendering shows.</param>
    /// <param name="format">The output format id, such as <c>png</c>.</param>
    /// <param name="pages">The pages (or slides) shown, numbered from 1.</param>
    EvaluationMarks Inspect(TDocument document, string format, IReadOnlyCollection<int> pages) => Inspect(document, format);

    /// <summary>
    /// Removes from a document the marks evaluation mode added when it opened it, where they
    /// would stand as content once the document becomes part of another, such as the banner of
    /// a document appended to another. Called only in evaluation mode; by default it does nothing.
    /// </summary>
    void Neutralize(TDocument document)
    {
    }
}

/// <summary>
/// The evaluation marks of a document, as its product's <see cref="IEvaluationProfile{TDocument}"/>
/// found them.
/// </summary>
/// <param name="Marks">
/// Each mark, as a phrase that completes "the document carries ...", such as
/// <c>the evaluation watermark on its pages</c>; empty when there is none.
/// </param>
/// <param name="IsTruncated">
/// Whether evaluation mode cut the document's content short, so content is missing; the notice
/// that says so stays in the document.
/// </param>
public sealed record EvaluationMarks(IReadOnlyList<string> Marks, bool IsTruncated = false)
{
    /// <summary>A document without evaluation marks.</summary>
    public static EvaluationMarks None { get; } = new([]);

    /// <summary>Whether the document carries any evaluation mark.</summary>
    public bool Any => Marks.Count > 0 || IsTruncated;

    /// <summary>The marks of either document, each once, this document's first.</summary>
    public EvaluationMarks Union(EvaluationMarks other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new EvaluationMarks([.. Marks.Union(other.Marks, StringComparer.Ordinal)], IsTruncated || other.IsTruncated);
    }
}
