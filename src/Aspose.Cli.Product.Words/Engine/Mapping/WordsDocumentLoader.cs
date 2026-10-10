using System.Text;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Words;
using Aspose.Words.Loading;

namespace Aspose.Cli.Product.Words.Engine.Mapping;

internal sealed class WordsDocumentLoader
{
    /// <summary>
    /// How a word-processing input that does not load is reported: Aspose.Words raises
    /// <see cref="IncorrectPasswordException"/> for a missing or wrong password,
    /// <see cref="FileCorruptedException"/> for damaged bytes, the parser's
    /// <see cref="System.Xml.XmlException"/> for damaged WordML or Flat OPC XML and
    /// <see cref="UnsupportedFileFormatException"/> for a format it cannot load.
    /// </summary>
    internal static readonly InputLoading Loading = new(
        "supported word-processing document",
        "Verify the file opens in Word and that its content matches a format listed by 'aspose-cli capabilities'.",
        Classify);

    // A damaged PDF is named as a PDF, so the hint sends the reader to a PDF reader, not Word.
    private static readonly InputLoading PdfLoading = new(
        "PDF document",
        "Verify the file opens in a PDF reader; a damaged or truncated PDF cannot be read.",
        Classify);

    private readonly ResourceBudgetLedger _resourceBudgets;
    private readonly ILicenseState? _license;

    /// <summary>
    /// Creates a loader. With the license state, loaded documents know whether evaluation mode
    /// altered them; without one (font inspection) no evaluation artifacts are reported.
    /// </summary>
    internal WordsDocumentLoader(ResourceBudgetLedger resourceBudgets, ILicenseState? license = null)
    {
        _resourceBudgets = resourceBudgets ?? throw new ArgumentNullException(nameof(resourceBudgets));
        _license = license;
    }

    /// <summary>
    /// Opens the built-in A4 design that new documents use without a template. It is a
    /// product resource, so it loads under the deny-all resource policy.
    /// </summary>
    internal static Document OpenDefaultTemplate()
    {
        using Stream stream = typeof(WordsDocumentLoader).Assembly.GetManifestResourceStream(DefaultTemplateResource)
            ?? throw new InvalidOperationException($"The built-in resource {DefaultTemplateResource} is missing.");
        return new Document(stream, new LoadOptions { ResourceLoadingCallback = DenyAllResources.Instance });
    }

    // The built-in design draws East Asian text in Microsoft YaHei and declares Chinese as its East
    // Asian language, without which the engine ignores the East Asian line-breaking rules
    // (WORDS-CJK-LINE-BREAK).
    private const string DefaultTemplateResource = "Templates/default-a4.docx";

    /// <summary>
    /// Opens an admitted input. <paramref name="warnings"/> receives what the SDK reports while
    /// it loads the document; font substitutions arrive only when the document is laid out.
    /// </summary>
    public LoadedDocument Open(string path, Secret? password, IWarningCallback? warnings = null)
    {
        InputSizeGuard.Ensure(_resourceBudgets, path);
        return OpenCore(path, password, warnings);
    }

    // Generated candidates are bounded by publication, not a second user-input admission.
    internal LoadedDocument OpenPublishedCandidate(string path, Secret? password) => OpenCore(path, password, warnings: null);

    private LoadedDocument OpenCore(string path, Secret? password, IWarningCallback? warnings)
    {
        InputLoading loading = For(path);
        using FileStream input = loading.Load(path, password, () => InputFiles.OpenRead(path));
        return OpenCore(input, path, password, loading, warnings);
    }

    private LoadedDocument OpenCore(FileStream input, string path, Secret? password, InputLoading loading, IWarningCallback? warnings)
    {
        FileFormatInfo detected = loading.Load(path, password, () =>
        {
            FileFormatInfo format = FileFormatUtil.DetectFileFormat(input);
            input.Position = 0;
            return format;
        });

        string id = WordsEngineFormats.IdOf(detected.LoadFormat);
        if (id == "unknown" || IsTextFallbackForNonTextPath(id, path))
        {
            throw loading.Unreadable(
                path,
                id == "unknown"
                    ? "the SDK could not identify a supported document format"
                    : "the bytes were detected as plain text but the file name claims a document container");
        }

        if (!WordsFormats.IsLoad(id))
        {
            throw loading.Unloadable(path, id);
        }

        if (detected.IsEncrypted && password is null)
        {
            throw InputLoading.PasswordRefused(path, password);
        }

        var resources = new LocalDocumentResourceLoader(path, _resourceBudgets);
        try
        {
            Document document = Load(options => new Document(input, options),
                detected.LoadFormat, resources, path, password, loading, warnings);
            // A stream carries no name; FILENAME fields name the file, as a path load does.
            document.FieldOptions.FileName = path;
            return new LoadedDocument(document, detected, id, resources,
                _license?.IsEvaluation == true);
        }
        catch
        {
            resources.Dispose();
            throw;
        }
    }

    /// <summary>
    /// The format id <paramref name="path"/>'s content has, detected without loading it; null when
    /// detection fails, since the load that follows reports why.
    /// </summary>
    internal static string? DetectFormatId(string path)
    {
        try
        {
            using FileStream input = InputFiles.OpenRead(path);
            string id = WordsEngineFormats.IdOf(FileFormatUtil.DetectFileFormat(input).LoadFormat);
            return id == "unknown" ? null : id;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>Imports inline Markdown using the owning document's resource boundary and lifetime.</summary>
    internal Document OpenMarkdown(string markdown, LoadedDocument owner)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        ArgumentNullException.ThrowIfNull(owner);
        _resourceBudgets.Consume(ResourceBudgetKinds.MemoryBufferBytes,
            Encoding.UTF8.GetByteCount(markdown), "bytes", "markdown-buffer");
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(markdown), writable: false);
        Document document = Load(options => new Document(input, options),
            LoadFormat.Markdown, owner.Resources, "inline Markdown", password: null, Loading, warnings: null, onDisk: false);
        owner.Retain(document);
        return document;
    }

