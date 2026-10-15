using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Words;

/// <summary>Product-owned bounded signatures for word-processing routing.</summary>
internal static class WordsFormatRecognition
{
    private static readonly FileFormatRecognition CompoundDocument =
        FileFormatRecognition.Match(
            FileProbePattern.OleCompoundFile(),
            "OLE compound-file signature with extension-qualified format",
            70);

    // A password-encrypted OOXML document is an OLE compound file holding the encrypted package.
    private static readonly FileFormatRecognition OpenXmlDocument =
        FileFormatRecognition.FirstOf(
            FileFormatRecognition.Match(
                FileProbePattern.ZipContainsAny(
                    "word/",
                    "application/vnd.openxmlformats-officedocument.wordprocessingml"),
                "word-processing package marker"),
            CompoundDocument);

    private static readonly FileFormatRecognition OpenDocumentText =
        FileFormatRecognition.Match(
            FileProbePattern.ZipContainsAny(
                "application/vnd.oasis.opendocument.text"),
            "OpenDocument text mimetype");

    private static readonly FileFormatRecognition PlainText =
        FileFormatRecognition.Match(
            FileProbePattern.ValidText(),
            "bounded text validation",
            55);

    // Word's single-file XML: a flat OPC package of a word-processing document, and Word 2003 XML.
    private static readonly FileFormatRecognition FlatOpc =
        FileFormatRecognition.Match(
            FileProbePattern.All(
                FileProbePattern.TextStartsIgnoringBomAndWhitespace("<"),
                FileProbePattern.TextContains("schemas.microsoft.com/office/2006/xmlPackage"),
                FileProbePattern.Any(
                    FileProbePattern.TextContains("progid=\"Word.Document\""),
                    FileProbePattern.TextContains("pkg:name=\"/word/"))),
            "flat OPC word-processing package");

    private static readonly FileFormatRecognition WordMl =
        FileFormatRecognition.Match(
            FileProbePattern.All(
                FileProbePattern.TextStartsIgnoringBomAndWhitespace("<"),
                FileProbePattern.TextContains("schemas.microsoft.com/office/word/2003/wordml")),
            "Word 2003 XML namespace");

    private static readonly FileFormatRecognition Mobipocket =
        FileFormatRecognition.Match(
            FileProbePattern.AsciiBytesAt(60, "BOOKMOBI"),
            "Mobipocket BOOKMOBI signature");

    internal static IReadOnlyDictionary<string, FileFormatRecognition>
        Rules
    { get; } = new Dictionary<string, FileFormatRecognition>(
        StringComparer.Ordinal)
    {
        ["doc"] = CompoundDocument,
        ["dot"] = CompoundDocument,
        ["docx"] = OpenXmlDocument,
        ["docm"] = OpenXmlDocument,
        ["dotx"] = OpenXmlDocument,
        ["dotm"] = OpenXmlDocument,
        ["flatopc"] = FlatOpc,
        ["wordml"] = WordMl,
        ["rtf"] = FileFormatRecognition.Match(
            FileProbePattern.TextStartsIgnoringBomAndWhitespace(@"{\rtf"),
            "RTF header"),
        ["odt"] = OpenDocumentText,
        ["ott"] = OpenDocumentText,
        ["txt"] = PlainText,
        ["md"] = PlainText,
        ["epub"] = FileFormatRecognition.Match(
            FileProbePattern.ZipContainsAny(
                "application/epub+zip",
                "META-INF/container.xml"),
            "EPUB container marker"),
        ["mobi"] = Mobipocket,
        ["azw3"] = Mobipocket,
        ["chm"] = FileFormatRecognition.Match(
            FileProbePattern.AsciiBytesAt(0, "ITSF"),
            "CHM signature"),
    };
}
