using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Sdk.Errors;

/// <summary>
/// Factory for every error the CLI raises. Centralizing construction keeps
/// messages consistent: each states the fact and includes the valid alternatives.
/// </summary>
public static partial class CliErrors
{
    /// <summary>The most names an error lists in <c>details.available</c>.</summary>
    public const int MaximumAvailableNames = 50;

    /// <summary>
    /// The error for a command line that does not parse. <paramref name="mistake"/> is the first
    /// unknown command, option or value: the message ends with the names it could have been,
    /// which <c>details.available</c> lists too. <c>details.errors</c> lists the problems only
    /// when there are several.
    /// </summary>
    /// <param name="problems">Each problem, one sentence each.</param>
    /// <param name="mistake">The first unknown command, option or value, if any.</param>
    /// <param name="parameter">
    /// The option or argument the first problem is about, such as an unknown or missing option,
    /// stated in <c>details.option</c> or <c>details.argument</c>; null when it is about none.
    /// </param>
    public static CliException Usage(
        IReadOnlyList<string> problems,
        Mistake? mistake = null,
        string? parameter = null)
    {
        ArgumentNullException.ThrowIfNull(problems);
        var details = new JsonObject();
        if (parameter is not null)
        {
            details[ParameterKey(parameter)] = parameter;
        }

        if (problems.Count > 1)
        {
            details["errors"] = Strings(problems);
        }

        mistake?.WriteTo(details);
        string message = string.Join(" ", problems.Select(static problem => Sentence(problem)));
        if (mistake is { Available.Count: > 0 })
        {
            message = $"{message} {mistake.MustBeOneOf()}";
        }

        return CliException.Create(ErrorCodes.UsageError, message, details.Count == 0 ? null : details);
    }

    /// <summary>
    /// A command line whose options and arguments all parse but whose values the command refuses,
    /// such as a value outside an option's list or a number that does not parse:
    /// <c>OPTION_INVALID</c> naming the first refused <paramref name="parameter"/> in
    /// <c>details.option</c> or <c>details.argument</c>. <paramref name="mistake"/>, when the value
    /// has a list of values, ends the message with them.
    /// </summary>
    /// <param name="problems">Each refused value, one sentence each.</param>
    /// <param name="parameter">The option or argument of the first refused value.</param>
    /// <param name="mistake">The first refused value among a list of values, if any.</param>
    public static CliException OptionValuesRefused(IReadOnlyList<string> problems, string parameter, Mistake? mistake = null)
    {
        ArgumentNullException.ThrowIfNull(problems);
        ArgumentException.ThrowIfNullOrWhiteSpace(parameter);
        var details = new JsonObject { [ParameterKey(parameter)] = parameter };
        if (problems.Count > 1)
        {
            details["errors"] = Strings(problems);
        }

        mistake?.WriteTo(details);
        string message = string.Join(" ", problems.Select(static problem => Sentence(problem)));
        if (mistake is { Available.Count: > 0 })
        {
            message = $"{message} {mistake.MustBeOneOf()}";
        }

        return CliException.Create(ErrorCodes.OptionInvalid, message, details);
    }

    /// <summary>
    /// A command that only groups other commands, run without one: it does nothing on its own, so
    /// the error lists its commands in <c>details.available</c> by their path below the root.
    /// Options it does not know, given after the group, are problems before the missing
    /// command; the first one is stated in <c>details.option</c>.
    /// </summary>
    /// <param name="command">The group as the caller typed it, such as <c>aspose-cli cells</c>.</param>
    /// <param name="commands">Its visible commands by their path below the root, such as <c>cells inspect</c>.</param>
    /// <param name="unknownOptions">The tokens given to the group that name none of its options, in order.</param>
    public static CliException CommandRequired(
        string command,
        IReadOnlyList<string> commands,
        IReadOnlyList<string>? unknownOptions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentNullException.ThrowIfNull(commands);
        unknownOptions ??= [];
        var mistake = Mistake.Of(string.Empty, commands);
        var details = new JsonObject();
        if (unknownOptions.Count > 0)
        {
            details["option"] = unknownOptions[0];
        }

        mistake.WriteTo(details);
        string unknown = string.Concat(unknownOptions.Select(token => $"'{token}' is not an option of '{command}'. "));
        return CliException.Create(
            ErrorCodes.UsageError,
            $"{unknown}'{command}' needs a command. {mistake.MustBeOneOf()}",
            details);
    }

    /// <summary>A problem as a sentence that ends with a period.</summary>
    private static string Sentence(string problem)
    {
        string trimmed = problem.Trim();
        return trimmed.EndsWith('.') ? trimmed : trimmed + ".";
    }

    /// <summary>
    /// An input file that does not exist. Its details are the not-found details of a
    /// <c>file</c>, or of the file an option names, such as an <c>ops file</c>: the path as the
    /// caller wrote it and as it resolved.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="option">The option that names a file the command reads besides its input, such as <c>--ops</c>; null for an input.</param>
    public static CliException FileNotFound(string path, string? option = null) => CliException.Create(
        ErrorCodes.FileNotFound,
        option is null ? $"Input file not found: {path}" : $"The {option} file was not found: {path}",
        details: FileNotFoundDetails(option is null ? "file" : $"{option.TrimStart('-')} file", path));

    private static JsonObject FileNotFoundDetails(string subject, string path) => new()
    {
        ["subject"] = subject,
        ["requested"] = path,
        ["path"] = Path.GetFullPath(path),
    };

