using System.Globalization;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>One part a character-budgeted read returned, in reading order.</summary>
/// <param name="Number">The 1-based number of the part.</param>
/// <param name="Truncated">Whether the budget cut the part's content short.</param>
public readonly record struct ReadPart(int Number, bool Truncated);

/// <summary>Where the next read of a numbered selection resumes.</summary>
/// <param name="Parts">The unread parts as range text, such as <c>4-9,12</c>.</param>
/// <param name="MaxCharacters">The character budget the next read needs.</param>
public sealed record ReadContinuation(string Parts, int MaxCharacters)
{
    /// <summary>The <c>next</c> of a read's window: the unread parts under <paramref name="partsOption"/> and the character budget.</summary>
    /// <param name="partsOption">The option that selects the parts, such as <c>--pages</c>.</param>
    public IReadOnlyDictionary<string, string> Next(string partsOption) =>
        ResultWindow.Options((partsOption, Parts), (MaxCharactersOption.Name, MaxCharacters.ToString(CultureInfo.InvariantCulture)));

    /// <summary>The largest character budget a read accepts.</summary>
    internal const int MaximumCharacters = 10_000_000;

    /// <summary>
    /// Computes the unread remainder of a read over numbered parts, or null when the read
    /// covered the whole selection. A part the budget cut short is read again, so its tail
    /// is never skipped; when that part alone exceeded the budget, the next read doubles the
    /// budget up to <see cref="MaximumCharacters"/> so the chain always makes progress.
    /// </summary>
    /// <param name="selection">Every part the caller selected, in reading order.</param>
    /// <param name="returned">The parts this read returned, in reading order.</param>
    /// <param name="maxCharacters">The character budget of this read.</param>
    public static ReadContinuation? After(
        IReadOnlyList<int> selection,
        IReadOnlyList<ReadPart> returned,
        int maxCharacters)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(returned);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCharacters);
        if (returned.Count == 0)
        {
            return null;
        }

        ReadPart last = returned[^1];
        int budget = maxCharacters;
        bool rereadLast = last.Truncated;
        if (rereadLast && returned.Count == 1)
        {
            // The part alone exceeds the budget: re-reading it unchanged would loop.
            rereadLast = budget < MaximumCharacters;
            budget = (int)Math.Min((long)budget * 2, MaximumCharacters);
        }

        int position = IndexOf(selection, last.Number);
        int resume = rereadLast ? position : position + 1;
        return PageRange.Describe(selection.Skip(resume)) is { } parts
            ? new ReadContinuation(parts, budget)
            : null;
    }

    private static int IndexOf(IReadOnlyList<int> selection, int number)
    {
        for (int index = 0; index < selection.Count; index++)
        {
            if (selection[index] == number)
            {
                return index;
            }
        }

        throw new ArgumentException($"Part {number} is not in the selection.", nameof(selection));
    }
}
