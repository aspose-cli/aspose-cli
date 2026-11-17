using System.Globalization;
using System.Text;
using Aspose.Cells;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Formats;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.IO.Files;
using Aspose.Cli.Sdk.IO.Input;
using Aspose.Cli.Sdk.Results;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// Owns content-driven workbook detection, password application and guarded
/// open policy. No query, rendering or mutation behavior belongs here. With the write pipeline,
/// a workbook opened skips its active evaluation warning sheet and the pipeline learns whether
/// each opened input is encrypted; without one (font inspection) it opens as stored. A delimited
/// text input loses the evaluation notice row an unlicensed export wrote after its data.
/// </summary>
internal sealed class CellsWorkbookLoader
{
    private readonly ResourceBudgetLedger _resourceBudgets;
    private readonly OutputPipeline? _outputs;

    /// <param name="resourceBudgets">The invocation's resource budgets.</param>
    /// <param name="outputs">The invocation's write pipeline.</param>
    internal CellsWorkbookLoader(ResourceBudgetLedger resourceBudgets, OutputPipeline outputs)
        : this(resourceBudgets)
    {
        _outputs = outputs ?? throw new ArgumentNullException(nameof(outputs));
    }

    private CellsWorkbookLoader(ResourceBudgetLedger resourceBudgets)
    {
        _resourceBudgets = resourceBudgets ?? throw new ArgumentNullException(nameof(resourceBudgets));
    }

    /// <summary>A loader for font inspection, which publishes nothing: workbooks open as stored.</summary>
    internal static CellsWorkbookLoader ForFontInspection(ResourceBudgetLedger resourceBudgets) => new(resourceBudgets);

    // Excel's limit on the length of a sheet name.
    private const int MaxSheetNameLength = 31;

    /// <summary>
    /// How a workbook input that does not load is reported. Opening a file fails only inside the
    /// engine, so an exception the engine raises is the file's: a pathological xls can make the
    /// reader throw a raw <see cref="ArgumentOutOfRangeException"/> rather than a
    /// <see cref="CellsException"/>, which is a damaged input the user can act on, never a defect
    /// of the CLI. An encrypted workbook opened without its password, or with a wrong one, fails
    /// with <see cref="ExceptionType.IncorrectPassword"/> or <see cref="ExceptionType.Permission"/>.
    /// </summary>
    internal static readonly InputLoading Loading = new(
        "spreadsheet",
        Classify);

    private static LoadFailureKind Classify(Exception exception) =>
        exception is CellsException { Code: ExceptionType.IncorrectPassword or ExceptionType.Permission }
            ? LoadFailureKind.Password
            : LoadFailureKind.Corrupt;

    private static readonly HashSet<FileFormatType> OpenableFormats =
    [
        FileFormatType.Xlsx,
        FileFormatType.Xlsm,
        FileFormatType.Xlsb,
        FileFormatType.Xltx,
        FileFormatType.Xltm,
        FileFormatType.Excel97To2003,
        FileFormatType.Excel95,
        FileFormatType.Excel2,
        FileFormatType.Excel3,
        FileFormatType.Excel4,
        FileFormatType.Ods,
        FileFormatType.Ots,
        FileFormatType.SpreadsheetML,
        FileFormatType.Csv,
        FileFormatType.TabDelimited,
    ];

    // Every declared input: delimited text the engine detects imports under any of their
    // extensions, as a tab-separated "Excel" export named .xls does, but never under an
    // extension no input declares, such as .json. Content no format recognizes is imported as
    // delimited text under an extension of CellsFormats.Delimited.
    private static readonly IReadOnlyList<FormatDescriptor> Inputs =
        [.. CellsFormats.Definitions.Where(static format => format.Uses.HasFlag(FormatUse.Input))];

