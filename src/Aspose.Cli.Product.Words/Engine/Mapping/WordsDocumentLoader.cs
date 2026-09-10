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

    public LoadedDocument Open(string path, string? password)
    {
        InputSizeGuard.Ensure(
            _resourceBudgets,
            path,
            InputSizeGuard.ResolveMaxBytes(Environment.GetEnvironmentVariable));
        FileFormatInfo detected;
        try
        {
            detected = FileFormatUtil.DetectFileFormat(path);
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

        var blocked = new BlockingResourceCallback(path);
        var options = new LoadOptions
        {
            Password = password,
            LoadFormat = detected.LoadFormat,
            ResourceLoadingCallback = blocked,
            PreserveIncludePictureField = true,
        };

        try
        {
            var document = new Document(path, options);
            try
            {
                _resourceBudgets.EnsureWithin(
                    WordsBudgetDomains.Pages,
                    document.PageCount,
                    "items",
                    "post-load");
                _resourceBudgets.EnsureWithin(
                    WordsBudgetDomains.Nodes,
                    document.GetChildNodes(NodeType.Any, true).Count,
                    "items",
                    "projection");
            }
            catch
            {
                throw;
            }
            return new LoadedDocument(
                document,
                detected,
                id,
                blocked.Blocked,
                HasEvaluationTruncationMarker(document));
        }
        catch (IncorrectPasswordException)
        {
            throw string.IsNullOrEmpty(password) ? CliErrors.PasswordRequired(path) : CliErrors.PasswordInvalid(path);
        }
        catch (Exception ex) when (ex is FileCorruptedException or UnsupportedFileFormatException)
        {
            throw InvalidDocument(path, ex.Message, ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw TranslateIo(path, ex);
        }
    }

    private static CliException TranslateIo(string path, Exception ex) =>
        ex is UnauthorizedAccessException ? CliErrors.FileAccessDenied(path) :
        File.Exists(path) ? CliErrors.FileLocked(path) : CliErrors.FileNotFound(path);

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

    private sealed class BlockingResourceCallback : IResourceLoadingCallback
    {
        private readonly LocalDocumentResourceLoader _resources;

        public BlockingResourceCallback(string documentPath)
        {
            _resources = new LocalDocumentResourceLoader(documentPath);
        }

        public int Blocked { get; private set; }

        public ResourceLoadingAction ResourceLoading(ResourceLoadingArgs args)
        {
            if (_resources.TryRead(args.Uri, out byte[] data))
            {
                args.SetData(data);
                return ResourceLoadingAction.UserProvided;
            }

            Blocked++;
            return ResourceLoadingAction.Skip;
        }
    }
}

internal sealed record LoadedDocument(
    Document Document,
    FileFormatInfo Format,
    string FormatId,
    int RemoteResourcesBlocked,
    bool EvaluationInputTruncated) : IDisposable
{
    public void Dispose() => Document.Cleanup();
}
