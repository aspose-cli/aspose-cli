using Aspose.Cells;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.IO.Files;
using Aspose.Cli.Sdk.IO.Input;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Tables;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// Opens workbooks as Aspose.Cells loads them, under the invocation's budgets, with the engine's
/// warnings passed to the invocation's log and the linked pictures the engine fetches while it
/// renders supplied only from beneath the input's folder (<see cref="CellsResourcePolicy"/>).
/// A workbook is opened in the format its content has; CSV and TSV are opened only by
/// <see cref="OpenText"/>, with every text option the caller stated.
/// </summary>
internal sealed class CellsWorkbookLoader
{
    private readonly ResourceBudgetLedger _resourceBudgets;
    private readonly OutputPipeline _outputs;

    /// <param name="resourceBudgets">The invocation's resource budgets.</param>
    /// <param name="outputs">The write pipeline, which learns whether each opened input is encrypted.</param>
    internal CellsWorkbookLoader(ResourceBudgetLedger resourceBudgets, OutputPipeline outputs)
    {
        _resourceBudgets = resourceBudgets ?? throw new ArgumentNullException(nameof(resourceBudgets));
        _outputs = outputs ?? throw new ArgumentNullException(nameof(outputs));
    }

    /// <summary>
    /// How a workbook input that does not load is reported: an encrypted workbook opened without
    /// its password, or with a wrong one, fails with <see cref="ExceptionType.IncorrectPassword"/>
    /// or <see cref="ExceptionType.Permission"/>; any other exception the engine raises while it
    /// opens a file is the file's.
    /// </summary>
    internal static readonly InputLoading Loading = new("spreadsheet", Classify);

    private static LoadFailureKind Classify(Exception exception) =>
        exception is CellsException { Code: ExceptionType.IncorrectPassword or ExceptionType.Permission }
            ? LoadFailureKind.Password
            : LoadFailureKind.Corrupt;

    /// <summary>Opens a user input workbook with the password its options give, if any.</summary>
    internal LoadedWorkbook Open(string path, Secret? password, EngineWarningLog warnings) =>
        InputOpening.Open(_outputs, _resourceBudgets, CellsFormats.Definitions, path, () => OpenWorkbook(path, PasswordSource.Option(password), warnings));

    /// <summary>Opens the workbook a convert reads, with the password its options give, if any.</summary>
    internal LoadedWorkbook OpenForConvert(string path, Secret? password, EngineWarningLog warnings) =>
        InputOpening.OpenForConvert(_outputs, _resourceBudgets, CellsFormats.Definitions, path, () => OpenWorkbook(path, PasswordSource.Option(password), warnings));

    /// <summary>Opens a user input, such as a template, with the password <paramref name="password"/> gives.</summary>
    internal LoadedWorkbook Open(string path, PasswordSource password, EngineWarningLog warnings) =>
        InputOpening.Open(_outputs, _resourceBudgets, CellsFormats.Definitions, path, () => OpenWorkbook(path, password, warnings));

    /// <summary>Opens an output this invocation just saved, to read it back; its size is bounded by publication.</summary>
    internal LoadedWorkbook Reopen(string path, Secret? password, EngineWarningLog warnings) =>
        InputOpening.OpenPublished(() => OpenWorkbook(path, PasswordSource.Option(password), warnings));

    /// <summary>Opens CSV or TSV text with the engine's text options the caller stated.</summary>
    internal LoadedWorkbook OpenText(string path, string formatId, CellsTextInput text, EngineWarningLog warnings) =>
        InputOpening.OpenForConvert(_outputs, _resourceBudgets, CellsFormats.Definitions, path, () =>
        {
            var options = new TxtLoadOptions(CellsEngineFormats.TextLoad(formatId))
            {
                Separator = text.Separator,
                Encoding = text.Encoding,
                ConvertNumericData = text.ConvertNumbers,
                ConvertDateTimeData = text.ConvertDates,
                WarningCallback = new CellsWarnings(warnings),
            };
            if (text.Culture is { } culture)
            {
                options.CultureInfo = culture;
            }

            return Load(path, PasswordSource.None(), options, formatId, encrypted: false);
        });

    private LoadedWorkbook OpenWorkbook(string path, PasswordSource password, EngineWarningLog warnings)
    {
        FileFormatInfo detected = Loading.Load(path, password: default, () =>
        {
            using FileStream input = InputFiles.OpenRead(path);
            return FileFormatUtil.DetectFileFormat(input);
        });

        // An encrypted package is detected as Ooxml, and its format is known once it is opened;
        // any other format no command here reads, such as CSV text or an OpenDocument
        // spreadsheet, is refused before the engine loads it.
        string? formatId = CellsEngineFormats.ReadAs(detected.FileFormatType);
        if (formatId is null && !(detected.IsEncrypted && detected.FileFormatType == FileFormatType.Ooxml))
        {
            throw detected.FileFormatType == FileFormatType.Unknown
                ? Loading.Unreadable(path, "its content is not in any spreadsheet format the engine reads")
                : Unsupported(path, CellsEngineFormats.Name(detected.FileFormatType));
        }

        if (detected.IsEncrypted && password.Password is null)
        {
            // Detection already knows the workbook is encrypted: refuse before the engine runs.
            throw InputLoading.PasswordRefused(path, password);
        }

        var options = new LoadOptions
        {
            Password = password.Password?.Reveal(),
            WarningCallback = new CellsWarnings(warnings),
        };
        return Load(path, password, options, formatId, detected.IsEncrypted);
    }

