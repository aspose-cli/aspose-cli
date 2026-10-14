using System.Globalization;
using System.Text;

namespace Aspose.Cli.Sdk.Addressing;

/// <summary>
/// A name as one segment of a target address, such as the <c>Q3 plan</c> of
/// <c>section/Q3 plan</c>. Only the characters that would break the address are escaped, as
/// <c>%XX</c> of their UTF-8 bytes: a slash, a percent sign, a control character, and white
/// space at the start or end. Any other character, such as a CJK letter or an inner space, stays
/// as it is, so <c>Q3 / plan 100% </c> is written <c>Q3 %2F plan 100%25%20</c>. An empty name is
/// written <c>%00</c>, so the address always names something.
/// </summary>
public static class TargetName
{
    /// <summary>Escapes a name for one segment of a target address.</summary>
    /// <param name="name">The name as the document holds it.</param>
    /// <param name="alsoEscaped">
    /// Characters that separate the parts of the product's address, which the name must then not
    /// hold as they are either, such as the <c>!</c> between a sheet and a cell of <c>cell/Sales!A1</c>.
    /// </param>
    public static string Escape(string name, params ReadOnlySpan<char> alsoEscaped)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Length == 0)
        {
            return "%00";
        }

        int start = 0;
        while (start < name.Length && char.IsWhiteSpace(name[start]))
        {
            start++;
        }

        int end = name.Length;
        while (end > start && char.IsWhiteSpace(name[end - 1]))
        {
            end--;
        }

        var segment = new StringBuilder(name.Length);
        Span<byte> bytes = stackalloc byte[4];
        int index = 0;
        while (index < name.Length)
        {
            Rune.DecodeFromUtf16(name.AsSpan(index), out Rune rune, out int length);
            char character = name[index];
            bool breaks = index < start || index >= end
                || character is '/' or '%'
                || char.IsControl(character)
                || length == 1 && alsoEscaped.Contains(character);
            if (breaks)
            {
                int written = rune.EncodeToUtf8(bytes);
                foreach (byte value in bytes[..written])
                {
                    segment.Append(CultureInfo.InvariantCulture, $"%{value:X2}");
                }
            }
            else
            {
                segment.Append(name, index, length);
            }

            index += length;
        }

        return segment.ToString();
    }
}
