using System.Text.Json;

namespace Aspose.Cli.Sdk.Contracts;

/// <summary>Shared structural checks for bounded JSON inputs.</summary>
public static class BoundedJsonValidation
{
    /// <summary>
    /// Rejects duplicate object properties at every depth. Property names are
    /// compared exactly; schema or DTO validation remains responsible for
    /// rejecting unsupported casing and unknown names.
    /// </summary>
    public static void ValidateNoDuplicateProperties(
        JsonElement value,
        Func<string, Exception> errorFactory) =>
        ValidateNoDuplicateProperties(value, "$", errorFactory);

    internal static void ValidateNoDuplicateProperties(
        JsonElement value,
        string path,
        Func<string, Exception> errorFactory)
    {
        ArgumentNullException.ThrowIfNull(errorFactory);
        if (value.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            foreach (JsonElement item in value.EnumerateArray())
            {
                ValidateNoDuplicateProperties(
                    item,
                    $"{path}[{index}]",
                    errorFactory);
                index++;
            }

            return;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in value.EnumerateObject())
        {
            if (!names.Add(property.Name))
            {
                throw errorFactory(
                    $"{path} property '{property.Name}' is duplicated");
            }

            ValidateNoDuplicateProperties(
                property.Value,
                $"{path}.{property.Name}",
                errorFactory);
        }
    }
}
