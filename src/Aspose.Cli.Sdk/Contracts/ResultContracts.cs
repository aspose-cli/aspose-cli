namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// Publishes a record that is not a <see cref="ResultEnvelope"/> as a schema of its own, such as
/// a shared block that results reference. The id is relative to the record's owner: the SDK and
/// the host publish under <c>v2/common/</c>, a product under <c>v2/&lt;product&gt;/</c>. A result
/// states its id through its <see cref="ResultEnvelope"/> constructor instead.
/// </summary>
/// <param name="id">The relative schema id, such as <c>backup</c>.</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class SchemaIdAttribute(string id) : Attribute
{
    /// <summary>The relative schema id.</summary>
    public string Id { get; } = id;
}

/// <summary>
/// States the JSON type of a member that depends on another member of its record, the
/// discriminator: when the discriminator is one of <see cref="Values"/>, the member has
/// <see cref="Type"/>. The member's attributes together list one case per type, and their values
/// together must be exactly the discriminator's allowed values; the record's schema then accepts
/// exactly one case. For example a cell <c>{t, v}</c> declares
/// <c>[OneOfBy("t", "number", Type = "number")]</c> on <c>V</c>.
/// </summary>
/// <param name="discriminator">The member that decides, by wire name.</param>
/// <param name="values">The discriminator's values this case covers.</param>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public sealed class OneOfByAttribute(string discriminator, params string[] values) : Attribute
{
    /// <summary>The member that decides, by wire name.</summary>
    public string Discriminator { get; } = discriminator;

    /// <summary>The discriminator's values this case covers.</summary>
    public IReadOnlyList<string> Values { get; } = values;

    /// <summary>
    /// The JSON type the member has in this case: <c>string</c>, <c>number</c>,
    /// <c>integer</c> or <c>boolean</c>, and then it is present; <c>null</c>, the default, when
    /// it is omitted or null.
    /// </summary>
    public string Type { get; set; } = "null";
}

/// <summary>
/// Marks a string member whose <see cref="AllowedValuesAttribute"/> lists the values the CLI
/// names, while the engine may report others that match <see cref="Pattern"/>, such as a chart
/// type outside the vocabulary an operation accepts. The schema accepts either.
/// </summary>
/// <param name="pattern">The pattern every value outside the list matches.</param>
[AttributeUsage(AttributeTargets.Property)]
public sealed class OpenEnumAttribute(string pattern) : Attribute
{
    /// <summary>The pattern every value outside the list matches.</summary>
    public string Pattern { get; } = pattern;
}

/// <summary>
/// Requires members, by wire name, that are optional where they are declared but always present
/// here. On a result record it names inherited members, such as the <c>window</c> every read
/// returns; on a member that holds a record it names that record's members, such as the
/// <c>fingerprint</c> of an edited source.
/// </summary>
/// <param name="members">The wire names of the members that are always present.</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, Inherited = false)]
public sealed class AlwaysPresentAttribute(params string[] members) : Attribute
{
    /// <summary>The wire names of the members that are always present.</summary>
    public IReadOnlyList<string> Members { get; } = members;
}

/// <summary>
/// Implemented by generated code on every JSON context that lists a result record, so the
/// catalog reaches the records that the context's assembly describes without reflection.
/// </summary>
public interface IResultSchemaSource
{
    /// <summary>Every result record the assembly describes: published schemas, their bases and nested records.</summary>
    IReadOnlyList<ResultRecord> ResultRecords { get; }
}

/// <summary>The JSON kind of a result value.</summary>
public enum ResultValueKind
{
    /// <summary>A JSON string.</summary>
    String,

    /// <summary>A whole JSON number.</summary>
    Integer,

    /// <summary>Any JSON number.</summary>
    Number,

    /// <summary><c>true</c> or <c>false</c>.</summary>
    Boolean,

    /// <summary>An object with the members of a described record.</summary>
    Record,

    /// <summary>A JSON array of one item shape.</summary>
    Array,

    /// <summary>An object whose member values share one shape.</summary>
    Map,

    /// <summary>Any JSON object; the description gives its meaning.</summary>
    Object,

    /// <summary>Any JSON value; the description gives its meaning.</summary>
    Any,
}

/// <summary>The shape of one result value, as the contract generator extracted it from a record.</summary>
public sealed class ResultValue
{
    /// <summary>The JSON kind.</summary>
    public required ResultValueKind Kind { get; init; }

    /// <summary>Whether an array item or map value may be JSON null.</summary>
    public bool Nullable { get; init; }

    /// <summary>The shape of an array's items or a map's values.</summary>
    public ResultValue? Items { get; init; }

    /// <summary>The record type of a <see cref="ResultValueKind.Record"/> value.</summary>
    public Type? Record { get; init; }

    /// <summary>The JSON Schema format of a string, such as <c>date-time</c>, or null.</summary>
    public string? Format { get; init; }
}

/// <summary>One serialized member of a result record.</summary>
public sealed class ResultProperty
{
    /// <summary>The wire name.</summary>
    public required string Name { get; init; }

    /// <summary>The member's documentation summary, or null.</summary>
    public string? Description { get; init; }

    /// <summary>The member's value shape.</summary>
    public required ResultValue Value { get; init; }

    /// <summary>
    /// Whether every serialized record has the member: it is not nullable and not omitted when it
    /// has its default value.
    /// </summary>
    public bool Required { get; init; }

    /// <summary>The JSON literal of a read-only member's constant value, or null.</summary>
    public string? Const { get; init; }

    /// <summary>The member's position among the record's members, as its JSON property order states it.</summary>
    public int Order { get; init; }

    /// <summary>The member's declared value constraints, each at the level it applies to.</summary>
    public IReadOnlyList<ValueConstraintAttribute> Constraints { get; init; } = [];

    /// <summary>The pattern of the values outside the allowed ones (see <see cref="OpenEnumAttribute"/>), or null.</summary>
    public string? OpenPattern { get; init; }

    /// <summary>The cases of a member whose type another member decides (see <see cref="OneOfByAttribute"/>).</summary>
    public IReadOnlyList<OneOfByAttribute> Cases { get; init; } = [];

    /// <summary>The members of the held record that are always present here (see <see cref="AlwaysPresentAttribute"/>).</summary>
    public IReadOnlyList<string> AlwaysPresent { get; init; } = [];

    /// <summary>
    /// Whether the member is the record's extension data: its entries are written as members of
    /// the record, so the record accepts members it does not declare.
    /// </summary>
    public bool Extension { get; init; }
}

/// <summary>One result record: a published schema, a base it inherits members from, or a nested object.</summary>
public sealed class ResultRecord
{
    /// <summary>The record's CLR type.</summary>
    public required Type Type { get; init; }

    /// <summary>The record's documentation summary, or null.</summary>
    public string? Description { get; init; }

    /// <summary>The described record whose members come first, or null.</summary>
    public Type? Base { get; init; }

    /// <summary>The relative id the record is published under, or null for a record that is only referenced.</summary>
    public string? SchemaId { get; init; }

    /// <summary>The <c>schemaVersion</c> a published result states, or 0 when the record states none.</summary>
    public int SchemaVersion { get; init; }

    /// <summary>The record's own serialized members in declaration order; inherited members come from <see cref="Base"/>.</summary>
    public IReadOnlyList<ResultProperty> Properties { get; init; } = [];

    /// <summary>The inherited members that are always present on this record (see <see cref="AlwaysPresentAttribute"/>).</summary>
    public IReadOnlyList<string> AlwaysPresent { get; init; } = [];
}
