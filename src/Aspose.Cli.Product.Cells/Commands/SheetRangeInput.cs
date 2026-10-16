using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Cells.Commands;

/// <summary>
/// Reads the <c>--range</c> option of read and render: A1 notation, optionally sheet-qualified,
/// or <c>all</c> for no window. A malformed range is a usage error of the option.
/// </summary>
internal static class SheetRangeInput
{
    /// <summary>The option name.</summary>
    public const string RangeOption = "--range";

    /// <summary>The parsed range, or null when the option is omitted or <c>all</c>.</summary>
    /// <param name="text">The option's value.</param>
    /// <param name="option">The option, <c>--range</c> unless another option takes A1 notation.</param>
    /// <exception cref="CliException"><c>OPTION_INVALID</c> when the value is not A1 notation.</exception>
    public static RangeSpec? Parse(string? text, string option = RangeOption)
    {
        // all is lowercase, the rule of the SDK's selection keyword (PageRange) for every option.
        if (text is null || string.Equals(text.Trim(), PageRange.AllText, StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            return A1.ParseRange(text);
        }
        catch (CliException ex) when (ex.Code == ErrorCodes.OpsInvalid)
        {
            throw CliErrors.OptionInvalid(
                option,
                ex.Details?["reason"]?.GetValue<string>() ?? $"'{text}' is not a range",
                "Use A1 notation such as B2:D10, a single cell such as C5, a sheet-qualified range such as Sales!A1:C10, or all.");
        }
    }

    /// <summary>
    /// The effective sheet and range of <c>--sheet</c> with <c>--range</c>: rejects a sheet named
    /// two different ways.
    /// </summary>
    public static (string? SheetName, RangeRef? Range) Resolve(string? sheet, string? rangeText)
    {
        RangeSpec? rangeSpec = Parse(rangeText);

        if (rangeSpec?.SheetName is { } rangeSheet && sheet is not null
            && !string.Equals(rangeSheet, sheet, StringComparison.Ordinal))
        {
            throw CliErrors.OptionInvalid(
                RangeOption,
                $"the range names sheet '{rangeSheet}' but --sheet names '{sheet}'",
                "Qualify the sheet in one place only.");
        }

        return (sheet ?? rangeSpec?.SheetName, rangeSpec?.Range);
    }
}
