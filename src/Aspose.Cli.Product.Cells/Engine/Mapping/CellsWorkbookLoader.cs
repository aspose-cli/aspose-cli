using System.Globalization;
using System.Text;
using Aspose.Cells;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// Owns content-driven workbook detection, password application and guarded
/// open policy. No query, rendering or mutation behavior belongs here.
/// </summary>
internal sealed class CellsWorkbookLoader(ResourceBudgetLedger resourceBudgets)
{
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
        "Verify the file opens in a spreadsheet application and is one of the supported input formats.",
        static exception => exception is CellsException { Code: ExceptionType.IncorrectPassword or ExceptionType.Permission }
            ? LoadFailureKind.Password
            : LoadFailureKind.Corrupt);

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
        FileFormatType.Html,
        FileFormatType.XHtml,
        FileFormatType.MHtml,
    ];

    private static readonly HashSet<string> TextExtensions =
        new(StringComparer.Ordinal)
        {
            ".csv",
            ".tsv",
            ".txt",
            ".json",
        };

    /// <summary>
    /// Opens a user input. A workbook that asks to be calculated when opened is calculated, as
    /// Excel does, unless <paramref name="calculateOnOpen"/> is false because the caller
    /// recalculates itself or was told not to. In evaluation mode an active evaluation warning
    /// sheet gives way to the workbook's content (<see cref="CellsEvaluation.SkipActiveWarningSheet"/>).
    /// </summary>
    internal LoadedWorkbook Open(
        string path,
        Secret? password,
        TextImportOptions? textImport = null,
        bool calculateOnOpen = true)
    {
        InputSizeGuard.Ensure(resourceBudgets, path);
        return OpenCore(path, password, textImport, published: false, calculateOnOpen);
    }

    // Derived output is already bounded by publication; it is not a new user input, and its
    // text was written with invariant formats, so its quoted text fields are not re-judged.
    // It is read as stored, since verification compares what was written.
    internal LoadedWorkbook OpenPublishedCandidate(string path, Secret? password, string? resourceSource = null) =>
        OpenCore(path, password, textImport: null, published: true, calculateOnOpen: false, resourceSource);

    private LoadedWorkbook OpenCore(
        string path,
        Secret? password,
        TextImportOptions? textImport,
        bool published,
        bool calculateOnOpen,
        string? resourceSource = null)
    {
        LoadPlan plan = ResolveLoadPlan(path, textImport, published);
        if (plan.Encrypted && password is null)
        {
            // Detection already knows the workbook is encrypted: refuse before the engine runs.
            throw InputLoading.PasswordRefused(path, password);
        }

        var resources = new WorkbookResources(resourceSource ?? path, resourceBudgets, plan.Format == LoadFormat.MHtml);
        Workbook? workbook = null;
        bool transferred = false;
        try
        {
            LoadOptions options = plan.ToLoadOptions(resources);
            options.Password = password?.Reveal();
            using (FileStream input = InputFiles.OpenRead(path))
            {
                workbook = new Workbook(input, options);
            }
            // A stream carries no name: relative links and resources resolve against the file
            // name, and a delimited text sheet is named after the file, as the path loader does;
            // a file name without a stem keeps the engine's default sheet name.
            workbook.FileName = path;
            if (plan.Separator is not null && Path.GetFileNameWithoutExtension(path) is { Length: > 0 } stem)
            {
                workbook.Worksheets[0].Name = stem.Replace('[', '(').Replace(']', ')')[..Math.Min(stem.Length, MaxSheetNameLength)];
            }
            resources.MaterializeLinkedPictures(workbook);
            resources.ThrowIfFailed();
            resourceBudgets.EnsureWithin(CellsBudgetDomains.Sheets,
                workbook.Worksheets.Count, "items", "post-load");
            resourceBudgets.EnsureWithin(CellsBudgetDomains.Objects,
                workbook.Worksheets.Cast<Worksheet>().Sum(static sheet => (long)sheet.Shapes.Count),
                "items", "post-load");
            if (workbook.FileFormat is FileFormatType.Html or FileFormatType.XHtml or FileFormatType.MHtml)
            {
                foreach (Worksheet sheet in workbook.Worksheets)
                {
                    resourceBudgets.Deadline.ThrowIfExpired("post-load");
                    sheet.AutoFitColumns();
                }
            }
            resources.ThrowIfFailed();
            Warning? calculated = calculateOnOpen ? CellsOpenCalculation.Apply(workbook, resourceBudgets) : null;
            CellsEvaluation.SkippedWarningSheet? skipped = published ? null : CellsEvaluation.SkipActiveWarningSheet(workbook);
            transferred = true;
            return new LoadedWorkbook(workbook, resources, plan.Encrypted) { CalculatedOnOpen = calculated, EvaluationSheetSkipped = skipped, IsDelimitedText = plan.Separator is not null };
        }
        catch (Exception exception) when (exception is not CliException and not OperationCanceledException)
        {
            resources.ThrowIfFailed();
            throw exception is CellsException { Code: ExceptionType.License }
                ? CellsErrors.EvaluationOpenLimit(path)
                : Loading.Failure(exception, path, password);
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

    private LoadPlan ResolveLoadPlan(string path, TextImportOptions? textImport, bool published)
    {
        LoadPlan plan = ResolveFormatPlan(path);
        if (plan.Separator is not { } separator)
        {
            if (textImport is { Encoding: not null } or { Culture: not null })
            {
                throw CliErrors.OptionInvalid(
                    textImport.Encoding is not null ? "--encoding" : "--culture",
                    "it applies only to delimited text input such as CSV or TSV",
                    "Drop --encoding and --culture for this input.");
            }

            return plan;
        }

        (Encoding encoding, CultureInfo culture) = published
            ? (new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), CultureInfo.InvariantCulture)
            : CellsTextImport.Resolve(path, separator, textImport, resourceBudgets);
        return plan with { TextEncoding = encoding, TextCulture = culture };
    }

    private static LoadPlan ResolveFormatPlan(string path)
    {
        FileFormatInfo detected = Loading.Load(path, password: null, () =>
        {
            using FileStream input = InputFiles.OpenRead(path);
            return FileFormatUtil.DetectFileFormat(input);
        });

        if (detected.IsEncrypted)
        {
            return LoadPlan.Auto with { Encrypted = true };
        }

        if (OpenableFormats.Contains(detected.FileFormatType))
        {
            if (detected.FileFormatType
                is FileFormatType.Csv or FileFormatType.TabDelimited)
            {
                char separator = DetectDelimiter(path)
                    ?? (detected.FileFormatType == FileFormatType.TabDelimited
                        ? '\t'
                        : ',');
                return new LoadPlan(null, separator);
            }

            return detected.FileFormatType switch
            {
                FileFormatType.Html or FileFormatType.XHtml => new LoadPlan(LoadFormat.Html, null),
                FileFormatType.MHtml => new LoadPlan(LoadFormat.MHtml, null),
                _ => LoadPlan.Auto,
            };
        }

        if (detected.FileFormatType == FileFormatType.Unknown)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (TextExtensions.Contains(extension))
            {
                return extension == ".json"
                    ? LoadPlan.Auto
                    : new LoadPlan(null, DetectDelimiter(path) ?? ',');
            }

            if (ContentLooksLikeHtml(path))
            {
                return new LoadPlan(LoadFormat.Html, null);
            }
        }

        if (detected.FileFormatType != FileFormatType.Unknown)
        {
            throw Loading.Unloadable(path, detected.FileFormatType.ToString());
        }

        throw Loading.Unreadable(path, ContainerSignatures.IsZip(ContainerSignatures.ReadPrefix(path, ContainerSignatures.ZipLocalFileHeader.Length))
            ? "it is an incomplete or damaged ZIP container, as an interrupted download or copy leaves it; ask for the file again"
            : "content does not match any supported spreadsheet format");
    }

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

    private static bool ContentLooksLikeHtml(string path)
    {
        byte[] prefix;
        try
        {
            using FileStream stream = InputFiles.OpenRead(path);
            int length = (int)Math.Min(4096, stream.Length);
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
            return false;
        }

        string text = Encoding.ASCII.GetString(prefix).ToLowerInvariant();
        string[] signatures =
        [
            "<!doctype html",
            "<html",
            "<head",
            "<body",
            "<meta",
            "<table",
            "mso-number-format",
        ];
        return Array.Exists(
            signatures,
            signature => text.Contains(signature, StringComparison.Ordinal));
    }

    /// <summary>
    /// How one input is opened. A delimited text input always carries its separator, encoding
    /// and culture, so neither the engine's guesses nor the machine's regional settings apply.
    /// </summary>
    private readonly record struct LoadPlan(LoadFormat? Format, char? Separator, bool Encrypted = false)
    {
        internal static LoadPlan Auto => default;

        internal Encoding? TextEncoding { get; init; }

        internal CultureInfo? TextCulture { get; init; }

        internal LoadOptions ToLoadOptions(IStreamProvider resources)
        {
            if (Separator is { } separator)
            {
                return new TxtLoadOptions(LoadFormat.Csv)
                {
                    Separator = separator,
                    Encoding = TextEncoding,
                    CultureInfo = TextCulture,
                };
            }
            return Format switch
            {
                LoadFormat.Html or LoadFormat.MHtml => new HtmlLoadOptions(Format.Value)
                {
                    StreamProvider = resources,
                },
                { } format => new LoadOptions(format),
                _ => new LoadOptions(),
            };
        }
    }
}
