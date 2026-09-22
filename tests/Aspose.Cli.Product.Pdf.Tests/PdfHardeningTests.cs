using System.Text;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Pdf;
using Aspose.Pdf.Text;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

public sealed class PdfHardeningTests
{
    [Fact]
    public void RegexTimeout_IsEnforcedByTheSdkAndPreventsPublication()
    {
        using var fixture = new PdfEngineFixture();
        fixture.Gate.EnsureApplied();
        using var document = new Document();
        Page page = document.Pages.Add();
        page.Paragraphs.Add(new TextFragment(new string('a', 4096) + "!"));
        document.ProcessParagraphs();
        const string pattern = "(a+)+$";
        var absorber = new TextFragmentAbsorber(
            Sdk.Text.SafeRegex.Create(pattern, caseSensitive: true), new TextSearchOptions(true));
        Assert.Throws<System.Text.RegularExpressions.RegexMatchTimeoutException>(() => page.Accept(absorber));
        string input = fixture.File("regex-timeout.pdf");
        document.Save(input);
        string output = fixture.File("regex-timeout.out.pdf");
        CliException error = Assert.Throws<CliException>(() => fixture.Engine.ApplyOps(input,
            new PdfOpsBatch { Ops = [new RedactTextOp { Pattern = pattern, Regex = true }] },
            new PdfEditRequest { OutputPath = output }));
        Assert.Equal(ErrorCodes.OperationTimeout, error.Code);
        Assert.False(File.Exists(output));
    }

    [Theory]
    [InlineData("(?=Portable)")]
    [InlineData("Portable|(?=page)")]
    public void ZeroWidthRegex_IsRejectedBeforeTheSdkMutation(string pattern)
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("zero-width.pdf", pages: 1);
        string output = fixture.File("zero-width.out.pdf");
        CliException error = Assert.Throws<CliException>(() => fixture.Engine.ApplyOps(input,
            new PdfOpsBatch { Ops = [new RedactTextOp { Pattern = pattern, Regex = true }] },
            new PdfEditRequest { OutputPath = output }));
        Assert.Contains("empty string", error.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
    }
    [Fact]
    public void Read_CjkTextRoundTripsWithoutLatinSubstitution()
    {
        using var fixture = new PdfEngineFixture();
        const string text = "简体中文";
        string input = fixture.File("cjk.pdf");
        using (var document = new Document())
        {
            var fragment = new TextFragment(text);
            fragment.TextState.Font = FontRepository.FindFont("SimSun");
            document.Pages.Add().Paragraphs.Add(fragment);
            document.Save(input);
        }

        var result = fixture.Engine.Read(input, new PdfReadRequest { MaxCharacters = 20_000 });

        Assert.Contains(text, result.Pages[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_TruncatedCrossReferenceIsRecoveredWithStableStructure()
    {
        using var fixture = new PdfEngineFixture();
        string valid = fixture.CreateRawDocument("valid.pdf", pages: 2);
        byte[] bytes = File.ReadAllBytes(valid);
        int xref = LastIndexOf(bytes, "xref"u8);
        string input = fixture.File("truncated-xref.pdf");
        File.WriteAllBytes(input, bytes[..(xref + 6)]);

        var result = fixture.Engine.GetInfo(input, new PdfInfoRequest());

        Assert.Equal(2, result.Pdf.Pages);
        Assert.Equal("pdf", result.Source.Format);
    }

    [Fact]
    public void Read_InvalidCompressedStreamNeverEscapesAnSdkException()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("bad-stream.pdf");
        File.WriteAllBytes(input, BuildBadStreamPdf());

        Exception? failure = Record.Exception(() =>
            fixture.Engine.Read(input, new PdfReadRequest { MaxCharacters = 20_000 }));

        Assert.True(
            failure is null or CliException,
            $"Expected recovery or a CLI domain error, got {failure?.GetType().FullName}: {failure?.Message}");
        if (failure is CliException error)
        {
            Assert.NotEqual(ErrorCodes.Internal, error.Code);
        }
    }

    private static int LastIndexOf(byte[] bytes, ReadOnlySpan<byte> value)
    {
        for (int index = bytes.Length - value.Length; index >= 0; index--)
        {
            if (bytes.AsSpan(index, value.Length).SequenceEqual(value))
            {
                return index;
            }
        }

        throw new InvalidOperationException("Expected marker was not present.");
    }

    private static byte[] BuildBadStreamPdf()
    {
        const string stream = "this-is-not-deflate-data";
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Count 1 /Kids [3 0 R] >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R >>",
            $"<< /Length {stream.Length} /Filter /FlateDecode >>\nstream\n{stream}\nendstream",
        ];
        using var output = new MemoryStream();
        Write(output, "%PDF-1.7\n");
        var offsets = new List<long> { 0 };
        for (int index = 0; index < objects.Length; index++)
        {
            offsets.Add(output.Position);
            Write(output, $"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }

        long xref = output.Position;
        Write(output, $"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (long offset in offsets.Skip(1))
        {
            Write(output, $"{offset:0000000000} 00000 n \n");
        }

        Write(output, $"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return output.ToArray();
    }

    private static void Write(Stream stream, string text) =>
        stream.Write(Encoding.ASCII.GetBytes(text));
}
