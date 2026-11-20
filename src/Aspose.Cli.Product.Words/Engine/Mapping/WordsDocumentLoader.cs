using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Formats;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.IO.Files;
using Aspose.Cli.Sdk.IO.Input;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Tables;
using Aspose.Words;
using Aspose.Words.Loading;

namespace Aspose.Cli.Product.Words.Engine.Mapping;

/// <summary>
/// Opens documents as Aspose.Words loads them, under the invocation's budgets, with the engine's
/// warnings passed to the invocation's log and every external resource the engine asks for
/// through its <see cref="IResourceLoadingCallback"/> supplied only from beneath the input's
/// folder (SECURITY.md). The callback stays with the document, so saving, rendering and updating
/// fields ask it too. read, search, edit and render open the formats edit writes; convert opens
/// every format the engine reads, a text format with the options the caller stated.
/// </summary>
internal sealed class WordsDocumentLoader
{
    /// <summary>
    /// How a document input that does not load is reported: Aspose.Words raises
    /// <see cref="IncorrectPasswordException"/> for a missing or wrong password, a PDF's included;
    /// any other exception it raises while it opens a file is the file's.
    /// </summary>
    internal static readonly InputLoading Loading = new("supported word-processing document", Classify);

    private readonly ResourceBudgetLedger _resourceBudgets;
    private readonly OutputPipeline _outputs;

    internal WordsDocumentLoader(ResourceBudgetLedger resourceBudgets, OutputPipeline outputs)
    {
        _resourceBudgets = resourceBudgets ?? throw new ArgumentNullException(nameof(resourceBudgets));
        _outputs = outputs ?? throw new ArgumentNullException(nameof(outputs));
    }

    /// <summary>Opens a user input in a format edit writes, with the password its options give.</summary>
    internal LoadedDocument Open(string path, Secret? password, EngineWarningLog warnings) =>
        InputOpening.Open(_outputs, _resourceBudgets, WordsFormats.Definitions, path, () => OpenCore(path, PasswordSource.Option(password), warnings, editable: true, text: null));

    /// <summary>Opens a user input such as a template, with the password <paramref name="password"/> gives.</summary>
    internal LoadedDocument Open(string path, PasswordSource password, EngineWarningLog warnings) =>
        InputOpening.Open(_outputs, _resourceBudgets, WordsFormats.Definitions, path, () => OpenCore(path, password, warnings, editable: true, text: null));

    /// <summary>Opens a user input in any format the engine reads; a text format with <paramref name="text"/>.</summary>
    internal LoadedDocument OpenAny(string path, Secret? password, WordsTextInput? text, EngineWarningLog warnings) =>
        InputOpening.OpenForConvert(_outputs, _resourceBudgets, WordsFormats.Definitions, path, () => OpenCore(path, PasswordSource.Option(password), warnings, editable: false, text));

    /// <summary>Opens an output this invocation just saved, to read it back; its size is bounded by publication.</summary>
    internal LoadedDocument Reopen(string path, Secret? password, EngineWarningLog warnings) =>
        InputOpening.OpenPublished(() => OpenCore(path, PasswordSource.Option(password), warnings, editable: true, text: null));

    /// <summary>
    /// The format id <paramref name="path"/>'s content has, detected without loading it; null when
    /// detection fails, since the load that follows reports why.
    /// </summary>
    internal static string? DetectFormatId(string path)
    {
        try
        {
            using FileStream input = InputFiles.OpenRead(path);
            return WordsEngineFormats.IdOf(FileFormatUtil.DetectFileFormat(input).LoadFormat);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return null;
        }
    }

