using System.Text.RegularExpressions;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>
/// Text that writes a number whose characters a number cell would not keep. Both a delimited text
/// input read as written and a <c>--set</c> value keep such a number as text.
/// </summary>
internal static partial class NumberText
{
    /// <summary>
    /// Whether <paramref name="text"/> writes a number with leading zeros, such as an id (007,
    /// $007, (007)), whose zeros a number drops.
    /// </summary>
    internal static bool HasLeadingZeros(string text) => LeadingZeros().IsMatch(text);

    // A zero followed by a digit after any leading spaces, currency symbols, parentheses and signs.
    [GeneratedRegex(@"^[\s(+\-\p{Sc}]*0\d", RegexOptions.CultureInvariant)]
    private static partial Regex LeadingZeros();
}
