using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.Operations;

/// <summary>
/// One rule of an operation contract that JSON Schema can express. The same instance checks
/// a value when a document is prepared and writes itself into the published schema, so the
/// two cannot disagree. The operation generator re-creates every constraint declared on a
/// contract property or record with the same arguments, at the level it applies to.
/// </summary>
/// <remarks>
/// Each constraint applies to one kind of value, which the operation generator knows from its
/// SDK type. An array constraint (<see cref="MinItemsAttribute"/>, <see cref="MaxItemsAttribute"/>)
/// applies exactly at its <see cref="Depth"/>; any other constraint applies at the first level
/// at or below its depth that is not an array or map, so <c>[HexColor]</c> on a list of
/// strings checks every item. A constraint that no level of its member can take is a compile
/// error (APCLI012).
/// </remarks>
public abstract class ValueConstraintAttribute : Attribute
{
    /// <summary>
    /// How many array or map levels below the property the constraint applies: 0 is the
    /// property itself, 1 its items, 2 the items of its items. The operation generator writes
    /// the level it resolved.
    /// </summary>
    public int Depth { get; set; }

    /// <summary>
    /// The shared <c>$defs</c> entry that holds this constraint, so the schema states a value
    /// kind such as <c>pages</c> once; null when the constraint is written inline.
    /// </summary>
    public virtual string? Definition => null;

    /// <summary>
    /// Returns why <paramref name="value"/> breaks the constraint, phrased to follow the
    /// value's field path (for example <c>must not be empty</c>), or null when it holds.
    /// </summary>
    /// <param name="value">
    /// A non-null string, number, Boolean, list or JSON value, or for a record rule the members
    /// the record sets (see <see cref="RecordRuleAttribute"/>).
    /// </param>
    public abstract string? Check(object value);

    /// <summary>
    /// The mistake <paramref name="value"/> makes when it names none of the values the constraint
    /// allows, with the closest ones; null for a constraint that does not list its values.
    /// </summary>
    /// <param name="value">A value <see cref="Check"/> rejected.</param>
    public virtual Mistake? MistakeOf(object value) => null;

    /// <summary>Writes the constraint's keywords into the schema of a value it applies to.</summary>
    /// <param name="schema">The value's schema.</param>
    /// <param name="value">The value's shape, which names a record's members.</param>
    public abstract void Describe(JsonObject schema, OperationValue value);

    private protected static double Number(object value) => Convert.ToDouble(value, CultureInfo.InvariantCulture);

    private protected static string Text(double value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Whether a value equals an allowed string, number or Boolean constant.</summary>
    private protected static bool Same(object allowed, object value) => allowed switch
    {
        string text => value is string actual && string.Equals(text, actual, StringComparison.Ordinal),
        bool flag => value is bool actual && flag == actual,
        _ => value is not (string or bool) && Number(allowed) == Number(value),
    };

    /// <summary>A string, number or Boolean constant as it reads in a message.</summary>
    private protected static string Spell(object value) => value switch
    {
        string text => text,
        bool flag => flag ? "true" : "false",
        _ => Text(Number(value)),
    };

    /// <summary>A string, number or Boolean constant as a JSON value.</summary>
    private protected static JsonNode Json(object value) => value switch
    {
        string text => JsonValue.Create(text),
        bool flag => JsonValue.Create(flag),
        _ => JsonValue.Create(Number(value)),
    };

    /// <summary>The length of a string in Unicode code points, as JSON Schema counts it.</summary>
    private protected static int CodePoints(object value) => ((string)value).EnumerateRunes().Count();

    /// <summary>The number of items in a list.</summary>
    private protected static int Items(object value) => ((System.Collections.IEnumerable)value).Cast<object?>().Count();
}

/// <summary>
/// A value of kind <see cref="OperationValueKind.Any"/> that must be a JSON string, number,
/// Boolean or null, such as one cell of a table of values.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class JsonScalarAttribute : ValueConstraintAttribute
{
    /// <inheritdoc />
    public override string? Check(object value) =>
        value switch
        {
            JsonElement element => element.ValueKind is JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.Undefined,
            string or bool => false,
            _ => !IsNumeric(value),
        }
            ? "must be a string, number, Boolean or null"
            : null;

    /// <inheritdoc />
    public override void Describe(JsonObject schema, OperationValue value) =>
        schema["type"] = new JsonArray("string", "number", "boolean", "null");

    private static bool IsNumeric(object value) =>
        value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;
}

/// <summary>The shortest allowed string in code points; a length of 1 means the string must not be empty.</summary>
[AttributeUsage(AttributeTargets.Property)]
public class MinLengthAttribute(int length) : ValueConstraintAttribute
{
    /// <summary>The shortest allowed length.</summary>
    public int Length { get; } = length;

    /// <inheritdoc />
    public override string? Check(object value) =>
        CodePoints(value) >= Length ? null
        : Length == 1 ? "must not be empty"
        : $"must be at least {Length} characters long";