    /// <summary>
    /// Opens a user input. A workbook that asks to be calculated when opened is calculated, as
    /// Excel does, unless <paramref name="calculateOnOpen"/> is false because the caller
    /// recalculates itself or was told not to. An active evaluation warning sheet gives way to the
    /// workbook's content (<see cref="CellsEvaluation.SkipActiveWarningSheet"/>), and a delimited
    /// text input to its data without the evaluation notice rows (<see cref="CellsEvaluation.TakeNoticeRows"/>).
    /// </summary>
    internal LoadedWorkbook Open(
        string path,
        PasswordSource password,
        CellsTextImportOptions? textImport = null,
        bool calculateOnOpen = true)
    {
        return InputOpening.Open(_outputs, _resourceBudgets, path,
            () => OpenCore(path, password, textImport, published: false, calculateOnOpen));
    }

    /// <inheritdoc cref="Open(string, PasswordSource, CellsTextImportOptions?, bool)"/>
    /// <param name="path">The input file.</param>
    /// <param name="password">The password from the command's <c>--password-env</c>, if any.</param>
    /// <param name="textImport">How delimited text is imported.</param>
    /// <param name="calculateOnOpen">Whether a workbook that asks to be calculated when opened is.</param>
    internal LoadedWorkbook Open(
        string path,
        Secret? password,
        CellsTextImportOptions? textImport = null,
        bool calculateOnOpen = true) =>
        Open(path, PasswordSource.Option(password), textImport, calculateOnOpen);

    // Derived output is already bounded by publication; it is not a new user input, and its
    // text was written with invariant formats, so its quoted text fields are not re-judged.
    // It is read as stored, and its text as written, since verification compares what was
    // written. A copy of an input names the input as its resourceSource, which its links resolve
    // against and its delimited text sheet is named after, as the input's is.
    internal LoadedWorkbook OpenPublishedCandidate(string path, Secret? password, string? resourceSource = null) =>
        InputOpening.OpenPublished(() => OpenCore(path, PasswordSource.Option(password), textImport: null, published: true, calculateOnOpen: false, resourceSource));

