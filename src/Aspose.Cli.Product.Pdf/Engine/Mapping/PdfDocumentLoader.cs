using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Results;
using Aspose.Pdf;
using Aspose.Pdf.Facades;

namespace Aspose.Cli.Product.Pdf.Engine.Mapping;

internal sealed class PdfDocumentLoader
{
    private readonly ResourceBudgetLedger _resourceBudgets;
    private readonly OutputPipeline? _outputs;

    /// <param name="resourceBudgets">The invocation's resource budgets.</param>
    /// <param name="outputs">The write pipeline, which learns whether each opened input is encrypted.</param>
    internal PdfDocumentLoader(ResourceBudgetLedger resourceBudgets, OutputPipeline outputs)
        : this(resourceBudgets)
    {
        _outputs = outputs ?? throw new ArgumentNullException(nameof(outputs));
    }

    private PdfDocumentLoader(ResourceBudgetLedger resourceBudgets)
    {
        _resourceBudgets = resourceBudgets ?? throw new ArgumentNullException(nameof(resourceBudgets));
    }

    /// <summary>A loader for font inspection, which publishes nothing.</summary>
    internal static PdfDocumentLoader ForFontInspection(ResourceBudgetLedger resourceBudgets) => new(resourceBudgets);

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

    public LoadedPdf Open(string path, PasswordSource password) =>
        InputOpening.Open(_outputs, _resourceBudgets, path, () => OpenCore(path, password));

    /// <summary>Opens a user input with the password from the command's <c>--password-env</c>, if any.</summary>
    public LoadedPdf Open(string path, Secret? password) => Open(path, PasswordSource.Option(password));

    /// <summary>
    /// The <c>encrypt_document</c> fields that grant a copy what the document grants readers
    /// without the owner password, named as <c>pdf inspect --detail permissions</c> names them.
    /// </summary>
    private static JsonObject Restrictions(Document document)
    {
        var permissions = (Permissions)document.Permissions;
        var granted = new PdfPermissionsInput
        {
            Print = permissions.HasFlag(Permissions.PrintDocument),
            Copy = permissions.HasFlag(Permissions.ExtractContent),
            Modify = permissions.HasFlag(Permissions.ModifyContent),
            Annotate = permissions.HasFlag(Permissions.ModifyTextAnnotations),
            FillForms = permissions.HasFlag(Permissions.FillForm),
            ExtractAccessibility = permissions.HasFlag(Permissions.ExtractContentWithDisabilities),
            Assemble = permissions.HasFlag(Permissions.AssembleDocument),
            PrintHighResolution = permissions.HasFlag(Permissions.PrintingQuality),
        };
        return new JsonObject
        {
            ["permissions"] = JsonSerializer.SerializeToNode(granted, PdfOpsJsonContext.Default.PdfPermissionsInput),
        };
    }

    // Generated candidates are bounded by publication, not a second user-input admission.
    internal LoadedPdf OpenPublishedCandidate(string path, Secret? password) =>
        InputOpening.OpenPublished(() => OpenCore(path, PasswordSource.Option(password)));

    // An encrypted file's restrictions are read once it has loaded, outside the translation of
    // its load failures: an engine failure to read them is not a damaged input.
    private LoadedPdf OpenCore(string path, PasswordSource password)
    {
        LoadedPdf loaded = Load(path, password);
        if (!loaded.Encrypted)
        {
            return loaded;
        }

        try
        {
            return loaded with { Restrictions = Restrictions(loaded.Document) };
        }
        catch
        {
            loaded.Dispose();
            throw;
        }
    }

    private LoadedPdf Load(string path, PasswordSource password)
    {
        EnsurePdfHeader(path);

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

    private static void EnsurePdfHeader(string path)
    {
        bool found = Loading.Load(path, password: default, () =>
        {
            using FileStream stream = InputFiles.OpenRead(path);
            return ContainerSignatures.HasPdfHeader(ContainerSignatures.ReadPrefix(stream));
        });
        if (!found)
        {
            throw Loading.Unreadable(path, $"the PDF header was not found in the first {ContainerSignatures.PdfHeaderWindow} bytes");
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
    : IOpenedInput, IDisposable
{
    /// <summary>Whether the file is encrypted, as it was opened; a later decryption does not change it.</summary>
    public bool Encrypted { get; } = Document.IsEncrypted;

    /// <summary>
    /// For an encrypted file, the <c>encrypt_document</c> fields that grant a copy what the file
    /// grants readers without the owner password; null for a file that is not encrypted.
    /// </summary>
    public JsonObject? Restrictions { get; init; }

    /// <inheritdoc />
    public InputProtection Protection => new(Encrypted, HasOpenPassword, Restrictions);

    public void Dispose()
    {
        Document.Dispose();
        Source.Dispose();
    }
}
