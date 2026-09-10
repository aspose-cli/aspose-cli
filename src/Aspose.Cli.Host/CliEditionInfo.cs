namespace Aspose.Cli.Host;

/// <summary>Identifies one statically composed CLI distribution.</summary>
public sealed record CliEditionInfo
{
    /// <summary>Creates a validated, immutable edition identity.</summary>
    public CliEditionInfo(string id, string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        if (!IsValidId(id))
        {
            throw new ArgumentException(
                "Edition ids must start with a lowercase letter and contain only lowercase letters, digits, or hyphens.",
                nameof(id));
        }

        if (!string.Equals(displayName, displayName.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Edition display names cannot have leading or trailing whitespace.",
                nameof(displayName));
        }

        Id = id;
        DisplayName = displayName;
    }

    /// <summary>Stable machine-readable edition id.</summary>
    public string Id { get; }

    /// <summary>Human-readable edition name.</summary>
    public string DisplayName { get; }

    private static bool IsValidId(string value)
    {
        if (value.Length == 0 || value[0] is < 'a' or > 'z')
        {
            return false;
        }

        return value.All(static character =>
            character is >= 'a' and <= 'z'
            or >= '0' and <= '9'
            or '-');
    }
}
