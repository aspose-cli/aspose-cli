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
    /// <param name="commands">Its visible commands by their path below the root, such as <c>cells read</c>.</param>
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
                or ResourceBudgetKinds.OutputSetDirectories => OutputBudgetExceeded(resource, observed, limit, unit, phase),
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
        $"Input changed while it was read: {path}",
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
    /// Content the engine reads but gives nothing to render, such as a sheet it lays out on no
    /// page.
    /// </summary>
    /// <param name="target">What could not be rendered, starting the message, such as <c>Sheet 'Q1'</c>.</param>
    /// <param name="reason">Why, completing the message, such as <c>the engine lays out no page for it (SheetRender.PageCount is 0)</c>.</param>
    /// <param name="facts">The product's facts that identify the target, if any.</param>
    public static CliException RenderFailed(string target, string reason, JsonObject? facts = null) => CliException.Create(
        ErrorCodes.RenderFailed,
        $"{target} could not be rendered: {reason}.",
        details: facts);

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
    /// A regular expression the caller supplied ran past its execution budget, which bounds
    /// the time any one match may take.
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

    /// <summary>An output file exists and the caller did not ask to replace it.</summary>
    /// <param name="path">The file.</param>
    public static CliException OutputExists(string path) => CliException.Create(
        ErrorCodes.OutputExists,
        $"Output file already exists: {path}",
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
    /// mode's job alone.
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

    /// <summary>Two outputs of one operation resolve to the same path.</summary>
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

    /// <summary>
    /// An input document whose extension, or lack of one, is none of the extensions of the
    /// formats its product reads, refused before the file is read: <c>FORMAT_UNSUPPORTED</c>.
    /// </summary>
    /// <param name="path">The input file.</param>
    /// <param name="extensions">The extensions the product reads, in declaration order.</param>
    public static CliException ExtensionNotRead(string path, IReadOnlyList<string> extensions)
    {
        ArgumentNullException.ThrowIfNull(extensions);
        return CliException.Create(
            ErrorCodes.FormatUnsupported,
            $"Input '{path}' has the extension {Path.GetExtension(path)}, which names a format this command does not read; it reads {string.Join(", ", extensions)}.",
            details: new JsonObject { ["path"] = path, ["available"] = Strings(extensions) });
    }

    /// <summary>
    /// Restates a product's <c>FILE_CORRUPT</c> about a file its format declarations do not let it
    /// read, such as a format only <c>convert</c> reads, as <c>FORMAT_UNSUPPORTED</c>. The
    /// product's message and details stay: they say, in its terms, what it does not read.
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
            innerException: corrupt.InnerException,
            explanation: corrupt.Explanation);
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
    /// An output password given for a format that cannot carry one, naming the option the caller
    /// passed and the formats among those the command writes that can.
    /// </summary>
    /// <param name="option">The option that gave the password, such as <c>--encrypt-env</c>.</param>
    /// <param name="format">The id of the format the output is written in.</param>
    /// <param name="protectable">The ids of the formats the command writes that can carry a password, in declared order.</param>
    internal static CliException Unprotectable(string option, string format, IReadOnlyList<string> protectable)
    {
        string reason = $"the '{format}' format cannot be password-protected";
        return OptionInvalid(option, protectable.Count == 0 ? reason : $"{reason}; {string.Join(", ", protectable)} can");
    }

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

    public static CliException LicenseFileNotFound(string path, string source) => CliException.Create(
        ErrorCodes.LicenseFileNotFound,
        $"License file configured via {source} does not exist: {path}",
        details: LicenseFileDetails(path, source));

    public static CliException LicensePathIsDirectory(string path, string source) => CliException.Create(
        ErrorCodes.LicenseFileNotFound,
        $"License path configured via {source} is a directory, not a license file: {path}",
        details: LicenseFileDetails(path, source));

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
        innerException: inner,
        explanation: reason);

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
        innerException: engineError);

    /// <summary>
    /// The error for a named target the document does not contain. The details list the names
    /// that exist (at most <see cref="MaximumAvailableNames"/>); the message ends with the same names.
    /// </summary>
    /// <param name="code">A code declared with <see cref="ErrorCode.NotFound"/>.</param>
    /// <param name="subject">What was looked up, e.g. <c>sheet</c> or <c>bookmark</c>.</param>
    /// <param name="requested">The name as the caller wrote it.</param>
    /// <param name="available">Every name of this kind in document order.</param>
    public static CliException NotFound(
        ErrorCode code,
        string subject,
        string requested,
        IReadOnlyCollection<string> available)
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
        };
        mistake.WriteTo(details);
        string message = available.Count > 0
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
