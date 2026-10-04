namespace Aspose.Cli.Sdk.Operations;

/// <summary>
/// Marks the abstract, partial base record of a product's operation vocabulary. The operation
/// generator reads every <see cref="OperationAttribute"/> record derived from it and generates
/// the vocabulary's catalog (<see cref="IOperationVocabulary{TOp}.Catalog"/>), its handler
/// interface <c>I{Base}Handler&lt;TResult&gt;</c> and the <c>Accept</c> dispatch on the base.
/// </summary>
/// <param name="schemaId">Canonical schema identifier of the operation document.</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class OperationVocabularyAttribute(string schemaId) : Attribute
{
    /// <summary>Canonical schema identifier of the operation document.</summary>
    public string SchemaId { get; } = schemaId;

    /// <summary>Largest accepted number of operations in one document.</summary>
    public int MaximumOperations { get; set; }

    /// <summary>
    /// The source-generated <c>JsonSerializerContext</c> that lists every operation of the
    /// vocabulary with camelCase property names.
    /// </summary>
    public Type? JsonContext { get; set; }
}

/// <summary>Declares one operation of a vocabulary by its stable wire name.</summary>
/// <param name="name">The value of the operation's <c>op</c> discriminator.</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class OperationAttribute(string name) : Attribute
{
    /// <summary>The value of the operation's <c>op</c> discriminator.</summary>
    public string Name { get; } = name;
}

/// <summary>
/// Names the field names a caller commonly writes for this member, such as <c>backgroundColor</c>
/// for a fill color. They are never accepted: an unknown field of one of these names suggests
/// this member, ahead of the names that merely look alike. Names compare ignoring case.
/// </summary>
/// <param name="names">The mistaken field names.</param>
[AttributeUsage(AttributeTargets.Property)]
public sealed class MistakenForAttribute(params string[] names) : Attribute
{
    /// <summary>The mistaken field names.</summary>
    public IReadOnlyList<string> Names { get; } = names;
}

/// <summary>
/// Marks a top-level string property that names a file the operation reads. The edit command
/// resolves it against the invocation directory and never publishes over it; it must not be
/// empty or only white space.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class InputPathAttribute() : NotBlankAttribute;

/// <summary>
/// Marks a top-level string property that names the environment variable holding a secret.
/// The edit command reads each named variable once; it must not be empty or only white space.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SecretEnvAttribute() : NotBlankAttribute;

/// <summary>Base of the string members that name something: neither empty nor only white space.</summary>
public abstract class NotBlankAttribute : MinLengthAttribute
{
    private static readonly PatternAttribute Visible = new(@"\S");

    private protected NotBlankAttribute()
        : base(1)
    {
    }

    /// <inheritdoc />
    public sealed override string? Check(object value) =>
        base.Check(value) ?? (Visible.Check(value) is null ? null : "must not be blank");

    /// <inheritdoc />
    public sealed override void Describe(System.Text.Json.Nodes.JsonObject schema, OperationValue value)
    {
        base.Describe(schema, value);
        Visible.Describe(schema, value);
    }
}
