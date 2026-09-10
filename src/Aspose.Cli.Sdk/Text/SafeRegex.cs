using System.Text.RegularExpressions;

namespace Aspose.Cli.Sdk.Text;

/// <summary>Creates every user-provided regular expression with a hard timeout.</summary>
public static class SafeRegex
{
    /// <summary>Maximum execution time allowed for one user-supplied expression.</summary>
    public static TimeSpan DefaultTimeout { get; } = TimeSpan.FromSeconds(1);

    /// <summary>Creates a culture-invariant regular expression with a hard timeout.</summary>
    public static Regex Create(string pattern, bool caseSensitive) => new(
        pattern,
        (caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase) | RegexOptions.CultureInvariant,
        DefaultTimeout);
}
