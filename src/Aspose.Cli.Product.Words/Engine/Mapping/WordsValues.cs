using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Tables;
using Aspose.Words.Properties;

namespace Aspose.Cli.Product.Words.Engine.Mapping;

/// <summary>The value kinds of the Words table that the SDK's own do not cover.</summary>
internal static class WordsValues
{
    private const string DateTimeForm = "yyyy-MM-ddTHH:mm:ss";

    /// <summary>A date and time as the engine holds it, written yyyy-MM-ddTHH:mm:ss, without a time zone.</summary>
    internal static ValueKind<DateTime> DateTime { get; } = ValueKinds.Create<DateTime>(
        "date-time", "A date and time written yyyy-MM-ddTHH:mm:ss, such as 2026-03-01T09:30:00, without a time zone.",
        static () => new JsonObject { ["type"] = "string", ["pattern"] = "^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}$" },
        static value => JsonValue.Create(value.ToString(DateTimeForm, CultureInfo.InvariantCulture)),
        static value => value.ValueKind == JsonValueKind.String
            && System.DateTime.TryParseExact(value.GetString(), DateTimeForm, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed)
            ? parsed
            : throw ValueKinds.Expected("a date and time written yyyy-MM-ddTHH:mm:ss", value));

    /// <summary>
    /// A custom document property's value: a string, true or false, or a number, as the JSON writes
    /// it; a date the engine holds is written yyyy-MM-ddTHH:mm:ss.
    /// </summary>
    internal static ValueKind<object> PropertyValue { get; } = ValueKinds.Create<object>(
        "property-value", "A string, true or false, or a number; a date the engine holds is read as yyyy-MM-ddTHH:mm:ss.",
        static () => new JsonObject { ["type"] = new JsonArray("string", "boolean", "number") },
        static value => value switch
        {
            string text => JsonValue.Create(text),
            bool flag => JsonValue.Create(flag),
            int number => JsonValue.Create(number),
            double number => double.IsFinite(number) ? JsonValue.Create(number) : null,
            DateTime date => JsonValue.Create(date.ToString(DateTimeForm, CultureInfo.InvariantCulture)),
            null => null,
            _ => JsonValue.Create(Convert.ToString(value, CultureInfo.InvariantCulture)),
        },
        static value => value.ValueKind switch
        {
            JsonValueKind.String => value.GetString()!,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when value.TryGetInt32(out int number) => number,
            JsonValueKind.Number => value.GetDouble(),
            _ => throw ValueKinds.Expected("a string, true or false, or a number", value),
        });

    /// <summary>Adds a custom property with the engine's overload for the value's type.</summary>
    internal static DocumentProperty AddProperty(CustomDocumentProperties properties, string name, object value) => value switch
    {
        string text => properties.Add(name, text),
        bool flag => properties.Add(name, flag),
        int number => properties.Add(name, number),
        double number => properties.Add(name, number),
        _ => throw new InvalidOperationException($"A property value of {value.GetType().Name} reached the add."),
    };

    /// <summary>Rows of text, each a list of strings, such as the records of a mail merge.</summary>
    internal static ValueKind<IReadOnlyList<IReadOnlyList<string?>>> TextRows { get; } = ValueKinds.ListOf(ValueKinds.ListOf(ValueKinds.Text));
}
