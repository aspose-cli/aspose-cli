using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Pdf;
using Aspose.Pdf.Facades;

namespace Aspose.Cli.Product.Pdf.Engine.Mapping;

internal sealed class PdfDocumentLoader(
    ResourceBudgetLedger resourceBudgets)
{
    /// <summary>
    /// How a PDF input that does not load is reported: Aspose.PDF raises
    /// <see cref="InvalidPasswordException"/> for a missing or wrong password, and any other
    /// exception of its own for bytes it cannot read.
    /// </summary>
    internal static readonly InputLoading Loading = new(
        "PDF document",
        "Verify the file opens in a PDF reader and that its bytes are a PDF rather than a renamed file.",
        static exception => exception switch
        {
            InvalidPasswordException => LoadFailureKind.Password,
            _ when exception.GetType().Assembly.GetName().Name == "Aspose.PDF" => LoadFailureKind.Corrupt,
            _ => LoadFailureKind.Other,
        });

    public LoadedPdf Open(string path, Secret? password)
    {
        InputSizeGuard.Ensure(resourceBudgets, path);
        return OpenCore(path, password);
    }

    // Generated candidates are bounded by publication, not a second user-input admission.
    internal LoadedPdf OpenPublishedCandidate(string path, Secret? password) => OpenCore(path, password);

    private LoadedPdf OpenCore(string path, Secret? password)
    {
        EnsurePdfHeader(path);

        FileStream? stream = null;
        try
        {
            // The engine reads the document from the stream as it needs it, so the stream
            // stays open for the document's lifetime.
            stream = InputFiles.OpenRead(path);
            var document = password is null
                ? new Document(stream)
                : new Document(stream, password.Reveal());
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
            var loaded = new LoadedPdf(document, fileInfo.PasswordType, fileInfo.HasOpenPassword, stream);
            stream = null;
            return loaded;
        }
        catch (Exception exception) when (exception is not CliException and not OperationCanceledException)
        {
            throw Loading.Failure(exception, path, password);
        }
        finally
        {
            stream?.Dispose();
        }
    }

    private static void EnsurePdfHeader(string path)
    {
        bool found = Loading.Load(path, password: null, () =>
        {
            using FileStream stream = InputFiles.OpenRead(path);
            int length = (int)Math.Min(1024, stream.Length);
            Span<byte> bytes = stackalloc byte[length];
            _ = stream.Read(bytes);
            return bytes.IndexOf("%PDF-"u8) >= 0;
        });
        if (!found)
        {
            throw Loading.Unreadable(path, "the PDF header was not found in the first 1024 bytes");
        }
    }
}

/// <summary>A loaded document and the input stream it reads from, disposed together.</summary>
/// <param name="Document">The loaded document.</param>
/// <param name="PasswordType">
/// The password the file was opened with. Aspose.PDF reports a file that has only an owner
/// password as opened with its empty user password, without one given; <paramref name="HasOpenPassword"/>
/// tells that case apart.
/// </param>
/// <param name="HasOpenPassword">Whether opening the file requires a user password.</param>
/// <param name="Source">The input stream the document reads from.</param>
internal sealed record LoadedPdf(Document Document, PasswordType PasswordType, bool HasOpenPassword, Stream Source)
    : IDisposable
{
    public void Dispose()
    {
        Document.Dispose();
        Source.Dispose();
    }
}