    private LoadedWorkbook OpenCore(
        string path,
        PasswordSource password,
        CellsTextImportOptions? textImport,
        bool published,
        bool calculateOnOpen,
        string? resourceSource = null)
    {
        LoadPlan plan = ResolveLoadPlan(path, textImport, published);
        if (plan.Encrypted && password.Password is null)
        {
            // Detection already knows the workbook is encrypted: refuse before the engine runs.
            throw InputLoading.PasswordRefused(path, password);
        }

        var resources = new WorkbookResources(resourceSource ?? path, _resourceBudgets);
        Workbook? workbook = null;
        bool transferred = false;
        try
        {
            LoadOptions options = plan.ToLoadOptions();
            options.Password = password.Password?.Reveal();
            using (Stream input = OpenPackage(path, plan, resources))
            {
                workbook = plan.Separator is null
                    ? new Workbook(input, options)
                    : CellsTextImport.Invariant(() => new Workbook(input, options));
            }
            // A stream carries no name: relative links and resources resolve against the file
            // name, and a delimited text sheet is named after the file, as the path loader does;
            // a file name without a stem keeps the engine's default sheet name.
            workbook.FileName = path;
            if (plan.Separator is not null && Path.GetFileNameWithoutExtension(resourceSource ?? path) is { Length: > 0 } stem)
            {
                workbook.Worksheets[0].Name = stem.Replace('[', '(').Replace(']', ')')[..Math.Min(stem.Length, MaxSheetNameLength)];
            }

            // A read-back reads its text as the input was read, but is no input to warn about.
            IReadOnlyList<int> notice = plan.Separator is null ? [] : CellsEvaluation.TakeNoticeRows(workbook.Worksheets[0]);
            if (plan.Separator is not null)
            {
                CellsTextImport.KeepErrorsAsText(workbook.Worksheets[0]);
            }

            Warning? numbersAsText = plan.Separator is null ? null
                : CellsTextImport.KeepSeparatedNumbersAsText(options, workbook.Worksheets[0], published ? null : path);
            resources.MaterializeLinkedPictures(workbook);
            resources.CleanSvgPictures(workbook);
            resources.ThrowIfFailed();
            _resourceBudgets.EnsureWithin(CellsBudgetDomains.Sheets,
                workbook.Worksheets.Count, "items", "post-load");
            _resourceBudgets.EnsureWithin(CellsBudgetDomains.Objects,
                workbook.Worksheets.Cast<Worksheet>().Sum(static sheet => (long)sheet.Shapes.Count),
                "items", "post-load");
            resources.ThrowIfFailed();
            Warning? calculated = calculateOnOpen ? CellsOpenCalculation.Apply(workbook, _resourceBudgets) : null;
            CellsEvaluation.SkippedWarningSheet? skipped = published || _outputs is null
                ? null
                : CellsEvaluation.SkipActiveWarningSheet(workbook);
            transferred = true;
            // Delimited text is the format its name gives it, as an output's name gives an output's
            // and an edit writes it back; detection decides only where the name gives none.
            string? named = plan.Separator is null ? null : CellsFormats.Delimited.NamedBy(resourceSource ?? path).FirstOrDefault()?.Id;
            return new LoadedWorkbook(workbook, resources, plan.Encrypted)
            {
                SourceFormat = CellsEngineFormats.DetectedAs(named) ?? plan.Detected ?? workbook.FileFormat,
                CalculatedOnOpen = calculated,
                NumbersReadAsText = numbersAsText,
                EvaluationNoticeRows = notice,
                EvaluationNotice = notice.Count > 0 && !published ? CellsEvaluation.NoticeTaken(workbook.Worksheets[0].Name, notice) : null,
                EvaluationSheetSkipped = skipped,
                DelimitedText = plan.Separator is { } separator
                    ? new DelimitedTextForm(named, separator, plan.TextMarked)
                    : null,
                ReadAsWritten = plan.Separator is not null && plan.TextCulture is null,
            };
        }
        catch (Exception exception) when (exception is not CliException and not OperationCanceledException
            && IsLoadFailure(exception))
        {
            resources.ThrowIfFailed();
            throw Loading.Failure(exception, path, password);
        }
        finally
        {
            if (!transferred)
            {
                try { workbook?.Dispose(); }
                finally { resources.Dispose(); }
            }
        }
    }

    /// <summary>
    /// Whether an exception the engine raised while opening a file is reported as that file's
    /// load failure. In evaluation mode the refusal to open another file is no fault of this one,
    /// so it passes on to the SDK, which reports the evaluation limit; with a license it is.
    /// </summary>
    internal bool IsLoadFailure(Exception exception) =>
        !(_outputs?.IsEvaluation == true && CellsEvaluationProfile.IsOpenLimit(exception));