    private LoadedDocument OpenCore(string path, PasswordSource password, EngineWarningLog warnings, bool editable, WordsTextInput? text)
    {
        using FileStream input = Loading.Load(path, password, () => InputFiles.OpenRead(path));
        FileFormatInfo detected = Loading.Load(path, password, () =>
        {
            FileFormatInfo format = FileFormatUtil.DetectFileFormat(input);
            input.Position = 0;
            return format;
        });

        string? id = WordsEngineFormats.IdOf(detected.LoadFormat);
        if (id is null)
        {
            throw Loading.Unreadable(path, "the engine identifies no format of its content it reads");
        }

        // Text the engine detects in a file whose name says another format is not that format.
        bool namedText = WordsFormats.Definitions.NamedBy(path).Any(candidate => candidate.Id == id);
        if (editable && !WordsFormats.IsEditable(id) && (namedText || !WordsFormats.IsText(id)))
        {
            throw CliErrors.FormatUnsupported(Loading.Unreadable(path, $"its content is {id}, which only convert reads"));
        }

        if (WordsFormats.IsText(id) && text is null)
        {
            throw Loading.Unreadable(path, $"the engine reads its content as {id} text, which convert reads only from a .{id} file with the options for it stated");
        }

        if (detected.IsEncrypted && password.Password is null)
        {
            throw InputLoading.PasswordRefused(path, password);
        }

        var resources = new LocalDocumentResourceLoader(path, _resourceBudgets);
        bool loaded = false;
        try
        {
            LoadOptions options = WordsFormats.IsText(id) ? TextOptions(path, id, text!) : new LoadOptions();
            options.LoadFormat = detected.LoadFormat;
            options.Password = password.Password?.Reveal();
            // A stream carries no name: relative links resolve against the file's folder, as a path load does.
            options.BaseUri = resources.BaseUri;
            options.ResourceLoadingCallback = new LocalResources(resources, checksImages: WordsFormats.IsMarkdown(id));
            options.WarningCallback = new WordsWarnings(warnings);
            var document = new Document(input, options);
            // FILENAME fields name the file, as a path load does.
            document.FieldOptions.FileName = path;
            document.WarningCallback = options.WarningCallback;
            resources.ThrowIfFailed();
            _resourceBudgets.EnsureWithin(WordsBudgetDomains.Nodes, document.GetChildNodes(NodeType.Any, true).Count, "items", "post-load");
            loaded = true;
            return new LoadedDocument(document, id, resources, new InputProtection(detected.IsEncrypted));
        }
        catch (Exception exception) when (exception is not CliException and not OperationCanceledException)
        {
            resources.ThrowIfFailed();
            throw Loading.Failure(exception, path, password);
        }
        finally
        {
            if (!loaded)
            {
                resources.Dispose();
            }
        }
    }

    /// <summary>The engine's load options of a txt or md input, with the values the caller stated.</summary>
    private static LoadOptions TextOptions(string path, string formatId, WordsTextInput text)
    {
        if (WordsFormats.IsMarkdown(formatId))
        {
            return new MarkdownLoadOptions { Encoding = text.Encoding };
        }

        if (text.AutoNumberingDetection is not { } numbering || text.DetectNumberingWithWhitespaces is not { } whitespaces
            || text.DetectHyperlinks is not { } hyperlinks || text.LeadingSpaces is not { } leading)
        {
            throw Loading.Unreadable(path, "the engine reads its content as txt text, which convert reads only from a .txt file with the options for it stated");
        }

        return new TxtLoadOptions
        {
            Encoding = text.Encoding,
            AutoNumberingDetection = numbering,
            DetectNumberingWithWhitespaces = whitespaces,
            DetectHyperlinks = hyperlinks,
            LeadingSpacesOptions = Enum.TryParse(leading, ignoreCase: false, out TxtLeadingSpacesOptions parsed) && Enum.IsDefined(parsed)
                && Enum.GetNames<TxtLeadingSpacesOptions>().Contains(leading, StringComparer.Ordinal)
                ? parsed
                : throw CliErrors.OptionInvalid("--leading-spaces", $"'{leading}' is no member of TxtLeadingSpacesOptions; it is one of: {string.Join(", ", Enum.GetNames<TxtLeadingSpacesOptions>())}"),
        };
    }

    private static LoadFailureKind Classify(Exception exception) => exception switch
    {
        IncorrectPasswordException => LoadFailureKind.Password,
        _ => LoadFailureKind.Corrupt,
    };

    /// <summary>Supplies the resources beneath the document's folder through its loader, and no other.</summary>
    private sealed class LocalResources(LocalDocumentResourceLoader resources, bool checksImages) : IResourceLoadingCallback
    {
        public ResourceLoadingAction ResourceLoading(ResourceLoadingArgs args)
        {
            if (resources.TryRead(args.Uri, out byte[] data, image: checksImages && args.ResourceType == ResourceType.Image, written: args.OriginalUri))
            {
                args.SetData(data);
                return ResourceLoadingAction.UserProvided;
            }

            return ResourceLoadingAction.Skip;
        }
    }
}

/// <summary>Passes each warning the engine reports to the invocation's log, as it reported it.</summary>
internal sealed class WordsWarnings(EngineWarningLog log) : IWarningCallback
{
    public void Warning(WarningInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        log.Add(info.WarningType.ToString(), info.Description);
    }
}

/// <summary>A document Aspose.Words opened, with the resource policy it loads its resources under.</summary>
internal sealed record LoadedDocument(Document Document, string FormatId, LocalDocumentResourceLoader Resources, InputProtection Protection) : IOpenedDocument<Document>
{
    /// <inheritdoc />
    public IReadOnlyList<Warning> Warnings => Resources.Warning is { } omitted ? [omitted] : [];

    public void Dispose() => Resources.Dispose();
}
