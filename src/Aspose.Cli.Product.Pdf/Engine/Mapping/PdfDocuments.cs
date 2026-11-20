using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.Tables;
using Aspose.Pdf;
using Aspose.Pdf.Facades;

namespace Aspose.Cli.Product.Pdf.Engine.Mapping;

/// <summary>
/// How the object table opens and saves a PDF: the loader's open, and Document.Save. An output
/// <c>--encrypt</c> gives a password is decrypted when it carries encryption and encrypted with
/// that password as both the user and the owner password, every permission granted, AES-256;
/// otherwise the engine saves the document with the encryption it carries.
/// </summary>
internal sealed class PdfDocuments(PdfDocumentLoader loader) : ITableDocuments<Document>
{
    /// <inheritdoc />
    public DocumentTable<Document> Table => PdfTable.Definition;

    /// <inheritdoc />
    public IReadOnlyList<Sdk.Formats.FormatDescriptor> Formats => PdfFormats.Document;

    /// <inheritdoc />
    public IOpenedDocument<Document> Open(string path, Secret? password, EngineWarningLog warnings) => loader.Open(path, password);

    /// <inheritdoc />
    public IOpenedDocument<Document> Reopen(string path, Secret? password, EngineWarningLog warnings) => loader.OpenPublishedCandidate(path, password);

    /// <inheritdoc />
    public void Save(IOpenedDocument<Document> document, string path, string format, OutputProtection protection, EngineWarningLog warnings) =>
        Save(document.Document, path, protection);

    /// <summary>Saves a PDF, encrypted as <paramref name="protection"/> says.</summary>
    internal void Save(Document pdf, string path, OutputProtection protection)
    {
        if (protection is OutputProtection.Replace replace)
        {
            if (pdf.IsEncrypted)
            {
                pdf.Decrypt();
            }

            string password = replace.NewPassword.Reveal();
            pdf.Encrypt(password, password, DocumentPrivilege.AllowAll, CryptoAlgorithm.AESx256, usePdf20: false);
        }

        pdf.Save(path);
    }
}
