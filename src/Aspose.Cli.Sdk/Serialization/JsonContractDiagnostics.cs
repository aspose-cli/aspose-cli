using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Aspose.Cli.Sdk.Serialization;

/// <summary>
/// Explains why a JSON value does not fit its contract type in the wire vocabulary of field
/// paths and JSON kinds, never CLR type names. It runs only after the serializer rejected the
/// value, so the serializer stays the one authority on what is accepted.
/// </summary>
internal static class JsonContractDiagnostics
{
    /// <summary>
    /// Returns the first field that does not fit the contract, or a reason that names the
    /// serializer's failure path when the value's shape is correct.
    /// </summary>
    /// <param name="value">The rejected JSON value.</param>
    /// <param name="type">The contract type the value was read as.</param>
    /// <param name="options">The options whose metadata describes the contract.</param>
    /// <param name="failurePath">The serializer's JSON path of the failure, if known.</param>
    public static string Explain(JsonElement value, Type type, JsonSerializerOptions options, string? failurePath)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(options);
        if (Find(value, type, options, string.Empty) is { } mismatch)
        {
            return mismatch;
        }

        string field = FieldPath(failurePath);
        return field.Length == 0 ? "the value does not match the documented contract" : $"'{field}' has an invalid value";
    }

    /// <summary>Converts a serializer path such as <c>$.style.color</c> into a field path.</summary>
    public static string FieldPath(string? jsonPath) =>
        jsonPath is null or "$" ? string.Empty
            : jsonPath.StartsWith("$.", StringComparison.Ordinal) ? jsonPath[2..]
            : jsonPath.StartsWith('$') ? jsonPath[1..]
            : jsonPath;

    private static string? Find(JsonElement value, Type type, JsonSerializerOptions options, string path)
    {
        if (!options.TryGetTypeInfo(type, out JsonTypeInfo? info))
        {
            return null;
        }

        return info.Kind switch
        {
            JsonTypeInfoKind.Object when info.PolymorphismOptions is null => FindInObject(value, info, options, path),
            JsonTypeInfoKind.Enumerable when info.ElementType is { } element => FindInArray(value, element, options, path),
            JsonTypeInfoKind.None => FindInScalar(value, type, path),
            _ => null,
        };
    }

    private static string? FindInObject(JsonElement value, JsonTypeInfo info, JsonSerializerOptions options, string path)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            return Expected(path, "an object");
        }

        StringComparer names = options.PropertyNameCaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        Dictionary<string, JsonPropertyInfo> properties = info.Properties.ToDictionary(static property => property.Name, names);
        bool strict = (info.UnmappedMemberHandling ?? options.UnmappedMemberHandling) == JsonUnmappedMemberHandling.Disallow;
        var seen = new HashSet<string>(names);
        foreach (JsonProperty member in value.EnumerateObject())
        {
            string field = Join(path, member.Name);
            if (!options.AllowDuplicateProperties && !seen.Add(member.Name))
            {
                return $"'{field}' is duplicated";
            }

            if (!properties.TryGetValue(member.Name, out JsonPropertyInfo? property))
            {
                if (strict)
                {
                    return $"unknown field '{field}'";
                }

                continue;
            }

            if (member.Value.ValueKind == JsonValueKind.Null)
            {
                if (!(property.AssociatedParameter?.IsNullable ?? property.IsSetNullable))
                {
                    return $"'{field}' must not be null";
                }

                continue;
            }

            if (Find(member.Value, property.PropertyType, options, field) is { } mismatch)
            {
                return mismatch;
            }
        }

        foreach (JsonPropertyInfo property in info.Properties)
        {
            if (property.IsRequired && !value.EnumerateObject().Any(member => names.Equals(member.Name, property.Name)))
            {
                return $"the required field '{Join(path, property.Name)}' is missing";
            }
        }

        return null;
    }

    private static string? FindInArray(JsonElement value, Type element, JsonSerializerOptions options, string path)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            return Expected(path, "an array");
        }

        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            if (Find(item, element, options, $"{path}[{index++}]") is { } mismatch)
            {
                return mismatch;
            }
        }

        return null;
    }

    private static string? FindInScalar(JsonElement value, Type type, string path)
    {
        Type? underlying = Nullable.GetUnderlyingType(type);
        if (value.ValueKind == JsonValueKind.Null)
        {
            return type.IsValueType && underlying is null ? $"'{path}' must not be null" : null;
        }

        Type scalar = underlying ?? type;
        return scalar == typeof(string) && value.ValueKind != JsonValueKind.String ? Expected(path, "a string")
            : scalar == typeof(bool) && value.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ? Expected(path, "true or false")
            : scalar == typeof(int) && !(value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _)) ? Expected(path, "a whole number")
            : scalar == typeof(long) && !(value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _)) ? Expected(path, "a whole number")
            : (scalar == typeof(double) || scalar == typeof(float) || scalar == typeof(decimal))
                && value.ValueKind != JsonValueKind.Number ? Expected(path, "a number")
            : null;
    }

    private static string Expected(string path, string kind) =>
        path.Length == 0 ? $"the document must be {kind}" : $"'{path}' must be {kind}";

    private static string Join(string path, string name) => path.Length == 0 ? name : $"{path}.{name}";
}