    public static CliException FileLocked(string path) => CliException.Create(
        ErrorCodes.FileLocked,
        $"File is in use by another process: {path}",
        details: new JsonObject { ["path"] = path });

    /// <summary>An encrypted file opened without a password.</summary>
    public static CliException PasswordRequired(string path) => CliException.Create(
        ErrorCodes.PasswordRequired,
        $"File is encrypted and requires a password: {path}",
        details: new JsonObject { ["path"] = path });

    /// <summary>A password that does not open the file, such as a certificate's.</summary>
    public static CliException PasswordInvalid(string path) => CliException.Create(
        ErrorCodes.PasswordInvalid,
        $"The provided password does not open the file: {path}",
        details: new JsonObject { ["path"] = path });

    /// <summary>
    /// A protection inside a document, such as a workbook's structure or a document's editing
    /// restriction, that has a password the operation did not supply or supplied wrongly. It is
    /// the same mistake as a file's open password, so it has the same codes.
    /// </summary>
    /// <param name="passwordGiven">Whether the operation supplied a password.</param>
    /// <param name="target">What is protected, such as <c>the workbook structure</c>.</param>
    /// <param name="operationField">The operation field that supplies the password.</param>
    /// <param name="innerException">The engine's refusal, if any.</param>
    public static CliException ProtectionPassword(
        bool passwordGiven,
        string target,
        string operationField,
        Exception? innerException = null)
    {
        ErrorCode code = passwordGiven ? ErrorCodes.PasswordInvalid : ErrorCodes.PasswordRequired;
        return CliException.Create(
            code,
            passwordGiven
                ? $"The provided password does not unprotect {target}."
                : $"The protection of {target} has a password.",
            details: new JsonObject { ["target"] = target, ["field"] = operationField },
            innerException: innerException);
    }

    /// <summary>
    /// An encrypted file the command opens with no password at all, such as a template:
    /// <c>PASSWORD_REQUIRED</c>.
    /// </summary>
    /// <param name="path">The input file.</param>
    public static CliException PasswordUnavailable(string path) => CliException.Create(
        ErrorCodes.PasswordRequired,
        $"File is encrypted, and the command takes no password for it: {path}",
        details: new JsonObject { ["path"] = path });

    /// <summary>
    /// Restates a password error about one of the documents a command reads, naming the
    /// argument of that input in <c>details.argument</c>.
    /// </summary>
    internal static CliException ForInput(CliException error, string input)
    {
        string path = error.Details!["path"]!.GetValue<string>();
        return CliException.Create(
            error.Code,
            $"{error.Message} (the {input} input)",
            details: new JsonObject { ["path"] = path, ["argument"] = input });
    }

    /// <summary>Whether <paramref name="error"/> is <c>PASSWORD_REQUIRED</c> or <c>PASSWORD_INVALID</c>.</summary>
    public static bool IsPasswordError(CliException error) =>
        error.Code == ErrorCodes.PasswordRequired || error.Code == ErrorCodes.PasswordInvalid;

    public static CliException FileAccessDenied(string path) => CliException.Create(
        ErrorCodes.FileAccessDenied,
        $"Access to the input file was denied: {path}",
        details: new JsonObject { ["path"] = path });

    /// <summary>
    /// An input that cannot be read as the document the command expects: damaged, truncated or
    /// another kind of file. <c>details.path</c> always names the file, so the Host can tell which
    /// input of a command failed and name the product whose format a renamed file has.
    /// </summary>
    /// <param name="path">The input file.</param>
    /// <param name="document">What the file should be, such as <c>PDF document</c> or <c>spreadsheet</c>.</param>
    /// <param name="reason">Why it is not one, completing a sentence; the message states it.</param>
    /// <param name="innerException">The engine or IO failure, if any.</param>
    public static CliException InputUnreadable(
        string path,
        string document,
        string reason,
        Exception? innerException = null) => CliException.Create(
        ErrorCodes.FileCorrupt,
        $"Input is not a valid {document}: {path} ({reason}).",
        details: new JsonObject { ["path"] = Path.GetFullPath(path) },
        innerException: innerException,
        explanation: reason);

    /// <summary>
    /// The input exceeds the <c>--max-input-bytes</c> budget: its size before it is read, or the
    /// bytes read of it, and of the files it names, while it is read.
    /// </summary>
    /// <param name="actual">The size, or the bytes read so far.</param>
    /// <param name="limit">The budget.</param>
    /// <param name="phase">When it was found, such as <c>pre-runtime-metadata</c> or <c>file-read</c>.</param>
    public static CliException FileTooLarge(long actual, long limit, string phase = PreRuntimePhase) => CliException.Create(
        ErrorCodes.FileTooLarge,
        phase == PreRuntimePhase
            ? $"Input file is {actual} bytes, exceeding the {limit}-byte budget."
            : $"Reading the input reached {actual} bytes, exceeding the {limit}-byte budget.",
        details: BudgetDetails(ResourceBudgetKinds.InputBytes, actual, limit, "bytes", phase));

    private const string PreRuntimePhase = "pre-runtime-metadata";

    /// <summary>
    /// A bounded resource of reading or processing the input exceeded its budget. The input's
    /// own bytes are <see cref="FileTooLarge"/> and the output's resources
    /// <see cref="OutputBudgetExceeded"/>; <see cref="ResourceBudgetExceeded"/> chooses among them.
    /// </summary>
    public static CliException InputBudgetExceeded(
        string resource,
        long observed,
        long limit,
        string unit,
        string phase) => CliException.Create(
        ErrorCodes.InputBudgetExceeded,
        $"Resource '{resource}' reached {observed} {unit}, exceeding the {limit}-{unit} budget.",
        details: BudgetDetails(resource, observed, limit, unit, phase));

