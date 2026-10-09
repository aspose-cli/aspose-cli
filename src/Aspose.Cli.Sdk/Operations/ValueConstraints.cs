using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.Operations;

/// <summary>
/// Base of the string value kinds that a parser reads, such as a page range. The schema states
/// the kind's pattern; the check runs the parser, whose reason for a rejection follows the
/// kind's expectation, and then the pattern, which may be narrower than the parser.
/// </summary>
/// <param name="expected">What the value must be, phrased to follow its field path.</param>
/// <param name="pattern">The pattern the schema publishes.</param>
/// <param name="definition">The shared <c>$defs</c> entry of the kind, or null to write it inline.</param>
/// <param name="description">The kind's own schema description, or null.</param>
public abstract class ValueKindAttribute(string expected, string pattern, string? definition = null, string? description = null)
    : ValueConstraintAttribute
{
    private readonly PatternAttribute _pattern = new(pattern);

    /// <inheritdoc />
    public sealed override string? Definition => definition;

    /// <inheritdoc />
    public sealed override string? Check(object value)
    {
        try
        {
            Parse((string)value);
        }
        catch (CliException rejection)
        {
            return $"{expected}: {rejection.Details?["reason"]?.GetValue<string>() ?? rejection.Message}";
        }

        return _pattern.Check(value) is null ? null : expected;
    }

    /// <inheritdoc />
    public sealed override void Describe(JsonObject schema)
    {
        if (description is not null)
        {
            schema["description"] = description;
        }

        _pattern.Describe(schema);
    }

    /// <summary>Parses the value; a <see cref="CliException"/> rejects it.</summary>
    protected abstract void Parse(string value);
}

/// <summary>A 1-based page range such as <c>1-3,7,9-</c>, the grammar of every page selection.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class PageRangeAttribute() : ValueKindAttribute(
    "must be a 1-based page range such as 1-3,7,9-",
    @"^\s*0*[1-9][0-9]*(-(0*[1-9][0-9]*)?)?(\s*,\s*0*[1-9][0-9]*(-(0*[1-9][0-9]*)?)?)*\s*$",
    "pages",
    "1-based pages and ranges separated by commas, such as 1-3,7,9-; a range with no end runs to the last page, and a range must not end before it starts.")
{
    /// <inheritdoc />
    protected override void Parse(string value) => _ = Addressing.PageRange.Parse(value);
}

/// <summary>A color written as <c>#RRGGBB</c>.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class HexColorAttribute : ValueConstraintAttribute
{
    private const string Pattern = "^#[0-9A-Fa-f]{6}$";

    /// <inheritdoc />
    public override string? Definition => "color";

    /// <inheritdoc />
    public override string? Check(object value) =>
        value is string { Length: 7 } text && text[0] == '#' && text.Skip(1).All(char.IsAsciiHexDigit)
            ? null
            : "must be a #RRGGBB color";

    /// <inheritdoc />
    public override void Describe(JsonObject schema) => schema["pattern"] = Pattern;
}

/// <summary>
/// A web link: an absolute <c>http://</c>, <c>https://</c> or <c>mailto:</c> URL, the one link
/// rule of every product. Relative links and every other scheme, such as <c>file:</c> and
/// <c>javascript:</c>, are rejected.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class WebLinkAttribute : ValueConstraintAttribute
{
    private static readonly string[] Prefixes = ["http://", "https://", "mailto:"];

    /// <inheritdoc />
    public override string? Definition => "webLink";

    /// <inheritdoc />
    public override string? Check(object value)
    {
        string text = (string)value;
        return Prefixes.Any(prefix => text.StartsWith(prefix, StringComparison.Ordinal))
            && Uri.TryCreate(text, UriKind.Absolute, out _)
                ? null
                : "must be an absolute URL that starts with http://, https:// or mailto:";
    }

    /// <inheritdoc />
    public override void Describe(JsonObject schema)
    {
        schema["description"] = "An absolute http://, https:// or mailto: URL.";
        // format is an annotation only, so the pattern states the prefixes the check requires.
        schema["format"] = "uri";
        schema["pattern"] = $"^({string.Join('|', Prefixes.Select(Regex.Escape))})";
    }
}

/// <summary>
/// Base of the rules over the members of a record, declared on the record. A member is set
/// when it is not null, and a Boolean member that is not nullable only when it is true. The
/// operation generator requires every member a rule counts to be nullable without a default,
/// or for a <see cref="MemberCountAttribute"/> a Boolean that defaults to false, so a member
/// is set exactly when the JSON has it, as the published schema sees it.
/// </summary>
public abstract class RecordRuleAttribute : ValueConstraintAttribute
{
    /// <inheritdoc />
    public sealed override string? Check(object value) => CheckSet((IReadOnlyDictionary<string, object>)value);

    /// <summary>A record rule needs the record's members; it is written by <see cref="Describe(JsonObject, OperationRecord)"/>.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public sealed override void Describe(JsonObject schema) =>
        throw new NotSupportedException($"{GetType().Name} describes a record; pass the record it is declared on.");

    /// <summary>Writes the rule's keywords into the schema of the record it is declared on.</summary>
    /// <param name="schema">The rule's own schema, one entry of the record's <c>allOf</c>.</param>
    /// <param name="record">The record, which names its members.</param>
    public abstract void Describe(JsonObject schema, OperationRecord record);

    /// <summary>Returns why a record that sets <paramref name="set"/> breaks the rule, or null when it holds.</summary>
    /// <param name="set">The members the record sets, by wire name, with their values.</param>
    protected abstract string? CheckSet(IReadOnlyDictionary<string, object> set);
}