    /// <summary>
    /// Whether an exception the engine raised while it opened a file is that file's load failure.
    /// In evaluation mode the engine's refusal to open another file is no fault of this one: it
    /// passes on, and the SDK reports it as the evaluation limit (<see cref="CellsEvaluationProfile"/>).
    /// </summary>
    internal bool IsLoadFailure(Exception exception) =>
        !(_outputs.IsEvaluation && CellsEvaluationProfile.IsOpenLimit(exception));

    /// <summary>
    /// A file whose format the engine recognizes and this command does not read, such as an ods
    /// file or another product's document: <c>FILE_CORRUPT</c>, as every product refuses it.
    /// </summary>
    private static CliException Unsupported(string path, string format) => Loading.Unloadable(path, format);

    private LoadedWorkbook Load(string path, PasswordSource password, LoadOptions options, string? formatId, bool encrypted)
    {
        var resources = CellsResourcePolicy.Beside(path, _resourceBudgets);
        Workbook? workbook = null;
        try
        {
            using (FileStream input = InputFiles.OpenRead(path))
            {
                workbook = new Workbook(input, options);
            }

            // A stream carries no name: relative links resolve against the file's name, as the
            // path loader does. Linked pictures are fetched while the workbook renders, through
            // the resource provider only.
            workbook.FileName = path;
            workbook.Settings.ResourceProvider = resources;
            string format = formatId ?? CellsEngineFormats.ReadAs(workbook.FileFormat)
                ?? throw Unsupported(path, CellsEngineFormats.Name(workbook.FileFormat));
            _resourceBudgets.EnsureWithin(CellsBudgetDomains.Sheets, workbook.Worksheets.Count, "items", "post-load");
            _resourceBudgets.EnsureWithin(CellsBudgetDomains.Objects,
                workbook.Worksheets.Sum(static sheet => (long)sheet.Shapes.Count), "items", "post-load");
            _resourceBudgets.EnsureWithin(CellsBudgetDomains.Cells,
                workbook.Worksheets.Sum(static sheet => sheet.Cells.CountLarge), "items", "post-load");
            var loaded = new LoadedWorkbook(workbook, format, resources, encrypted);
            workbook = null;
            return loaded;
        }
        catch (Exception exception) when (exception is not CliException and not OperationCanceledException && IsLoadFailure(exception))
        {
            throw Loading.Failure(exception, path, password);
        }
        finally
        {
            if (workbook is not null)
            {
                workbook.Dispose();
                resources.Dispose();
            }
        }
    }
}

/// <summary>A workbook Aspose.Cells opened, with the resource policy it renders under.</summary>
internal sealed record LoadedWorkbook(Workbook Workbook, string FormatId, CellsResourcePolicy Resources, bool Encrypted) : IOpenedDocument<Workbook>
{
    /// <inheritdoc />
    public Workbook Document => Workbook;

    /// <inheritdoc />
    public IReadOnlyList<Warning> Warnings => Resources.Warning is { } omitted ? [omitted] : [];

    /// <inheritdoc />
    public InputProtection Protection => new(Encrypted);

    public void Dispose()
    {
        try { Workbook.Dispose(); }
        finally { Resources.Dispose(); }
    }
}

/// <summary>
/// The document resource policy of a workbook: Aspose.Cells asks
/// <see cref="WorkbookSettings.ResourceProvider"/> for each linked picture while it renders or
/// converts the workbook, and without one reads the path itself. A verified file beneath the
/// input's folder is supplied; every other reference gets no stream, which the engine draws as
/// nothing, and is counted. The engine swallows provider failures, so callers check
/// <see cref="ThrowIfFailed"/> before they publish output.
/// </summary>
internal sealed class CellsResourcePolicy : IStreamProvider, IDisposable
{
    private readonly LocalDocumentResourceLoader _local;

    private CellsResourcePolicy(LocalDocumentResourceLoader local) => _local = local;

    /// <summary>Supplies verified resources beneath the document's own directory.</summary>
    internal static CellsResourcePolicy Beside(string documentPath, ResourceBudgetLedger budgets) =>
        new(new LocalDocumentResourceLoader(documentPath, budgets));

    public void InitStream(StreamProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Stream = _local.TryRead(options.DefaultPath, out byte[] data, image: true) ? new MemoryStream(data, writable: false) : null;
    }

    public void CloseStream(StreamProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Stream?.Dispose();
    }

    /// <summary>Rethrows a budget or cancellation failure the engine swallowed.</summary>
    internal void ThrowIfFailed() => _local.ThrowIfFailed();

    /// <summary>The completeness warning for the resources left out so far, or null.</summary>
    internal Warning? Warning => _local.Warning;

    public void Dispose() => _local.Dispose();
}
