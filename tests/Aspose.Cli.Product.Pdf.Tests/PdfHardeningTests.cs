using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Pdf;
using Aspose.Pdf.Text;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

public sealed class PdfHardeningTests
{
    [Fact]
    public void LoadedInput_CanBeReplacedInPlaceByAnotherWriter()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("shared.pdf", pages: 2);
        string replacement = fixture.CreateDocument("replacement.pdf", pages: 1);
        var loader = new PdfDocumentLoader(ProductTestBudgets.Create<PdfModule>());

        using (LoadedPdf loaded = loader.Open(input, password: null))
        {
            // Another edit of the same file publishes its output over it with File.Replace.
            File.Replace(replacement, input, destinationBackupFileName: null);

            Assert.Equal(2, loaded.Document.Pages.Count);
            Assert.False(string.IsNullOrWhiteSpace(PdfEngineSupport.ExtractText(loaded.Document.Pages[2], PdfReadModes.Plain)));
        }

        // A new command admits the replaced input afresh.
        using LoadedPdf reopened = new PdfDocumentLoader(ProductTestBudgets.Create<PdfModule>()).Open(input, password: null);
        Assert.Single(reopened.Document.Pages);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Create_ReadsASourceThatAnInPlaceEditIsReplacing(bool markdown)
    {
        using var fixture = new PdfEngineFixture();
        string source = fixture.File(markdown ? "source.md" : "source.txt");
        File.WriteAllText(source, markdown ? "# Shared source" : "Shared source");
        string output = fixture.File("created.pdf");

        // Another command's in-place edit holds the source open with delete access while it
        // replaces the file.
        using (new FileStream(source, FileMode.Open, FileAccess.Read,
                   FileShare.ReadWrite | FileShare.Delete, 1, FileOptions.DeleteOnClose))
        {
            PdfCreate.Run(fixture.Session, new NewPdfRequest { Output = TestOutput.At(output), TextPath = source });
        }

        using var created = new Document(output);
        Assert.Single(created.Pages);
    }

    [Fact]
    public void RegexTimeout_StopsTheRedactionWithoutPublishing()
    {
        using var fixture = new PdfEngineFixture();
        using var document = new Document();
        document.Pages.Add().Paragraphs.Add(new TextFragment(new string('a', 4096) + "!"));
        const string pattern = "(a+)+$";
        string input = fixture.File("regex-timeout.pdf");
        document.Save(input);
        string output = fixture.File("regex-timeout.out.pdf");
        CliException error = Assert.Throws<CliException>(() => PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = [new RedactTextOp { Pattern = pattern, Regex = true }] },
            Output = TestOutput.At(output),
        }));
        Assert.Equal(ErrorCodes.OperationTimeout, error.Code);
        Assert.False(File.Exists(output));
    }

    [Theory]
    [InlineData(null, "PASSWORD_REQUIRED")]
    [InlineData("wrong", "PASSWORD_INVALID")]
    public void InsertPagesFrom_AnEncryptedSourcesPasswordErrorPointsAtPasswordEnv(string? password, string code)
    {
        using var fixture = new PdfEngineFixture();
        string source = fixture.CreateEncryptedDocument("user-secret", "owner-secret", $"encrypted-{code}.pdf");
        string input = fixture.CreateDocument($"insert-{code}.pdf", pages: 1);
        string output = fixture.File($"insert-{code}.out.pdf");

        CliException error = Assert.Throws<CliException>(() => PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = [new InsertPagesFromOp { Path = source, At = 1, PasswordEnv = password is null ? null : "SOURCE_PWD" }] },
            Output = TestOutput.At(output),
            OpSecrets = password is null ? null : new Dictionary<string,
            Secret> { ["SOURCE_PWD"] = new(password) },
        }));

        Assert.Equal(code, error.Code.Name);
        Assert.Contains("\"passwordEnv\"", error.Hint, StringComparison.Ordinal);
        Assert.DoesNotContain("--password-env", error.Hint, StringComparison.Ordinal);
        Assert.Equal(source, error.Details!["path"]!.GetValue<string>());
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
        CliException error = Assert.Throws<CliException>(() => PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = [new RedactTextOp { Pattern = pattern, Regex = true }] },
            Output = TestOutput.At(output),
        }));
        Assert.Contains("empty string", error.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
    }
    [Theory]
    [InlineData("png")]
    [InlineData("jpeg")]
    [InlineData("tiff")]
    public void ConvertToRaster_RejectsAnOversizedPageBeforePublishing(string format)
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("poster.pdf");
        using (var document = new Document())
        {
            // 200 inches square: 38400 pixels per side at the 192 DPI of convert.
            document.Pages.Add().SetPageSize(14_400, 14_400);
            document.Save(input);
        }

        string output = fixture.File("poster" + PdfFormats.Definitions.ExtensionFor(format));
        CliException error = Assert.Throws<CliException>(() => PdfConvert.Run(fixture.Session, new PdfConvertRequest { Input = input, Output = TestOutput.At(output, format: format) }));

        Assert.Equal(ErrorCodes.RenderTooLarge, error.Code);
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

        var result = PdfRead.Run(fixture.Session, new PdfReadRequest { Input = input, MaxCharacters = 20_000 });

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

        var result = PdfInfo.Run(fixture.Session, new PdfInfoRequest { Input = input });

        Assert.Equal(2, result.Pdf.PageCount);
        Assert.Equal("pdf", result.Source.Format);
    }

    [Fact]
    public void Read_InvalidCompressedStreamNeverEscapesAnSdkException()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("bad-stream.pdf");
        WriteBadStreamPdf(input);

        Exception? failure = Record.Exception(() =>
            PdfRead.Run(fixture.Session, new PdfReadRequest { Input = input, MaxCharacters = 20_000 }));

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

    private static void WriteBadStreamPdf(string path)
    {
        const string stream = "this-is-not-deflate-data";
        PdfEngineFixture.WriteRawPdf(
            path,
            [
                "<< /Type /Catalog /Pages 2 0 R >>",
                "<< /Type /Pages /Count 1 /Kids [3 0 R] >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R >>",
                $"<< /Length {stream.Length} /Filter /FlateDecode >>\nstream\n{stream}\nendstream",
            ]);
    }
}
