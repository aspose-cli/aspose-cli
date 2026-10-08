using System.Text;

namespace Aspose.Cli.Sdk.IO;

/// <summary>
/// Immutable, product-neutral expression evaluated against one bounded file
/// prefix. Patterns never open files, invoke product code, or allocate work
/// outside the shared probe budget.
/// </summary>
public sealed class FileProbePattern
{
    private readonly PatternKind _kind;
    private readonly int _offset;
    private readonly byte[] _bytes;
    private readonly string[] _texts;
    private readonly IReadOnlyList<FileProbePattern> _children;

    private FileProbePattern(
        PatternKind kind,
        int offset = 0,
        IEnumerable<byte>? bytes = null,
        IEnumerable<string>? texts = null,
        IEnumerable<FileProbePattern>? children = null)
    {
        _kind = kind;
        _offset = offset;
        _bytes = bytes?.ToArray() ?? [];
        _texts = texts?.ToArray() ?? [];
        _children = Array.AsReadOnly(children?.ToArray() ?? []);
    }

    /// <summary>Matches an exact byte sequence at a bounded offset.</summary>
    public static FileProbePattern BytesAt(
        int offset,
        params byte[] bytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length == 0)
        {
            throw new ArgumentException(
                "A byte pattern cannot be empty.",
                nameof(bytes));
        }
        return new FileProbePattern(
            PatternKind.BytesAt,
            offset,
            bytes);
    }

    /// <summary>Matches an exact ASCII byte sequence at a bounded offset.</summary>
    public static FileProbePattern AsciiBytesAt(
        int offset,
        string value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentException.ThrowIfNullOrEmpty(value);
        return BytesAt(offset, Encoding.ASCII.GetBytes(value));
    }

    /// <summary>
    /// Matches a case-insensitive UTF-8 prefix after a BOM and leading
    /// whitespace are removed.
    /// </summary>
    public static FileProbePattern TextStartsIgnoringBomAndWhitespace(
        string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new FileProbePattern(
            PatternKind.TextStartsIgnoringBomAndWhitespace,
            texts: [value]);
    }

    /// <summary>Matches a case-insensitive UTF-8 substring.</summary>
    public static FileProbePattern TextContains(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        return new FileProbePattern(
            PatternKind.TextContains,
            texts: [value]);
    }

    /// <summary>
    /// Matches a non-empty bounded text prefix without NUL bytes or an
    /// excessive number of control bytes.
    /// </summary>
    public static FileProbePattern ValidText() =>
        new(PatternKind.ValidText);

    /// <summary>Matches only when every child pattern matches.</summary>
    public static FileProbePattern All(
        params FileProbePattern[] patterns) =>
        Composite(PatternKind.All, patterns);

    /// <summary>Matches when any child pattern matches.</summary>
    public static FileProbePattern Any(
        params FileProbePattern[] patterns) =>
        Composite(PatternKind.Any, patterns);

    /// <summary>
    /// Matches a ZIP prefix containing any declared package marker. A valid
    /// ZIP prefix without a visible marker is indeterminate rather than a
    /// negative match because the bounded prefix may not contain the entry.
    /// </summary>
    public static FileProbePattern ZipContainsAny(
        params string[] markers)
    {
        ArgumentNullException.ThrowIfNull(markers);
        if (markers.Length == 0
            || markers.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "At least one non-empty ZIP marker is required.",
                nameof(markers));
        }
        return new FileProbePattern(
            PatternKind.ZipContainsAny,
            texts: markers);
    }

    internal ProbePatternResult Evaluate(ReadOnlySpan<byte> bytes) =>
        _kind switch
        {
            PatternKind.BytesAt => Match(
                HasSlice(bytes, _offset, _bytes.Length)
                && bytes.Slice(_offset, _bytes.Length)
                    .SequenceEqual(_bytes)),
            PatternKind.TextStartsIgnoringBomAndWhitespace => Match(
                Decode(bytes)
                    .TrimStart('\uFEFF', ' ', '\t', '\r', '\n')
                    .StartsWith(
                        _texts[0],
                        StringComparison.OrdinalIgnoreCase)),
            PatternKind.TextContains => Match(
                Decode(bytes).Contains(
                    _texts[0],
                    StringComparison.OrdinalIgnoreCase)),
            PatternKind.ValidText => Match(LooksLikeText(bytes)),
            PatternKind.All => EvaluateAll(bytes),
            PatternKind.Any => EvaluateAny(bytes),
            PatternKind.ZipContainsAny => EvaluateZip(bytes),
            _ => throw new InvalidOperationException(
                $"Unsupported probe pattern '{_kind}'."),
        };

    internal FileProbePattern Snapshot() =>
        new(
            _kind,
            _offset,
            _bytes,
            _texts,
            _children.Select(static child => child.Snapshot()));

    private static FileProbePattern Composite(
        PatternKind kind,
        FileProbePattern[] patterns)
    {
        ArgumentNullException.ThrowIfNull(patterns);
        if (patterns.Length == 0
            || patterns.Any(static pattern => pattern is null))
        {
            throw new ArgumentException(
                "At least one non-null child pattern is required.",
                nameof(patterns));
        }
        return new FileProbePattern(kind, children: patterns);
    }

    private ProbePatternResult EvaluateAll(ReadOnlySpan<byte> bytes)
    {
        bool indeterminate = false;
        foreach (FileProbePattern child in _children)
        {
            ProbePatternResult result = child.Evaluate(bytes);
            if (result.Kind == ProbePatternKind.NoMatch)
            {
                return result;
            }
            indeterminate |= result.Kind == ProbePatternKind.Indeterminate;
        }
        return indeterminate
            ? ProbePatternResult.Indeterminate
            : ProbePatternResult.Match;
    }

    private ProbePatternResult EvaluateAny(ReadOnlySpan<byte> bytes)
    {
        bool indeterminate = false;
        foreach (FileProbePattern child in _children)
        {
            ProbePatternResult result = child.Evaluate(bytes);
            if (result.Kind == ProbePatternKind.Match)
            {
                return result;
            }
            indeterminate |= result.Kind == ProbePatternKind.Indeterminate;
        }
        return indeterminate
            ? ProbePatternResult.Indeterminate
            : ProbePatternResult.NoMatch;
    }

    private ProbePatternResult EvaluateZip(ReadOnlySpan<byte> bytes)
    {
        if (!IsZip(bytes))
        {
            return new ProbePatternResult(
                ProbePatternKind.NoMatch,
                "ZIP container signature not found");
        }
        string text = Decode(bytes);
        if (_texts.Any(marker =>
                text.Contains(
                    marker,
                    StringComparison.OrdinalIgnoreCase)))
        {
            return ProbePatternResult.Match;
        }
        return new ProbePatternResult(
            ProbePatternKind.Indeterminate,
            "ZIP container found but bounded package marker was unavailable");
    }

    private static ProbePatternResult Match(bool value) =>
        value
            ? ProbePatternResult.Match
            : ProbePatternResult.NoMatch;

    private static bool LooksLikeText(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return false;
        }
        int controls = 0;
        foreach (byte value in bytes)
        {
            if (value == 0)
            {
                return false;
            }
            if (value < 0x09 || value is > 0x0D and < 0x20)
            {
                controls++;
            }
        }
        return controls * 100 <= bytes.Length;
    }

    private static bool IsZip(ReadOnlySpan<byte> bytes) =>
        Starts(bytes, [0x50, 0x4B, 0x03, 0x04])
        || Starts(bytes, [0x50, 0x4B, 0x05, 0x06])
        || Starts(bytes, [0x50, 0x4B, 0x07, 0x08]);

    private static bool Starts(
        ReadOnlySpan<byte> bytes,
        ReadOnlySpan<byte> signature) =>
        bytes.Length >= signature.Length
        && bytes[..signature.Length].SequenceEqual(signature);

    private static bool HasSlice(
        ReadOnlySpan<byte> bytes,
        int offset,
        int length) =>
        offset <= bytes.Length
        && length <= bytes.Length - offset;

    private static string Decode(ReadOnlySpan<byte> bytes) =>
        Encoding.UTF8.GetString(bytes);

    private enum PatternKind
    {
        BytesAt,
        TextStartsIgnoringBomAndWhitespace,
        TextContains,
        ValidText,
        All,
        Any,
        ZipContainsAny,
    }
}

internal enum ProbePatternKind
{
    NoMatch,
    Match,
    Indeterminate,
}

internal readonly record struct ProbePatternResult(
    ProbePatternKind Kind,
    string? Evidence = null)
{
    internal static ProbePatternResult NoMatch { get; } =
        new(ProbePatternKind.NoMatch);

    internal static ProbePatternResult Match { get; } =
        new(ProbePatternKind.Match);

    internal static ProbePatternResult Indeterminate { get; } =
        new(ProbePatternKind.Indeterminate);
}
