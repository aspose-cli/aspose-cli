using System.Text.RegularExpressions;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>
/// Text that writes a number whose characters a number cell would not keep. A delimited text
/// input read as written, a <c>--set</c> value and a JSON number of an ops document keep such a
/// number as text.
/// </summary>
internal static partial class NumberText
{
    /// <summary>
    /// Whether <paramref name="text"/> writes a number with leading zeros, such as an id (007,
    /// $007, (007)), whose zeros a number drops.
    /// </summary>
    internal static bool HasLeadingZeros(string text) => LeadingZeros().IsMatch(text);

    /// <summary>
    /// Whether a number cell would drop some of the characters <paramref name="number"/> writes:
    /// leading zeros, a leading <c>+</c>, or the digits of an integer part of more than 15, since a
    /// number keeps 15 significant digits.
    /// </summary>
    internal static bool DropsCharacters(string number)
    {
        string trimmed = number.Trim();
        return HasLeadingZeros(trimmed) || trimmed.StartsWith('+')
            || trimmed.TrimStart('-').TakeWhile(char.IsAsciiDigit).Count() > 15;
    }

    // A zero followed by a digit after any leading spaces, currency symbols, parentheses and signs.
    [GeneratedRegex(@"^[\s(+\-\p{Sc}]*0\d", RegexOptions.CultureInvariant)]
    private static partial Regex LeadingZeros();
}
