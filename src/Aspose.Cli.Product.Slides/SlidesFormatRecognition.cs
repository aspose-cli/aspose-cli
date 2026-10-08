using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Slides;

/// <summary>Product-owned bounded signatures for presentation routing.</summary>
internal static class SlidesFormatRecognition
{
    private static readonly FileFormatRecognition CompoundPresentation =
        FileFormatRecognition.Match(
            FileProbePattern.OleCompoundFile(),
            "OLE compound-file signature with extension-qualified format",
            70);

    // A password-encrypted Open XML presentation is stored in an OLE compound file.
    private static readonly FileFormatRecognition OpenXmlPresentation =
        FileFormatRecognition.FirstOf(
            FileFormatRecognition.Match(
                FileProbePattern.ZipContainsAny(
                    "ppt/",
                    "application/vnd.openxmlformats-officedocument.presentationml"),
                "presentation package marker"),
            CompoundPresentation);

    private static readonly FileFormatRecognition OpenDocumentPresentation =
        FileFormatRecognition.Match(
            FileProbePattern.ZipContainsAny(
                "application/vnd.oasis.opendocument.presentation"),
            "OpenDocument presentation mimetype");

    internal static IReadOnlyDictionary<string, FileFormatRecognition>
        Rules
    { get; } = new Dictionary<string, FileFormatRecognition>(
        StringComparer.Ordinal)
    {
        ["ppt"] = CompoundPresentation,
        ["pptx"] = OpenXmlPresentation,
        ["pptm"] = OpenXmlPresentation,
        ["pps"] = CompoundPresentation,
        ["ppsx"] = OpenXmlPresentation,
        ["ppsm"] = OpenXmlPresentation,
        ["pot"] = CompoundPresentation,
        ["potx"] = OpenXmlPresentation,
        ["potm"] = OpenXmlPresentation,
        ["odp"] = OpenDocumentPresentation,
        ["otp"] = OpenDocumentPresentation,
        ["fodp"] = FileFormatRecognition.Match(
            FileProbePattern.All(
                XmlPrefix(),
                FileProbePattern.TextContains("office:presentation")),
            "flat OpenDocument presentation root"),
    };

    private static FileProbePattern XmlPrefix() =>
        FileProbePattern.Any(
            FileProbePattern.TextStartsIgnoringBomAndWhitespace("<?xml"),
            FileProbePattern.TextStartsIgnoringBomAndWhitespace("<"));
}