    /// <summary>
    /// What the command writes exceeded a budget of its output: its bytes, the files or
    /// directories of one output set, or the metadata that seals its publication.
    /// </summary>
    public static CliException OutputBudgetExceeded(
        string resource,
        long observed,
        long limit,
        string unit,
        string phase) => CliException.Create(
        ErrorCodes.OutputBudgetExceeded,
        $"The output's resource '{resource}' reached {observed} {unit}, exceeding the {limit}-{unit} budget.",
        details: BudgetDetails(resource, observed, limit, unit, phase));

    /// <summary>
    /// The error of a resource budget the ledger refuses, chosen by its resource: the input's
    /// bytes are <c>FILE_TOO_LARGE</c>, a resource of the output <c>OUTPUT_BUDGET_EXCEEDED</c>
    /// and every other resource <c>INPUT_BUDGET_EXCEEDED</c>.
    /// </summary>
    public static CliException ResourceBudgetExceeded(
        string resource,
        long observed,
        long limit,
        string unit,
        string phase) => resource switch
        {
            ResourceBudgetKinds.InputBytes => FileTooLarge(observed, limit, phase),
            ResourceBudgetKinds.OutputBytes
                or ResourceBudgetKinds.OutputSetEntries
                or ResourceBudgetKinds.OutputSetDirectories
                or ResourceBudgetKinds.PublicationMetadataBytes => OutputBudgetExceeded(resource, observed, limit, unit, phase),
            _ => InputBudgetExceeded(resource, observed, limit, unit, phase),
        };

    private static JsonObject BudgetDetails(string resource, long observed, long limit, string unit, string phase) => new()
    {
        ["resource"] = resource,
        ["actual"] = observed,
        ["limit"] = limit,
        ["unit"] = unit,
        ["phase"] = phase,
    };

    public static CliException InputChanged(
        string path,
        long expectedSizeBytes,
        long actualSizeBytes) => CliException.Create(
        ErrorCodes.InputChanged,
        $"Input changed after admission: {path}",
        details: new JsonObject
        {
            ["path"] = path,
            ["expectedSizeBytes"] = expectedSizeBytes,
            ["actualSizeBytes"] = actualSizeBytes,
            ["resource"] = ResourceBudgetKinds.InputBytes,
        });

    public static CliException InputChanged(
        string path,
        string expectedSha256,
        string actualSha256) => CliException.Create(
        ErrorCodes.InputChanged,
        $"Input no longer matches the requested fingerprint: {path}",
        details: new JsonObject
        {
            ["path"] = path,
            ["expectedSha256"] = expectedSha256,
            ["actualSha256"] = actualSha256,
        });

    public static CliException InputEncodingInvalid(
        string phase,
        Exception? inner = null) => CliException.Create(
        ErrorCodes.InputEncodingInvalid,
        "Text input is not valid UTF-8.",
        details: new JsonObject
        {
            ["resource"] = ResourceBudgetKinds.DecodedTextCharacters,
            ["phase"] = phase,
        },
        innerException: inner);

    /// <summary>
    /// A text file that is not UTF-8 and names no encoding: read as UTF-8, its invalid bytes
    /// would be replaced, so it is refused rather than imported with altered text.
    /// </summary>
    /// <param name="path">The text file.</param>
    /// <param name="offset">The offset of the first invalid byte.</param>
    public static CliException InputEncodingInvalid(string path, long offset) => CliException.Create(
        ErrorCodes.InputEncodingInvalid,
        $"'{Path.GetFileName(path)}' is not UTF-8 text (invalid byte at offset {offset}); read as UTF-8, its text would be replaced.",
        details: new JsonObject { ["path"] = path, ["offset"] = offset });

    public static CliException RenderTooLarge(long width, long height, int? dpi, long maxPixels, string? target = null)
    {
        string megabytes = ((double)width * height * 4d / 1_048_576d)
            .ToString("0", CultureInfo.InvariantCulture);
        string resolution = dpi is { } value
            ? string.Create(CultureInfo.InvariantCulture, $" at {value} DPI")
            : string.Empty;
        return CliException.Create(
            ErrorCodes.RenderTooLarge,
            $"Rendering{resolution} needs a {width}x{height} pixel image ({megabytes} MB), which exceeds the limit of {maxPixels} pixels per image.",
            details: new JsonObject
            {
                ["width"] = width,
                ["height"] = height,
                ["dpi"] = dpi,
                ["actual"] = width * height,
                ["limit"] = maxPixels,
                ["unit"] = "pixels",
                ["target"] = target,
            });
    }

    /// <summary>
    /// The engine failed while rasterizing content it could otherwise read.
    /// </summary>
    /// <param name="target">What could not be rendered, starting the message, such as <c>Sheet 'Q1'</c>.</param>
    /// <param name="engineMessage">The engine's own message.</param>
    /// <param name="facts">The product's facts that identify the target, if any.</param>
    public static CliException RenderFailed(string target, string engineMessage, JsonObject? facts = null)
    {
        JsonObject details = facts ?? [];
        details["engineMessage"] = engineMessage;
        return CliException.Create(
            ErrorCodes.RenderFailed,
            $"{target} could not be rendered: the engine failed while rasterizing its content ({engineMessage}).",
            details: details);
    }