    /// <inheritdoc />
    public override void Describe(JsonObject schema, OperationValue value) => schema["minLength"] = Length;
}

/// <summary>The longest allowed string in code points.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class MaxLengthAttribute(int length) : ValueConstraintAttribute
{
    /// <summary>The longest allowed length.</summary>
    public int Length { get; } = length;

    /// <inheritdoc />
    public override string? Check(object value) =>
        CodePoints(value) <= Length ? null : $"must be at most {Length} characters long";

    /// <inheritdoc />
    public override void Describe(JsonObject schema, OperationValue value) => schema["maxLength"] = Length;
}

/// <summary>The smallest allowed number.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class MinimumAttribute(double value) : ValueConstraintAttribute
{
    /// <summary>The smallest allowed number.</summary>
    public double Value { get; } = value;

    /// <inheritdoc />
    public override string? Check(object value) => Number(value) >= Value ? null : $"must be at least {Text(Value)}";

    /// <inheritdoc />
    public override void Describe(JsonObject schema, OperationValue value) => schema["minimum"] = Value;
}

/// <summary>The largest allowed number.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class MaximumAttribute(double value) : ValueConstraintAttribute
{
    /// <summary>The largest allowed number.</summary>
    public double Value { get; } = value;

    /// <inheritdoc />
    public override string? Check(object value) => Number(value) <= Value ? null : $"must be at most {Text(Value)}";

    /// <inheritdoc />
    public override void Describe(JsonObject schema, OperationValue value) => schema["maximum"] = Value;
}

/// <summary>A bound every allowed number must exceed.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ExclusiveMinimumAttribute(double value) : ValueConstraintAttribute
{
    /// <summary>The bound every allowed number exceeds.</summary>
    public double Value { get; } = value;

    /// <inheritdoc />
    public override string? Check(object value) => Number(value) > Value ? null : $"must be greater than {Text(Value)}";

    /// <inheritdoc />
    public override void Describe(JsonObject schema, OperationValue value) => schema["exclusiveMinimum"] = Value;
}

/// <summary>The fewest items an array may hold; 1 means it must not be empty.</summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public sealed class MinItemsAttribute(int count) : ValueConstraintAttribute
{
    /// <summary>The fewest allowed items.</summary>
    public int Count { get; } = count;

    /// <inheritdoc />
    public override string? Check(object value) =>
        Items(value) >= Count ? null
        : Count == 1 ? "must not be empty"
        : $"must contain at least {Count} items";

    /// <inheritdoc />
    public override void Describe(JsonObject schema, OperationValue value) => schema["minItems"] = Count;
}

/// <summary>The most items an array may hold.</summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public sealed class MaxItemsAttribute(int count) : ValueConstraintAttribute
{
    /// <summary>The most allowed items.</summary>
    public int Count { get; } = count;

    /// <inheritdoc />
    public override string? Check(object value) =>
        Items(value) <= Count ? null : $"must contain at most {Count} items";

    /// <inheritdoc />
    public override void Describe(JsonObject schema, OperationValue value) => schema["maxItems"] = Count;
}

/// <summary>
/// An ECMA-262 regular expression, as JSON Schema reads it, that every allowed string matches
/// somewhere; anchor it with <c>^</c> and <c>$</c> to match the whole string. The check runs
/// it with ECMAScript semantics and restores three ECMA-262 meanings that .NET's ECMAScript
/// mode lacks: <c>$</c> matches only at the very end, never before a final line break;
/// <c>\s</c> and <c>\S</c> include Unicode white space; and <c>.</c> excludes every line
/// terminator. <c>\S</c> inside a character class is rejected. A rejection states
/// <see cref="Meaning"/> when it is set, so the reason tells what the value stands for and where
/// it comes from rather than only the expression; the schema publishes the pattern either way.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class PatternAttribute(string pattern) : ValueConstraintAttribute
{
    // ECMA-262 WhiteSpace and LineTerminator, which \s matches.
    private const string Space = @"\t\n\v\f\r \u00A0\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000\uFEFF";
    private const string LineTerminators = @"\n\r\u2028\u2029";

    private readonly Regex _regex = new(Ecma262(pattern), RegexOptions.ECMAScript, TimeSpan.FromSeconds(1));

    /// <summary>The regular expression.</summary>
    public string Pattern { get; } = pattern;

    /// <summary>
    /// What a matching value is, phrased to follow the value's field path, such as
    /// <c>must be an identifier the inspect command reports, such as "a-1"</c>; null to state
    /// only the pattern. The rejection appends the pattern to it.
    /// </summary>
    public string? Meaning { get; set; }

    /// <inheritdoc />
    public override string? Check(object value) =>
        _regex.IsMatch((string)value) ? null
        : Meaning is null ? $"must match the pattern {Pattern}"
        : $"{Meaning} (pattern {Pattern})";

    /// <inheritdoc />
    public override void Describe(JsonObject schema, OperationValue value) => schema["pattern"] = Pattern;

    /// <summary>Rewrites the ECMA-262 constructs whose .NET ECMAScript meaning differs.</summary>
    private static string Ecma262(string pattern)
    {
        var result = new System.Text.StringBuilder(pattern.Length);
        bool escaped = false;
        bool inClass = false;
        foreach (char character in pattern)
        {
            if (escaped)
            {
                escaped = false;
                if (character is 's' or 'S')
                {
                    // The escape's backslash is already written; replace it with the class.
                    result.Length--;
                    result.Append((character, inClass) switch
                    {
                        ('s', true) => Space,
                        ('s', false) => $"[{Space}]",
                        ('S', false) => $"[^{Space}]",
                        _ => throw new ArgumentException(@"\S inside a character class is not supported.", nameof(pattern)),
                    });
                    continue;
                }
            }
            else if (!inClass && character is '$' or '.')
            {
                result.Append(character == '$' ? @"(?![\s\S])" : $"[^{LineTerminators}]");
                continue;
            }
            else if (character == '\\')
            {
                escaped = true;
            }
            else
            {
                inClass = character switch { '[' => true, ']' => false, _ => inClass };
            }

            result.Append(character);
        }

        return result.ToString();
    }
}

/// <summary>
/// The only allowed values: string, number or Boolean constants. <c>typeof(SomeValues)</c>
/// names the public constants of a static class, which the operation generator expands to
/// their values in declaration order.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class AllowedValuesAttribute : ValueConstraintAttribute
{
    /// <summary>Allows exactly <paramref name="values"/>, in published order.</summary>
    public AllowedValuesAttribute(params object[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        Values = Array.AsReadOnly(values);
    }

    /// <summary>
    /// Allows the public constants of <paramref name="constants"/> in declaration order. The
    /// operation generator expands this form to the constants' values, so generated contracts
    /// never read the type at run time.
    /// </summary>
    public AllowedValuesAttribute([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] Type constants)
        : this([.. (constants ?? throw new ArgumentNullException(nameof(constants)))
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(static field => field.IsLiteral)
            .Select(static field => field.GetRawConstantValue()!)])
    {
    }

    /// <summary>The allowed values in published order.</summary>
    public IReadOnlyList<object> Values { get; }

    /// <summary>The allowed values as a message lists them, such as <c>light, dark</c>.</summary>
    public string Listed => string.Join(", ", Values.Select(Spell));

    /// <inheritdoc />
    public override string? Check(object value) =>
        Values.Any(allowed => Same(allowed, value)) ? null : $"must be one of: {Listed}";

    /// <inheritdoc />
    public override Mistake? MistakeOf(object value) =>
        value is string text ? Mistake.Of(text, Values.Select(Spell)) : null;

    /// <inheritdoc />
    public override void Describe(JsonObject schema, OperationValue value) =>
        schema["enum"] = new JsonArray([.. Values.Select(Json)]);
}

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
    public sealed override void Describe(JsonObject schema, OperationValue value)
    {
        if (description is not null)
        {
            schema["description"] = description;
        }

        _pattern.Describe(schema, value);
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
    public override void Describe(JsonObject schema, OperationValue value) => schema["pattern"] = Pattern;
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
    public override void Describe(JsonObject schema, OperationValue value)
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
    private protected JsonArray Alternatives(OperationValue value) =>
        [.. Members.Select(member =>
        {
            var set = new JsonObject { ["required"] = new JsonArray(member) };
            if (value.Record!.Properties.Any(property => property.Name == member
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
    public override void Describe(JsonObject schema, OperationValue value) => schema["oneOf"] = Alternatives(value);
}

/// <summary>A record rule: at least one of the named members is set.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class AtLeastOneOfAttribute(params string[] members) : MemberCountAttribute(members)
{
    /// <inheritdoc />
    protected override string? CheckSet(IReadOnlyDictionary<string, object> set) =>
        CountSet(set) >= 1 ? null : $"must set at least one of: {string.Join(", ", Members)}";

    /// <inheritdoc />
    public override void Describe(JsonObject schema, OperationValue value) => schema["anyOf"] = Alternatives(value);
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
    public override void Describe(JsonObject schema, OperationValue value) => schema["minProperties"] = Count;
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
    public override void Describe(JsonObject schema, OperationValue value) =>
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
    public override void Describe(JsonObject schema, OperationValue value)
    {
        var condition = new JsonObject
        {
            ["properties"] = new JsonObject { [Condition] = new JsonObject { ["const"] = Json(Value) } },
        };

        // An omitted condition takes its default, so it meets the condition exactly when the
        // default is the value; otherwise the condition must be present to meet it.
        string? fallback = value.Record!.Properties.First(property => property.Name == Condition).Default;
        if (fallback is null || !JsonNode.DeepEquals(JsonNode.Parse(fallback), Json(Value)))
        {
            condition["required"] = new JsonArray(Condition);
        }

        schema["if"] = condition;
        schema["then"] = new JsonObject { ["required"] = new JsonArray(Member) };
        schema["else"] = new JsonObject { ["not"] = new JsonObject { ["required"] = new JsonArray(Member) } };
    }
}