/// <summary>Base of the record rules that count how many of the named members a record sets.</summary>
public abstract class MemberCountAttribute(string[] members) : RecordRuleAttribute
{
    /// <summary>The members counted, by wire name.</summary>
    public IReadOnlyList<string> Members { get; } = members;

    private protected int CountSet(IReadOnlyDictionary<string, object> set) => Members.Count(set.ContainsKey);

    /// <summary>One schema per member that holds when the member is set.</summary>
    private protected JsonArray Alternatives(OperationRecord record) =>
        [.. Members.Select(member =>
        {
            var set = new JsonObject { ["required"] = new JsonArray(member) };
            if (record.Properties.Any(property => property.Name == member
                && property.Value is { Kind: OperationValueKind.Boolean, Nullable: false }))
            {
                set["properties"] = new JsonObject { [member] = new JsonObject { ["const"] = true } };
            }

            return (JsonNode)set;
        })];
}

/// <summary>A record rule: exactly one of the named members is set.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class ExactlyOneOfAttribute(params string[] members) : MemberCountAttribute(members)
{
    /// <inheritdoc />
    protected override string? CheckSet(IReadOnlyDictionary<string, object> set) =>
        CountSet(set) == 1 ? null : $"must set exactly one of: {string.Join(", ", Members)}";

    /// <inheritdoc />
    public override void Describe(JsonObject schema, OperationRecord record) => schema["oneOf"] = Alternatives(record);
}

/// <summary>A record rule: at least one of the named members is set.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class AtLeastOneOfAttribute(params string[] members) : MemberCountAttribute(members)
{
    /// <inheritdoc />
    protected override string? CheckSet(IReadOnlyDictionary<string, object> set) =>
        CountSet(set) >= 1 ? null : $"must set at least one of: {string.Join(", ", Members)}";

    /// <inheritdoc />
    public override void Describe(JsonObject schema, OperationRecord record) => schema["anyOf"] = Alternatives(record);
}

/// <summary>
/// A record rule: the record sets at least <paramref name="count"/> of its members. It fits a
/// nested record only, because an operation's schema also counts its <c>op</c> and <c>id</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class MinPropertiesAttribute(int count) : RecordRuleAttribute
{
    /// <summary>The fewest members the record sets.</summary>
    public int Count { get; } = count;

    /// <inheritdoc />
    protected override string? CheckSet(IReadOnlyDictionary<string, object> set) =>
        set.Count >= Count ? null
        : Count == 1 ? "must set at least one member"
        : $"must set at least {Count} members";

    /// <inheritdoc />
    public override void Describe(JsonObject schema, OperationRecord record) => schema["minProperties"] = Count;
}

/// <summary>A record rule: when <paramref name="member"/> is set, every one of <paramref name="requiredMembers"/> is set too.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependentRequiredAttribute(string member, params string[] requiredMembers) : RecordRuleAttribute
{
    /// <summary>The member whose presence requires the others, by wire name.</summary>
    public string Member { get; } = member;

    /// <summary>The members it requires, by wire name.</summary>
    public IReadOnlyList<string> RequiredMembers { get; } = requiredMembers;

    /// <inheritdoc />
    protected override string? CheckSet(IReadOnlyDictionary<string, object> set) =>
        !set.ContainsKey(Member) || RequiredMembers.All(set.ContainsKey)
            ? null
            : $"must set {string.Join(", ", RequiredMembers)} when it sets {Member}";

    /// <inheritdoc />
    public override void Describe(JsonObject schema, OperationRecord record) =>
        schema["dependentRequired"] = new JsonObject { [Member] = new JsonArray([.. RequiredMembers.Select(static name => (JsonNode)name)]) };
}

/// <summary>
/// A record rule: <paramref name="member"/> is set exactly when <paramref name="condition"/>
/// equals <paramref name="value"/>, compared after omitted members take their defaults.
/// </summary>
/// <param name="member">The member that depends on the condition, by wire name.</param>
/// <param name="condition">The member whose value decides, by wire name.</param>
/// <param name="value">The string, number or Boolean constant that requires the member.</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class PresentWhenAttribute(string member, string condition, object value) : RecordRuleAttribute
{
    /// <summary>The member that depends on the condition, by wire name.</summary>
    public string Member { get; } = member;

    /// <summary>The member whose value decides, by wire name.</summary>
    public string Condition { get; } = condition;

    /// <summary>The value of <see cref="Condition"/> that requires <see cref="Member"/>.</summary>
    public object Value { get; } = value;

    /// <inheritdoc />
    protected override string? CheckSet(IReadOnlyDictionary<string, object> set)
    {
        bool required = set.TryGetValue(Condition, out object? actual) && Same(Value, actual);
        return set.ContainsKey(Member) == required ? null
            : required ? $"must set {Member} when {Condition} is {Spell(Value)}"
            : $"must not set {Member} unless {Condition} is {Spell(Value)}";
    }

    /// <inheritdoc />
    public override void Describe(JsonObject schema, OperationRecord record)
    {
        var condition = new JsonObject
        {
            ["properties"] = new JsonObject { [Condition] = new JsonObject { ["const"] = Json(Value) } },
        };

        // An omitted condition takes its default, so it meets the condition exactly when the
        // default is the value; otherwise the condition must be present to meet it.
        string? fallback = record.Properties.First(property => property.Name == Condition).Default;
        if (fallback is null || !JsonNode.DeepEquals(JsonNode.Parse(fallback), Json(Value)))
        {
            condition["required"] = new JsonArray(Condition);
        }

        schema["if"] = condition;
        schema["then"] = new JsonObject { ["required"] = new JsonArray(Member) };
        schema["else"] = new JsonObject { ["not"] = new JsonObject { ["required"] = new JsonArray(Member) } };
    }
}
