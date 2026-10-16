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
    /// The engine whose evaluation mode the profile recognizes, which also names the license
    /// that lifts its limits, such as <c>Aspose.PDF</c>. The SDK writes it into every evaluation
    /// error and warning it builds from the profile's facts.
    /// </summary>
    string Engine { get; }

    /// <summary>
    /// What evaluation mode leaves out of a document it cuts short
    /// (<see cref="EvaluationMarks.IsTruncated"/>), completing "<c>Aspose.X evaluation mode ...</c>",
    /// such as <c>reads only the first 4 pages of a document</c>. The SDK writes the
    /// <c>EVALUATION_INPUT_TRUNCATED</c> warning of a command that read such a document from it.
    /// </summary>
    string Truncation => "cuts the content of some documents short";

    /// <summary>
    /// The evaluation limit an exception of the engine reports, such as a page past the ones
    /// evaluation mode shows; null for any other exception, and by default. Only the SDK calls
    /// it, in evaluation mode, on an exception a command or a view let through, and reports the
    /// limit as <c>EVALUATION_LIMIT</c>; a product never translates the exception itself.
    /// </summary>
    /// <param name="exception">The exception the engine threw.</param>
    EvaluationLimitFact? RecognizeLimit(Exception exception) => null;

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
    /// Whether evaluation mode writes its watermark into an output in <paramref name="format"/>:
    /// by default it does. A product whose evaluation mode leaves a format's outputs without one,
    /// such as a text export it only cuts short, says so here, and the <c>EVALUATION_MODE</c>
    /// warning of outputs that all take none claims no watermark.
    /// </summary>
    /// <param name="format">The output format id, such as <c>md</c>.</param>
    bool WritesWatermark(string format) => true;

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
    /// The evaluation marks of an output as the file holds it, read from <paramref name="path"/>,
    /// for an output the product saves through something other than the document it hands the
    /// pipeline, such as a facade that signs it, so the document does not show what the file
    /// carries. Null when the profile reads no file, and by default.
    /// </summary>
    /// <param name="path">The staged output file.</param>
    /// <param name="format">The output format id, such as <c>pdf</c>.</param>
    EvaluationMarks? InspectSaved(string path, string format) => null;

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

/// <summary>
/// An evaluation limit of an engine, as its <see cref="IEvaluationProfile{TDocument}"/> recognizes
/// it in the engine's exception. The SDK writes the <c>EVALUATION_LIMIT</c> error, its message,
/// hint and details, from these facts and the profile's engine, the same way for every product.
/// </summary>
/// <param name="Allows">
/// What evaluation mode allows, completing "<c>Aspose.X evaluation mode ...</c>", such as
/// <c>reads only the first 4 pages of a document</c>.
/// </param>
/// <param name="Limit">The limit, in <paramref name="Unit"/>.</param>
/// <param name="Unit">The unit of <paramref name="Limit"/>, such as <c>pages</c>.</param>
/// <param name="Alternative">
/// What any command can do without a license, completing "<c>Apply a license, or ...</c>", or
/// null. A selection of fewer <paramref name="Unit"/> is not stated here: the SDK offers it only
/// for a command whose selection option over that unit keeps the engine within the limit.
/// </param>
public sealed record EvaluationLimitFact(string Allows, int Limit, string Unit, string? Alternative = null);
