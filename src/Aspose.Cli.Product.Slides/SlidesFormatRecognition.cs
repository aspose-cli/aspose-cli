
using Aspose.Cli.Sdk.Formats;

namespace Aspose.Cli.Product.Slides;

/// <summary>Bounded signatures that several presentation formats share for routing.</summary>
internal static class SlidesFormatRecognition
{
    internal static readonly FileFormatRecognition CompoundPresentation =
        FileFormatRecognition.Match(
            FileProbePattern.OleCompoundFile(),
            "OLE compound-file signature with extension-qualified format",
            70);

    // A password-encrypted Open XML presentation is stored in an OLE compound file.
    internal static readonly FileFormatRecognition OpenXmlPresentation =
        FileFormatRecognition.FirstOf(
            FileFormatRecognition.Match(
                FileProbePattern.ZipContainsAny(
                    "ppt/",
                    "application/vnd.openxmlformats-officedocument.presentationml"),
                "presentation package marker"),
            CompoundPresentation);

    internal static readonly FileFormatRecognition OpenDocumentPresentation =
        FileFormatRecognition.Match(
            FileProbePattern.ZipContainsAny(
                "application/vnd.oasis.opendocument.presentation"),
            "OpenDocument presentation mimetype");
}
