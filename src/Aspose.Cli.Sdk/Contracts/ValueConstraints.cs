using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// One rule of a contract value that JSON Schema can express, declared on a member of an
/// operation or result record. On an operation the same instance checks a value when a document
/// is prepared and writes itself into the published schema, so the two cannot disagree; on a
/// result it only writes itself into the schema. The contract generators re-create every
/// constraint declared on a contract property or record with the same arguments, at the level
/// it applies to.
/// </summary>
/// <remarks>
/// Each constraint applies to one kind of value, which the generators know from its SDK type.
/// An array constraint (<see cref="MinItemsAttribute"/>, <see cref="MaxItemsAttribute"/>)
/// applies exactly at its <see cref="Depth"/>; any other constraint applies at the first level
/// at or below its depth that is not an array or map, so <c>[HexColor]</c> on a list of
/// strings checks every item. A constraint that no level of its member can take is a compile
/// error (APCLI012, APCLI013).
/// </remarks>
public abstract class ValueConstraintAttribute : Attribute
{
    /// <summary>
    /// How many array or map levels below the property the constraint applies: 0 is the
    /// property itself, 1 its items, 2 the items of its items. The generators write the level
    /// they resolved.
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
    /// A non-null string, number, Boolean, list or JSON value, or for a rule over the members of
    /// a record the members the record sets.
    /// </param>
    public abstract string? Check(object value);

    /// <summary>Writes the constraint's keywords into the schema of a value it applies to.</summary>
    /// <param name="schema">The value's schema.</param>
    public abstract void Describe(JsonObject schema);

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
/// A value of any JSON kind, such as an <c>object</c> or <c>JsonElement</c> member, that must
/// be a JSON string, number, Boolean or null, such as one cell of a table of values.
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
    public override void Describe(JsonObject schema) =>
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
    public override void Describe(JsonObject schema) => schema["minLength"] = Length;
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
    public override void Describe(JsonObject schema) => schema["maxLength"] = Length;
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
    public override void Describe(JsonObject schema) => schema["minimum"] = Value;
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
    public override void Describe(JsonObject schema) => schema["maximum"] = Value;
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
    public override void Describe(JsonObject schema) => schema["exclusiveMinimum"] = Value;
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
    public override void Describe(JsonObject schema) => schema["minItems"] = Count;
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
    public override void Describe(JsonObject schema) => schema["maxItems"] = Count;
}

/// <summary>An array whose items are all distinct.</summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public sealed class UniqueItemsAttribute : ValueConstraintAttribute
{
    /// <inheritdoc />
    public override string? Check(object value)
    {
        object?[] items = [.. ((System.Collections.IEnumerable)value).Cast<object?>()];
        return items.Distinct().Count() == items.Length ? null : "must not repeat an item";
    }

    /// <inheritdoc />
    public override void Describe(JsonObject schema) => schema["uniqueItems"] = true;
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

    private readonly string _ecma262 = Ecma262(pattern);

    // Built on the first check: the contract records' attributes are created whenever the
    // product catalog starts, and most invocations check no value.
    private Regex? _regex;

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
        (_regex ??= new Regex(_ecma262, RegexOptions.ECMAScript, TimeSpan.FromSeconds(1))).IsMatch((string)value) ? null
        : Meaning is null ? $"must match the pattern {Pattern}"
        : $"{Meaning} (pattern {Pattern})";

    /// <inheritdoc />
    public override void Describe(JsonObject schema) => schema["pattern"] = Pattern;

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
/// names the public constants of a static class, which the generators expand to
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
    /// generators expand this form to the constants' values, so generated contracts
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

    /// <summary>The allowed values as messages spell them, in published order.</summary>
    public IEnumerable<string> Spelled => Values.Select(Spell);

    /// <inheritdoc />
    public override void Describe(JsonObject schema) =>
        schema["enum"] = new JsonArray([.. Values.Select(Json)]);
}