    private LoadPlan ResolveLoadPlan(string path, CellsTextImportOptions? textImport, bool published)
    {
        LoadPlan plan = ResolveFormatPlan(path, published);
        if (plan.Separator is null)
        {
            if (textImport is { Encoding: not null } or { Culture: not null })
            {
                throw CliErrors.OptionInvalid(
                    textImport.Encoding is not null ? CellsOptionNames.Encoding : CellsOptionNames.Culture,
                    "it applies only to delimited text input such as CSV or TSV");
            }

            return plan;
        }

        // A read-back is never written again, so its byte order mark only names its encoding.
        (Encoding encoding, Encoding? marked, CultureInfo? culture) = published
            ? (CellsTextImport.ByteOrderMark(path) ?? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), null, null)
            : CellsTextImport.Resolve(path, textImport, _resourceBudgets);
        return plan with { TextEncoding = encoding, TextCulture = culture, TextMarked = marked };
    }

    // The OpenDocument spreadsheet packages, whose SVG pictures the engine reads by path.
    private static bool IsOpenDocument(FileFormatType format) => format is FileFormatType.Ods or FileFormatType.Ots;

    /// <summary>
    /// The input as the engine reads it: an OpenDocument package always rebuilt with its SVG
    /// pictures kept to themselves (<see cref="OdsPackages"/>, CELLS-SVG-EGRESS), any other file
    /// as it is.
    /// </summary>
    private Stream OpenPackage(string path, LoadPlan plan, WorkbookResources resources)
    {
        if (plan.Detected is not { } format || !IsOpenDocument(format))
        {
            return InputFiles.OpenRead(path);
        }

        OdsPackages.Cleaned cleaned;
        try
        {
            cleaned = OdsPackages.KeepSvgInternal(path, _resourceBudgets);
        }
        catch (InvalidDataException exception)
        {
            throw Loading.Unreadable(path, "its OpenDocument package is damaged: " + exception.Message, exception);
        }

        resources.NoteSvgPictures(cleaned.References, cleaned.Pictures);
        return new MemoryStream(cleaned.Package, writable: false);
    }

    private static LoadPlan ResolveFormatPlan(string path, bool published)
    {
        FileFormatInfo detected = Loading.Load(path, password: default, () =>
        {
            using FileStream input = InputFiles.OpenRead(path);
            return FileFormatUtil.DetectFileFormat(input);
        });

        if (detected.IsEncrypted)
        {
            if (IsOpenDocument(detected.FileFormatType) && !published)
            {
                // CELLS-SVG-EGRESS: the engine opens what an SVG picture names while it loads,
                // and the pictures of an encrypted package cannot be kept to themselves first.
                // A read-back is an output the CLI wrote or the edit baseline, a verified copy of
                // an input this loader opened first, and so refused already if it was encrypted.
                throw CliErrors.EngineDefect(
                    $"The OpenDocument spreadsheet {path} is encrypted: while it loads, the engine would open the files and addresses its SVG pictures name, and the CLI cannot remove them from an encrypted package first.");
            }

            return LoadPlan.Auto with { Encrypted = true };
        }

        return ResolveDetectedFormatPlan(path, detected.FileFormatType);
    }

    private static LoadPlan ResolveDetectedFormatPlan(string path, FileFormatType detected)
    {
        if (OpenableFormats.Contains(detected))
        {
            if (detected is FileFormatType.Csv or FileFormatType.TabDelimited)
            {
                if (Inputs.NamedBy(path).Count == 0)
                {
                    throw NotNamedAsInput(path);
                }

                char separator = DetectDelimiter(path)
                    ?? (detected == FileFormatType.TabDelimited
                        ? '\t'
                        : ',');
                return new LoadPlan(separator) { Detected = detected };
            }

            return LoadPlan.Auto with { Detected = detected };
        }

        // Content no format recognizes is delimited text under a name delimited text has, and so
        // is content the engine takes for Markdown, which it guesses at from plain lines of text
        // (CELLS-MARKDOWN-DETECTION), under a name delimited text claims: not .txt, which is
        // plain text's, so the guess stands there and the file goes to the product that reads it.
        if (detected == FileFormatType.Unknown && CellsFormats.Delimited.NamedBy(path).Count > 0
            || detected == FileFormatType.Markdown && CellsFormats.Delimited.Claiming(path).Count > 0)
        {
            return new LoadPlan(DetectDelimiter(path) ?? ',');
        }

        // HTML and MHTML, which cells writes but does not read whatever the file's name: reading
        // them, the engine opens the images they name by path, past every resource hook.
        string content = CellsEngineFormats.IdOf(detected == FileFormatType.XHtml ? FileFormatType.Html : detected);
        if (WrittenOnlyFormats.Any(format => format.Id == content))
        {
            throw Loading.Unloadable(path, content);
        }

        if (detected != FileFormatType.Unknown)
        {
            throw Loading.Unloadable(path, detected.ToString());
        }

        throw Loading.Unreadable(path, ContainerSignatures.IsZip(ContainerSignatures.ReadPrefix(path, ContainerSignatures.ZipLocalFileHeader.Length))
            ? "it is an incomplete or damaged ZIP container, as an interrupted download or copy leaves it; ask for the file again"
            : "content does not match any supported spreadsheet format");
    }

    // The formats convert writes and no input declares.
    private static readonly IReadOnlyList<FormatDescriptor> WrittenOnlyFormats =
        [.. CellsFormats.Definitions
            .Where(static format => format.Uses.HasFlag(FormatUse.Convert) && !format.Uses.HasFlag(FormatUse.Input))];

    private static CliException NotNamedAsInput(string path) =>
        Loading.NotNamedAsInput(path, "delimited text", CellsFormats.Delimited, "as text");

    private static char? DetectDelimiter(string path)
    {
        string? line = ReadFirstNonEmptyLine(path);
        if (line is null)
        {
            return null;
        }

        int comma = 0;
        int semicolon = 0;
        int tab = 0;
        int pipe = 0;
        bool inQuotes = false;
        foreach (char character in line)
        {
            if (character == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }
            if (inQuotes)
            {
                continue;
            }

            switch (character)
            {
                case ',': comma++; break;
                case ';': semicolon++; break;
                case '\t': tab++; break;
                case '|': pipe++; break;
            }
        }

        (char Character, int Count) best = (';', semicolon);
        if (tab > best.Count)
        {
            best = ('\t', tab);
        }
        if (pipe > best.Count)
        {
            best = ('|', pipe);
        }
        return best.Count > comma ? best.Character : null;
    }

    private static string? ReadFirstNonEmptyLine(string path)
    {
        byte[] prefix;
        try
        {
            using FileStream stream = InputFiles.OpenRead(path);
            int length = (int)Math.Min(8192, stream.Length);
            prefix = new byte[length];
            int read = stream.Read(prefix, 0, length);
            if (read < length)
            {
                Array.Resize(ref prefix, read);
            }
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        Encoding encoding = Encoding.UTF8;
        if (prefix.Length >= 2 && prefix[0] == 0xFF && prefix[1] == 0xFE)
        {
            encoding = Encoding.Unicode;
        }
        else if (prefix.Length >= 2 && prefix[0] == 0xFE && prefix[1] == 0xFF)
        {
            encoding = Encoding.BigEndianUnicode;
        }

        // These encodings replace undecodable bytes instead of throwing.
        string text = encoding.GetString(prefix);
        if (text.Length > 0 && text[0] == '\uFEFF')
        {
            text = text[1..];
        }

        return text.Split('\n')
            .Select(static line => line.TrimEnd('\r'))
            .FirstOrDefault(static line => !string.IsNullOrWhiteSpace(line));
    }

    /// <summary>
    /// How one input is opened. A delimited text input always carries its separator and encoding,
    /// and the culture named for it, if any (<see cref="CellsTextImport.LoadOptions"/>), so neither
    /// the engine's guesses nor the machine's regional settings apply.
    /// </summary>
    private readonly record struct LoadPlan(char? Separator, bool Encrypted = false)
    {
        internal static LoadPlan Auto => default;

        /// <summary>
        /// The on-disk format detection saw; null when detection cannot see it, for an encrypted
        /// container or content no format recognizes or that it takes for Markdown, so the loaded
        /// workbook's format stands.
        /// </summary>
        internal FileFormatType? Detected { get; init; }

        internal Encoding? TextEncoding { get; init; }

        internal CultureInfo? TextCulture { get; init; }

        internal Encoding? TextMarked { get; init; }

        internal LoadOptions ToLoadOptions() => Separator is { } separator
            ? CellsTextImport.LoadOptions(separator, TextEncoding, TextCulture)
            : new LoadOptions();
    }
}
