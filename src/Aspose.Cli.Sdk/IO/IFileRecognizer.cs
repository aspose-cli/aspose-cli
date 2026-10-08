namespace Aspose.Cli.Sdk.IO;

/// <summary>Performs bounded, side-effect-free content recognition for one product.</summary>
public interface IFileRecognizer
{
    /// <summary>Stable, side-effect-free recognizer metadata.</summary>
    FileRecognizerDescriptor Descriptor =>
        FileRecognizerDescriptor.Custom;

    /// <summary>Recognizes one file without opening it for product execution.</summary>
    /// <param name="file">Bounded, cached probe session.</param>
    /// <param name="cancellationToken">Cancellation requested by the host.</param>
    ValueTask<FileRecognition> RecognizeAsync(
        FileProbeSession file,
        CancellationToken cancellationToken);
}

/// <summary>Outcome of one bounded product recognizer.</summary>
public enum FileRecognitionKind
{
    /// <summary>The available evidence does not match this product.</summary>
    NoMatch,

    /// <summary>The content strongly matches this product.</summary>
    Match,

    /// <summary>
    /// The container matches but requires a password for final validation;
    /// generic routing treats it as indeterminate and fails closed.
    /// </summary>
    Encrypted,

    /// <summary>The configured probe budget cannot decide safely.</summary>
    Indeterminate,

    /// <summary>The recognizer failed without exposing an internal exception.</summary>
    Failed,
}

/// <summary>Stable, explainable result returned by a product recognizer.</summary>
public sealed record FileRecognition
{
    /// <summary>Recognition outcome.</summary>
    public required FileRecognitionKind Kind { get; init; }

    /// <summary>Product-specific format identifier when known.</summary>
    public string? FormatId { get; init; }

    /// <summary>Short diagnostic evidence suitable for verbose logs.</summary>
    public string? Evidence { get; init; }

    /// <summary>
    /// Relative confidence from 0 through 100. Generic matches must carry a
    /// positive value; the router uses it only to prefer stronger evidence and
    /// never to turn an uncertain result into a match.
    /// </summary>
    public int Confidence { get; init; }
}

/// <summary>Auditable limits and behavior of one file recognizer.</summary>
public sealed record FileRecognizerDescriptor
{
    /// <summary>Metadata used for externally supplied recognizers.</summary>
    public static FileRecognizerDescriptor Custom { get; } = new()
    {
        Strategy = "custom",
        Version = "1",
        MaxProbeBytes = 64 * 1024,
        EvidenceTypes = ["bounded-prefix"],
        CooperativeCancellation = false,
    };

    /// <summary>Stable strategy identifier.</summary>
    public required string Strategy { get; init; }

    /// <summary>Recognizer contract version.</summary>
    public required string Version { get; init; }

    /// <summary>Maximum cached bytes the recognizer consumes.</summary>
    public required int MaxProbeBytes { get; init; }

    /// <summary>Stable evidence categories used by the recognizer.</summary>
    public required IReadOnlyList<string> EvidenceTypes { get; init; }

    /// <summary>Whether cancellation is guaranteed to be observed cooperatively.</summary>
    public required bool CooperativeCancellation { get; init; }
}
