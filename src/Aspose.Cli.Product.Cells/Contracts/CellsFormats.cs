using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>
/// The format vocabulary of the cells product. This list is public contract:
/// ids are stable, and the split between <c>convert</c> (documents) and
/// <c>render</c> (images of a sheet) is intentional.
/// </summary>
public static class CellsFormats
{
    /// <summary>Formats accepted by <c>cells convert --to</c>.</summary>
    public static IReadOnlyList<FormatInfo> Convert { get; } =
        Create(FormatUse.Convert);

    /// <summary>Formats accepted by <c>cells render --to</c>.</summary>
    public static IReadOnlyList<FormatInfo> Render { get; } =
        Create(FormatUse.Render);

    /// <summary>
    /// Convert formats that can be limited to a single sheet with
    /// <c>--sheet</c>; every other format always converts the whole workbook.
    /// csv, tsv and md are single-sheet formats by nature — they export the
    /// active sheet — so for them <c>--sheet</c> is the only way to reach any
    /// other one.
    /// </summary>
    public static IReadOnlyList<string> SheetScopedConvertIds { get; } = ["csv", "tsv", "md", "pdf"];

    /// <summary>Canonical convert format ids, in declaration order.</summary>
    public static IReadOnlyList<string> ConvertIds { get; } = Convert.Select(static f => f.Id).ToArray();

    /// <summary>Canonical render format ids, in declaration order.</summary>
    public static IReadOnlyList<string> RenderIds { get; } = Render.Select(static f => f.Id).ToArray();

    /// <summary>Resolves a user-supplied convert format id or alias.</summary>
    /// <exception cref="CliException"><c>FORMAT_UNSUPPORTED</c> when unknown.</exception>
    public static FormatInfo ResolveConvert(string requested) => Resolve(Convert, ConvertIds, requested);

    /// <summary>Resolves a user-supplied render format id or alias.</summary>
    /// <exception cref="CliException"><c>FORMAT_UNSUPPORTED</c> when unknown.</exception>
    public static FormatInfo ResolveRender(string requested) => Resolve(Render, RenderIds, requested);

    /// <summary>
    /// Resolves a render format id, alias or file extension, or <c>null</c> when
    /// it names none — for callers inferring a format from a path, where an
    /// unrecognized extension is a reason to fall back, not to fail.
    /// </summary>
    public static FormatInfo? TryResolveRender(string requested) => Find(Render, requested);

    private static FormatInfo Resolve(IReadOnlyList<FormatInfo> formats, IReadOnlyList<string> ids, string requested)
    {
        ArgumentException.ThrowIfNullOrEmpty(requested);
        return Find(formats, requested) ?? throw CliErrors.FormatUnsupported(requested, ids);
    }

    private static FormatInfo? Find(IReadOnlyList<FormatInfo> formats, string requested)
    {
        ArgumentException.ThrowIfNullOrEmpty(requested);
        string normalized = requested.Trim().TrimStart('.');

        foreach (FormatInfo format in formats)
        {
            if (string.Equals(format.Id, normalized, StringComparison.OrdinalIgnoreCase)
                || format.Aliases.Any(alias => string.Equals(alias, normalized, StringComparison.OrdinalIgnoreCase)))
            {
                return format;
            }
        }

        return null;
    }

    private static IReadOnlyList<FormatInfo> Create(FormatUse use) =>
        CellsModule.Formats
            .IdsFor(use)
            .Select(id =>
            {
                FormatDescriptor descriptor = CellsModule.Formats.Single(
                    format => string.Equals(format.Id, id, StringComparison.Ordinal));
                return new FormatInfo(
                    descriptor.Id,
                    descriptor.OutputExtension ?? descriptor.Extensions[0],
                    [.. descriptor.Aliases]);
            })
            .ToArray();
}
