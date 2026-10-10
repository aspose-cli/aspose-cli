using Aspose.Cli.Sdk.IO;

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
        FormatDescriptor.Declare("xlsx", FormatUse.Input | FormatUse.Convert, 0, 0, null, true, ".xlsx") with { Protectable = true },
        FormatDescriptor.Declare("xltx", FormatUse.Input | FormatUse.Convert, 1, 1, null, true, ".xltx") with { Protectable = true },
        FormatDescriptor.Declare("xlsm", FormatUse.Input | FormatUse.Convert, 2, 2, null, true, ".xlsm") with { Protectable = true },
        FormatDescriptor.Declare("xltm", FormatUse.Input | FormatUse.Convert, 3, 3, null, true, ".xltm") with { Protectable = true },
        FormatDescriptor.Declare("xlsb", FormatUse.Input | FormatUse.Convert, 4, 4, null, true, ".xlsb") with { Protectable = true },
        FormatDescriptor.Declare("xls", FormatUse.Input | FormatUse.Convert, 5, 5, null, true, ".xls") with { Protectable = true },
        FormatDescriptor.Declare("ods", FormatUse.Input | FormatUse.Convert, 6, 6, null, true, ".ods") with { Protectable = true },
        FormatDescriptor.Declare("csv", FormatUse.Input | FormatUse.Convert, 7, 7, null, true, ".csv", ".txt")
            with { UnroutedExtensions = [".txt"] },
        FormatDescriptor.Declare("tsv", FormatUse.Input | FormatUse.Convert, 8, 8, null, true, ".tsv", ".txt")
            with { UnroutedExtensions = [".txt"] },
        FormatDescriptor.Declare("html", FormatUse.Input | FormatUse.Convert, 9, 9, null, false, ".html", ".htm"),
        FormatDescriptor.Declare("mhtml", FormatUse.Input | FormatUse.Convert, 10, 10, null, false, ".mhtml"),
        FormatDescriptor.Declare("pdf", FormatUse.Convert, null, 11, null, false, ".pdf"),
        FormatDescriptor.Declare("xps", FormatUse.Convert, null, 12, null, false, ".xps"),
        FormatDescriptor.Declare("json", FormatUse.Convert, null, 13, null, false, ".json"),
        FormatDescriptor.Declare("md", FormatUse.Convert, null, 14, null, false, ".md", ".markdown")
            with { Aliases = ["markdown"] },
        FormatDescriptor.Declare("png", FormatUse.Render, null, null, 0, false, ".png"),
        FormatDescriptor.Declare("jpeg", FormatUse.Render, null, null, 1, false, ".jpg", ".jpeg")
            with { Aliases = ["jpg"] },
        FormatDescriptor.Declare("svg", FormatUse.Render, null, null, 2, false, ".svg"),
    ], CellsFormatRecognition.Rules);

    /// <summary>Workbook formats whose edited output can be reopened and verified.</summary>
    internal static IReadOnlyList<FormatDescriptor> Editable { get; } =
        [.. Definitions.Where(static format => format.Uses.HasFlag(FormatUse.Input | FormatUse.Convert))];

    /// <summary>The formats a new workbook can be created in.</summary>
    internal static IReadOnlyList<FormatDescriptor> Convertible { get; } =
        [.. Definitions.Where(static format => format.Uses.HasFlag(FormatUse.Convert)).OrderBy(static format => format.ConvertOrder)];

    /// <summary>
    /// Convert formats that hold one worksheet by nature: they export the active sheet, so for
    /// them <c>--sheet</c> is the only way to reach any other one.
    /// </summary>
    public static IReadOnlyList<string> ActiveSheetOnly { get; } = ["csv", "tsv", "md"];

    /// <summary>
    /// Convert formats that can be limited to a single sheet with <c>--sheet</c>: the
    /// <see cref="ActiveSheetOnly"/> formats and pdf. Every other format always converts the
    /// whole workbook.
    /// </summary>
    public static IReadOnlyList<string> SheetScoped { get; } = [.. ActiveSheetOnly, "pdf"];
}
