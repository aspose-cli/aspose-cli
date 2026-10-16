using System.IO.Compression;
using Aspose.Words;
using Aspose.Words.Drawing;
using Aspose.Words.Saving;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>
/// What Aspose.Words returns as an image's bytes, with the SDK alone. <c>words extract --what
/// images</c> copies <see cref="ImageData.ImageBytes"/>, the bytes of the image part the document
/// stores. That is only half of what an undisclosed copy of embedded files needs: without a
/// license the engine adds its evaluation watermark image to the document, and the extraction
/// finds that image beside the document's own, so the extraction is not declared
/// <c>ExtractionContent.EmbeddedFiles</c> and evaluation mode is disclosed for it.
/// </summary>
public sealed class WordsImageBytesTests
{
    private const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";

    [Fact]
    public void ImageBytes_AreTheStoredImagePartByteForByte()
    {
        using var fixture = new WordsFixture();
        var document = new Document();
        new DocumentBuilder(document).InsertImage(Convert.FromBase64String(Png));
        using var package = new MemoryStream();
        document.Save(package, SaveFormat.Docx);

        // Bytes after the PNG's end chunk survive only a copy: a decoder ignores them and an
        // encoder never writes them, so a re-encoded image would lose them.
        byte[] stored = [.. Convert.FromBase64String(Png), .. "stored bytes"u8];
        using (var archive = new ZipArchive(package, ZipArchiveMode.Update, leaveOpen: true))
        {
            ZipArchiveEntry media = Assert.Single(archive.Entries, static entry => entry.FullName.StartsWith("word/media/", StringComparison.Ordinal));
            string name = media.FullName;
            media.Delete();
            using Stream written = archive.CreateEntry(name).Open();
            written.Write(stored);
        }

        package.Position = 0;
        var reopened = new Document(package);
        Shape shape = Assert.Single(reopened.GetChildNodes(NodeType.Shape, true).Cast<Shape>(), static s => s.HasImage);

        Assert.Equal(stored, shape.ImageData.ImageBytes);
    }
}
