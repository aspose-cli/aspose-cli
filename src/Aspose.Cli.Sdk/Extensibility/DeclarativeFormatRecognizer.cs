namespace Aspose.Cli.Sdk.Extensibility;

/// <summary>
/// Product-neutral evaluator for product-owned declarative recognition data.
/// </summary>
internal sealed class DeclarativeFormatRecognizer(
    IReadOnlyList<FormatDescriptor> formats) : IFileRecognizer
{
    /// <summary>The lowest confidence that proves a format without a declared extension.</summary>
    internal const int StrongConfidence = 90;

    private readonly FormatDescriptor[] _formats = formats
        .Where(static format =>
            format.Uses.HasFlag(FormatUse.Input)
            && format.Ownership == RouteOwnership.Default)
        .OrderBy(static format => format.InputOrder)
        .ThenBy(static format => format.Id, StringComparer.Ordinal)
        .ToArray();

    public FileRecognizerDescriptor Descriptor { get; } = new()
    {
        Strategy = "standard-signature-v1",
        Version = "1",
        MaxProbeBytes = 64 * 1024,
        EvidenceTypes = Array.AsReadOnly(
        [
            "magic-bytes",
            "bounded-container-markers",
            "bounded-text-grammar",
        ]),
        CooperativeCancellation = true,
    };

    public ValueTask<FileRecognition> RecognizeAsync(
        FileProbeSession file,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string extension = Normalize(file.Extension);
        FormatDescriptor[] extensionFormats = _formats
            .Where(format => format.RoutedExtensions.Any(candidate =>
                string.Equals(
                    Normalize(candidate),
                    extension,
                    StringComparison.Ordinal)))
            .ToArray();
        bool extensionGrounded = extensionFormats.Length > 0;
        IEnumerable<FormatDescriptor> candidates = extensionGrounded
            ? extensionFormats
            : _formats;

        FileRecognition[] results = candidates
            .Select(format => format.Recognition!.Evaluate(
                format.Id,
                file.Prefix.Span))
            .Where(static result =>
                result.Kind != FileRecognitionKind.NoMatch)
            .OrderByDescending(static result => result.Confidence)
            .ThenBy(static result => result.FormatId, StringComparer.Ordinal)
            .ToArray();
        if (results.Length == 0)
        {
            return ValueTask.FromResult(new FileRecognition
            {
                Kind = FileRecognitionKind.NoMatch,
                Evidence = extensionGrounded
                    ? "declared extension failed bounded content validation"
                    : "no standard signature matched",
            });
        }

        FileRecognition best = results[0];
        bool uniqueStrongestMatch = results.Count(result =>
            result.Kind == FileRecognitionKind.Match
            && result.Confidence == best.Confidence) == 1;
        if (!extensionGrounded
            && (best.Kind != FileRecognitionKind.Match
                || best.Confidence < StrongConfidence
                || !uniqueStrongestMatch))
        {
            return ValueTask.FromResult(new FileRecognition
            {
                Kind = FileRecognitionKind.Indeterminate,
                FormatId = best.FormatId,
                Evidence =
                    "content evidence is not unique without a declared extension",
                Confidence = best.Confidence,
            });
        }

        return ValueTask.FromResult(best);
    }

    private static string Normalize(string extension) =>
        extension.StartsWith('.')
            ? extension.ToLowerInvariant()
            : "." + extension.ToLowerInvariant();
}
