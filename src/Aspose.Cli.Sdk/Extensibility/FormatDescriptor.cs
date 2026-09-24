namespace Aspose.Cli.Sdk.Extensibility;

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
}

/// <summary>How a format participates in generic file routing.</summary>
public enum RouteOwnership
{
    /// <summary>The format is available only through an explicit product command.</summary>
    Explicit = 0,

    /// <summary>The product is the default generic-routing owner.</summary>
    Default = 1,
}

/// <summary>
/// One immutable format declaration from which routing and public capability
/// metadata are derived.
/// </summary>
public sealed record FormatDescriptor
{
    /// <summary>
    /// Creates a fully ordered declaration for formats that participate in
    /// several input and output uses.
    /// </summary>
    public static FormatDescriptor Declare(
        string id,
        FormatUse uses,
        int? inputOrder,
        int? convertOrder,
        int? renderOrder,
        bool defaultOwner,
        params string[] extensions) =>
        new(id, uses, extensions)
        {
            Ownership = defaultOwner
                ? RouteOwnership.Default
                : RouteOwnership.Explicit,
            InputOrder = inputOrder ?? int.MaxValue,
            ConvertOrder = convertOrder ?? int.MaxValue,
            RenderOrder = renderOrder ?? int.MaxValue,
        };

    /// <summary>
    /// Creates a fully ordered declaration whose input formats own their
    /// generic routes by default.
    /// </summary>
    public static FormatDescriptor Routed(
        string id,
        FormatUse uses,
        int? inputOrder,
        int? convertOrder,
        int? renderOrder,
        params string[] extensions) =>
        Declare(
            id,
            uses,
            inputOrder,
            convertOrder,
            renderOrder,
            uses.HasFlag(FormatUse.Input),
            extensions);

    /// <summary>Creates an explicitly routed input format.</summary>
    public static FormatDescriptor Input(
        string id,
        int inputOrder,
        params string[] extensions) =>
        Input(
            id,
            inputOrder,
            RouteOwnership.Explicit,
            extensions);

    /// <summary>Creates an input format with explicit routing ownership.</summary>
    public static FormatDescriptor Input(
        string id,
        int inputOrder,
        RouteOwnership ownership,
        params string[] extensions) =>
        new(id, FormatUse.Input, extensions)
        {
            Ownership = ownership,
            InputOrder = inputOrder,
        };

    /// <summary>Creates a conversion-only output format.</summary>
    public static FormatDescriptor Output(
        string id,
        int convertOrder,
        params string[] extensions) =>
        new(id, FormatUse.Convert, extensions)
        {
            ConvertOrder = convertOrder,
        };

    /// <summary>Creates a rendering-only output format.</summary>
    public static FormatDescriptor Render(
        string id,
        int renderOrder,
        params string[] extensions) =>
        new(id, FormatUse.Render, extensions)
        {
            RenderOrder = renderOrder,
        };

    /// <summary>Creates a format supported by conversion and rendering.</summary>
    public static FormatDescriptor Output(
        string id,
        int convertOrder,
        int renderOrder,
        params string[] extensions) =>
        new(id, FormatUse.Convert | FormatUse.Render, extensions)
        {
            ConvertOrder = convertOrder,
            RenderOrder = renderOrder,
        };

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

    /// <summary>File extensions associated with this format.</summary>
    public IReadOnlyList<string> Extensions { get; init; }

    /// <summary>Accepted command-line aliases in addition to the stable id.</summary>
    public IReadOnlyList<string> Aliases { get; init; } = [];

    /// <summary>
    /// Preferred extension for newly produced files. The first associated
    /// extension is used when this value is not specified.
    /// </summary>
    public string? OutputExtension { get; init; }

    /// <summary>Generic-routing ownership for input extensions.</summary>
    public RouteOwnership Ownership { get; init; } = RouteOwnership.Explicit;

    /// <summary>
    /// Generic operations for which this input declaration is eligible.
    /// Empty means the standard input operations (<c>open</c>, <c>app</c>,
    /// <c>fonts</c>, <c>preview</c>, and <c>review</c>).
    /// </summary>
    public IReadOnlyList<string> Operations { get; init; } = [];

    /// <summary>Stable ordering among advertised input formats.</summary>
    public int InputOrder { get; init; } = int.MaxValue;

    /// <summary>Stable ordering among advertised convert formats.</summary>
    public int ConvertOrder { get; init; } = int.MaxValue;

    /// <summary>Stable ordering among advertised render formats.</summary>
    public int RenderOrder { get; init; } = int.MaxValue;

    /// <summary>Optional content recognizer for ambiguous or renamed inputs.</summary>
    public IFileRecognizer? Recognizer { get; init; }

    /// <summary>
    /// Product-owned declarative recognition rules for this default input
    /// format. The SDK evaluates the immutable rules within the shared probe
    /// budget without knowing the format's semantics.
    /// </summary>
    public FileFormatRecognition? Recognition { get; init; }

}

internal static class StandardFileRouteOperations
{
    internal static readonly IReadOnlyList<string> All =
        ["app", "fonts", "open", "preview", "review"];

    internal static bool Contains(string operation) =>
        All.Contains(operation, StringComparer.Ordinal);
}

/// <summary>Deterministic projections over a product's format descriptors.</summary>
public static class FormatDescriptorExtensions
{
    /// <summary>Returns format ids for one use in declared stable order.</summary>
    public static IReadOnlyList<string> IdsFor(
        this IEnumerable<FormatDescriptor> descriptors,
        FormatUse use) =>
        Array.AsReadOnly(Ordered(descriptors, use, extension: null)
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
        return Ordered(descriptors, use, extension: null).FirstOrDefault(format =>
            string.Equals(format.Id, name, StringComparison.OrdinalIgnoreCase)
            || format.Aliases.Contains(name, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns the formats for one use that declare a file extension such as <c>.htm</c>,
    /// in that use's declared stable order. Extensions compare case-insensitively.
    /// </summary>
    public static IReadOnlyList<FormatDescriptor> WithExtension(
        this IEnumerable<FormatDescriptor> descriptors,
        FormatUse use,
        string extension)
    {
        ArgumentNullException.ThrowIfNull(extension);
        return Array.AsReadOnly(Ordered(descriptors, use, extension).ToArray());
    }

    private static IEnumerable<FormatDescriptor> Ordered(
        IEnumerable<FormatDescriptor> descriptors,
        FormatUse use,
        string? extension)
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
            .Where(format => format.Uses.HasFlag(use)
                && (extension is null || format.Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase)))
            .OrderBy(format => Order(format, use))
            .ThenBy(static format => format.Id, StringComparer.Ordinal);
    }

    /// <summary>Returns the preferred output extension for one format id.</summary>
    public static string ExtensionFor(
        this IEnumerable<FormatDescriptor> descriptors,
        string formatId)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        ArgumentException.ThrowIfNullOrWhiteSpace(formatId);
        FormatDescriptor descriptor = descriptors.SingleOrDefault(format =>
                string.Equals(
                    format.Id,
                    formatId,
                    StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException(
                $"Format '{formatId}' is not declared.");
        string? extension = descriptor.OutputExtension
            ?? descriptor.Extensions.FirstOrDefault();
        return extension
            ?? throw new InvalidOperationException(
                $"Format '{descriptor.Id}' has no associated extension.");
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
