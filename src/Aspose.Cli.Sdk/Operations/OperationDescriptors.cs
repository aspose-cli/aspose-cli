using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Sdk.Operations;

/// <summary>
/// Implemented by generated code on the base record of every <see cref="OperationVocabularyAttribute"/>
/// vocabulary, so shared code reaches the vocabulary's catalog through its operation type.
/// </summary>
public interface IOperationVocabulary<TOp>
    where TOp : BoundedOperation, IOperationVocabulary<TOp>
{
    /// <summary>The vocabulary's generated catalog.</summary>
    static abstract OperationCatalog<TOp> Catalog { get; }
}

/// <summary>The JSON kind of a contract value.</summary>
public enum OperationValueKind
{
    /// <summary>A JSON string.</summary>
    String,

    /// <summary>A whole JSON number.</summary>
    Integer,

    /// <summary>Any JSON number.</summary>
    Number,

    /// <summary><c>true</c> or <c>false</c>.</summary>
    Boolean,

    /// <summary>An object with the declared members of a contract record.</summary>
    Record,

    /// <summary>A JSON array of one item shape.</summary>
    Array,

    /// <summary>An object whose member values share one shape.</summary>
    Map,

    /// <summary>Any JSON value; the description gives its meaning.</summary>
    Any,
}

/// <summary>The shape of one contract value, as the operation generator extracted it from a record.</summary>
public sealed class OperationValue
{
    /// <summary>The JSON kind.</summary>
    public required OperationValueKind Kind { get; init; }

    /// <summary>
    /// Whether the CLR type admits null. An array item or map value may then be JSON null; a
    /// nullable property is optional and is omitted rather than set to null.
    /// </summary>
    public bool Nullable { get; init; }

    /// <summary>The shape of an array's items or a map's values.</summary>
    public OperationValue? Items { get; init; }

    /// <summary>The members of a <see cref="OperationValueKind.Record"/> value.</summary>
    public OperationRecord? Record { get; init; }
}

/// <summary>One contract record: an operation or a nested input object.</summary>
public sealed class OperationRecord
{
    /// <summary>The record's CLR type.</summary>
    public required Type Type { get; init; }

    /// <summary>
    /// The record's <c>$defs</c> key: an operation's wire name, or the camelCase name of a
    /// nested record's type.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>The record's documentation summary, or null.</summary>
    public string? Description { get; init; }

    /// <summary>Public members in declaration order, inherited members first.</summary>
    public IReadOnlyList<OperationProperty> Properties { get; init; } = [];

    /// <summary>Rules over several members, such as <see cref="ExactlyOneOfAttribute"/>.</summary>
    public IReadOnlyList<ValueConstraintAttribute> Constraints { get; init; } = [];
}

/// <summary>One member of a contract record.</summary>
public sealed class OperationProperty
{
    /// <summary>The camelCase wire name.</summary>
    public required string Name { get; init; }

    /// <summary>The member's documentation summary, or null.</summary>
    public string? Description { get; init; }

    /// <summary>The member's value shape.</summary>
    public required OperationValue Value { get; init; }

    /// <summary>Whether the member is declared <c>required</c>.</summary>
    public bool Required { get; init; }

    /// <summary>
    /// The JSON literal an omitted member takes, written into the payload before it is read and
    /// published as the schema default; null when an omitted member has no value.
    /// </summary>
    public string? Default { get; init; }

    /// <summary>The member's declared constraints.</summary>
    public IReadOnlyList<ValueConstraintAttribute> Constraints { get; init; } = [];

    /// <summary>The field names commonly written for this member (see <see cref="MistakenForAttribute"/>).</summary>
    public IReadOnlyList<string> MistakenFor { get; init; } = [];

    /// <summary>Reads the member from an instance of its record.</summary>
    public required Func<object, object?> Get { get; init; }
}

/// <summary>One operation of a generated vocabulary.</summary>
public sealed class OperationDescriptor
{
    /// <summary>The operation record; its name is the wire name.</summary>
    public required OperationRecord Record { get; init; }

    /// <summary>
    /// Returns the operation with every <see cref="InputPathAttribute"/> member resolved by the
    /// given function; null when the operation reads no file.
    /// </summary>
    public Func<object, Func<string, string>, object>? ResolveInputPaths { get; init; }

    /// <summary>
    /// Returns the values of every <see cref="SecretEnvAttribute"/> member, null for an omitted
    /// one; null when the operation reads no secret.
    /// </summary>
    public Func<object, IEnumerable<string?>>? SecretVariables { get; init; }
}
