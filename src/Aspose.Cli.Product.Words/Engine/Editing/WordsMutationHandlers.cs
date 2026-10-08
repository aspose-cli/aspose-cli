using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Results;
using Aspose.Words;
using Aspose.Words.Fields;

namespace Aspose.Cli.Product.Words.Engine.Editing;

/// <summary>
/// Applies one validated Words operation at the anchors and sections resolved before the batch
/// started. Each operation has its handler in the content, formatting, table, object or
/// structure part of this class, and returns the number of items it changed.
/// </summary>
internal sealed partial class WordsMutationHandlers : IWordsOpHandler<long>
{
    private readonly LoadedDocument _loaded;
    private readonly Document _document;
    private readonly ResolvedWordsOp _resolved;
    private readonly OutputPipeline<Document> _outputs;
    private readonly WordsDocumentLoader _loader;
    private readonly InputSource _inputs;
    private readonly InputResourceScope _operationInputs;
    private readonly IReadOnlyDictionary<string, Secret>? _secrets;
    private readonly WordsRevisionTracking? _tracking;
    private readonly ICollection<Warning> _warnings;
    private readonly ICollection<Node> _changed;
    private readonly ICollection<Field> _pageFields;

    /// <summary>Creates the handlers of one resolved operation.</summary>
    /// <param name="loaded">The document being edited.</param>
    /// <param name="resolved">The operation with its original anchors and sections.</param>
    /// <param name="outputs">The write pipeline, which neutralizes the evaluation marks of imported documents.</param>
    /// <param name="loader">Opens the documents and Markdown that operations import.</param>
    /// <param name="inputs">Reads the merge data and watermark images that operations read.</param>
    /// <param name="operationInputs">Opens and charges the images that operations insert.</param>
    /// <param name="secrets">The operations' secrets by environment variable name.</param>
    /// <param name="tracking">The edit's revision tracking, or null when changes are not tracked.</param>
    /// <param name="warnings">Collects the warnings operations disclose about their result.</param>
    /// <param name="changed">Collects the nodes an operation without a block address changed.</param>
    /// <param name="pageFields">Collects the inserted page fields to update after the batch.</param>
    internal WordsMutationHandlers(
        LoadedDocument loaded,
        ResolvedWordsOp resolved,
        OutputPipeline<Document> outputs,
        WordsDocumentLoader loader,
        InputSource inputs,
        InputResourceScope operationInputs,
        IReadOnlyDictionary<string, Secret>? secrets,
        WordsRevisionTracking? tracking,
        ICollection<Warning> warnings,
        ICollection<Node> changed,
        ICollection<Field> pageFields)
    {
        _loaded = loaded;
        _document = loaded.Document;
        _resolved = resolved;
        _outputs = outputs;
        _loader = loader;
        _inputs = inputs;
        _operationInputs = operationInputs;
        _secrets = secrets;
        _tracking = tracking;
        _warnings = warnings;
        _changed = changed;
        _pageFields = pageFields;
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
