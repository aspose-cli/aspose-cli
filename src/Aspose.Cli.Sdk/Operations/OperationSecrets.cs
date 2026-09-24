namespace Aspose.Cli.Sdk.Operations;

/// <summary>
/// The one rule for secrets that operations name by environment variable. The edit command
/// reads each named variable once and passes only the values it found; an operation that
/// needs a missing or empty one fails alone, naming the variable, so a best-effort batch keeps
/// its other operations and a dry run still reports every outcome.
/// </summary>
public static class OperationSecrets
{
    /// <summary>The secret that <paramref name="variable"/> holds, or null when the operation names none.</summary>
    /// <param name="secrets">The secrets the edit command resolved, by variable name.</param>
    /// <param name="variable">The variable an operation field names, or null when the field was omitted.</param>
    /// <exception cref="OperationInvalidException">The variable is missing or empty.</exception>
    public static string? Resolve(IReadOnlyDictionary<string, string>? secrets, string? variable)
    {
        if (variable is null)
        {
            return null;
        }

        return secrets is not null && secrets.TryGetValue(variable, out string? value) && !string.IsNullOrEmpty(value)
            ? value
            : throw new OperationInvalidException(
                $"environment variable '{variable}' is missing or empty",
                $"Set {variable} to the secret before running the edit.");
    }
}
