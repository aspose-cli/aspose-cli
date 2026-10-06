using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Slides.Contracts;

/// <summary>Deny-by-default presentation format registry.</summary>
public static class SlidesFormats
{
    /// <summary>Canonical immutable format declarations owned by this product.</summary>
    internal static readonly IReadOnlyList<FormatDescriptor> Definitions =
        FileFormatRecognition.AttachTo(
    [
        FormatDescriptor.Routed("ppt", FormatUse.Input | FormatUse.Convert, 0, 1, null, ".ppt"),
        FormatDescriptor.Routed("pptx", FormatUse.Input | FormatUse.Convert, 1, 0, null, ".pptx"),
        FormatDescriptor.Routed("pptm", FormatUse.Input | FormatUse.Convert, 2, 2, null, ".pptm"),
        FormatDescriptor.Routed("pps", FormatUse.Input, 3, null, null, ".pps"),
        FormatDescriptor.Routed("ppsx", FormatUse.Input, 4, null, null, ".ppsx"),
        FormatDescriptor.Routed("ppsm", FormatUse.Input, 5, null, null, ".ppsm"),
        FormatDescriptor.Routed("pot", FormatUse.Input, 6, null, null, ".pot"),
        FormatDescriptor.Routed("potx", FormatUse.Input, 7, null, null, ".potx"),
        FormatDescriptor.Routed("potm", FormatUse.Input, 8, null, null, ".potm"),
        FormatDescriptor.Routed("odp", FormatUse.Input | FormatUse.Convert, 9, 3, null, ".odp"),
        FormatDescriptor.Routed("otp", FormatUse.Input, 10, null, null, ".otp"),
        FormatDescriptor.Routed("fodp", FormatUse.Input, 11, null, null, ".fodp"),
        FormatDescriptor.Routed("pdf", FormatUse.Convert, null, 4, null, ".pdf"),
        FormatDescriptor.Routed("xps", FormatUse.Convert, null, 5, null, ".xps"),
        FormatDescriptor.Routed("html", FormatUse.Convert, null, 6, null, ".html"),
        FormatDescriptor.Routed("html5", FormatUse.Convert, null, 7, null, ".html"),
        FormatDescriptor.Routed("png", FormatUse.Convert | FormatUse.Render, null, 8, 0, ".png"),
        FormatDescriptor.Routed("jpeg", FormatUse.Convert | FormatUse.Render, null, 9, 1, ".jpg", ".jpeg")
            with { Aliases = ["jpg"] },
        FormatDescriptor.Routed("tiff", FormatUse.Convert, null, 10, null, ".tiff"),
        FormatDescriptor.Routed("gif", FormatUse.Convert, null, 11, null, ".gif"),
        FormatDescriptor.Routed("svg", FormatUse.Convert | FormatUse.Render, null, 12, 2, ".svg"),
        FormatDescriptor.Routed("md", FormatUse.Convert, null, 13, null, ".md"),
    ], SlidesFormatRecognition.Rules);

    public static IReadOnlyList<string> LoadIds { get; } =
        Definitions.IdsFor(FormatUse.Input);

    public static IReadOnlyList<string> ConvertIds { get; } =
        Definitions.IdsFor(FormatUse.Convert);

    public static IReadOnlyList<string> WriteIds { get; } = ["pptx", "pptm"];

    /// <summary>Output formats that can carry a password.</summary>
    public static IReadOnlyList<string> EncryptIds { get; } = ["pptx", "pptm"];

    /// <summary>The format id of a written presentation: its lowercase extension.</summary>
    /// <exception cref="CliException"><c>FORMAT_UNSUPPORTED</c> for an extension that names no format in <see cref="WriteIds"/>, or none at all.</exception>
    internal static string ForOutput(string path)
    {
        string format = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        return WriteIds.Contains(format, StringComparer.Ordinal) ? format : throw CliErrors.FormatUnsupported(format, WriteIds);
    }
}
