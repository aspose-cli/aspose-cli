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

    /// <summary>Stable SCREAMING_SNAKE_CASE identifier, e.g. <c>EVAL_MODE</c>.</summary>
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
    public static readonly WarningCode EvalMode = new("EVAL_MODE") { LicenseSurface = true };

    /// <summary>A document referenced remote resources, which the CLI did not load.</summary>
    public static readonly WarningCode RemoteResourcesBlocked = new("REMOTE_RESOURCES_BLOCKED");

    /// <summary>The output format cannot hold everything the input holds.</summary>
    public static readonly WarningCode LossyConversion = new("LOSSY_CONVERSION");

    /// <summary>A change invalidated a digital signature of the input.</summary>
    public static readonly WarningCode SignatureInvalidated = new("SIGNATURE_INVALIDATED");

    /// <summary>Without a license, the engine loaded only the start of the input.</summary>
    public static readonly WarningCode EvalInputTruncated = new("EVAL_INPUT_TRUNCATED") { LicenseSurface = true };

    /// <summary>
    /// With a license, the output keeps evaluation marks that an earlier save without a license
    /// wrote into its input, or renders a source that carries them. Only the write pipeline
    /// reports it.
    /// </summary>
    public static readonly WarningCode EvalInputMarked = new("EVAL_INPUT_MARKED") { LicenseSurface = true };

    /// <summary>A list inside a result was capped; the warning's location names the list.</summary>
    public static readonly WarningCode ListTruncated = new("LIST_TRUNCATED");

    /// <summary>An in-place edit kept an existing backup that holds an earlier version than the file it replaced.</summary>
    public static readonly WarningCode BackupPredatesEdit = new("BACKUP_PREDATES_EDIT");

    /// <summary>An edit went through restrictions the input declares but the engine does not enforce.</summary>
    public static readonly WarningCode ProtectionNotEnforced = new("PROTECTION_NOT_ENFORCED");

    /// <summary>A replace_text operation matched no text in its scope, so it changed nothing; the warning never repeats the find text.</summary>
    public static readonly WarningCode ReplaceNoMatch = new("REPLACE_NO_MATCH");

    /// <summary>Every common warning code in this SDK build.</summary>
    public static IReadOnlyList<WarningCode> All { get; } =
    [
        EvalMode,
        RemoteResourcesBlocked,
        LossyConversion,
        SignatureInvalidated,
        EvalInputTruncated,
        EvalInputMarked,
        ListTruncated,
        BackupPredatesEdit,
        ProtectionNotEnforced,
        ReplaceNoMatch,
    ];
}
