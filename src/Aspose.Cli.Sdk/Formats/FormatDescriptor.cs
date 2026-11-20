namespace Aspose.Cli.Sdk.Formats;

/// <summary>How a product can use a declared file format.</summary>
[Flags]
public enum FormatUse
{
    /// <summary>The format is accepted as an input document.</summary>
    Input = 1,

    /// <summary>The format can be produced by a convert command.</summary>
    Convert = 2,

    /// <summary>The format can be produced by a render command.</summary>
    Render = 4,

    /// <summary>
    /// The format is accepted as an input document by the convert command only; every other
    /// command that opens a document refuses it.
    /// </summary>
    ConvertInput = 8,
}

/// <summary>
/// One immutable format declaration, from which a product's format lists, its <c>--to</c>
/// values and the formats document of <c>schema</c> are derived.
/// </summary>
public sealed record FormatDescriptor
{
    /// <summary>
    /// Creates a declaration listed, for each of its uses, in the order the product declares it.
    /// </summary>
    public static FormatDescriptor Declare(
        string id,
        FormatUse uses,
        params string[] extensions) =>
        new(id, uses, extensions);

    /// <summary>Creates one unconditional format declaration.</summary>
    /// <param name="id">Stable lower-case format identifier.</param>
    /// <param name="uses">Supported input and output uses.</param>
    /// <param name="extensions">Associated file extensions.</param>
    public FormatDescriptor(
        string id,
        FormatUse uses,
        params string[] extensions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(extensions);
        Id = id;
        Uses = uses;
        Extensions = extensions;
    }

    /// <summary>Stable lower-case format identifier.</summary>
    public string Id { get; init; }

    /// <summary>Input, convert, and render uses supported by the product.</summary>
    public FormatUse Uses { get; init; }

    /// <summary>
    /// File extensions associated with this format: an output may carry any of them, and an
    /// input format is read under any of them.
    /// </summary>
    public IReadOnlyList<string> Extensions { get; init; }

    /// <summary>Accepted command-line aliases in addition to the stable id.</summary>
    public IReadOnlyList<string> Aliases { get; init; } = [];

    /// <summary>
    /// Preferred extension for newly produced files. The first associated
    /// extension is used when this value is not specified.
    /// </summary>
    public string? OutputExtension { get; init; }

    /// <summary>The extension for newly produced files, or null when the format declares none.</summary>
    internal string? PreferredExtension => OutputExtension ?? Extensions.FirstOrDefault();

    /// <summary>Whether an output of this format can carry a password (<c>--encrypt</c>).</summary>
    public bool Protectable { get; init; }

    /// <summary>
    /// Whether a file of this format is an image, which shows a document rather than keeping a
    /// copy of it, so the write pipeline never discloses it as one that lost the encryption of
    /// its input. Every render format and every <see cref="Raster"/> format is an image; a
    /// product declares it for any other image format it only converts to.
    /// </summary>
    public bool Image
    {
        get => _image || Raster || Uses.HasFlag(FormatUse.Render);
        init => _image = value;
    }

    private readonly bool _image;

    /// <summary>
    /// Whether the format is a bitmap, whose pixel size a resolution sets, as png and jpeg are.
    /// Any other image, such as svg, is vector: a command refuses a resolution or a pixel size
    /// for it (<c>--dpi</c>).
    /// </summary>
    public bool Raster { get; init; }

    /// <summary>
    /// Whether the engine writes companion files, such as scripts and style sheets, beside an
    /// output of this format; the SDK publishes them with the named file as one set.
    /// </summary>
    public bool CompanionFiles { get; init; }

    /// <summary>
    /// Whether an output's extension alone chooses this format when no <c>--to</c> names one.
    /// A format that changes what the file keeps, as a PDF/A profile drops what the profile
    /// forbids, declares false: under an extension it shares, only <c>--to</c> chooses it.
    /// </summary>
    public bool ChosenByExtension { get; init; } = true;

    /// <summary>Whether a file name carries one of this format's extensions, ignoring case.</summary>
    internal bool Names(string path) =>
        Path.GetExtension(path) is { Length: > 1 } extension
        && Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Rank among advertised input formats; formats of one rank keep declaration order. A product
    /// sets it only when its input formats are listed in another order than they are declared.
    /// </summary>
    public int InputOrder { get; init; }

    /// <summary>Rank among advertised convert formats, as <see cref="InputOrder"/> is for input.</summary>
    public int ConvertOrder { get; init; }

    /// <summary>Rank among advertised render formats, as <see cref="InputOrder"/> is for input.</summary>
    public int RenderOrder { get; init; }

}

/// <summary>Deterministic projections over a product's format descriptors.</summary>
public static class FormatDescriptorExtensions
{
    /// <summary>Returns format ids for one use by rank, then in declaration order.</summary>
    public static IReadOnlyList<string> IdsFor(
        this IEnumerable<FormatDescriptor> descriptors,
        FormatUse use) =>
        Array.AsReadOnly(Ordered(descriptors, use)
            .Select(static format => format.Id)
            .ToArray());

    /// <summary>
    /// Returns the format for one use whose id or alias is <paramref name="name"/>, ignoring
    /// case, or null when none is.
    /// </summary>
    public static FormatDescriptor? Named(
        this IEnumerable<FormatDescriptor> descriptors,
        FormatUse use,
        string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return Ordered(descriptors, use).FirstOrDefault(format =>
            string.Equals(format.Id, name, StringComparison.OrdinalIgnoreCase)
            || format.Aliases.Contains(name, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns the formats of any use that declare a file extension such as <c>.htm</c>, in
    /// declaration order. Extensions compare case-insensitively.
    /// </summary>
    public static IReadOnlyList<FormatDescriptor> DeclaringExtension(
        this IEnumerable<FormatDescriptor> descriptors,
        string extension)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        ArgumentNullException.ThrowIfNull(extension);
        return Array.AsReadOnly(descriptors
            .Where(format => format.Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            .ToArray());
    }

    /// <summary>
    /// Returns the formats among <paramref name="descriptors"/> whose extensions the file name
    /// <paramref name="path"/> carries, ignoring case, in declaration order; none for a name
    /// without an extension. A product asks this rather than reading the extension itself.
    /// </summary>
    public static IReadOnlyList<FormatDescriptor> NamedBy(
        this IEnumerable<FormatDescriptor> descriptors,
        string path)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        ArgumentNullException.ThrowIfNull(path);
        return Array.AsReadOnly(descriptors.Where(format => format.Names(path)).ToArray());
    }

    private static IEnumerable<FormatDescriptor> Ordered(
        IEnumerable<FormatDescriptor> descriptors,
        FormatUse use)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        if (use is not (FormatUse.Input or FormatUse.Convert or FormatUse.Render))
        {
            throw new ArgumentOutOfRangeException(
                nameof(use),
                use,
                "Select exactly one format use.");
        }

        return descriptors
            .Where(format => format.Uses.HasFlag(use))
            .OrderBy(format => Order(format, use));
    }

    private static int Order(FormatDescriptor format, FormatUse use) =>
        use switch
        {
            FormatUse.Input => format.InputOrder,
            FormatUse.Convert => format.ConvertOrder,
            FormatUse.Render => format.RenderOrder,
            _ => throw new ArgumentOutOfRangeException(nameof(use)),
        };
}
