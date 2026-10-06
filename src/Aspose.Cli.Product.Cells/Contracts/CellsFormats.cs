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
        FormatDescriptor.Declare("xlsx", FormatUse.Input | FormatUse.Convert, 0, 0, null, true, ".xlsx"),
        FormatDescriptor.Declare("xltx", FormatUse.Input | FormatUse.Convert, 1, 1, null, true, ".xltx"),
        FormatDescriptor.Declare("xlsm", FormatUse.Input | FormatUse.Convert, 2, 2, null, true, ".xlsm"),
        FormatDescriptor.Declare("xltm", FormatUse.Input | FormatUse.Convert, 3, 3, null, true, ".xltm"),
        FormatDescriptor.Declare("xlsb", FormatUse.Input | FormatUse.Convert, 4, 4, null, true, ".xlsb"),
        FormatDescriptor.Declare("xls", FormatUse.Input | FormatUse.Convert, 5, 5, null, true, ".xls"),
        FormatDescriptor.Declare("ods", FormatUse.Input | FormatUse.Convert, 6, 6, null, true, ".ods"),
        FormatDescriptor.Declare("csv", FormatUse.Input | FormatUse.Convert, 7, 7, null, true, ".csv"),
        FormatDescriptor.Declare("tsv", FormatUse.Input | FormatUse.Convert, 8, 8, null, true, ".tsv"),
        FormatDescriptor.Declare("html", FormatUse.Input | FormatUse.Convert, 9, 9, null, false, ".html", ".htm"),
        FormatDescriptor.Declare("mhtml", FormatUse.Input | FormatUse.Convert, 10, 10, null, false, ".mhtml"),
        FormatDescriptor.Declare("pdf", FormatUse.Input | FormatUse.Convert, 11, 11, null, false, ".pdf"),
        FormatDescriptor.Declare("xps", FormatUse.Input | FormatUse.Convert, 12, 12, null, false, ".xps"),
        FormatDescriptor.Declare("json", FormatUse.Input | FormatUse.Convert, 13, 13, null, false, ".json"),
        FormatDescriptor.Declare("md", FormatUse.Input | FormatUse.Convert, 14, 14, null, false, ".md")
            with { Aliases = ["markdown"] },
        FormatDescriptor.Declare("png", FormatUse.Render, null, null, 0, false, ".png"),
        FormatDescriptor.Declare("jpeg", FormatUse.Render, null, null, 1, false, ".jpg", ".jpeg")
            with { Aliases = ["jpg"] },
        FormatDescriptor.Declare("svg", FormatUse.Render, null, null, 2, false, ".svg"),
    ], CellsFormatRecognition.Rules);

    /// <summary>Workbook formats whose edited output can be reopened and verified.</summary>
    public static IReadOnlyList<string> EditIds { get; } = Array.AsReadOnly(new[]
    { "xlsx", "xltx", "xlsm", "xltm", "xlsb", "xls", "ods", "csv", "tsv", "html", "mhtml" });

    /// <summary>Workbook formats that can carry a password.</summary>
    public static IReadOnlyList<string> EncryptableIds { get; } = Array.AsReadOnly(new[]
    { "xlsx", "xltx", "xlsm", "xltm", "xlsb", "xls", "ods" });

    /// <summary>The convert format whose id, alias or declared extension a path carries; xlsx without one.</summary>
    /// <exception cref="CliException"><c>FORMAT_UNSUPPORTED</c> when the extension names no format.</exception>
    public static string ForOutputPath(string path) => ForOutputPath(path, Definitions.IdsFor(FormatUse.Convert));

    /// <summary>
    /// The format whose id, alias or declared extension a path carries, one of the formats a
    /// command writes; xlsx without one.
    /// </summary>
    /// <exception cref="CliException"><c>FORMAT_UNSUPPORTED</c>, listing <paramref name="writes"/>, when the extension names no format among them.</exception>
    public static string ForOutputPath(string path, IReadOnlyList<string> writes)
    {
        string extension = Path.GetExtension(path);
        string format = extension.Length <= 1 ? "xlsx"
            : (Definitions.Named(FormatUse.Convert, extension[1..])
                ?? Definitions.WithExtension(FormatUse.Convert, extension).FirstOrDefault())?.Id
            ?? extension[1..];
        return writes.Contains(format, StringComparer.Ordinal) ? format : throw CliErrors.FormatUnsupported(format, writes);
    }

    /// <summary>
    /// Convert formats that can be limited to a single sheet with
    /// <c>--sheet</c>; every other format always converts the whole workbook.
    /// csv, tsv and md are single-sheet formats by nature — they export the
    /// active sheet — so for them <c>--sheet</c> is the only way to reach any
    /// other one.
    /// </summary>
    public static IReadOnlyList<string> SheetScopedConvertIds { get; } = ["csv", "tsv", "md", "pdf"];
}
