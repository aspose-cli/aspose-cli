using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.IO;
using Aspose.Words;

namespace Aspose.Cli.Product.Words.Engine.Editing;

/// <summary>
/// Applies one validated Words operation at the anchors and sections resolved before the batch
/// started. Each operation has its handler in the content, formatting, table, object or
/// structure part of this class, and returns the number of items it changed.
/// </summary>
internal sealed partial class WordsOpHandlers : IWordsOpHandler<long>
{
    private readonly LoadedDocument _loaded;
    private readonly Document _document;
    private readonly ResolvedWordsOp _resolved;
    private readonly WordsDocumentLoader _loader;
    private readonly InputSource _inputs;
    private readonly InputResourceScope _operationInputs;
    private readonly IReadOnlyDictionary<string, string>? _secrets;

    /// <summary>Creates the handlers of one resolved operation.</summary>
    /// <param name="loaded">The document being edited.</param>
    /// <param name="resolved">The operation with its original anchors and sections.</param>
    /// <param name="loader">Opens the documents and Markdown that operations import.</param>
    /// <param name="inputs">Reads the merge data and watermark images that operations read.</param>
    /// <param name="operationInputs">Opens and charges the images that operations insert.</param>
    /// <param name="secrets">The operations' secrets by environment variable name.</param>
    internal WordsOpHandlers(
        LoadedDocument loaded,
        ResolvedWordsOp resolved,
        WordsDocumentLoader loader,
        InputSource inputs,
        InputResourceScope operationInputs,
        IReadOnlyDictionary<string, string>? secrets)
    {
        _loaded = loaded;
        _document = loaded.Document;
        _resolved = resolved;
        _loader = loader;
        _inputs = inputs;
        _operationInputs = operationInputs;
        _secrets = secrets;
    }

    /// <summary>The operation's target blocks.</summary>
    private IReadOnlyList<Node> Nodes => _resolved.Nodes;

    /// <summary>The block an insertion is placed before or after.</summary>
    private Node Anchor => _resolved.Nodes[0];

    /// <summary>The operation's target sections.</summary>
    private IReadOnlyList<Section> Sections => _resolved.Sections;

    /// <summary>Applies the operation unless an earlier one removed its anchor.</summary>
    internal long Run()
    {
        try
        {
            WordsAnchorResolver.EnsureAttached(_document, _resolved);
            return _resolved.Op.Accept(this);
        }
        finally { _operationInputs.ThrowIfFailed(); }
    }
}