    /// <summary>The invocation's resource budgets, for operations that allocate beyond the document.</summary>
    internal ResourceBudgetLedger ResourceBudgets => _resourceBudgets;

    internal void EnsureWithinBudgets(Document document, LocalDocumentResourceLoader resources)
    {
        resources.ThrowIfFailed();
        // The node count is cheap; the page count lays out the whole document, so it goes last.
        _resourceBudgets.EnsureWithin(
            WordsBudgetDomains.Nodes, document.GetChildNodes(NodeType.Any, true).Count,
            "items", "projection");
        _resourceBudgets.EnsureWithin(
            WordsBudgetDomains.Pages, document.PageCount, "items", "post-load");
        resources.ThrowIfFailed();
    }

    /// <summary>
    /// Rejects an operation before it allocates <paramref name="additional"/> nodes that would take
    /// the document past its node budget.
    /// </summary>
    internal void EnsureNodeCapacity(Document document, long additional) =>
        _resourceBudgets.EnsureWithin(
            WordsBudgetDomains.Nodes,
            checked(document.GetChildNodes(NodeType.Any, true).Count + additional),
            "items",
            "pre-allocation");

    private Document Load(Func<LoadOptions, Document> open, LoadFormat format,
        LocalDocumentResourceLoader resources, string path, Secret? password, InputLoading loading, IWarningCallback? warnings,
        bool onDisk = true)
    {
        Document? document = null;
        try
        {
            document = open(new LoadOptions
            {
                Password = password?.Reveal(),
                LoadFormat = format,
                BaseUri = resources.BaseUri,
                ResourceLoadingCallback = new BlockingResourceCallback(resources),
                PreserveIncludePictureField = true,
                WarningCallback = warnings,
            });
            EnsureWithinBudgets(document, resources);
            return document;
        }
        catch (Exception exception)
        {
            try
            {
                resources.ThrowIfFailed();
                if (exception is CliException or OperationCanceledException)
                {
                    throw;
                }
                throw onDisk ? loading.Failure(exception, path, password) : loading.InMemoryFailure(exception, path);
            }
            finally { document?.Cleanup(); }
        }
    }

    private static LoadFailureKind Classify(Exception exception) => exception switch
    {
        IncorrectPasswordException => LoadFailureKind.Password,
        FileCorruptedException or System.Xml.XmlException or UnsupportedFileFormatException => LoadFailureKind.Corrupt,
        _ => LoadFailureKind.Other,
    };

    /// <summary>The translation for an input: a file that starts as a PDF is named as one.</summary>
    private static InputLoading For(string path) => StartsAsPdf(path) ? PdfLoading : Loading;

    private static bool IsTextFallbackForNonTextPath(string formatId, string path)
    {
        if (formatId is not ("txt" or "md"))
        {
            return false;
        }

        string extension = Path.GetExtension(path);
        return formatId == "txt"
            ? !string.Equals(extension, ".txt", StringComparison.OrdinalIgnoreCase)
            : !string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Whether a file begins with the PDF header; false when it cannot be read.</summary>
    private static bool StartsAsPdf(string path) =>
        ContainerSignatures.StartsWithPdfHeader(ContainerSignatures.ReadPrefix(path, ContainerSignatures.PdfHeader.Length));

    private sealed class DenyAllResources : IResourceLoadingCallback
    {
        internal static readonly DenyAllResources Instance = new();

        public ResourceLoadingAction ResourceLoading(ResourceLoadingArgs args) => ResourceLoadingAction.Skip;
    }

    private sealed class BlockingResourceCallback : IResourceLoadingCallback
    {
        private readonly LocalDocumentResourceLoader _resources;

        public BlockingResourceCallback(LocalDocumentResourceLoader resources)
        {
            _resources = resources;
        }

        public ResourceLoadingAction ResourceLoading(ResourceLoadingArgs args)
        {
            if (_resources.TryRead(args.Uri, out byte[] data))
            {
                args.SetData(data);
                return ResourceLoadingAction.UserProvided;
            }

            return ResourceLoadingAction.Skip;
        }
    }
}

internal sealed record LoadedDocument(
    Document Document,
    FileFormatInfo Format,
    string FormatId,
    LocalDocumentResourceLoader Resources,
    bool Evaluation) : IDisposable
{
    private readonly List<Document> _imports = [];

    /// <summary>Whether evaluation mode cut this document short while loading it.</summary>
    public bool EvaluationInputTruncated { get; } = Evaluation && WordsEvaluation.IsTruncated(Document);

    /// <summary>Whether evaluation mode cut short a document imported into this one.</summary>
    public bool ImportedInputTruncated { get; private set; }

    internal void Retain(Document document)
    {
        _imports.Add(document);
        ImportedInputTruncated |= Evaluation && WordsEvaluation.IsTruncated(document);
    }

    /// <summary>Records content imported from another loaded document.</summary>
    internal void Imported(LoadedDocument source) =>
        ImportedInputTruncated |= source.EvaluationInputTruncated || source.ImportedInputTruncated;

    public int RemoteResourcesBlocked
    {
        get
        {
            Resources.ThrowIfFailed();
            return Resources.OmittedCount;
        }
    }

    public void Dispose()
    {
        try
        {
            Document.Cleanup();
            foreach (Document document in _imports) { document.Cleanup(); }
        }
        finally { Resources.Dispose(); }
    }
}
