namespace Aspose.Cli.Sdk.Formats;

/// <summary>
/// Immutable ordered recognition clauses for one product-owned format.
/// </summary>
public sealed class FileFormatRecognition
{
    private readonly IReadOnlyList<Clause> _clauses;

    private FileFormatRecognition(IEnumerable<Clause> clauses)
    {
        Clause[] snapshot = clauses
            .Select(static clause => clause.Snapshot())
            .ToArray();
        if (snapshot.Length == 0)
        {
            throw new ArgumentException(
                "At least one recognition clause is required.",
                nameof(clauses));
        }
        _clauses = Array.AsReadOnly(snapshot);
    }

    /// <summary>Creates one positive recognition clause.</summary>
    public static FileFormatRecognition Match(
        FileProbePattern pattern,
        string evidence,
        int confidence = 100)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidence);
        ArgumentOutOfRangeException.ThrowIfLessThan(confidence, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(confidence, 100);
        return new FileFormatRecognition(
            [new Clause(pattern, evidence, confidence)]);
    }

    /// <summary>
    /// Tries clauses in descending confidence order and retains the strongest
    /// available evidence.
    /// </summary>
    public static FileFormatRecognition FirstOf(
        params FileFormatRecognition[] alternatives)
    {
        ArgumentNullException.ThrowIfNull(alternatives);
        if (alternatives.Length == 0
            || alternatives.Any(static item => item is null))
        {
            throw new ArgumentException(
                "At least one non-null recognition alternative is required.",
                nameof(alternatives));
        }
        return new FileFormatRecognition(
            alternatives
                .SelectMany(static alternative => alternative._clauses)
                .OrderByDescending(static clause => clause.Confidence));
    }

    internal FileRecognition Evaluate(
        string formatId,
        ReadOnlySpan<byte> bytes)
    {
        Clause? indeterminate = null;
        string? indeterminateEvidence = null;
        foreach (Clause clause in _clauses)
        {
            ProbePatternResult result = clause.Pattern.Evaluate(bytes);
            if (result.Kind == ProbePatternKind.Match)
            {
                return new FileRecognition
                {
                    Kind = FileRecognitionKind.Match,
                    FormatId = formatId,
                    Evidence = clause.Evidence,
                    Confidence = clause.Confidence,
                };
            }
            if (result.Kind == ProbePatternKind.Indeterminate
                && (indeterminate is null
                    || clause.Confidence > indeterminate.Confidence))
            {
                indeterminate = clause;
                indeterminateEvidence = result.Evidence;
            }
        }
        if (indeterminate is not null)
        {
            return new FileRecognition
            {
                Kind = FileRecognitionKind.Indeterminate,
                FormatId = formatId,
                Evidence = indeterminateEvidence ?? indeterminate.Evidence,
                Confidence = 40,
            };
        }
        return new FileRecognition
        {
            Kind = FileRecognitionKind.NoMatch,
            FormatId = formatId,
            Evidence = $"{_clauses[0].Evidence} not found",
        };
    }

    internal FileFormatRecognition Snapshot() =>
        new(_clauses);

    private sealed record Clause(
        FileProbePattern Pattern,
        string Evidence,
        int Confidence)
    {
        internal Clause Snapshot() =>
            new(Pattern.Snapshot(), Evidence, Confidence);
    }
}
