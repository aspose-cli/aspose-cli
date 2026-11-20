using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.IO.Files;
using Aspose.Cli.Sdk.IO.Input;
using Aspose.Cli.Sdk.Operations;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Tables;
using Aspose.Pdf;
using Aspose.Pdf.Facades;

namespace Aspose.Cli.Product.Pdf.Engine.Mapping;

internal sealed class PdfDocumentLoader
{
    private readonly ResourceBudgetLedger _resourceBudgets;
    private readonly OutputPipeline _outputs;

    /// <param name="resourceBudgets">The invocation's resource budgets.</param>
    /// <param name="outputs">The write pipeline, which learns whether each opened input is encrypted.</param>
    internal PdfDocumentLoader(ResourceBudgetLedger resourceBudgets, OutputPipeline outputs)
    {
        _resourceBudgets = resourceBudgets ?? throw new ArgumentNullException(nameof(resourceBudgets));
        _outputs = outputs ?? throw new ArgumentNullException(nameof(outputs));
    }

    /// <summary>
    /// How a PDF input that does not load is reported: Aspose.PDF raises
    /// <see cref="InvalidPasswordException"/> for a missing or wrong password, and any other
    /// exception of its own for bytes it cannot read.
    /// </summary>
    internal static readonly InputLoading Loading = new(
        "PDF document",
        static exception => exception switch
        {
            InvalidPasswordException => LoadFailureKind.Password,
            _ when exception.GetType().Assembly.GetName().Name == "Aspose.PDF" => LoadFailureKind.Corrupt,
            _ => LoadFailureKind.Other,
        });

    public LoadedPdf Open(string path, PasswordSource password) =>
        InputOpening.Open(_outputs, _resourceBudgets, PdfFormats.Definitions, path, () => OpenCore(path, password));

    /// <summary>Opens a user input with the password from the command's <c>--password-env</c>, if any.</summary>
    public LoadedPdf Open(string path, Secret? password) => Open(path, PasswordSource.Option(password));

    /// <summary>Opens the document a convert reads, with the password from its <c>--password-env</c>, if any.</summary>
    internal LoadedPdf OpenForConvert(string path, Secret? password) =>
        InputOpening.OpenForConvert(_outputs, _resourceBudgets, PdfFormats.Definitions, path, () => OpenCore(path, PasswordSource.Option(password)));

    // Generated candidates are bounded by publication, not a second user-input admission.
    internal LoadedPdf OpenPublishedCandidate(string path, Secret? password) =>
        InputOpening.OpenPublished(() => OpenCore(path, PasswordSource.Option(password)));

    /// <summary>
    /// Imports an HTML page: new Document(stream, new HtmlLoadOptions(folder) { CustomLoaderOfExternalResources }),
    /// the engine's default page size and margins.
    /// </summary>
    internal LoadedPdf OpenHtml(string path, HtmlImportResources resources) =>
        InputOpening.OpenForConvert(_outputs, _resourceBudgets, PdfFormats.Definitions, path, () =>
        {
            FileStream? stream = null;
            try
            {
                stream = InputFiles.OpenRead(path);
                var document = new Document(stream, new HtmlLoadOptions(resources.BaseDirectory) { CustomLoaderOfExternalResources = resources.Load });
                resources.ThrowIfFailed();
                var loaded = new LoadedPdf(document, PasswordType.None, false, false, false, stream);
                stream = null;
                return loaded;
            }
            catch (Exception exception) when (exception is not CliException and not OperationCanceledException)
            {
                // An import that throws has failed inside the engine: the page is no PDF that could
                // be corrupt. The references the loader supplied nothing for are listed as they are.
                resources.ThrowIfFailed();
                if (resources.NotSupplied() is { } details)
                {
                    throw EngineErrors.EngineFailed($"PDF failed inside its document engine: {exception.Message}", exception, details);
                }

                throw;
            }
            finally
            {
                stream?.Dispose();
            }
        });

    private LoadedPdf OpenCore(string path, PasswordSource password)
    {
        FileStream? stream = null;
        try
        {
            // The engine reads the document from the stream as it needs it, so the stream
            // stays open for the document's lifetime.
            stream = InputFiles.OpenRead(path);
            var document = password.Password is null
                ? new Document(stream)
                : new Document(stream, password.Password.Reveal());
            try
            {
                _resourceBudgets.EnsureWithin(
                    PdfBudgetDomains.Pages,
                    document.Pages.Count,
                    "items",
                    "post-load");
                // Annotations are the per-page objects a loaded PDF exposes; Paragraphs
                // belongs to the generator model and is always empty after a load.
                long objects = document.Pages.Cast<Page>()
                    .Sum(static page => (long)page.Annotations.Count);
                _resourceBudgets.EnsureWithin(
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
            // Aspose.PDF reports the one password of a file whose open and owner passwords are
            // the same as its user password, and the file as having no edit password.
            bool openPasswordIsOwner = document.IsEncrypted && fileInfo.HasOpenPassword && !fileInfo.HasEditPassword;
            var loaded = new LoadedPdf(
                document,
                openPasswordIsOwner ? PasswordType.Owner : fileInfo.PasswordType,
                fileInfo.HasOpenPassword,
                fileInfo.HasEditPassword || openPasswordIsOwner,
                openPasswordIsOwner,
                stream);
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
}

/// <summary>A loaded document and the input stream it reads from, disposed together.</summary>
/// <param name="Document">The loaded document.</param>
/// <param name="PasswordType">
/// The password the file was opened with: <c>Owner</c> for a password that is the owner
/// password, also when it is the open password too. Aspose.PDF reports a file that has only an
/// owner password as opened with its empty user password, without one given;
/// <paramref name="HasOpenPassword"/> tells that case apart.
/// </param>
/// <param name="HasOpenPassword">Whether opening the file requires a user password.</param>
/// <param name="HasOwnerPassword">Whether the file has an owner password, which lifts its permissions.</param>
/// <param name="OpenPasswordIsOwner">
/// Whether the file's open password is also its owner password, so whoever opens it has every
/// permission, as <c>--encrypt</c> writes it.
/// </param>
/// <param name="Source">The input stream the document reads from.</param>
internal sealed record LoadedPdf(
    Document Document, PasswordType PasswordType, bool HasOpenPassword, bool HasOwnerPassword, bool OpenPasswordIsOwner, Stream Source)
    : IOpenedDocument<Document>
{
    /// <inheritdoc />
    public string FormatId => "pdf";

    /// <inheritdoc />
    public IReadOnlyList<Warning> Warnings => [];

    /// <summary>Whether the file is encrypted, as it was opened; a later decryption does not change it.</summary>
    public bool Encrypted { get; } = Document.IsEncrypted;

    /// <inheritdoc />
    public InputProtection Protection => new(Encrypted, HasOpenPassword);

    public void Dispose()
    {
        Document.Dispose();
        Source.Dispose();
    }
}