    public static CliException OperationTimeout(int seconds) => CliException.Create(
        ErrorCodes.OperationTimeout,
        $"The operation did not finish within the {seconds}s timeout.",
        details: new JsonObject { ["timeoutSeconds"] = seconds });

    public static CliException OperationTimeout(int seconds, string phase) => CliException.Create(
        ErrorCodes.OperationTimeout,
        $"The operation did not finish within the {seconds}s timeout.",
        details: new JsonObject
        {
            ["timeoutSeconds"] = seconds,
            ["phase"] = phase,
        });

    /// <summary>
    /// The content recognizers that choose a file's product ran past their shared routing
    /// budget, a fixed bound that <c>--timeout</c> does not raise.
    /// </summary>
    /// <param name="budget">The routing budget that ran out.</param>
    public static CliException FileRoutingTimeout(TimeSpan budget)
    {
        int seconds = Math.Max(1, (int)Math.Ceiling(budget.TotalSeconds));
        return CliException.Create(
            ErrorCodes.OperationTimeout,
            $"Recognizing the file's content to choose its product did not finish within the {seconds}s routing budget.",
            details: new JsonObject
            {
                ["timeoutSeconds"] = seconds,
                ["phase"] = "file-routing",
            });
    }

    /// <summary>
    /// A regular expression the caller supplied ran past its execution budget, which bounds
    /// the time any one match may take whatever <c>--timeout</c> allows.
    /// </summary>
    /// <param name="exception">The timeout, which states the budget.</param>
    public static CliException RegexTimeout(RegexMatchTimeoutException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        string seconds = exception.MatchTimeout.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);
        return CliException.Create(
            ErrorCodes.OperationTimeout,
            $"The regular expression exceeded its {seconds}-second execution budget.",
            details: new JsonObject { ["phase"] = "regex" },
            innerException: exception);
    }

    public static CliException WorkerTerminationFailed(int processId) => CliException.Create(
        ErrorCodes.WorkerTerminationFailed,
        "The operation deadline expired, but the worker process tree could not be confirmed stopped.",
        details: new JsonObject
        {
            ["pid"] = processId,
            ["quiescenceConfirmed"] = false,
        });

    /// <summary>An output file exists and the caller did not ask to replace it.</summary>
    /// <param name="path">The file.</param>
    /// <param name="partsDependOnDocument">
    /// Whether the file is one the command may write as a numbered part, which a dry run cannot
    /// tell without reading how many parts the document has.
    /// </param>
    public static CliException OutputExists(string path, bool partsDependOnDocument = false) => CliException.Create(
        ErrorCodes.OutputExists,
        partsDependOnDocument
            ? $"Output file already exists: {path}; the command may write it, since how many parts it writes, and so their names, depend on the document."
            : $"Output file already exists: {path}",
        details: new JsonObject { ["path"] = path });

    /// <summary>
    /// An output that the CLI manages, such as an installed Skill, holds content it did not
    /// write, so updating it could destroy that content.
    /// </summary>
    /// <param name="subject">What the output is, starting the message, such as <c>The Skill target</c>.</param>
    /// <param name="path">The output.</param>
    /// <param name="reason">What in it the CLI does not manage.</param>
    /// <param name="innerException">The failure that showed it, if any.</param>
    public static CliException OutputNotManaged(
        string subject,
        string path,
        string reason,
        Exception? innerException = null) => CliException.Create(
        ErrorCodes.OutputExists,
        $"{subject} cannot be updated safely: {path} ({reason}).",
        details: new JsonObject { ["path"] = path },
        innerException: innerException,
        explanation: reason);

    /// <summary>
    /// A product's own file operation failed for a reason of the file system, such as a lock,
    /// a permission or a full disk, rather than of the document.
    /// </summary>
    /// <param name="product">The product whose operation failed.</param>
    /// <param name="exception">The file system failure.</param>
    public static CliException FileOperationFailed(string product, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return CliException.Create(
            ErrorCodes.OutputUnwritable,
            $"{product} could not complete a file operation: {exception.Message}",
            innerException: exception);
    }

    /// <summary>
    /// An explicit output resolves to an input document. Replacing the input is the in-place
    /// mode's job, which alone carries its backup and fingerprint precondition.
    /// </summary>
    internal static CliException OutputIsInput(string parameter, string path) => CliException.Create(
        ErrorCodes.OptionInvalid,
        $"Invalid use of {parameter}: the output resolves to an input file: {path}",
        details: new JsonObject { [ParameterKey(parameter)] = parameter, ["path"] = path });

    /// <summary>
    /// A file the publication would write or remove is one of the invocation's inputs under
    /// another name, such as an extracted file named like the input or a directory to replace
    /// that holds it. It is the fact the overload with a parameter states, found where no
    /// option names the file, so it has the same code.
    /// </summary>
    /// <param name="path">The target that is an input.</param>
    internal static CliException OutputIsInput(string path) => CliException.Create(
        ErrorCodes.OptionInvalid,
        $"The output would replace an input file: {path}",
        details: new JsonObject { ["path"] = path, ["reason"] = "output-is-input" });

    /// <summary>
    /// An output named <c>-</c>, which names standard input only: every output is a file or a
    /// directory, never standard output.
    /// </summary>
    /// <param name="parameter">The option or argument that names the output, such as <c>--out</c>.</param>
    public static CliException OutputIsStandardStream(string parameter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameter);
        return CliException.Create(
            ErrorCodes.OptionInvalid,
            $"Invalid use of {parameter}: '-' names standard input, not an output",
            details: new JsonObject { [ParameterKey(parameter)] = parameter });
    }

    /// <summary>Two outputs of one operation, including a backup, resolve to the same path.</summary>
    internal static CliException DuplicateOutput(string path) => CliException.Create(
        ErrorCodes.UsageError,
        $"Two outputs of this operation resolve to the same path: {path}",
        details: new JsonObject { ["path"] = path });

    /// <summary>
    /// The engine wrote a directory beside an output published with its companion files. Only
    /// files are published with an output, so the set is refused and nothing is published.
    /// </summary>
    internal static CliException CompanionDirectoryUnpublished(string path, string directory) => CliException.Create(
        ErrorCodes.OutputPublicationFailed,
        $"The output set could not be published: the engine wrote the directory '{directory}' beside {path}, and only companion files are published with an output.",
        details: new JsonObject { ["path"] = path, ["directory"] = directory });

    public static CliException OutputUnwritable(
        string path,
        string reason,
        Exception? inner = null,
        string phase = "prepare") => CliException.Create(
        ErrorCodes.OutputUnwritable,
        $"Output file could not be written: {path} ({reason})",
        details: new JsonObject
        {
            ["path"] = path,
            ["phase"] = phase,
        },
        innerException: inner,
        explanation: reason);

    /// <summary>A format id that names none of <paramref name="supported"/>; the details list them.</summary>
    public static CliException FormatUnsupported(string requested, IReadOnlyList<string> supported)
    {
        var mistake = Mistake.Of(requested, supported);
        var details = new JsonObject { ["requested"] = requested };
        mistake.WriteTo(details);
        return CliException.Create(
            ErrorCodes.FormatUnsupported,
            $"Unsupported format '{requested}'. {mistake.MustBeOneOf()}",
            details: details);
    }

    /// <summary>
    /// An output the caller named, or the one derived from the input, has an extension that is
    /// not one of the format it would be written in, or that names no format the command writes.
    /// It is refused before any work, so nothing is written.
    /// </summary>
    /// <param name="parameter">The option or argument that names the output, such as <c>--out</c>.</param>
    /// <param name="output">The output as the caller gave it, or the input it is derived from.</param>
    /// <param name="subject">The start of the message naming the file, such as <c>--out 'x.ppt'</c>.</param>
    /// <param name="problem">What is wrong with its extension, completing a sentence about the subject.</param>
    /// <param name="extensions">The extensions the output may have, which <c>details.available</c> lists.</param>
    /// <param name="facts">The format that decided <paramref name="extensions"/>, or the one the extension names.</param>
    internal static CliException OutputExtensionInvalid(
        string parameter,
        string output,
        string subject,
        string problem,
        IReadOnlyList<string> extensions,
        JsonObject facts)
    {
        string extension = Path.GetExtension(output);
        var mistake = Mistake.Of(extension, extensions);
        facts[ParameterKey(parameter)] = parameter;
        facts["extension"] = extension;
        mistake.WriteTo(facts);
        return CliException.Create(
            ErrorCodes.OptionInvalid,
            $"{subject} {problem}. {mistake.MustBeOneOf()}.",
            details: facts);
    }

    /// <summary>Creates an extension/content ownership conflict error.</summary>
    public static CliException FormatMismatch(
        string path,
        string declaredProduct,
        IReadOnlyList<string> detectedProducts)
    {
        JsonArray detected = Strings(detectedProducts);

        return CliException.Create(
            ErrorCodes.FormatMismatch,
            Mismatch(path, declaredProduct, detectedProducts),
            details: new JsonObject
            {
                ["path"] = path,
                ["declared"] = declaredProduct,
                ["detected"] = detected,
            });
    }

    /// <summary>
    /// Restates a product's <c>FILE_CORRUPT</c> about a file in no format a product reads, as
    /// generic routing reports it, as <c>FORMAT_UNSUPPORTED</c>, so every command refuses the
    /// file with one code. The product's message and details stay: they say, in its terms, what it
    /// could not read.
    /// </summary>
    /// <param name="corrupt">The product's <c>FILE_CORRUPT</c>.</param>
    public static CliException FormatUnsupported(CliException corrupt)
    {
        ArgumentNullException.ThrowIfNull(corrupt);
        if (corrupt.Code != ErrorCodes.FileCorrupt)
        {
            throw new ArgumentException($"{corrupt.Code.Name} is not a corrupt input.", nameof(corrupt));
        }

        return CliException.Create(
            ErrorCodes.FormatUnsupported,
            corrupt.Message,
            details: corrupt.Details?.DeepClone().AsObject(),
            docs: corrupt.Docs,
            innerException: corrupt.InnerException,
            explanation: corrupt.Explanation);
    }

    /// <summary>
    /// Creates the error for a file whose extension names a product but whose content no
    /// product recognizes: a damaged file rather than a renamed one, <c>FILE_CORRUPT</c> as the
    /// product itself reports it. <c>FORMAT_MISMATCH</c> is for content that looks like another
    /// product's.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <param name="declaredProduct">The product its extension names.</param>
    public static CliException ContentUnrecognized(string path, string declaredProduct)
    {
        string reason = $"its content is not a {declaredProduct} document of its {Path.GetExtension(path)} extension";
        return CliException.Create(
            ErrorCodes.FileCorrupt,
            $"Input is not a readable {declaredProduct} document: {path} ({reason}).",
            details: new JsonObject { ["path"] = path },
            explanation: reason);
    }

    /// <summary>
    /// Creates the error for a file whose content no product recognizes under an extension that
    /// is no product's own, such as <c>.xml</c> or <c>.html</c>, which some products read:
    /// <c>FILE_CORRUPT</c>, as a damaged file of theirs, since the file can be another kind of file
    /// of that extension.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <param name="readers">The products that read its extension, at least one.</param>
    public static CliException ContentUnrecognized(string path, IReadOnlyList<string> readers)
    {
        ArgumentNullException.ThrowIfNull(readers);
        string extension = Path.GetExtension(path);
        string reason = $"no product that reads {extension} files ({string.Join(", ", readers)}) recognizes its content";
        return CliException.Create(
            ErrorCodes.FileCorrupt,
            $"Input is not a readable document: {path} ({reason}).",
            details: new JsonObject { ["path"] = path },
            explanation: reason);
    }

    /// <summary>
    /// Creates the error for a file in a format this CLI only writes, such as the <c>.xps</c> a
    /// convert wrote: <c>FORMAT_UNSUPPORTED</c>, as for content no product recognizes.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <param name="candidates">The products that take part in the operation.</param>
    public static CliException FormatOnlyWritten(
        string path,
        IReadOnlyList<string> candidates)
    {
        string extension = Path.GetExtension(path);
        return CliException.Create(
            ErrorCodes.FormatUnsupported,
            $"No product reads '{Path.GetFileName(path)}': {DistributionInfo.CommandName} writes {extension} files but reads none.",
            details: new JsonObject
            {
                ["path"] = path,
                ["declared"] = null,
                ["detected"] = new JsonArray(),
                ["available"] = new JsonArray(candidates.Select(static value => JsonValue.Create(value)).ToArray()),
            });
    }

    /// <summary>
    /// Creates a fail-closed error when the file's content does not show which product reads
    /// it. Content that looks like a product's format is <c>FORMAT_MISMATCH</c>: the extension
    /// names another product's format, or no format the content can be read as. Content no
    /// product recognizes is <c>FORMAT_UNSUPPORTED</c>; under an extension a product reads, such
    /// content is that product's damaged file
    /// (<see cref="ContentUnrecognized(string, IReadOnlyList{string})"/>).
    /// </summary>
    public static CliException FormatUnrecognized(
        string path,
        string? declaredProduct,
        IReadOnlyList<string> detectedProducts,
        IReadOnlyList<string> candidates)
    {
        var detected = new JsonArray(
            detectedProducts.Select(static value => JsonValue.Create(value)).ToArray());
        var available = new JsonArray(
            candidates.Select(static value => JsonValue.Create(value)).ToArray());
        string file = Path.GetFileName(path);
        // The message states what details.detected does, as the product's own mismatch does.
        string message = declaredProduct is null || detectedProducts.Count == 0
            ? $"The content of '{file}' does not show which product reads it."
            : Mismatch(path, declaredProduct, detectedProducts);
        return CliException.Create(
            detectedProducts.Count == 0 ? ErrorCodes.FormatUnsupported : ErrorCodes.FormatMismatch,
            message,
            details: new JsonObject
            {
                ["path"] = path,
                ["declared"] = declaredProduct,
                ["detected"] = detected,
                ["available"] = available,
            });
    }

    // The one sentence of a mismatch: what the content is, then the product that expected
    // another document, whether the command's or the extension's.
    private static string Mismatch(string path, string declaredProduct, IReadOnlyList<string> detectedProducts) =>
        $"By its content, '{Path.GetFileName(path)}' is {LooksLike(detectedProducts)}; it is not a {declaredProduct} document.";

    private static string LooksLike(IReadOnlyList<string> products) => products.Count == 1
        ? $"a {products[0]} document"
        : $"a document of one of: {string.Join(", ", products)}";

    /// <summary>Creates an ambiguous strong-content recognition error.</summary>
    public static CliException FormatAmbiguous(
        string path,
        IReadOnlyList<string> candidates)
    {
        JsonArray available = Strings(candidates);

        return CliException.Create(
            ErrorCodes.FormatAmbiguous,
            $"The content of '{Path.GetFileName(path)}' looks like a document of more than one product: "
                + $"{string.Join(", ", candidates)}.",
            details: new JsonObject
            {
                ["path"] = path,
                ["available"] = available,
            });
    }

    /// <summary>
    /// A value of an operation field that does not parse, such as an A1 reference, raised by a
    /// parser that operations and options share: <c>OPS_INVALID</c> whose <c>details.reason</c>
    /// is <c>invalid-value</c> and whose <see cref="CliException.Explanation"/> is the reason an
    /// operation's field check quotes. A command that parses an option with the same parser
    /// reports <see cref="OptionInvalid(string, string)"/> instead.
    /// </summary>
    /// <param name="message">The sentence that names the value and why it does not parse.</param>
    /// <param name="reason">Why it does not parse, without the value's field.</param>
    public static CliException OperationValueInvalid(string message, string reason) => CliException.Create(
        ErrorCodes.OpsInvalid,
        message,
        details: new JsonObject { ["reason"] = OpsInvalidReasons.InvalidValue },
        explanation: reason);

    /// <summary>
    /// An option or positional argument used wrongly. <paramref name="option"/> names an option
    /// such as <c>--pages</c>, stated in <c>details.option</c>, or an argument such as
    /// <c>file</c>, stated in <c>details.argument</c>.
    /// </summary>
    public static CliException OptionInvalid(string option, string reason) => CliException.Create(
        ErrorCodes.OptionInvalid,
        $"Invalid use of {option}: {reason}",
        details: new JsonObject { [ParameterKey(option)] = option });

    /// <summary>The details key that names a parameter: <c>option</c> for an option, <c>argument</c> for a positional argument.</summary>
    private static string ParameterKey(string parameter) => parameter.StartsWith('-') ? "option" : "argument";

    /// <summary>
    /// An <c>*-env</c> option names an environment variable that is not set or is empty. An
    /// operation's <c>*Env</c> field reports the same mistake with the same words.
    /// </summary>
    /// <param name="option">The option, such as <c>--password-env</c>.</param>
    /// <param name="variable">The variable it names.</param>
    public static CliException SecretMissing(string option, string variable) => CliException.Create(
        ErrorCodes.OptionInvalid,
        $"Invalid use of {option}: {SecretMissingReason(variable)}",
        details: new JsonObject { ["option"] = option, ["variable"] = variable });

    /// <summary>The reason of a <see cref="SecretMissing"/> mistake.</summary>
    internal static string SecretMissingReason(string variable) => $"environment variable '{variable}' is not set or is empty";

    /// <summary>
    /// An option or argument value that names none of the values it accepts; the details list
    /// them, and the message ends with the same values.
    /// </summary>
    public static CliException OptionInvalid(string option, string reason, Mistake mistake)
    {
        ArgumentNullException.ThrowIfNull(mistake);
        var details = new JsonObject { [ParameterKey(option)] = option };
        mistake.WriteTo(details);
        string message = Sentence($"Invalid use of {option}: {reason}");
        return CliException.Create(
            ErrorCodes.OptionInvalid,
            mistake.Available.Count == 0 ? message : $"{message} {mistake.MustBeOneOf()}",
            details);
    }

    /// <summary>
    /// Input content names a resource the CLI does not let the document engine read: a network
    /// address, script, a compressed resource it cannot check, or a file outside the input's
    /// directory. Nothing was written. <c>details.reason</c> is <c>resource-refused</c>.
    /// </summary>
    /// <param name="message">What the input names and why it is refused.</param>
    public static CliException ResourceRefused(string message) => CliException.Create(
        ErrorCodes.FeatureUnsupported,
        message,
        details: new JsonObject { ["reason"] = "resource-refused" });

    /// <summary>
    /// Input content that the output cannot hold, such as a table larger than a slide table
    /// may be. The caller has to restructure the content. <c>details.reason</c> is
    /// <c>content-too-large</c>.
    /// </summary>
    /// <param name="message">What the content is and the limit it exceeds.</param>
    public static CliException ContentUnsupported(string message) => CliException.Create(
        ErrorCodes.FeatureUnsupported,
        message,
        details: new JsonObject { ["reason"] = "content-too-large" });

    /// <summary>
    /// Content the document engine would write wrong, a confirmed defect of the engine that
    /// <c>KNOWN-ISSUES.md</c> records, so the command refuses it before writing anything.
    /// <c>details.reason</c> is <c>engine-defect</c>.
    /// </summary>
    /// <param name="message">What the engine would write wrong, and for which content.</param>
    public static CliException EngineDefect(string message) => CliException.Create(
        ErrorCodes.FeatureUnsupported,
        message,
        details: new JsonObject { ["reason"] = "engine-defect" });

    /// <summary>
    /// The local environment a document engine reads, such as the font registry, holds
    /// something the engine cannot read, so every document would fail the same way.
    /// <c>details.reason</c> is <c>environment</c>.
    /// </summary>
    /// <param name="message">What in the environment the engine cannot read.</param>
    public static CliException EnvironmentUnsupported(string message) => CliException.Create(
        ErrorCodes.FeatureUnsupported,
        message,
        details: new JsonObject { ["reason"] = "environment" });

    public static CliException LicenseFileNotFound(string path, string source) => CliException.Create(
        ErrorCodes.LicenseFileNotFound,
        $"License file configured via {source} does not exist: {path}",
        details: LicenseFileDetails(path, source),
        docs: "licensing");

    public static CliException LicensePathIsDirectory(string path, string source) => CliException.Create(
        ErrorCodes.LicenseFileNotFound,
        $"License path configured via {source} is a directory, not a license file: {path}",
        details: LicenseFileDetails(path, source),
        docs: "licensing");

    private static JsonObject LicenseFileDetails(string path, string source)
    {
        JsonObject details = FileNotFoundDetails("license file", path);
        details["source"] = source;
        return details;
    }

    public static CliException LicenseInvalid(string source, string reason, Exception? inner = null) => CliException.Create(
        ErrorCodes.LicenseInvalid,
        $"The license configured via {source} was rejected: {reason}",
        details: new JsonObject { ["source"] = source },
        docs: "licensing",
        innerException: inner,
        explanation: reason);

    /// <summary>
    /// A command refuses what evaluation mode would silently get wrong, such as an export the
    /// engine would write from another worksheet than the one asked for. A limit the engine itself raises is reported by the SDK
    /// alone, from its product's evaluation profile; no product translates an engine exception.
    /// </summary>
    /// <param name="message">What evaluation mode allows and what it stopped.</param>
    /// <param name="details">The facts of the limit, if any.</param>
    public static CliException EvaluationLimit(
        string message,
        JsonObject? details = null) => CliException.Create(
        ErrorCodes.EvaluationLimit,
        message,
        details: details,
        docs: "licensing");

    /// <summary>
    /// The error for an evaluation limit the engine raised, which the SDK write pipeline alone
    /// builds from the facts its product's evaluation profile recognized in the engine's
    /// exception, so every product reports it with the same message and details.
    /// </summary>
    /// <param name="engine">The engine, which also names the license, such as <c>Aspose.PDF</c>.</param>
    /// <param name="allows">What evaluation mode allows, completing "Aspose.X evaluation mode ...".</param>
    /// <param name="limit">The limit, in <paramref name="unit"/>.</param>
    /// <param name="unit">The unit of the limit, such as <c>pages</c>.</param>
    /// <param name="engineError">The engine's exception, kept as the inner exception.</param>
    internal static CliException EngineEvaluationLimit(
        string engine,
        string allows,
        int limit,
        string unit,
        Exception engineError) => CliException.Create(
        ErrorCodes.EvaluationLimit,
        $"{engine} evaluation mode {allows}, and this command needs more, so it stopped and wrote nothing.",
        details: new JsonObject { ["limit"] = limit, ["unit"] = unit },
        docs: "licensing",
        innerException: engineError);

    /// <summary>
    /// The error for a named target the document does not contain. The details list the names
    /// that exist (at most <see cref="MaximumAvailableNames"/>); the message ends with the same names.
    /// </summary>
    /// <param name="code">A code declared with <see cref="ErrorCode.NotFound"/>.</param>
    /// <param name="subject">What was looked up, e.g. <c>sheet</c> or <c>bookmark</c>.</param>
    /// <param name="requested">The name as the caller wrote it.</param>
    /// <param name="available">Every name of this kind in document order.</param>
    /// <param name="sheet">The worksheet the names are of, when the target is looked up on one sheet.</param>
    public static CliException NotFound(
        ErrorCode code,
        string subject,
        string requested,
        IReadOnlyCollection<string> available,
        string? sheet = null) => NotFoundIn(code, subject, requested, available, listInMessage: true, sheet);

    /// <summary>
    /// The error for a text a search did not find, such as a heading or a find text. The texts
    /// searched are not names to choose from, so the message does not list them; the details
    /// state them as <see cref="NotFound"/> does.
    /// </summary>
    /// <param name="code">A code declared with <see cref="ErrorCode.NotFound"/>.</param>
    /// <param name="subject">What was searched for, e.g. <c>text</c> or <c>heading</c>.</param>
    /// <param name="requested">The text as the caller wrote it.</param>
    /// <param name="searched">The texts searched, in document order.</param>
    public static CliException TextNotFound(
        ErrorCode code,
        string subject,
        string requested,
        IReadOnlyCollection<string> searched) => NotFoundIn(code, subject, requested, searched, listInMessage: false);

    private static CliException NotFoundIn(
        ErrorCode code,
        string subject,
        string requested,
        IReadOnlyCollection<string> available,
        bool listInMessage,
        string? sheet = null)
    {
        RequireNotFoundCode(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentNullException.ThrowIfNull(requested);
        ArgumentNullException.ThrowIfNull(available);

        var mistake = Mistake.Of(requested, available);
        var details = new JsonObject
        {
            ["subject"] = subject,
            ["requested"] = requested,
            ["availableCount"] = available.Count,
            ["sheet"] = sheet,
        };
        mistake.WriteTo(details);
        string message = listInMessage && available.Count > 0
            ? $"No {subject} '{requested}' was found. {mistake.MustBeOneOf()}"
            : $"No {subject} '{requested}' was found.";
        return CliException.Create(code, message, details);
    }

    /// <summary>
    /// The error for a number or range that reaches past the targets the document contains,
    /// such as page 12 of 10. Numbering starts at 1.
    /// </summary>
    /// <param name="code">A code declared with <see cref="ErrorCode.NotFound"/>.</param>
    /// <param name="subject">What was looked up, e.g. <c>page</c> or <c>slide</c>.</param>
    /// <param name="requested">The number or range as the caller wrote it.</param>
    /// <param name="count">How many targets of this kind exist, numbered from 1.</param>
    public static CliException NotFoundAt(
        ErrorCode code,
        string subject,
        string requested,
        int count)
    {
        RequireNotFoundCode(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(requested);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        return CliException.Create(
            code,
            count == 1 ? $"No {subject} '{requested}' was found; 1 exists." : $"No {subject} '{requested}' was found; {count} exist.",
            new JsonObject
            {
                ["subject"] = subject,
                ["requested"] = requested,
                ["availableCount"] = count,
            });
    }

    private static void RequireNotFoundCode(ErrorCode code)
    {
        ArgumentNullException.ThrowIfNull(code);
        if (code.DetailsSchemaId != NotFoundDetails.CatalogId)
        {
            throw new ArgumentException(
                $"Error code '{code.Name}' is not declared with ErrorCode.NotFound.", nameof(code));
        }
    }

    private static JsonArray Strings(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (string value in values)
        {
            array.Add(value);
        }

        return array;
    }

    public static CliException Internal(
        Exception exception,
        string diagnosticId = "SDK-UNEXPECTED-0001")
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentException.ThrowIfNullOrWhiteSpace(diagnosticId);
        return CliException.Create(
            ErrorCodes.Internal,
            "Unexpected internal error.",
            details: new JsonObject { ["diagnosticId"] = diagnosticId },
            innerException: exception);
    }
}
