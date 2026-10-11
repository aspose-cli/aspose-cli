using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.Addressing;

/// <summary>Parsed selection of 1-based parts: <c>all</c>, or numbers and ranges such as <c>1-3,7,9-</c>.</summary>
public sealed class PageRange
{
    private readonly IReadOnlyList<Segment> _segments;

    private PageRange(IReadOnlyList<Segment> segments, string text)
    {
        _segments = segments;
        Text = text;
    }

    /// <summary>Original normalized spelling.</summary>
    public string Text { get; }

    /// <summary>
    /// The highest number the selection names, or null when a range runs to the last part, so
    /// the parts it selects depend on how many there are.
    /// </summary>
    internal int? Last => _segments.All(static segment => segment.End is not null)
        ? _segments.Max(static segment => segment.End!.Value)
        : null;

    /// <summary>The selection of every part, <c>all</c>, spelled <c>1-</c>.</summary>
    public static PageRange All { get; } = new([new Segment(1, null)], "1-");

    /// <summary>How a selection of every part is written.</summary>
    public const string AllText = "all";

    /// <summary>
    /// Parses the selection of an operation field, which its schema has already checked; a
    /// trailing open end is allowed.
    /// </summary>
    /// <exception cref="CliException"><c>OPS_INVALID</c> when the text is not a selection.</exception>
    public static PageRange Parse(string text) =>
        TryParse(text, out PageRange? range, out string? reason)
            ? range
            : throw CliException.Create(
                ErrorCodes.OpsInvalid,
                $"Invalid selection '{text}': {reason}.",
                hint: Hint,
                details: new System.Text.Json.Nodes.JsonObject { ["reason"] = reason });

    /// <summary>Parses the value of a selection option, such as <c>--pages</c>.</summary>
    /// <param name="option">The option, such as <c>--pages</c>.</param>
    /// <param name="text">Its value.</param>
    /// <exception cref="CliException"><c>OPTION_INVALID</c> when the value is not a selection.</exception>
    public static PageRange ParseOption(string option, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(option);
        return TryParse(text, out PageRange? range, out string? reason)
            ? range
            : throw CliErrors.OptionInvalid(option, $"'{text}' is not a selection: {reason}", Hint);
    }

    private const string Hint = "Use 1-based numbers and ranges such as 1-3,7,9-, or all.";

    /// <summary>
    /// Parses a selection: <c>all</c>, or 1-based numbers and ranges such as <c>1-3,7,9-</c>,
    /// where a range with no end runs to the last part.
    /// </summary>
    /// <returns>Whether the text is a selection; otherwise <paramref name="reason"/> says why not.</returns>
    public static bool TryParse(
        string? text,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out PageRange? range,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out string? reason)
    {
        range = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            reason = "the selection is empty";
            return false;
        }

        if (string.Equals(text.Trim(), AllText, StringComparison.Ordinal))
        {
            range = All;
            reason = null;
            return true;
        }

        var segments = new List<Segment>();
        foreach (string raw in text.Split(',', StringSplitOptions.TrimEntries))
        {
            if (raw.Length == 0)
            {
                reason = "an empty segment was found";
                return false;
            }

            int dash = raw.IndexOf('-');
            if (dash < 0)
            {
                if (Positive(raw) is not int value)
                {
                    reason = NotPositive(raw);
                    return false;
                }

                segments.Add(new Segment(value, value));
                continue;
            }

            if (raw.IndexOf('-', dash + 1) >= 0 || dash == 0)
            {
                reason = $"'{raw}' is not a valid segment";
                return false;
            }

            if (Positive(raw[..dash]) is not int start)
            {
                reason = NotPositive(raw[..dash]);
                return false;
            }

            int? end = null;
            if (dash < raw.Length - 1)
            {
                end = Positive(raw[(dash + 1)..]);
                if (end is null)
                {
                    reason = NotPositive(raw[(dash + 1)..]);
                    return false;
                }
            }

            if (end < start)
            {
                reason = $"segment '{raw}' ends before it starts";
                return false;
            }

            segments.Add(new Segment(start, end));
        }

        range = new PageRange(segments, string.Join(",", segments.Select(static s =>
            s.End is null ? $"{s.Start}-" : s.End == s.Start ? s.Start.ToString() : $"{s.Start}-{s.End}")));
        reason = null;
        return true;
    }

    /// <summary>
    /// Spells 1-based numbers as the shortest range text, such as <c>2-4,7</c>, or returns
    /// null when there are none.
    /// </summary>
    public static string? Describe(IEnumerable<int> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        int[] sorted = values.Distinct().Order().ToArray();
        if (sorted.Length == 0)
        {
            return null;
        }

        if (sorted[0] < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(values), "Range values are 1-based.");
        }

        var segments = new List<string>();
        int start = sorted[0];
        int previous = start;
        foreach (int value in sorted.Skip(1).Append(int.MinValue))
        {
            if (value == previous + 1)
            {
                previous = value;
                continue;
            }

            segments.Add(start == previous
                ? start.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{start}-{previous}"));
            start = previous = value;
        }

        return string.Join(",", segments);
    }

    /// <summary>
    /// Resolves the range against the document's page count, sorted and deduplicated. A range
    /// that reaches past the count, including any range over none, is <c>PAGE_NOT_FOUND</c>.
    /// </summary>
    /// <param name="available">How many pages exist; zero when there are none.</param>
    public IReadOnlyList<int> Resolve(int available) =>
        Resolve(available, ErrorCodes.PageNotFound, "page");

    /// <summary>
    /// Resolves the range against the count of a product's numbered targets, such as slides
    /// or blocks, sorted and deduplicated. A range that reaches past the count, including any
    /// range over none, is the product's not-found error.
    /// </summary>
    /// <param name="available">How many targets exist; zero when there are none.</param>
    /// <param name="notFound">A code declared with <see cref="ErrorCode.NotFound"/>.</param>
    /// <param name="subject">What one target is called, e.g. <c>slide</c>.</param>
    public IReadOnlyList<int> Resolve(int available, ErrorCode notFound, string subject)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(available);
        var values = new SortedSet<int>();
        foreach (Segment segment in _segments)
        {
            int end = segment.End ?? available;
            if (available == 0 || segment.Start > available || end > available)
            {
                throw CliErrors.NotFoundAt(notFound, subject, Text, available);
            }

            for (int value = segment.Start; value <= end; value++)
            {
                values.Add(value);
            }
        }

        return values.ToArray();
    }

    private static int? Positive(string value) =>
        int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int parsed)
            && parsed >= 1
            ? parsed
            : null;

    private static string NotPositive(string value) => $"'{value}' is not a positive 1-based number";

    private sealed record Segment(int Start, int? End);
}
