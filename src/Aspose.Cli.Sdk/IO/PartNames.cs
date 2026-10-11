using System.Globalization;

namespace Aspose.Cli.Sdk.IO;

/// <summary>The one naming of the files of an output written in parts.</summary>
internal static class PartNames
{
    /// <summary>
    /// The labels of the parts numbered <paramref name="numbers"/>, each padded with zeros to the
    /// digits of the highest number: pages 9 to 12 are <c>09</c> to <c>12</c>.
    /// </summary>
    internal static IReadOnlyList<string> Numbered(IReadOnlyList<int> numbers)
    {
        ArgumentNullException.ThrowIfNull(numbers);
        if (numbers.Count == 0)
        {
            return [];
        }

        foreach (int number in numbers)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(number, nameof(numbers));
        }

        int digits = numbers.Max().ToString(CultureInfo.InvariantCulture).Length;
        return [.. numbers.Select(number => number.ToString(CultureInfo.InvariantCulture).PadLeft(digits, '0'))];
    }

    /// <summary>
    /// A file name stem with the characters no file name takes replaced by <c>-</c>, trimmed,
    /// and <c>untitled</c> when nothing is left.
    /// </summary>
    internal static string SafeStem(string? stem)
    {
        char[] invalid = System.IO.Path.GetInvalidFileNameChars();
        string sanitized = new((stem ?? string.Empty).Select(character =>
            invalid.Contains(character) || char.IsControl(character) ? '-' : character).ToArray());
        sanitized = sanitized.Trim().TrimEnd('.');
        return string.IsNullOrWhiteSpace(sanitized) ? "untitled" : sanitized;
    }
}
