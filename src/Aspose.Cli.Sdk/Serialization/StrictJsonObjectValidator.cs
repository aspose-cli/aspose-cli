using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aspose.Cli.Sdk.Serialization;

/// <summary>Rejects unknown fields recursively when a polymorphic converter selects a concrete type.</summary>
public static class StrictJsonObjectValidator
{
    /// <summary>Rejects unknown fields recursively for one selected contract type.</summary>
    public static void Validate(
        JsonElement element,
        Type type,
        JsonSerializerOptions options,
        string path,
        bool allowDiscriminator = false)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (element.ValueKind == JsonValueKind.Null || IsScalar(type))
        {
            return;
        }

        if (TryElementType(type, out Type? elementType) && element.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            foreach (JsonElement item in element.EnumerateArray())
            {
                Validate(item, elementType, options, $"{path}[{index++}]");
            }

            return;
        }

        if (TryDictionaryValueType(type, out Type? valueType) && element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                Validate(property.Value, valueType, options, $"{path}.{property.Name}");
            }

            return;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        Dictionary<string, Type> properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(static property => property.Name != "OpName"
                && property.GetCustomAttribute<JsonIgnoreAttribute>() is null)
            .ToDictionary(
                property => property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                    ?? options.PropertyNamingPolicy?.ConvertName(property.Name)
                    ?? property.Name,
                static property => property.PropertyType,
                StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (allowDiscriminator && property.NameEquals("op"))
            {
                continue;
            }

            if (!properties.TryGetValue(property.Name, out Type? propertyType))
            {
                throw new JsonException($"Unknown field '{property.Name}' at {path} for {type.Name}.");
            }

            Validate(property.Value, propertyType, options, $"{path}.{property.Name}");
        }
    }

    private static bool IsScalar(Type type) =>
        type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal)
        || type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(Guid)
        || type == typeof(JsonElement);

    private static bool TryElementType(Type type, out Type elementType)
    {
        if (type.IsArray)
        {
            elementType = type.GetElementType()!;
            return true;
        }

        Type? enumerable = type.GetInterfaces().Append(type)
            .FirstOrDefault(candidate => candidate.IsGenericType
                && candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        elementType = enumerable?.GetGenericArguments()[0] ?? typeof(object);
        return enumerable is not null;
    }

    private static bool TryDictionaryValueType(Type type, out Type valueType)
    {
        Type? dictionary = type.GetInterfaces().Append(type)
            .FirstOrDefault(candidate => candidate.IsGenericType
                && candidate.GetGenericTypeDefinition() is var definition
                && (definition == typeof(IDictionary<,>) || definition == typeof(IReadOnlyDictionary<,>))
                && candidate.GetGenericArguments()[0] == typeof(string));
        valueType = dictionary?.GetGenericArguments()[1] ?? typeof(object);
        return dictionary is not null;
    }
}
