using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.IO.Input;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.Formats;

namespace Aspose.Cli.Sdk.Tables;

/// <summary>A document a product's engine opened, which closes its engine objects and files when disposed.</summary>
/// <typeparam name="TDocument">The engine's document type.</typeparam>
public interface IOpenedDocument<out TDocument> : IOpenedInput, IDisposable
    where TDocument : class
{
    /// <summary>The engine's document.</summary>
    TDocument Document { get; }

    /// <summary>The format id the engine detected, such as <c>pptx</c>.</summary>
    string FormatId { get; }

    /// <summary>
    /// The CLI's own warnings about the document so far, such as the external resources the
    /// document policy left out (SECURITY.md); never a warning about the document's content.
    /// </summary>
    IReadOnlyList<Warning> Warnings { get; }
}

/// <summary>
/// The engine calls an object table product supplies for opening and saving: the only place
/// its engine is called outside the table's rows, adds and actions. Every engine warning it
/// receives through the engine's warning callback goes to <see cref="EngineWarningLog"/>, as
/// the engine reported it.
/// </summary>
/// <typeparam name="TDocument">The engine's document type.</typeparam>
public interface ITableDocuments<TDocument>
    where TDocument : class
{
    /// <summary>The product's table.</summary>
    DocumentTable<TDocument> Table { get; }

    /// <summary>The formats the engine opens and saves a whole document in; an edit writes the input's own.</summary>
    IReadOnlyList<FormatDescriptor> Formats { get; }

    /// <summary>Opens a user input, through the SDK's <c>InputOpening</c>.</summary>
    IOpenedDocument<TDocument> Open(string path, Secret? password, EngineWarningLog warnings);

    /// <summary>Opens an output this invocation just saved, to read it back.</summary>
    IOpenedDocument<TDocument> Reopen(string path, Secret? password, EngineWarningLog warnings);

    /// <summary>Saves the opened document in <paramref name="format"/>, with <paramref name="protection"/>.</summary>
    void Save(IOpenedDocument<TDocument> document, string path, string format, OutputProtection protection, EngineWarningLog warnings);
}

/// <summary>
/// What a batch reads outside the document before the document is opened: the files and images
/// its values name, resolved as the command line resolves paths, and the secrets its values name.
/// </summary>
public interface ITableInputs
{
    /// <summary>Resolves the path of a file an operation reads.</summary>
    string ResolvePath(string path);

    /// <summary>Reads an image file, resolved, and refuses it unless it is an intact image.</summary>
    byte[] ReadImage(string path);

    /// <summary>The secret an environment variable holds, or null when it is unset or empty.</summary>
    Secret? ReadSecret(string variable);
}

/// <summary>
/// What an action may use beside the document: other documents it opens through the product's
/// own open, which stay open until the edit ends.
/// </summary>
/// <typeparam name="TDocument">The engine's document type.</typeparam>
public sealed class TableActionContext<TDocument>
    where TDocument : class
{
    private readonly ITableDocuments<TDocument> _documents;
    private readonly EngineWarningLog _warnings;
    private readonly List<IOpenedDocument<TDocument>> _opened = [];

    internal TableActionContext(ITableDocuments<TDocument> documents, EngineWarningLog warnings)
    {
        _documents = documents;
        _warnings = warnings;
    }

    /// <summary>Opens another document as a user input; it is closed when the edit ends.</summary>
    public IOpenedDocument<TDocument> Open(string path, Secret? password)
    {
        IOpenedDocument<TDocument> opened = _documents.Open(path, password, _warnings);
        _opened.Add(opened);
        return opened;
    }

    /// <summary>What the document policy left out of each document the actions opened, while they are open.</summary>
    internal IReadOnlyList<Warning> Warnings => [.. _opened.SelectMany(static opened => opened.Warnings)];

    /// <summary>Closes every document the actions opened.</summary>
    internal void Close()
    {
        foreach (IOpenedDocument<TDocument> opened in _opened)
        {
            opened.Dispose();
        }

        _opened.Clear();
    }
}

/// <summary>The engine warnings of one invocation, in the order the engine reported them, none dropped or merged.</summary>
public sealed class EngineWarningLog
{
    private readonly List<EngineWarning> _warnings = [];

    /// <summary>When the warnings now reported arrive: <c>load</c>, <c>save</c> or <c>reopen</c>.</summary>
    public string During { get; set; } = "load";

    /// <summary>The warnings so far.</summary>
    public IReadOnlyList<EngineWarning> Warnings => _warnings;

    /// <summary>Records one warning as the engine's callback reported it.</summary>
    /// <param name="type">The engine's warning type, by its member name.</param>
    /// <param name="description">The engine's description.</param>
    public void Add(string type, string description)
    {
        lock (_warnings)
        {
            _warnings.Add(new EngineWarning { During = During, Type = type, Description = description });
        }
    }
}
