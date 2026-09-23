using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Pdf;
using Aspose.Pdf.Facades;

namespace Aspose.Cli.Product.Pdf.Engine.Mapping;

internal sealed class PdfDocumentLoader(
    ResourceBudgetLedger resourceBudgets)
{
    public LoadedPdf Open(string path, string? password)
    {
        InputSizeGuard.Ensure(resourceBudgets, path);
        return OpenCore(path, password);
    }

    // Generated candidates are bounded by publication, not a second user-input admission.
    internal LoadedPdf OpenPublishedCandidate(string path, string? password) => OpenCore(path, password);

    private LoadedPdf OpenCore(string path, string? password)
    {
        EnsurePdfHeader(path);

        try
        {
            var document = string.IsNullOrEmpty(password)
                ? new Document(path)
                : new Document(path, password);
            try
            {
                resourceBudgets.EnsureWithin(
                    PdfBudgetDomains.Pages,
                    document.Pages.Count,
                    "items",
                    "post-load");
                // Annotations are the per-page objects a loaded PDF exposes; Paragraphs
                // belongs to the generator model and is always empty after a load.
                long objects = document.Pages.Cast<Page>()
                    .Sum(static page => (long)page.Annotations.Count);
                resourceBudgets.EnsureWithin(
                    PdfBudgetDomains.Objects,
                    objects,
                    "items",
                    phase: "post-load");
            }
            catch
            {
                document.Dispose();
                throw;
            }
            var fileInfo = new PdfFileInfo(document);
            return new LoadedPdf(document, fileInfo.PasswordType);
        }
        catch (InvalidPasswordException)
        {
            throw string.IsNullOrEmpty(password)
                ? CliErrors.PasswordRequired(path)
                : CliErrors.PasswordInvalid(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw exception is UnauthorizedAccessException
                ? CliErrors.FileAccessDenied(path)
                : File.Exists(path) ? CliErrors.FileLocked(path) : CliErrors.FileNotFound(path);
        }
        catch (Exception exception) when (exception.GetType().Assembly.GetName().Name == "Aspose.PDF")
        {
            throw InvalidPdf(path, exception.Message, exception);
        }
    }

    private static void EnsurePdfHeader(string path)
    {
        try
        {
            using FileStream stream = File.OpenRead(path);
            int length = (int)Math.Min(1024, stream.Length);
            Span<byte> bytes = stackalloc byte[length];
            _ = stream.Read(bytes);
            if (bytes.IndexOf("%PDF-"u8) < 0)
            {
                throw InvalidPdf(path, "the PDF header was not found in the first 1024 bytes");
            }
        }
        catch (CliException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw exception is UnauthorizedAccessException
                ? CliErrors.FileAccessDenied(path)
                : File.Exists(path) ? CliErrors.FileLocked(path) : CliErrors.FileNotFound(path);
        }
    }

    private static CliException InvalidPdf(string path, string reason, Exception? inner = null) => new(
        ErrorCodes.FileCorrupt,
        $"Input is not a valid PDF document: {path} ({reason}).",
        hint: "Verify the file opens in a PDF reader and that its bytes are a PDF rather than a renamed file.",
        innerException: inner);
}

internal sealed record LoadedPdf(Document Document, PasswordType PasswordType) : IDisposable
{
    public void Dispose() => Document.Dispose();
}
