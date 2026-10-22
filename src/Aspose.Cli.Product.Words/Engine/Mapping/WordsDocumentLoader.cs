using System.Text;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Formats;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.IO.Files;
using Aspose.Cli.Sdk.IO.Input;
using Aspose.Cli.Sdk.Results;
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
    private readonly OutputPipeline<Document>? _outputs;

    /// <summary>
    /// Creates a loader. Loaded documents know whether evaluation mode altered them, and the
    /// write pipeline learns of each input the command read, which discloses one evaluation mode
    /// cut short, and whether it is encrypted.
    /// </summary>
    internal WordsDocumentLoader(ResourceBudgetLedger resourceBudgets, OutputPipeline<Document> outputs)
        : this(resourceBudgets)
    {
        _outputs = outputs ?? throw new ArgumentNullException(nameof(outputs));
    }

    private WordsDocumentLoader(ResourceBudgetLedger resourceBudgets)
    {
        _resourceBudgets = resourceBudgets ?? throw new ArgumentNullException(nameof(resourceBudgets));
    }

    /// <summary>A loader for font inspection, which publishes nothing: no evaluation artifacts are reported.</summary>
    internal static WordsDocumentLoader ForFontInspection(ResourceBudgetLedger resourceBudgets) => new(resourceBudgets);

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
    public LoadedDocument Open(string path, PasswordSource password, IWarningCallback? warnings = null)
    {
        LoadedDocument loaded = InputOpening.Open(_outputs, _resourceBudgets, path, () => OpenCore(path, password, warnings));
        _outputs?.ReadInput(loaded.Document);
        return loaded;
    }

    /// <summary>Opens an admitted input with the password from the command's <c>--password-env</c>, if any.</summary>
    public LoadedDocument Open(string path, Secret? password, IWarningCallback? warnings = null) =>
        Open(path, PasswordSource.Option(password), warnings);

    // Generated candidates are bounded by publication, not a second user-input admission.
    internal LoadedDocument OpenPublishedCandidate(string path, Secret? password) =>
        InputOpening.OpenPublished(() => OpenCore(path, PasswordSource.Option(password), warnings: null));

    private LoadedDocument OpenCore(string path, PasswordSource password, IWarningCallback? warnings)
    {
        InputLoading loading = For(path);
        using FileStream input = loading.Load(path, password, () => InputFiles.OpenRead(path));
        return OpenCore(input, path, password, loading, warnings);
    }

    private LoadedDocument OpenCore(FileStream input, string path, PasswordSource password, InputLoading loading, IWarningCallback? warnings)
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

        if (detected.IsEncrypted && password.Password is null)
        {
            throw InputLoading.PasswordRefused(path, password);
        }

        bool fixedPages = WordsEngineFormats.Has(id, static format => format.MayHoldFixedPages) && IsFixedPageHtml(input);
        var resources = new LocalDocumentResourceLoader(path, _resourceBudgets);
        try
        {
            Document document = Load(options => new Document(input, options),
                detected.LoadFormat, resources, path, password, loading, warnings);
            // A stream carries no name; FILENAME fields name the file, as a path load does.
            document.FieldOptions.FileName = path;
            return new LoadedDocument(document, detected, id, resources,
                _outputs?.IsEvaluation == true)
            {
                FixedPageHtml = fixedPages,
            };
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
        // WORDS-MD-FOOTNOTE-HEADING: inline and file Markdown load as written; the Words Skill
        // tells authors to put footnote definitions at the end.
        Document document = Load(options => new Document(input, options),
            LoadFormat.Markdown, owner.Resources, "inline Markdown", password: default, Loading, warnings: null, onDisk: false);
        owner.Retain(document);
        _outputs?.ReadInput(document);
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
        LocalDocumentResourceLoader resources, string path, PasswordSource password, InputLoading loading, IWarningCallback? warnings,
        bool onDisk = true)
    {
        Document? document = null;
        try
        {
            document = open(new LoadOptions
            {
                Password = password.Password?.Reveal(),
                LoadFormat = format,
                BaseUri = resources.BaseUri,
                ResourceLoadingCallback = new BlockingResourceCallback(resources, checksImages: format == LoadFormat.Markdown),
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

    /// <summary>
    /// Whether bytes detected as plain text or Markdown come under a name no extension of that
    /// format declares, as a damaged document container's do.
    /// </summary>
    private static bool IsTextFallbackForNonTextPath(string formatId, string path) =>
        WordsEngineFormats.Has(formatId, static format => format.PlainText)
        && new[] { WordsFormats.Definitions.Named(FormatUse.Input, formatId)! }.NamedBy(path).Count == 0;

    // The page container html-fixed output gives each page; the SDK has no reader for it.
    private const string FixedPageMarker = "class=\"awdiv awpage\"";

    /// <summary>
    /// Whether an HTML input is an html-fixed page rendition, which the SDK detects and loads as
    /// flowing HTML. The text is scanned in chunks, since embedded fonts can make it large, and
    /// the stream is left at its start.
    /// </summary>
    private static bool IsFixedPageHtml(Stream input)
    {
        try
        {
            using var reader = new StreamReader(input, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 81920, leaveOpen: true);
            char[] buffer = new char[81920 + FixedPageMarker.Length];
            int kept = 0;
            int read;
            while ((read = reader.Read(buffer, kept, buffer.Length - kept)) > 0)
            {
                int length = kept + read;
                if (buffer.AsSpan(0, length).IndexOf(FixedPageMarker, StringComparison.Ordinal) >= 0)
                {
                    return true;
                }

                kept = Math.Min(FixedPageMarker.Length - 1, length);
                buffer.AsSpan(length - kept, kept).CopyTo(buffer);
            }

            return false;
        }
        finally { input.Position = 0; }
    }

    /// <summary>Whether a file begins with the PDF header; false when it cannot be read.</summary>
    private static bool StartsAsPdf(string path) =>
        ContainerSignatures.StartsWithPdfHeader(ContainerSignatures.ReadPrefix(path, ContainerSignatures.PdfHeader.Length));

    private sealed class DenyAllResources : IResourceLoadingCallback
    {
        internal static readonly DenyAllResources Instance = new();

        public ResourceLoadingAction ResourceLoading(ResourceLoadingArgs args) => ResourceLoadingAction.Skip;
    }

    /// <summary>
    /// Supplies local resources through the document's loader. The images a Markdown file names
    /// must be intact images, which the loader checks.
    /// </summary>
    private sealed class BlockingResourceCallback : IResourceLoadingCallback
    {
        private readonly LocalDocumentResourceLoader _resources;
        private readonly bool _checksImages;

        public BlockingResourceCallback(LocalDocumentResourceLoader resources, bool checksImages)
        {
            _resources = resources;
            _checksImages = checksImages;
        }

        public ResourceLoadingAction ResourceLoading(ResourceLoadingArgs args)
        {
            if (_resources.TryRead(args.Uri, out byte[] data, image: _checksImages && args.ResourceType == ResourceType.Image, written: args.OriginalUri))
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
    bool Evaluation) : IOpenedInput, IDisposable
{
    /// <inheritdoc />
    public InputProtection Protection => new(Format.IsEncrypted);

    private readonly List<Document> _imports = [];

    /// <summary>Whether the input is an html-fixed page rendition loaded as flowing HTML.</summary>
    public bool FixedPageHtml { get; init; }

    internal void Retain(Document document) => _imports.Add(document);

    public IReadOnlyList<OmittedResource> OmittedResources
    {
        get
        {
            Resources.ThrowIfFailed();
            return Resources.Omitted;
        }
    }

    /// <summary>The number of resources the load left out, each reference once.</summary>
    public int RemoteResourcesBlocked => OmittedResources.Count;

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
