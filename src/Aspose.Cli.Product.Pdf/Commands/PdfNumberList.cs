using System.Globalization;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Pdf.Commands;

/// <summary>Parses an option's comma-separated list of numbers, such as <c>--margins</c> or <c>--rect</c>.</summary>
internal static class PdfNumberList
{
    /// <summary>
    /// Returns the numbers of <paramref name="text"/> in order, or refuses <paramref name="option"/>
    /// with <paramref name="hint"/> when an entry is not a number.
    /// </summary>
    public static double[] Parse(string text, string option, string hint)
    {
        string[] tokens = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var values = new double[tokens.Length];
        for (int index = 0; index < tokens.Length; index++)
        {
            if (!double.TryParse(tokens[index], NumberStyles.Float, CultureInfo.InvariantCulture, out values[index]))
            {
                throw CliErrors.OptionInvalid(option, $"'{text}' is not numeric", hint);
            }
        }

        return values;
    }
}
