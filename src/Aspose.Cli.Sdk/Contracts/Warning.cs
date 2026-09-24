using System.Text.Json.Serialization;

namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// A non-fatal condition the caller should act on. Warnings are part of the
/// public contract, and <see cref="Hint"/> tells an agent what to do next.
/// </summary>
public sealed record Warning
{
    /// <summary>Stable SCREAMING_SNAKE_CASE identifier, e.g. <c>EVAL_MODE</c>.</summary>
    public required string Code { get; init; }

    /// <summary>Human-readable statement of the condition.</summary>
    public required string Message { get; init; }

    /// <summary>Recommended next action for the caller.</summary>
    public string? Hint { get; init; }

    /// <summary>Topic name for <c>aspose-cli docs &lt;topic&gt;</c> with background details.</summary>
    public string? Docs { get; init; }

    /// <summary>Stable product-owned address of the affected item, when known.</summary>
    public string? Location { get; init; }

    /// <summary>Whether this condition makes the produced visual evidence incomplete.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool AffectsCompleteness { get; init; }
}

/// <summary>Well-known warning codes.</summary>
public static partial class WarningCodes
{
    /// <summary>
    /// Emitted on every output-producing operation that ran without a license.
    /// The produced file carries an Aspose evaluation watermark.
    /// </summary>
    public const string EvalMode = "EVAL_MODE";

    public const string RemoteResourcesBlocked = "REMOTE_RESOURCES_BLOCKED";
    public const string LossyConversion = "LOSSY_CONVERSION";
    public const string SignatureInvalidated = "SIGNATURE_INVALIDATED";
    public const string EvalInputTruncated = "EVAL_INPUT_TRUNCATED";

    /// <summary>Every common warning code in this SDK build.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        EvalMode,
        RemoteResourcesBlocked,
        LossyConversion,
        SignatureInvalidated,
        EvalInputTruncated,
    ];
}
