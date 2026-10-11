namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// The size of a page in points (72 per inch) as a read reports it: both dimensions, and the
/// preset they match in either orientation. An operation that sets a page size takes a preset
/// or both dimensions under the same names.
/// </summary>
[SchemaId("page-size")]
public sealed record PageSizeInfo
{
    /// <summary>The preset the dimensions match; omitted when they match none.</summary>
    [AllowedValues(typeof(PageSizePresets))]
    public string? Preset { get; init; }

    /// <summary>The page width in points.</summary>
    [ExclusiveMinimum(0)]
    public required double Width { get; init; }

    /// <summary>The page height in points.</summary>
    [ExclusiveMinimum(0)]
    public required double Height { get; init; }

    /// <summary>The size of a page <paramref name="width"/> by <paramref name="height"/> points, with the preset it matches.</summary>
    public static PageSizeInfo Of(double width, double height) => new()
    {
        Preset = PageSizePresets.Matching(width, height),
        Width = width,
        Height = height,
    };
}

/// <summary>
/// The page size presets every product names the same way, in lower case; a preset is portrait,
/// and a page of its dimensions turned on its side matches it too.
/// </summary>
public static class PageSizePresets
{
    public const string A3 = "a3";
    public const string A4 = "a4";
    public const string A5 = "a5";
    public const string Letter = "letter";
    public const string Legal = "legal";
    public const string Tabloid = "tabloid";

    // How far, in points, a dimension may be from a preset's and still match it.
    private const double Tolerance = 1;

    private static readonly (string Preset, double Width, double Height)[] Sizes =
    [
        (A3, 841.89, 1190.55),
        (A4, 595.28, 841.89),
        (A5, 419.53, 595.28),
        (Letter, 612, 792),
        (Legal, 612, 1008),
        (Tabloid, 792, 1224),
    ];

    /// <summary>The portrait width and height of <paramref name="preset"/>, in points.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="preset"/> is not a preset.</exception>
    public static (double Width, double Height) Dimensions(string preset)
    {
        foreach ((string name, double width, double height) in Sizes)
        {
            if (string.Equals(name, preset, StringComparison.Ordinal))
            {
                return (width, height);
            }
        }

        throw new ArgumentOutOfRangeException(nameof(preset), preset, "Not a page size preset.");
    }

    /// <summary>The preset a page <paramref name="width"/> by <paramref name="height"/> points matches in either orientation, or null.</summary>
    public static string? Matching(double width, double height)
    {
        double shorter = Math.Min(width, height);
        double longer = Math.Max(width, height);
        foreach ((string name, double presetWidth, double presetHeight) in Sizes)
        {
            if (Math.Abs(shorter - presetWidth) <= Tolerance && Math.Abs(longer - presetHeight) <= Tolerance)
            {
                return name;
            }
        }

        return null;
    }
}

/// <summary>
/// The styles of a page or list number every product names the same way: <c>decimal</c> (1, 2,
/// 3), upper and lower Roman numerals, upper and lower letters, or <c>none</c> for no number.
/// </summary>
public static class PageNumberStyles
{
    public const string Decimal = "decimal";
    public const string RomanUpper = "roman-upper";
    public const string RomanLower = "roman-lower";
    public const string LettersUpper = "letters-upper";
    public const string LettersLower = "letters-lower";
    public const string None = "none";
}

/// <summary>
/// Which formatting content brought in from another document keeps: its own
/// (<c>keep-source</c>) or that of the document it joins (<c>use-destination</c>).
/// </summary>
public static class FormattingModes
{
    public const string KeepSource = "keep-source";
    public const string UseDestination = "use-destination";
}
