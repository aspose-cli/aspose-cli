using System.Globalization;
using System.Text.Json.Nodes;

namespace Aspose.Cli.Sdk.Errors;

public static partial class CliErrors
{
    /// <summary>
    /// A selection, given or by default, holds more parts than one command run takes, such as a
    /// render of 35 pages when it renders at most 20.
    /// </summary>
    /// <param name="option">The selection option, such as <c>--pages</c>.</param>
    /// <param name="part">What one part is called, such as <c>page</c>.</param>
    /// <param name="actual">How many parts the selection holds.</param>
    /// <param name="limit">The most parts one run takes.</param>
    /// <param name="hint">How to select fewer parts with <paramref name="option"/>.</param>
    public static CliException SelectionTooLarge(string option, string part, int actual, int limit, string hint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(option);
        ArgumentException.ThrowIfNullOrWhiteSpace(part);
        ArgumentException.ThrowIfNullOrWhiteSpace(hint);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(actual, limit);
        return CliException.Create(
            ErrorCodes.UsageError,
            string.Create(CultureInfo.InvariantCulture, $"The selection holds {actual} {part}s; one run takes at most {limit}."),
            hint,
            new JsonObject
            {
                ["option"] = option,
                ["limit"] = limit,
                ["actual"] = actual,
            });
    }
}
