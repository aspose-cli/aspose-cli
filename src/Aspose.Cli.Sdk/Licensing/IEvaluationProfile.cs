namespace Aspose.Cli.Sdk.Licensing;

/// <summary>
/// How a product names its engine's evaluation mode and recognizes the limits it reports: a run
/// without a license is stated once, as the result's license mode, and what evaluation mode
/// writes stays in the output as the engine wrote it.
/// </summary>
public interface IEvaluationProfile
{
    /// <summary>
    /// The engine whose evaluation mode the profile recognizes, which also names the license
    /// that lifts its limits, such as <c>Aspose.PDF</c>. The SDK writes it into every evaluation
    /// error it builds from the profile's facts.
    /// </summary>
    string Engine { get; }

    /// <summary>
    /// The evaluation limit an exception of the engine reports, such as a page past the ones
    /// evaluation mode shows; null for any other exception, and by default. Only the SDK calls
    /// it, in evaluation mode, on an exception a command or a view let through, and reports the
    /// limit as <c>EVALUATION_LIMIT</c>; a product never translates the exception itself.
    /// </summary>
    /// <param name="exception">The exception the engine threw.</param>
    EvaluationLimitFact? RecognizeLimit(Exception exception) => null;
}

/// <summary>
/// An evaluation limit of an engine, as its <see cref="IEvaluationProfile"/> recognizes
/// it in the engine's exception. The SDK writes the <c>EVALUATION_LIMIT</c> error, its message
/// and details, from these facts and the profile's engine, the same way for every product.
/// </summary>
/// <param name="Allows">
/// What evaluation mode allows, completing "<c>Aspose.X evaluation mode ...</c>", such as
/// <c>reads only the first 4 pages of a document</c>.
/// </param>
/// <param name="Limit">The limit, in <paramref name="Unit"/>.</param>
/// <param name="Unit">The unit of <paramref name="Limit"/>, such as <c>pages</c>.</param>
public sealed record EvaluationLimitFact(string Allows, int Limit, string Unit);
