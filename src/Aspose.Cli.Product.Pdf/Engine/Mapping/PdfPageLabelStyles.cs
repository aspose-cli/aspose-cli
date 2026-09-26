using Aspose.Cli.Sdk.Operations;
using Aspose.Pdf;

namespace Aspose.Cli.Product.Pdf.Engine.Mapping;

/// <summary>
/// The one mapping between page-label style names, which <c>set_page_labels</c> accepts and
/// inspect reports, and the engine's numbering styles.
/// </summary>
internal static class PdfPageLabelStyles
{
    private static readonly (string Style, NumberingStyle Value)[] Styles =
    [
        ("arabic", NumberingStyle.NumeralsArabic),
        ("roman-upper", NumberingStyle.NumeralsRomanUppercase),
        ("roman-lower", NumberingStyle.NumeralsRomanLowercase),
        ("letters-upper", NumberingStyle.LettersUppercase),
        ("letters-lower", NumberingStyle.LettersLowercase),
        ("none", NumberingStyle.None),
    ];

    /// <summary>The style name of an engine numbering style.</summary>
    internal static string ToStyle(NumberingStyle value)
    {
        foreach ((string style, NumberingStyle candidate) in Styles)
        {
            if (candidate == value)
            {
                return style;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(value), value, "Page-label style is missing from the mapping.");
    }

    /// <summary>The engine numbering style of a style name.</summary>
    internal static NumberingStyle FromStyle(string style)
    {
        foreach ((string name, NumberingStyle value) in Styles)
        {
            if (string.Equals(name, style, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        throw new OperationInvalidException($"Unknown page-label style '{style}'.");
    }
}
