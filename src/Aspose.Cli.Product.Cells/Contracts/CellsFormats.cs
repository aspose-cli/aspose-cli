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
    /// <summary>Canonical immutable format declarations owned by this product.</summary>
    internal static readonly IReadOnlyList<FormatDescriptor> Definitions =
        FileFormatRecognition.AttachTo(
    [
        FormatDescriptor.Declare("xlsx", FormatUse.Input | FormatUse.Convert, 0, 0, null, true, ".xlsx", ".xltx"),
        FormatDescriptor.Declare("xlsm", FormatUse.Input | FormatUse.Convert, 1, 1, null, true, ".xlsm", ".xltm"),
        FormatDescriptor.Declare("xlsb", FormatUse.Input | FormatUse.Convert, 2, 2, null, true, ".xlsb"),
        FormatDescriptor.Declare("xls", FormatUse.Input | FormatUse.Convert, 3, 3, null, true, ".xls"),
        FormatDescriptor.Declare("ods", FormatUse.Input | FormatUse.Convert, 4, 4, null, true, ".ods"),
        FormatDescriptor.Declare("csv", FormatUse.Input | FormatUse.Convert, 5, 5, null, true, ".csv"),
        FormatDescriptor.Declare("tsv", FormatUse.Input | FormatUse.Convert, 6, 6, null, true, ".tsv"),
        FormatDescriptor.Declare("html", FormatUse.Input | FormatUse.Convert, 7, 7, null, false, ".html", ".htm"),
        FormatDescriptor.Declare("mhtml", FormatUse.Input | FormatUse.Convert, 8, 8, null, false, ".mhtml"),
        FormatDescriptor.Declare("pdf", FormatUse.Input | FormatUse.Convert, 9, 9, null, false, ".pdf"),
        FormatDescriptor.Declare("xps", FormatUse.Input | FormatUse.Convert, 10, 10, null, false, ".xps"),
        FormatDescriptor.Declare("json", FormatUse.Input | FormatUse.Convert, 11, 11, null, false, ".json"),
        FormatDescriptor.Declare("md", FormatUse.Input | FormatUse.Convert, 12, 12, null, false, ".md")
            with { Aliases = ["markdown"] },
        FormatDescriptor.Declare("png", FormatUse.Render, null, null, 0, false, ".png"),
        FormatDescriptor.Declare("jpeg", FormatUse.Render, null, null, 1, false, ".jpg", ".jpeg")
            with { Aliases = ["jpg"] },
        FormatDescriptor.Declare("svg", FormatUse.Render, null, null, 2, false, ".svg"),
    ], CellsFormatRecognition.Rules);

    /// <summary>Workbook formats whose edited output can be reopened and verified.</summary>
    public static IReadOnlyList<string> EditIds { get; } = Array.AsReadOnly(new[]
    { "xlsx", "xlsm", "xlsb", "xls", "ods", "csv", "tsv", "html", "mhtml" });

    /// <summary>Workbook formats that can carry a password.</summary>
    public static IReadOnlyList<string> EncryptableIds { get; } = Array.AsReadOnly(new[]
    { "xlsx", "xlsm", "xlsb", "xls", "ods" });

    /// <summary>The convert format whose id, alias or declared extension a path carries; xlsx without one.</summary>
    /// <exception cref="CliException"><c>FORMAT_UNSUPPORTED</c> when the extension names no format.</exception>
    public static string ForOutputPath(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Length > 1 ? ResolveConvert(extension).Id : "xlsx";
    }


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
    public static FormatInfo ResolveConvert(string requested) => Resolve(Convert, ConvertIds, FormatUse.Convert, requested);

    /// <summary>Resolves a user-supplied render format id or alias.</summary>
    /// <exception cref="CliException"><c>FORMAT_UNSUPPORTED</c> when unknown.</exception>
    public static FormatInfo ResolveRender(string requested) => Resolve(Render, RenderIds, FormatUse.Render, requested);

    private static FormatInfo Resolve(IReadOnlyList<FormatInfo> formats, IReadOnlyList<string> ids, FormatUse use, string requested)
    {
        ArgumentException.ThrowIfNullOrEmpty(requested);
        return Find(formats, use, requested) ?? throw CliErrors.FormatUnsupported(requested, ids);
    }

    /// <summary>Finds a format by id or alias, then by any extension it declares (<c>.htm</c>, <c>.xltx</c>).</summary>
    private static FormatInfo? Find(IReadOnlyList<FormatInfo> formats, FormatUse use, string requested)
    {
        ArgumentException.ThrowIfNullOrEmpty(requested);
        string normalized = requested.Trim().TrimStart('.');
        return formats.FirstOrDefault(format =>
                string.Equals(format.Id, normalized, StringComparison.OrdinalIgnoreCase)
                || format.Aliases.Any(alias => string.Equals(alias, normalized, StringComparison.OrdinalIgnoreCase)))
            ?? Definitions.WithExtension(use, "." + normalized)
                .Select(descriptor => formats.First(format => string.Equals(format.Id, descriptor.Id, StringComparison.Ordinal)))
                .FirstOrDefault();
    }

    private static IReadOnlyList<FormatInfo> Create(FormatUse use) =>
        CellsFormats.Definitions
            .IdsFor(use)
            .Select(id =>
            {
                FormatDescriptor descriptor = CellsFormats.Definitions.Single(
                    format => string.Equals(format.Id, id, StringComparison.Ordinal));
                return new FormatInfo(
                    descriptor.Id,
                    descriptor.OutputExtension ?? descriptor.Extensions[0],
                    [.. descriptor.Aliases]);
            })
            .ToArray();
}
