
using Aspose.Cli.Sdk.Formats;

namespace Aspose.Cli.Product.Words;

/// <summary>Bounded signatures that several word-processing formats share for routing.</summary>
internal static class WordsFormatRecognition
{
    internal static readonly FileFormatRecognition CompoundDocument =
        FileFormatRecognition.Match(
            FileProbePattern.OleCompoundFile(),
            "OLE compound-file signature with extension-qualified format",
            70);

    // A password-encrypted OOXML document is an OLE compound file holding the encrypted package.
    internal static readonly FileFormatRecognition OpenXmlDocument =
        FileFormatRecognition.FirstOf(
            FileFormatRecognition.Match(
                FileProbePattern.ZipContainsAny(
                    "word/",
                    "application/vnd.openxmlformats-officedocument.wordprocessingml"),
                "word-processing package marker"),
            CompoundDocument);

    internal static readonly FileFormatRecognition OpenDocumentText =
        FileFormatRecognition.Match(
            FileProbePattern.ZipContainsAny(
                "application/vnd.oasis.opendocument.text"),
            "OpenDocument text mimetype");

    internal static readonly FileFormatRecognition PlainText =
        FileFormatRecognition.Match(
            FileProbePattern.ValidText(),
            "bounded text validation",
            55);

    internal static readonly FileFormatRecognition Mobipocket =
        FileFormatRecognition.Match(
            FileProbePattern.AsciiBytesAt(60, "BOOKMOBI"),
            "Mobipocket BOOKMOBI signature");
}
