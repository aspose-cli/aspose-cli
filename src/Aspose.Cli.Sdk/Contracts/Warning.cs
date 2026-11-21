using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// A non-fatal condition of a command that succeeded. Warnings are part of the public contract.
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

    /// <summary>Stable SCREAMING_SNAKE_CASE identifier, e.g. <c>ENCRYPTION_REMOVED</c>.</summary>
    [Pattern("^[A-Z][A-Z0-9_]*$")]
    public WarningCode Code { get; }

    /// <summary>Human-readable statement of the condition.</summary>
    [MinLength(1)]
    public string Message { get; init; }

    /// <summary>
    /// The affected item's address in the targets syntax <c>&lt;unit&gt;/&lt;address&gt;</c>, such as
    /// <c>page/3</c>, <c>slide/2/shape/4</c> or <c>cell/Sheet1!A1</c>, with names escaped as targets escape
    /// them; omitted when the condition concerns no single item, such as the whole document.
    /// </summary>
    [MinLength(1)]
    public string? Location { get; init; }

    /// <summary>
    /// Structured context, with the keys an error's details use, such as the <c>id</c> and
    /// <c>op</c> of the operation a warning is about.
    /// </summary>
    public JsonObject? Details { get; init; }

    /// <summary>
    /// Whether this condition leaves the output or its visual evidence incomplete, or different
    /// from the input; an edit's verification reports it as an issue when it is about content the
    /// edit lost.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool AffectsCompleteness { get; init; }
}

/// <summary>The warning codes the SDK's shared mechanisms emit.</summary>
public static class WarningCodes
{
    /// <summary>A document referenced remote resources, which the CLI did not load.</summary>
    public static readonly WarningCode RemoteResourcesBlocked = new("REMOTE_RESOURCES_BLOCKED");

    /// <summary>The input was encrypted and the output is not: its format cannot hold the encryption.</summary>
    public static readonly WarningCode EncryptionRemoved = new("ENCRYPTION_REMOVED");

    /// <summary>A list inside a result was capped; <c>details.list</c> names the list.</summary>
    public static readonly WarningCode ListTruncated = new("LIST_TRUNCATED");

    /// <summary>Every common warning code in this SDK build.</summary>
    public static IReadOnlyList<WarningCode> All { get; } =
    [
        RemoteResourcesBlocked,
        EncryptionRemoved,
        ListTruncated,
    ];
}
