using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Cells.Commands;

/// <summary>
/// Resolves the <c>--sheet</c> / <c>--range</c> option pair shared by read and
/// render: parses the range, rejects a sheet named two different ways, and
/// yields the effective sheet and range.
/// </summary>
internal static class SheetRangeInput
{
    public static (string? SheetName, RangeRef? Range) Resolve(string? sheet, string? rangeText)
    {
        RangeSpec? rangeSpec = rangeText is { } text ? A1.ParseRange(text) : null;

        if (rangeSpec?.SheetName is { } rangeSheet && sheet is not null
            && !string.Equals(rangeSheet, sheet, StringComparison.Ordinal))
        {
            throw CliErrors.OptionInvalid(
                "--range",
                $"the range names sheet '{rangeSheet}' but --sheet names '{sheet}'",
                "Qualify the sheet in one place only.");
        }

        return (sheet ?? rangeSpec?.SheetName, rangeSpec?.Range);
    }
}
