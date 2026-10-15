using System.Globalization;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.Addressing;

/// <summary>
/// The parts one render run writes: the selection, every part by default, and at most
/// <see cref="MaximumParts"/> of them.
/// </summary>
public static class RenderSelection
{
    /// <summary>The most parts one render run writes, as many as a PDF preview shows.</summary>
    public const int MaximumParts = 20;

    /// <summary>
    /// Resolves a render selection, every part when <paramref name="range"/> is null, against
    /// the document's parts.
    /// </summary>
    /// <param name="range">The selected parts, or null for every part.</param>
    /// <param name="available">How many parts the document has.</param>
    /// <param name="notFound">The product's not-found code for a part past the count.</param>
    /// <param name="part">What one part is called, such as <c>page</c>; its selection option is <c>--{part}s</c>.</param>
    /// <exception cref="CliException">
    /// The not-found error for a part past the count; <c>OPTION_INVALID</c> when the selection holds
    /// more than <see cref="MaximumParts"/> parts.
    /// </exception>
    public static IReadOnlyList<int> Resolve(PageRange? range, int available, ErrorCode notFound, string part)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(part);
        IReadOnlyList<int> parts = (range ?? PageRange.All).Resolve(available, notFound, part);
        if (parts.Count > MaximumParts)
        {
            string option = "--" + part + "s";
            throw CliErrors.SelectionTooLarge(
                option,
                part,
                parts.Count,
                MaximumParts,
                string.Create(CultureInfo.InvariantCulture, $"Render at most {MaximumParts} {part}s per run, such as {option} 1-{MaximumParts}."));
        }

        return parts;
    }
}
