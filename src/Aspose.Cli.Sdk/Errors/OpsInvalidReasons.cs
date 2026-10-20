namespace Aspose.Cli.Sdk.Errors;

/// <summary>
/// Why an operation document or one of its operations was refused: the <c>details.reason</c> of
/// an <c>OPS_INVALID</c>. The message explains it in words; a reason about one field names the
/// field's path in <c>details.field</c>, such as <c>style.color</c>.
/// </summary>
public static class OpsInvalidReasons
{
    /// <summary>The document is not JSON.</summary>
    public const string InvalidJson = "invalid-json";

    /// <summary>The document, an operation or a field holds another kind of value, such as a string where a number belongs, or null.</summary>
    public const string WrongType = "wrong-type";

    /// <summary>An object names a field its contract does not declare.</summary>
    public const string UnknownField = "unknown-field";

    /// <summary>An object omits a required field.</summary>
    public const string MissingField = "missing-field";

    /// <summary>An object names a field twice.</summary>
    public const string DuplicateField = "duplicate-field";

    /// <summary>An operation's <c>op</c> names no operation of the vocabulary.</summary>
    public const string UnknownOp = "unknown-op";

    /// <summary>
    /// A value of the right kind that the operation refuses: outside its range or allowed values,
    /// not parsed by its value kind, or refused by the operation's own check against the others.
    /// </summary>
    public const string InvalidValue = "invalid-value";
}
