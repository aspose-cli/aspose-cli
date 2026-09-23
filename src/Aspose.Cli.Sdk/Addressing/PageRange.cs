using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.Addressing;

/// <summary>Parsed 1-based range such as <c>1-3,7,9-</c>.</summary>
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

    /// <summary>Parses a range; a trailing open end is allowed.</summary>
    public static PageRange Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw Invalid(text ?? string.Empty, "the range is empty");
        }

        var segments = new List<Segment>();
        foreach (string raw in text.Split(',', StringSplitOptions.TrimEntries))
        {
            if (raw.Length == 0)
            {
                throw Invalid(text, "an empty segment was found");
            }

            int dash = raw.IndexOf('-');
            if (dash < 0)
            {
                int value = Positive(raw, text);
                segments.Add(new Segment(value, value));
                continue;
            }

            if (raw.IndexOf('-', dash + 1) >= 0 || dash == 0)
            {
                throw Invalid(text, $"'{raw}' is not a valid segment");
            }

            int start = Positive(raw[..dash], text);
            int? end = dash == raw.Length - 1 ? null : Positive(raw[(dash + 1)..], text);
            if (end is not null && end < start)
            {
                throw Invalid(text, $"segment '{raw}' ends before it starts");
            }

            segments.Add(new Segment(start, end));
        }

        return new PageRange(segments, string.Join(",", segments.Select(static s =>
            s.End is null ? $"{s.Start}-" : s.End == s.Start ? s.Start.ToString() : $"{s.Start}-{s.End}")));
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

    /// <summary>Resolves the range against the available count, sorted and deduplicated.</summary>
    public IReadOnlyList<int> Resolve(int available)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(available);
        var values = new SortedSet<int>();
        foreach (Segment segment in _segments)
        {
            int end = segment.End ?? available;
            if (segment.Start > available || end > available)
            {
                throw new CliException(
                    ErrorCodes.PageNotFound,
                    $"Requested item range '{Text}' exceeds the available count of {available}.",
                    hint: $"Use values from 1 through {available}.",
                    details: new System.Text.Json.Nodes.JsonObject { ["available"] = available, ["range"] = Text });
            }

            for (int value = segment.Start; value <= end; value++)
            {
                values.Add(value);
            }
        }

        return values.ToArray();
    }

    private static int Positive(string value, string whole)
    {
        if (!int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int parsed)
            || parsed < 1)
        {
            throw Invalid(whole, $"'{value}' is not a positive 1-based number");
        }

        return parsed;
    }

    private static CliException Invalid(string text, string reason) => new(
        ErrorCodes.PageRangeInvalid,
        $"Invalid page range '{text}': {reason}.",
        hint: "Use 1-based ranges such as '1-3,7,9-'.",
        details: new System.Text.Json.Nodes.JsonObject { ["range"] = text, ["reason"] = reason });

    private sealed record Segment(int Start, int? End);
}
