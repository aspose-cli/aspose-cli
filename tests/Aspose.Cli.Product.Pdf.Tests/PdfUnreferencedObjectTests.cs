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

    /// <summary>
    /// A field on a page the batch moved is the field a later redact_area covers: pages that are not
    /// one run move by copying the field's page, and the move names the copy back and deletes the
    /// original, so the value is nowhere in the file, as an uncompressed stream or a Flate one.
    /// </summary>
    [Fact]
    public void RedactArea_AfterMovePages_LeavesTheFieldValueNowhereInTheFile()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("moved-form.pdf");
        using (var document = new Document())
        {
            Page page = document.Pages.Add();
            page.Paragraphs.Add(new TextFragment("ID number"));
            document.Form.Add(new TextBoxField(page, new Rectangle(72, 650, 280, 680)) { PartialName = FieldName });
            document.Save(input);
        }

        string redacted = fixture.File("moved-form.redacted.pdf");
        PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch
            {
                Ops =
                [
                    new SetFormFieldOp { Name = FieldName, Value = Secret },
                    new InsertPageOp { At = 2 },
                    new InsertPageOp { At = 3 },
                    new MovePagesOp { Pages = "1,3", To = 2 },
                    new RedactAreaOp { Page = 2, Rect = new PdfRectInput { X = 62, Y = 150, Width = 228, Height = 60 } },
                ],
            },
            Output = TestOutput.At(redacted),
            Verify = true,
        });

        Assert.True(result.Verification!.Ok, string.Join("; ", result.Verification.Issues.Select(static issue => issue.Message)));
        using (var reopened = new Document(redacted))
        {
            Assert.Empty(reopened.Form.Fields);
        }

        Assert.Equal(0, Occurrences(File.ReadAllBytes(redacted), Secret));
    }

    /// <summary>
    /// A field with a parent in its name on the page that moves stays one field, since the move
    /// copies the page it passes: covered after the move, its value is nowhere in the file.
    /// </summary>
    [Fact]
    public void RedactArea_AfterMovingThePageOfAFieldWithAParent_LeavesTheValueNowhereInTheFile()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("hierarchy.pdf");
        PdfEngineFixture.WriteRawPdf(input,
        [
            "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [5 0 R 8 0 R] >> >>",
            "<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [7 0 R] >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [8 0 R] >>",
            "<< /T (form) /Kids [6 0 R] >>",
            "<< /T (person) /Parent 5 0 R /Kids [7 0 R] >>",
            $"<< /Type /Annot /Subtype /Widget /FT /Tx /T (name) /V ({Secret}) /Parent 6 0 R /Rect [72 600 272 620] /P 3 0 R >>",
            "<< /Type /Annot /Subtype /Widget /FT /Tx /T (other) /V (Kept) /Rect [72 600 272 620] /P 4 0 R >>",
        ]);

        string redacted = fixture.File("hierarchy.redacted.pdf");
        PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch
            {
                Ops =
                [
                    new MovePagesOp { Pages = "1", To = 2 },
                    new RedactAreaOp { Page = 2, Rect = new PdfRectInput { X = 60, Y = 160, Width = 230, Height = 45 } },
                ],
            },
            Output = TestOutput.At(redacted),
            Verify = true,
        });

        Assert.True(result.Verification!.Ok, string.Join("; ", result.Verification.Issues.Select(static issue => issue.Message)));
        Assert.Equal(0, Occurrences(File.ReadAllBytes(redacted), Secret));
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
