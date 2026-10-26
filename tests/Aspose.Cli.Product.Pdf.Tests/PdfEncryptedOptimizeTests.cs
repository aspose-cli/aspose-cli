using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.TestKit;
using Aspose.Pdf;
using Aspose.Pdf.Text;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>
/// optimize_document on an encrypted PDF never publishes a file its password cannot open
/// (PDF-ENCRYPTED-OBJECT-STREAMS): packing objects into object streams is refused while the
/// document is encrypted, and works between decrypt_document and encrypt_document.
/// </summary>
public sealed class PdfEncryptedOptimizeTests
{
    private const string UserPassword = "user-secret";

    [Fact]
    public void OptimizeDocument_OfAnEncryptedPdf_RefusesToPackObjectsAndWritesNothing()
    {
        using var fixture = new PdfEngineFixture();
        string input = Encrypted(fixture);
        string output = fixture.File("optimized.pdf");

        CliException error = Assert.Throws<CliException>(() => PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Password = new Secret(UserPassword),
            Batch = new PdfOpsBatch { Ops = [new OptimizeDocumentOp()] },
            Output = TestOutput.At(output),
        }));

        Assert.Contains("compressStreams", error.Message + error.Hint, StringComparison.Ordinal);
        Assert.Contains("decrypt_document", error.Hint, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
    }

    /// <summary>
    /// Without packing objects, or between decrypt_document and encrypt_document, the optimized
    /// output is encrypted, opens with its password and keeps its text.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OptimizeDocument_OfAnEncryptedPdf_WritesAFileItsPasswordOpens(bool decryptFirst)
    {
        using var fixture = new PdfEngineFixture();
        string input = Encrypted(fixture);
        string output = fixture.File("optimized.pdf");
        PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Password = new Secret(UserPassword),
            Batch = new PdfOpsBatch
            {
                Ops = decryptFirst
                    ? [new DecryptDocumentOp(), new OptimizeDocumentOp(), new EncryptDocumentOp { UserPasswordEnv = "P18_USER", OwnerPasswordEnv = "P18_OWNER" }]
                    : [new OptimizeDocumentOp { CompressStreams = false }],
            },
            Output = TestOutput.At(output),
            OpSecrets = new Dictionary<string, Secret>(StringComparer.Ordinal)
            {
                ["P18_USER"] = new Secret(UserPassword),
                ["P18_OWNER"] = new Secret("owner-secret"),
            },
        });

        Assert.ThrowsAny<Exception>(() => new Document(output).Dispose());
        using var reopened = new Document(output, UserPassword);
        Assert.True(reopened.IsEncrypted);
        var absorber = new TextFragmentAbsorber("Rack server 2U");
        reopened.Pages.Accept(absorber);
        Assert.Single(absorber.TextFragments);
    }

    private static string Encrypted(PdfEngineFixture fixture)
    {
        string markdown = fixture.File("annex.md");
        File.WriteAllText(markdown, "# Equipment list\n\nRack server 2U\n");
        string created = fixture.File("annex.pdf");
        PdfCreate.Run(fixture.Session, new PdfCreateRequest { Output = TestOutput.At(created), Source = new PdfMarkdownSource(new SourceFile("--from-markdown", markdown)) });
        string encrypted = fixture.File("encrypted.pdf");
        using var document = new Document(created);
        document.Encrypt(UserPassword, "owner-secret", Aspose.Pdf.Facades.DocumentPrivilege.AllowAll, CryptoAlgorithm.AESx256, usePdf20: false);
        document.Save(encrypted);
        return encrypted;
    }
}
