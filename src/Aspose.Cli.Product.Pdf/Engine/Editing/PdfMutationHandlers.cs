using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Pdf;

namespace Aspose.Cli.Product.Pdf.Engine.Editing;

/// <summary>
/// Applies validated PDF operations to one document. Each operation has its handler in the
/// page, content or document part of this class, and returns the number of items it changed.
/// </summary>
internal sealed partial class PdfMutationHandlers : IPdfOpHandler<long>
{
    private readonly PdfDocumentLoader _loader;
    private readonly InputResourceScope _inputs;
    private readonly Document _document;
    private readonly IReadOnlyDictionary<string, string>? _secrets;
    private readonly ISet<int> _touched;
    private readonly ISet<int> _textMoved;

    /// <summary>Creates the handlers of one operation.</summary>
    /// <param name="loader">Opens the PDFs that operations insert.</param>
    /// <param name="inputs">Reads and charges the files that operations read.</param>
    /// <param name="document">The document being edited.</param>
    /// <param name="secrets">The operations' secrets by environment variable name.</param>
    /// <param name="touched">Receives the pages the operation changes.</param>
    /// <param name="textMoved">Receives the pages on which a redaction moved the remaining text.</param>
    internal PdfMutationHandlers(
        PdfDocumentLoader loader,
        InputResourceScope inputs,
        Document document,
        IReadOnlyDictionary<string, string>? secrets,
        ISet<int> touched,
        ISet<int> textMoved)
    {
        _loader = loader;
        _inputs = inputs;
        _document = document;
        _secrets = secrets;
        _touched = touched;
        _textMoved = textMoved;
    }

    /// <summary>Applies one operation; an Aspose.PDF or I/O failure becomes an engine failure.</summary>
    internal long Run(PdfOp operation)
    {
        try
        {
            return operation.Accept(this);
        }
        catch (Exception exception) when (
            exception.GetType().Assembly.GetName().Name == "Aspose.PDF"
            || exception is IOException or UnauthorizedAccessException)
        {
            throw new EngineOpException(exception.Message, exception);
        }
        finally { _inputs.ThrowIfFailed(); }
    }
}
