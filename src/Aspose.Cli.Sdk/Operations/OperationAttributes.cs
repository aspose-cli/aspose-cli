using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Sdk.Operations;

/// <summary>
/// Marks the abstract, partial base record of a product's operation vocabulary. The operation
/// generator reads every <see cref="OperationAttribute"/> record derived from it and generates
/// the vocabulary's catalog (<see cref="IOperationVocabulary{TOp}.Catalog"/>), its handler
/// interface <c>I{Base}Handler&lt;TResult&gt;</c> and the <c>Accept</c> dispatch on the base.
/// </summary>
/// <param name="schemaId">
/// The schema id of the operation document relative to the product, such as <c>ops</c>; the
/// SDK publishes the schema under <c>v2/&lt;product&gt;/</c>, as it does a result's.
/// </param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class OperationVocabularyAttribute(string schemaId) : Attribute
{
    /// <summary>The schema id of the operation document relative to the product.</summary>
    public string SchemaId { get; } = schemaId;

    /// <summary>
    /// The source-generated <c>JsonSerializerContext</c> that lists every operation of the
    /// vocabulary with camelCase property names.
    /// </summary>
    public Type? JsonContext { get; set; }

    /// <summary>
    /// The wire names of the top-level operation fields that address a target, an anchor or a
    /// source, such as <c>page</c> or <c>path</c>: they are not state (C-R21), unless the member
    /// declares a read-back classification of its own.
    /// </summary>
    public string[]? Addresses { get; set; }

    /// <summary>
    /// The operations most documents use, most common first. An error for an unknown op lists
    /// them right after the names closest to it, so a long vocabulary cut to the first names an
    /// error lists still shows them.
    /// </summary>
    public string[]? Common { get; set; }
}

/// <summary>
/// Classifies an operation field for the read-back rule (C-R21): it is state the operation
/// writes, and a read result reports it at <paramref name="path"/>. Every field of an operation
/// is classified, by one of <see cref="ReadBackAttribute"/>, <see cref="NotReadBackAttribute"/>,
/// <see cref="ReadBackByPositionAttribute"/> or <see cref="NotStateAttribute"/>, or by the
/// vocabulary's <see cref="OperationVocabularyAttribute.Addresses"/>; a record field without one
/// is classified field by field in its record. The operation schema publishes the
/// classification of each field as <c>x-readBack</c>.
/// </summary>
/// <param name="path">
/// The schema of the property the read reports, as a URI reference relative to the product's
/// operation schema: the read result's schema file and a JSON Pointer into it, such as
/// <c>inspect-result.schema.json#/$defs/pdfPageInfo/properties/rotation</c>. A pointer that ends in a
/// dictionary's <c>additionalProperties</c>, such as
/// <c>inspect-result.schema.json#/properties/metadata/additionalProperties</c>, is the entry named
/// after the field. The operation schema publishes it resolved against its own <c>$id</c>, like
/// the absolute <c>$ref</c> of one schema to another.
/// </param>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ReadBackAttribute(string path) : Attribute
{
    /// <summary>The read path, published as the field's <c>x-readBack</c>.</summary>
    public string Path { get; } = path;
}

/// <summary>Classifies an operation field as state that no read result reports yet; published as <c>x-readBack: "unread"</c>.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class NotReadBackAttribute : Attribute;

/// <summary>
/// Classifies an operation field as state that a read result reports by position rather than
/// under a field of its own name, such as values written into cells; published as
/// <c>x-readBack: "position"</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ReadBackByPositionAttribute : Attribute;

/// <summary>
/// Classifies an operation field as not state: it selects a target, names a source or
/// parameterizes the action, such as a search pattern; published as <c>x-readBack: "not-state"</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class NotStateAttribute : Attribute;

/// <summary>The values of an operation field's <c>x-readBack</c> other than a read path.</summary>
public static class ReadBackKinds
{
    /// <summary>State that no read result reports yet.</summary>
    public const string Unread = "unread";

    /// <summary>State a read result reports by position.</summary>
    public const string Position = "position";

    /// <summary>Not state: a selector, a source or a parameter of the action.</summary>
    public const string NotState = "not-state";
}

/// <summary>
/// Marks a top-level partial record that the operations of several vocabularies take as a field,
/// such as <see cref="PageSizeInput"/>. The operation generator describes it once, where it is
/// declared, as the record's generated static <c>Contract</c>, and a vocabulary of another
/// assembly refers to that description; a field that holds it states its own read-back.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class OperationInputAttribute : Attribute;

/// <summary>Declares one operation of a vocabulary by its stable wire name.</summary>
/// <param name="name">The value of the operation's <c>op</c> discriminator.</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class OperationAttribute(string name) : Attribute
{
    /// <summary>The value of the operation's <c>op</c> discriminator.</summary>
    public string Name { get; } = name;

    /// <summary>
    /// What to do instead when an entry of this operation omits a required field or gives an
    /// unknown one, such as the operation that removes what this one sets; it leads the hint of
    /// the error. Null for none.
    /// </summary>
    public string? Hint { get; set; }
}

/// <summary>
/// Names the names a caller commonly writes for this member or operation, such as
/// <c>backgroundColor</c> for a fill color, or <c>set_style</c> for an operation that formats a
/// range. They are never accepted: an unknown field of one of these names suggests this member,
/// and an unknown op of one of these names this operation, ahead of the names that merely look
/// alike. Names compare ignoring case. On a record, only an operation may declare them.
/// </summary>
/// <param name="names">The mistaken field or operation names.</param>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Class, Inherited = false)]
public sealed class MistakenForAttribute(params string[] names) : Attribute
{
    /// <summary>The mistaken field or operation names.</summary>
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
    public sealed override void Describe(System.Text.Json.Nodes.JsonObject schema)
    {
        base.Describe(schema);
        Visible.Describe(schema);
    }
}

/// <summary>
/// Marks the operation that encrypts the document its edit publishes, with a password read only
/// from the environment variables its <see cref="SecretEnvAttribute"/> fields name, so the CLI can
/// tell how to protect an output another command wrote without a password.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class EncryptsOutputAttribute : Attribute;
