using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Input;
using Aspose.Pdf;

namespace Aspose.Cli.Product.Pdf.Engine.Editing;

/// <summary>
/// Applies validated PDF operations to one document. Each operation has its handler in the
/// page, content or document part of this class; what it changed is the SDK's comparison of
/// the snapshots <see cref="PdfSnapshots"/> takes around it.
/// </summary>
internal sealed partial class PdfMutationHandlers : IPdfOpHandler<ValueTuple>
{
    private readonly PdfDocumentLoader _loader;
    private readonly InputResourceScope _inputs;
    private readonly Document _document;
    private readonly IReadOnlyDictionary<string, Secret>? _secrets;
    private readonly PdfPageIdentities _pages;
    private readonly ISet<int> _textMoved;
    private readonly ICollection<(int Page, int Lines, int Characters)> _beside;
    private readonly ISet<int> _numbered;

    /// <summary>Creates the handlers of one operation.</summary>
    /// <param name="loader">Opens the PDFs that operations insert.</param>
    /// <param name="inputs">Reads and charges the files that operations read.</param>
    /// <param name="document">The document being edited.</param>
    /// <param name="secrets">The operations' secrets by environment variable name.</param>
    /// <param name="pages">The identities of the document's pages, which a moved page keeps.</param>
    /// <param name="textMoved">Receives the pages on which a redaction moved the remaining text.</param>
    /// <param name="beside">Receives, by page, how many characters on how many lines a redaction took beside its selection.</param>
    /// <param name="numbered">Receives the pages add_page_numbers numbered whose header or footer already showed a page number.</param>
    internal PdfMutationHandlers(
        PdfDocumentLoader loader,
        InputResourceScope inputs,
        Document document,
        IReadOnlyDictionary<string, Secret>? secrets,
        PdfPageIdentities pages,
        ISet<int> textMoved,
        ICollection<(int Page, int Lines, int Characters)> beside,
        ISet<int> numbered)
    {
        _loader = loader;
        _inputs = inputs;
        _document = document;
        _secrets = secrets;
        _pages = pages;
        _textMoved = textMoved;
        _beside = beside;
        _numbered = numbered;
    }

    /// <summary>
    /// Applies one operation; an Aspose.PDF failure becomes an engine failure. A file the
    /// operation reads is opened through the SDK, which reports a file it cannot open.
    /// </summary>
    internal void Run(PdfOp operation)
    {
        try
        {
            Engine(() => operation.Accept(this));
        }
        finally { _inputs.ThrowIfFailed(); }
    }

    /// <summary>Calls Aspose.PDF; a failure inside it becomes an engine failure.</summary>
    internal static T Engine<T>(Func<T> call)
    {
        try
        {
            return call();
        }
        catch (Exception exception) when (exception.GetType().Assembly.GetName().Name == "Aspose.PDF")
        {
            throw new EngineOpException(exception.Message, exception);
        }
    }
}
