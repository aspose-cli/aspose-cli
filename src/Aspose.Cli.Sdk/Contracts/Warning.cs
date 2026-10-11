using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// A non-fatal condition the caller should act on. Warnings are part of the
/// public contract, and <see cref="Hint"/> tells an agent what to do next.
/// </summary>
[SchemaId("warning")]
public sealed record Warning
{
    /// <summary>Creates a warning with a declared code.</summary>
    /// <param name="code">The declared code.</param>
    /// <param name="message">Human-readable statement of the condition.</param>
    public Warning(WarningCode code, string message)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(message);
        Code = code;
        Message = message;
    }

    /// <summary>Stable SCREAMING_SNAKE_CASE identifier, e.g. <c>EVALUATION_MODE</c>.</summary>
    [Pattern("^[A-Z][A-Z0-9_]*$")]
    public WarningCode Code { get; }

    /// <summary>Human-readable statement of the condition.</summary>
    [MinLength(1)]
    public string Message { get; init; }

    /// <summary>Recommended next action for the caller.</summary>
    [MinLength(1)]
    public string? Hint { get; init; }

    /// <summary>Topic name for <c>aspose-cli docs &lt;topic&gt;</c> with background details.</summary>
    [MinLength(1)]
    public string? Docs { get; init; }

    /// <summary>Stable product-owned address of the affected item, when known.</summary>
    [MinLength(1)]
    public string? Location { get; init; }

    /// <summary>
    /// Structured context, with the keys an error's details use, such as the <c>id</c> and
    /// <c>op</c> of the operation a warning is about.
    /// </summary>
    public JsonObject? Details { get; init; }

    /// <summary>
    /// Whether this condition leaves the output or its visual evidence incomplete, or different
    /// from the input; a review counts it against coverage and an edit's verification reports it
    /// as an issue.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool AffectsCompleteness { get; init; }
}

/// <summary>The warning codes the SDK's shared mechanisms emit.</summary>
public static class WarningCodes
{
    /// <summary>
    /// Emitted on every output-producing operation that ran without a license.
    /// The produced file carries an Aspose evaluation watermark.
    /// </summary>
    public static readonly WarningCode EvaluationMode = new("EVALUATION_MODE");

    /// <summary>A document referenced remote resources, which the CLI did not load.</summary>
    public static readonly WarningCode RemoteResourcesBlocked = new("REMOTE_RESOURCES_BLOCKED");

    /// <summary>The output format cannot hold everything the input holds.</summary>
    public static readonly WarningCode LossyConversion = new("LOSSY_CONVERSION");

    /// <summary>A change invalidated a digital signature of the input.</summary>
    public static readonly WarningCode SignatureInvalidated = new("SIGNATURE_INVALIDATED");

    /// <summary>Without a license, the engine loaded only the start of the input.</summary>
    public static readonly WarningCode EvaluationInputTruncated = new("EVALUATION_INPUT_TRUNCATED");

    /// <summary>
    /// With a license, the output keeps evaluation marks that an earlier save without a license
    /// wrote into its input, or renders a source that carries them. Only the write pipeline
    /// reports it.
    /// </summary>
    public static readonly WarningCode EvaluationInputMarked = new("EVALUATION_INPUT_MARKED");

    /// <summary>The input was encrypted and the output is not: its format cannot hold the encryption.</summary>
    public static readonly WarningCode EncryptionRemoved = new("ENCRYPTION_REMOVED");

    /// <summary>A list inside a result was capped; the warning's location names the list.</summary>
    public static readonly WarningCode ListTruncated = new("LIST_TRUNCATED");

    /// <summary>An in-place edit kept an existing backup that holds an earlier version than the file it replaced.</summary>
    public static readonly WarningCode BackupPredatesEdit = new("BACKUP_PREDATES_EDIT");

    /// <summary>An edit went through restrictions the input declares but the engine does not enforce.</summary>
    public static readonly WarningCode ProtectionNotEnforced = new("PROTECTION_NOT_ENFORCED");

    /// <summary>
    /// An operation applied but changed nothing, such as a replacement that matched no text;
    /// <c>details</c> names the operation by <c>id</c> and <c>op</c> and says why in <c>reason</c>.
    /// </summary>
    public static readonly WarningCode OpNoEffect = new("OP_NO_EFFECT");

    /// <summary>Every common warning code in this SDK build.</summary>
    public static IReadOnlyList<WarningCode> All { get; } =
    [
        EvaluationMode,
        RemoteResourcesBlocked,
        LossyConversion,
        SignatureInvalidated,
        EvaluationInputTruncated,
        EvaluationInputMarked,
        EncryptionRemoved,
        ListTruncated,
        BackupPredatesEdit,
        ProtectionNotEnforced,
        OpNoEffect,
    ];
}

/// <summary>Why an operation changed nothing: the <c>details.reason</c> of an <c>OP_NO_EFFECT</c> warning.</summary>
public static class NoEffectReasons
{
    /// <summary>What the operation looks for, such as text, a pattern or an author, is not in the document.</summary>
    public const string NoMatch = "no-match";

    /// <summary>
    /// The objects the operation addresses already had the state it sets, so a snapshot of them
    /// before and after it found no difference.
    /// </summary>
    public const string NoChange = "no-change";
}
