using System.Text;
using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Words;
using Aspose.Words.Loading;

namespace Aspose.Cli.Product.Words.Engine.Mapping;

internal sealed class WordsDocumentLoader
{
    private readonly ResourceBudgetLedger _resourceBudgets;

    internal WordsDocumentLoader(ResourceBudgetLedger resourceBudgets)
    {
        _resourceBudgets = resourceBudgets ?? throw new ArgumentNullException(nameof(resourceBudgets));
    }

    /// <summary>
    /// Creates an empty document under the resource policy of <paramref name="policySource"/>,
    /// or under a policy that denies every external resource. Engine code creates documents
    /// only here or through <see cref="Open"/>: a document without a resource callback lets
    /// field updates such as INCLUDETEXT read arbitrary files.
    /// </summary>
    internal static Document CreateBlank(Document? policySource) =>
        new() { ResourceLoadingCallback = policySource?.ResourceLoadingCallback ?? DenyAllResources.Instance };

    public LoadedDocument Open(string path, string? password)
    {
        InputSizeGuard.Ensure(_resourceBudgets, path);
        return OpenCore(path, password);
    }

    // Generated candidates are bounded by publication, not a second user-input admission.
    internal LoadedDocument OpenPublishedCandidate(string path, string? password) => OpenCore(path, password);

    private LoadedDocument OpenCore(string path, string? password)
    {
        FileFormatInfo detected;
        try
        {
            detected = FileFormatUtil.DetectFileFormat(path);
        }
        catch (Exception ex) when (ex is FileCorruptedException or UnsupportedFileFormatException)
        {
            throw InvalidDocument(path, ex.Message, ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw TranslateIo(path, ex);
        }

        string id = WordsFormatMapper.ToId(detected.LoadFormat);
        if (id == "unknown" || IsTextFallbackForNonTextPath(id, path))
        {
            throw InvalidDocument(
                path,
                id == "unknown"
                    ? "the SDK could not identify a supported document format"
                    : "the bytes were detected as plain text but the file name claims a document container");
        }

        if (!WordsFormats.IsLoad(id))
        {
            throw CliErrors.FormatUnsupported(id, WordsFormats.LoadIds);
        }

        if (detected.IsEncrypted && string.IsNullOrEmpty(password))
        {
            throw CliErrors.PasswordRequired(path);
        }

        var resources = new LocalDocumentResourceLoader(path, _resourceBudgets);
        try
        {
            Document document = Load(options => new Document(path, options),
                detected.LoadFormat, resources, path, password);
            return new LoadedDocument(document, detected, id, resources,
                HasEvaluationTruncationMarker(document));
        }
        catch
        {
            resources.Dispose();
            throw;
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
            LoadFormat.Markdown, owner.Resources, "inline Markdown", password: null);
        owner.Retain(document, HasEvaluationTruncationMarker(document));
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
        LocalDocumentResourceLoader resources, string path, string? password)
    {
        Document? document = null;
        try
        {
            document = open(new LoadOptions
            {
                Password = password,
                LoadFormat = format,
                BaseUri = resources.BaseUri,
                ResourceLoadingCallback = new BlockingResourceCallback(resources),
                PreserveIncludePictureField = true,
            });
            EnsureWithinBudgets(document, resources);
            return document;
        }
        catch (Exception exception)
        {
            try
            {
                resources.ThrowIfFailed();
                if (exception is IncorrectPasswordException)
                {
                    throw string.IsNullOrEmpty(password)
                        ? CliErrors.PasswordRequired(path) : CliErrors.PasswordInvalid(path);
                }
                if (exception is FileCorruptedException or UnsupportedFileFormatException)
                {
                    throw InvalidDocument(path, exception.Message, exception);
                }
                if (exception is IOException or UnauthorizedAccessException)
                {
                    throw TranslateIo(path, exception);
                }
                throw;
            }
            finally { document?.Cleanup(); }
        }
    }

    // These catches surround SDK input detection/parsing, not output publication.
    private static CliException TranslateIo(string path, Exception exception) => exception switch
    {
        FileNotFoundException or DirectoryNotFoundException => CliErrors.FileNotFound(path),
        UnauthorizedAccessException => CliErrors.FileAccessDenied(path),
        IOException io when FileAccessProbe.IsSharingViolation(io) => CliErrors.FileLocked(path),
        _ => InvalidDocument(path, exception.Message, exception),
    };

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

    private static CliException InvalidDocument(string path, string reason, Exception? inner = null) => new(
        ErrorCodes.FileCorrupt,
        $"Input is not a valid supported word-processing document: {path} ({reason}).",
        hint: "Verify the file opens in Word and that its content matches a format listed by 'aspose-cli capabilities'.",
        innerException: inner);

    private static bool HasEvaluationTruncationMarker(Document document) =>
        document.GetChildNodes(NodeType.Paragraph, true)
            .Cast<Paragraph>()
            .Select(static paragraph => paragraph.GetText())
            .Any(static text =>
                text.Contains("document was truncated", StringComparison.OrdinalIgnoreCase)
                && text.Contains("evaluation", StringComparison.OrdinalIgnoreCase));

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
    bool EvaluationInputTruncated) : IDisposable
{
    private readonly List<Document> _imports = [];
    public bool ImportedInputTruncated { get; private set; }

    internal void Retain(Document document, bool truncated)
    {
        _imports.Add(document);
        ImportedInputTruncated |= truncated;
    }

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
