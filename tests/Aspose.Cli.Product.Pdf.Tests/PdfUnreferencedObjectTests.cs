using System.IO.Compression;
using System.Text;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Pdf;
using Aspose.Pdf.Forms;
using Aspose.Pdf.Text;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>
/// An edit writes no object the output no longer references: a flattened field, a deleted
/// attachment or a redacted value cannot be read from the file's bytes.
/// </summary>
public sealed class PdfUnreferencedObjectTests
{
    private const string FieldName = "id_number";
    private const string Secret = "110101199003078515";

    [Fact]
    public void FlattenForms_WritesNoFlattenedFieldObjectAndKeepsTheVisibleValue()
    {
        using var fixture = new PdfEngineFixture();
        string flattened = Flatten(fixture);

        using (var reopened = new Document(flattened))
        {
            Assert.Empty(reopened.Form.Fields);
            Assert.Contains(Secret, PageText(reopened.Pages[1]), StringComparison.Ordinal);
        }

        // The value stays only as the page's drawn text, inside a content stream; no field
        // dictionary with its name or value remains in the file.
        byte[] bytes = File.ReadAllBytes(flattened);
        Assert.Equal(0, Count(bytes, "/T(" + FieldName));
        Assert.Equal(0, Count(bytes, "(" + Secret + ")"));
        Assert.Equal(0, Count(bytes, FieldName));
        Assert.All(InflatedStreams(bytes), stream => Assert.Equal(0, Count(stream, FieldName)));
    }

    [Fact]
    public void RedactText_AfterFlattenForms_LeavesTheValueNowhereInTheFile()
    {
        using var fixture = new PdfEngineFixture();
        string flattened = Flatten(fixture);
        string redacted = fixture.File("form.redacted.pdf");
        PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = flattened,
            Batch = new PdfOpsBatch { Ops = [new RedactTextOp { Find = Secret }] },
            Output = TestOutput.At(redacted),
        });

        Assert.Equal(0, Occurrences(File.ReadAllBytes(redacted), Secret));
    }

    [Fact]
    public void DeleteAttachment_WritesNoEmbeddedFileContent()
    {
        using var fixture = new PdfEngineFixture();
        const string content = "confidential-attachment-payload";
        string note = fixture.File("note.txt");
        File.WriteAllText(note, content);
        string attached = fixture.File("attached.pdf");
        PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = fixture.CreateDocument("plain.pdf", pages: 1),
            Batch = new PdfOpsBatch { Ops = [new AddAttachmentOp { Path = note }] },
            Output = TestOutput.At(attached),
        });
        Assert.NotEqual(0, Occurrences(File.ReadAllBytes(attached), content));
        string removed = fixture.File("removed.pdf");

        PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = attached,
            Batch = new PdfOpsBatch { Ops = [new DeleteAttachmentOp { Name = "note.txt" }] },
            Output = TestOutput.At(removed),
        });

        Assert.Equal(0, Occurrences(File.ReadAllBytes(removed), content));
    }

    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 2)]
    public void OptimizeDocument_RemoveUnusedObjectsDecidesTheImagesAPageListsButNeverDraws(bool remove, int kept)
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("unused-images.pdf");
        using (var document = new Document())
        {
            Page page = document.Pages.Add();
            page.Paragraphs.Add(new TextFragment("Text only"));
            page.Resources.Images.Add(new MemoryStream(PdfMutateTests.Bitmap(200, 200)));
            page.Resources.Images.Add(new MemoryStream(PdfMutateTests.Bitmap(100, 100)));
            document.Save(input);
        }

        string output = fixture.File("optimized.pdf");
        PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = [new OptimizeDocumentOp { RemoveUnusedObjects = remove, CompressStreams = false }] },
            Output = TestOutput.At(output),
        });

        using var reopened = new Document(output);
        Assert.Equal(kept, reopened.Pages[1].Resources.Images.Count);
    }

    private static string Flatten(PdfEngineFixture fixture)
    {
        string input = fixture.File("form.pdf");
        using (var document = new Document())
        {
            Page page = document.Pages.Add();
            page.Paragraphs.Add(new TextFragment("ID number"));
            document.Form.Add(new TextBoxField(page, new Rectangle(72, 650, 280, 680)) { PartialName = FieldName });
            document.Save(input);
        }

        string flattened = fixture.File("form.flattened.pdf");
        PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch
            {
                Ops = [new SetFormFieldOp { Name = FieldName, Value = Secret }, new FlattenFormsOp()],
            },
            Output = TestOutput.At(flattened),
        });
        return flattened;
    }

    /// <summary>The occurrences of <paramref name="text"/> in the file's bytes and in every stream it inflates.</summary>
    private static int Occurrences(byte[] bytes, string text) =>
        Count(bytes, text) + InflatedStreams(bytes).Sum(stream => Count(stream, text));

    /// <summary>Every stream of the file that inflates as Flate data, object streams included.</summary>
    private static IEnumerable<byte[]> InflatedStreams(byte[] bytes)
    {
        byte[] keyword = Encoding.ASCII.GetBytes("stream");
        for (int at = bytes.AsSpan().IndexOf(keyword); at >= 0;)
        {
            int start = at + keyword.Length;
            start += bytes[start] == '\r' ? 2 : 1;
            if (Inflate(bytes, start) is { } inflated)
            {
                yield return inflated;
            }

            int next = bytes.AsSpan(start).IndexOf(keyword);
            at = next < 0 ? -1 : start + next;
        }
    }

    private static byte[]? Inflate(byte[] bytes, int start)
    {
        try
        {
            using var source = new ZLibStream(new MemoryStream(bytes, start, bytes.Length - start), CompressionMode.Decompress);
            using var inflated = new MemoryStream();
            source.CopyTo(inflated);
            return inflated.ToArray();
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    private static int Count(byte[] bytes, string text)
    {
        byte[] needle = Encoding.Latin1.GetBytes(text);
        int count = 0;
        for (int at = bytes.AsSpan().IndexOf(needle); at >= 0;)
        {
            count++;
            int next = bytes.AsSpan(at + 1).IndexOf(needle);
            at = next < 0 ? -1 : at + 1 + next;
        }

        return count;
    }

    private static string PageText(Page page)
    {
        var absorber = new TextAbsorber();
        page.Accept(absorber);
        return absorber.Text ?? string.Empty;
    }
}
