using System.Globalization;
using System.Text.Json.Nodes;

namespace Aspose.Cli.Sdk.Errors;

public static partial class CliErrors
{
    /// <summary>
    /// A selection, given or by default, holds more parts than one command run takes, such as a
    /// render of 35 pages when it renders at most 20: a value out of range, so <c>OPTION_INVALID</c>
    /// for the selection option.
    /// </summary>
    /// <param name="option">The selection option, such as <c>--pages</c>.</param>
    /// <param name="part">What one part is called, such as <c>page</c>.</param>
    /// <param name="actual">How many parts the selection holds.</param>
    /// <param name="limit">The most parts one run takes.</param>
    public static CliException SelectionTooLarge(string option, string part, int actual, int limit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(option);
        ArgumentException.ThrowIfNullOrWhiteSpace(part);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(actual, limit);
        return CliException.Create(
            ErrorCodes.OptionInvalid,
            string.Create(CultureInfo.InvariantCulture, $"The selection holds {actual} {part}s; one run takes at most {limit}."),
            new JsonObject
            {
                ["option"] = option,
                ["limit"] = limit,
                ["actual"] = actual,
            });
    }
}
