using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Sdk.Errors;

/// <summary>
/// Factory for every error the CLI raises. Centralizing construction keeps
/// messages and hints consistent, and enforces the house rule that errors are
/// written for agent self-correction: state the fact, include the valid
/// alternatives, recommend the fix.
/// </summary>
public static partial class CliErrors
{
    /// <summary>The most names an error lists in <c>details.available</c>.</summary>
    public const int MaximumAvailableNames = 50;

    /// <summary>
    /// The error for a command line that does not parse. <paramref name="mistake"/> is the first
    /// unknown command, option or value: the message ends with the names it could have been,
    /// which <c>details.available</c> lists too, and the hint asks about the closest ones first.
    /// <c>details.errors</c> lists the problems only when there are several.
    /// </summary>
    /// <param name="problems">Each problem, one sentence each.</param>
    /// <param name="mistake">The first unknown command, option or value, if any.</param>
    /// <param name="corrected">
    /// The arguments of the command line without the mistake, when that alone corrects it: the
    /// hint ends with the command line they make, ready to run, in place of the advice to read help.
    /// </param>
    /// <param name="parameter">
    /// The option or argument the first problem is about, such as an unknown or missing option,
    /// stated in <c>details.option</c> or <c>details.argument</c>; null when it is about none.
    /// </param>
    /// <param name="hint">
    /// What to do instead, in place of the advice to read help, such as the command that does what
    /// an option the command does not have would; null for that advice.
    /// </param>
    public static CliException Usage(
        IReadOnlyList<string> problems,
        Mistake? mistake = null,
        IReadOnlyList<string>? corrected = null,
        string? parameter = null,
        string? hint = null)
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

        string next = corrected is not null ? $"Run '{CommandLineText.Of(corrected)}'." : hint ?? UsageHint;
        return CliException.Create(ErrorCodes.UsageError, message, mistake?.Hint(next) ?? next, details.Count == 0 ? null : details);
    }

    /// <summary>
    /// A command line whose options and arguments all parse but whose values the command refuses,
    /// such as a value outside an option's list or a number that does not parse:
    /// <c>OPTION_INVALID</c> naming the first refused <paramref name="parameter"/> in
    /// <c>details.option</c> or <c>details.argument</c>. <paramref name="mistake"/>, when the value
    /// has a list of values, ends the message with them and leads the hint with the closest ones.
    /// </summary>
    /// <param name="problems">Each refused value, one sentence each.</param>
    /// <param name="parameter">The option or argument of the first refused value.</param>
    /// <param name="mistake">The first refused value among a list of values, if any.</param>
    /// <param name="hint">The advice to run another command that takes the value, in place of the closest values and the advice to read help; null for none.</param>
    public static CliException OptionValuesRefused(IReadOnlyList<string> problems, string parameter, Mistake? mistake = null, string? hint = null)
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

        return CliException.Create(ErrorCodes.OptionInvalid, message, hint ?? mistake?.Hint(UsageHint) ?? UsageHint, details);
    }

    /// <summary>
    /// A command that only groups other commands, run without one: it does nothing on its own, so
    /// the error lists its commands in <c>details.available</c> by their path below the root.
    /// Options it does not know, given after the group, are problems before the missing
    /// command; the first one is stated in <c>details.option</c> and the options closest to it
    /// lead the suggestions.
    /// </summary>
    /// <param name="command">The group as the caller typed it, such as <c>aspose-cli cells</c>.</param>
    /// <param name="commands">Its visible commands by their path below the root, such as <c>cells inspect</c>.</param>
    /// <param name="unknownOptions">The tokens given to the group that name none of its options, in order.</param>
    /// <param name="meantOptions">The options the first unknown one most likely meant, best first.</param>
    public static CliException CommandRequired(
        string command,
        IReadOnlyList<string> commands,
        IReadOnlyList<string>? unknownOptions = null,
        IReadOnlyList<string>? meantOptions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentNullException.ThrowIfNull(commands);
        unknownOptions ??= [];
        // Only the options are compared with the unknown one; the commands are what the group needs.
        var mistake = Mistake.Of(string.Empty, commands, meantOptions ?? []);
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
            mistake.Hint($"Run '{command} <command>'; '{command} --help' describes each one."),
            details);
    }

    private static string UsageHint =>
        $"Run the command again with --help for usage; '{DistributionInfo.CommandName} --help' lists the commands and '{DistributionInfo.CommandName} docs' the documentation topics.";

    /// <summary>A problem as a sentence that ends with a period.</summary>
    private static string Sentence(string problem)
    {
        string trimmed = problem.Trim();
        return trimmed.EndsWith('.') ? trimmed : trimmed + ".";
    }

    /// <summary>
    /// An input file that does not exist. Its details are the not-found details of a
    /// <c>file</c>: the path as the caller wrote it and as it resolved.
    /// </summary>
    public static CliException FileNotFound(string path) => CliException.Create(
        ErrorCodes.FileNotFound,
        $"Input file not found: {path}",
        hint: "Check the path. Relative paths resolve against --workdir (or the current directory).",
        details: FileNotFoundDetails("file", path));

    /// <summary>
    /// A recent file the caller chose by its id that is no longer listed, so there is no path
    /// to name; the details state the id.
    /// </summary>
    /// <param name="id">The recent file's id as the caller gave it.</param>
    public static CliException RecentFileNotFound(string id) => CliException.Create(
        ErrorCodes.FileNotFound,
        $"The recent file '{id}' is no longer listed.",
        hint: "Choose the file again; the recent list keeps only files that still exist.",
        details: new JsonObject { ["subject"] = "recent file", ["requested"] = id });

    private static JsonObject FileNotFoundDetails(string subject, string path) => new()
    {
        ["subject"] = subject,
        ["requested"] = path,
        ["path"] = Path.GetFullPath(path),
    };

    public static CliException FileLocked(string path) => CliException.Create(
        ErrorCodes.FileLocked,
        $"File is in use by another process: {path}",
        hint: "Close the application holding this file, then retry.",
        details: new JsonObject { ["path"] = path });

    /// <summary>
    /// An encrypted file opened without a password. <paramref name="operationField"/> names the
    /// operation field that supplies the password of a file an operation reads; without it the
    /// hint names <paramref name="passwordEnvironmentOption"/>, the option that supplies this
    /// file's password, such as <c>--certificate-password-env</c>, by default <c>--password-env</c>.
    /// </summary>
    public static CliException PasswordRequired(string path, string? operationField = null, string? passwordEnvironmentOption = null) => CliException.Create(
        ErrorCodes.PasswordRequired,
        $"File is encrypted and requires a password: {path}",
        hint: PasswordHint(ErrorCodes.PasswordRequired, operationField, passwordEnvironmentOption),
        details: new JsonObject { ["path"] = path });

    /// <summary>
    /// A password that does not open the file, such as a certificate's; <paramref name="operationField"/>
    /// and <paramref name="passwordEnvironmentOption"/> as for <see cref="PasswordRequired"/>.
    /// </summary>
    public static CliException PasswordInvalid(string path, string? operationField = null, string? passwordEnvironmentOption = null) => CliException.Create(
        ErrorCodes.PasswordInvalid,
        $"The provided password does not open the file: {path}",
        hint: PasswordHint(ErrorCodes.PasswordInvalid, operationField, passwordEnvironmentOption),
        details: new JsonObject { ["path"] = path });

    /// <summary>
    /// A protection inside a document, such as a workbook's structure or a document's editing
    /// restriction, that has a password the operation did not supply or supplied wrongly. It is
    /// the same mistake as a file's open password, so it has the same codes and hint.
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
            hint: PasswordHint(code, operationField),
            details: new JsonObject { ["target"] = target, ["field"] = operationField },
            innerException: innerException);
    }

    /// <summary>
    /// Restates a password error raised while opening a file an operation reads, whose loader
    /// cannot know where the password came from, for the operation's own password field.
    /// </summary>
    public static CliException ForOperationSource(CliException error, string operationField)
    {
        ArgumentNullException.ThrowIfNull(error);
        if (!IsPasswordError(error) || error.Details?["path"]?.GetValue<string>() is not { } path)
        {
            throw new ArgumentException($"{error.Code.Name} is not a password error of a file.", nameof(error));
        }

        return error.Code == ErrorCodes.PasswordRequired
            ? PasswordRequired(path, operationField)
            : PasswordInvalid(path, operationField);
    }

    /// <summary>
    /// Restates a password error about a file the command opens with no password at all, such
    /// as a template, with <paramref name="hint"/> in place of the advice to retry with a
    /// password; the code and details stay.
    /// </summary>
    /// <param name="error">The <c>PASSWORD_REQUIRED</c> or <c>PASSWORD_INVALID</c> error the loader raised.</param>
    /// <param name="hint">What to do instead, such as using an unencrypted copy.</param>
    public static CliException WithoutPassword(CliException error, string hint)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentException.ThrowIfNullOrWhiteSpace(hint);
        if (!IsPasswordError(error))
        {
            throw new ArgumentException($"{error.Code.Name} is not a password error.", nameof(error));
        }

        return CliException.Create(error.Code, error.Message, hint, error.Details?.DeepClone() as JsonObject, error.Docs, error);
    }

    /// <summary>
    /// Restates a password error about one of the documents a command reads, naming the
    /// argument of that input in <c>details.argument</c> and its own password option in the hint.
    /// </summary>
    internal static CliException ForInput(CliException error, string input, string passwordEnvironmentOption)
    {
        string path = error.Details!["path"]!.GetValue<string>();
        return CliException.Create(
            error.Code,
            $"{error.Message} (the {input} input)",
            hint: PasswordHint(error.Code, operationField: null, passwordEnvironmentOption),
            details: new JsonObject { ["path"] = path, ["argument"] = input });
    }

    /// <summary>
    /// Restates a password error about a document that is neither of the two inputs a command
    /// reads with a hint that names both inputs' password options, since the command has no
    /// single one.
    /// </summary>
    internal static CliException ForPairedCommand(CliException error, string inputOption, string otherOption) => CliException.Create(
        error.Code,
        error.Message,
        hint: PasswordHint(error.Code, operationField: null, $"{inputOption} <NAME> or {otherOption}") + " Use the option of the input the password opens.",
        details: error.Details?.DeepClone() as JsonObject);

    /// <summary>Whether <paramref name="error"/> is <c>PASSWORD_REQUIRED</c> or <c>PASSWORD_INVALID</c>.</summary>
    public static bool IsPasswordError(CliException error) =>
        error.Code == ErrorCodes.PasswordRequired || error.Code == ErrorCodes.PasswordInvalid;

    /// <summary>
    /// The hint of a <c>PASSWORD_REQUIRED</c> or <c>PASSWORD_INVALID</c> <paramref name="code"/>:
    /// store the password in an environment variable and name it in the operation field that
    /// supplies it, or else in <paramref name="passwordEnvironmentOption"/>, by default
    /// <c>--password-env</c>. The environment form keeps the password out of the process list.
    /// </summary>
    public static string PasswordHint(ErrorCode code, string? operationField, string? passwordEnvironmentOption = null)
    {
        string lead = (code == ErrorCodes.PasswordRequired
            ? "Ask the user for the password" : "Ask the user to double-check the password")
            + ", store it in an environment variable and ";
        return operationField is not null
            ? $"{lead}name that variable in the operation's \"{operationField}\" field."
            : passwordEnvironmentOption is not null
                ? $"{lead}retry with {passwordEnvironmentOption} <NAME>."
                : $"{lead}retry with --password-env <NAME>.";
    }

    public static CliException FileAccessDenied(string path) => CliException.Create(
        ErrorCodes.FileAccessDenied,
        $"Access to the input file was denied: {path}",
        hint: "Check the file permissions and that no security policy prevents the process from reading it.",
        details: new JsonObject { ["path"] = path });

    /// <summary>
    /// An input that cannot be read as the document the command expects: damaged, truncated or
    /// another kind of file. <c>details.path</c> always names the file, so the Host can tell which
    /// input of a command failed and name the product whose format a renamed file has.
    /// </summary>
    /// <param name="path">The input file.</param>
    /// <param name="document">What the file should be, such as <c>PDF document</c> or <c>spreadsheet</c>.</param>
    /// <param name="reason">Why it is not one, completing a sentence.</param>
    /// <param name="hint">How to check the file, in the terms of the product that reads it.</param>
    /// <param name="innerException">The engine or IO failure, if any.</param>
    public static CliException InputUnreadable(
        string path,
        string document,
        string reason,
        string hint,
        Exception? innerException = null) => CliException.Create(
        ErrorCodes.FileCorrupt,
        $"Input is not a valid {document}: {path} ({reason}).",
        hint: hint,
        details: new JsonObject { ["path"] = Path.GetFullPath(path), ["reason"] = reason },
        innerException: innerException);

    public static CliException FileTooLarge(long sizeBytes, long limitBytes) => CliException.Create(
        ErrorCodes.FileTooLarge,
        $"Input file is {sizeBytes} bytes, exceeding the {limitBytes}-byte budget.",
        hint: "Raise --max-input-bytes within its advertised safety maximum, or split the input into smaller files.",
        details: new JsonObject
        {
            ["resource"] = ResourceBudgetKinds.InputBytes,
            ["actual"] = sizeBytes,
            ["limit"] = limitBytes,
            ["unit"] = "bytes",
            ["phase"] = "pre-runtime-metadata",
        });

    public static CliException InputBudgetExceeded(
        string resource,
        long observed,
        long limit,
        string unit,
        string phase) => CliException.Create(
        ErrorCodes.InputBudgetExceeded,
        $"Resource '{resource}' reached {observed} {unit}, exceeding the {limit}-{unit} budget.",
        hint: resource == ResourceBudgetKinds.InputBytes
            ? "Raise --max-input-bytes within the advertised safety maximum, or split the input into smaller files."
            : resource == ResourceBudgetKinds.OutputBytes
                ? "Produce fewer or smaller outputs; capabilities lists the active default and hard maximum."
                : "Use a smaller input or split the operation; capabilities lists the active default and hard maximum.",
        details: new JsonObject
        {
            ["resource"] = resource,
            ["actual"] = observed,
            ["limit"] = limit,
            ["unit"] = unit,
            ["phase"] = phase,
        });

    public static CliException InputChanged(
        string path,
        long expectedBytes,
        long observedBytes) => CliException.Create(
        ErrorCodes.InputChanged,
        $"Input changed after admission: {path}",
        hint: "Stop the process modifying the input, then retry against a stable file or a private copy.",
        details: new JsonObject
        {
            ["path"] = path,
            ["expectedBytes"] = expectedBytes,
            ["observedBytes"] = observedBytes,
            ["resource"] = ResourceBudgetKinds.InputBytes,
        });

    public static CliException InputChanged(
        string path,
        string expectedSha256,
        string observedSha256) => CliException.Create(
        ErrorCodes.InputChanged,
        $"Input no longer matches the requested fingerprint: {path}",
        hint: "Inspect the current file, review intervening changes, and retry with its new input.fingerprint.sha256 value.",
        details: new JsonObject
        {
            ["path"] = path,
            ["expectedSha256"] = expectedSha256,
            ["observedSha256"] = observedSha256,
        });

    public static CliException InputEncodingInvalid(
        string phase,
        Exception? inner = null) => CliException.Create(
        ErrorCodes.InputEncodingInvalid,
        "Text input is not valid UTF-8.",
        hint: "Save or pipe the text as UTF-8 (a UTF-8 BOM is accepted), then retry.",
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
    /// <param name="hint">How to name the file's encoding to the command that reads it.</param>
    public static CliException InputEncodingInvalid(string path, long offset, string hint) => CliException.Create(
        ErrorCodes.InputEncodingInvalid,
        $"'{Path.GetFileName(path)}' is not UTF-8 text (invalid byte at offset {offset}); read as UTF-8, its text would be replaced.",
        hint: hint,
        details: new JsonObject { ["path"] = path, ["offset"] = offset });

    /// <summary>
    /// A text file whose numbers read differently in different cultures, such as a decimal
    /// comma, so the caller must name its culture rather than have the CLI guess.
    /// </summary>
    /// <param name="path">The text file.</param>
    /// <param name="sample">A number from the file that the invariant culture would change.</param>
    /// <param name="line">The 1-based line of <paramref name="sample"/>.</param>
    /// <param name="hint">How to name the file's culture to the command that reads it.</param>
    public static CliException NumberFormatAmbiguous(string path, string sample, int line, string hint) => CliException.Create(
        ErrorCodes.FormatAmbiguous,
        $"'{Path.GetFileName(path)}' writes numbers such as '{sample}' (line {line}) with a decimal comma; read with invariant formats they would become different numbers.",
        hint: hint,
        details: new JsonObject { ["path"] = path, ["sample"] = sample, ["line"] = line });

    public static CliException PreviewBudgetExceeded(
        string resource,
        long actual,
        long limit) => CliException.Create(
        ErrorCodes.PreviewBudgetExceeded,
        $"The preview {resource} exceeds its resource budget.",
        hint:
            $"Reduce the rendered document or raise the matching {DistributionInfo.EnvironmentVariablePrefix}PREVIEW_MAX_* limit within its safety maximum.",
        details: new JsonObject
        {
            ["resource"] = resource,
            ["actual"] = actual,
            ["limit"] = limit,
        });

    public static CliException UploadBudgetExceeded(
        string resource,
        long actual,
        long limit) => CliException.Create(
        ErrorCodes.UploadBudgetExceeded,
        $"The App upload {resource} exceeds its session budget.",
        hint:
            $"Remove prior uploads, upload a smaller file, or raise the matching {DistributionInfo.EnvironmentVariablePrefix}APP_MAX_UPLOAD_* limit within its safety maximum.",
        details: new JsonObject
        {
            ["resource"] = resource,
            ["actual"] = actual,
            ["limit"] = limit,
        });

    public static CliException RenderTooLarge(long width, long height, int? dpi, long maxPixels, string hint)
    {
        string megabytes = ((double)width * height * 4d / 1_048_576d)
            .ToString("0", CultureInfo.InvariantCulture);
        string resolution = dpi is { } value
            ? string.Create(CultureInfo.InvariantCulture, $" at {value} DPI")
            : string.Empty;
        return CliException.Create(
            ErrorCodes.RenderTooLarge,
            $"Rendering{resolution} needs a {width}x{height} pixel image ({megabytes} MB), which exceeds the limit of {maxPixels} pixels per image.",
            hint: hint,
            details: new JsonObject
            {
                ["width"] = width,
                ["height"] = height,
                ["dpi"] = dpi,
                ["actual"] = width * height,
                ["limit"] = maxPixels,
                ["unit"] = "pixels",
            });
    }

    /// <summary>
    /// The engine failed while rasterizing content it could otherwise read. This is not a
    /// defect of the CLI, so the hint offers another way to deliver the content.
    /// </summary>
    /// <param name="target">What could not be rendered, starting the message, such as <c>Sheet 'Q1'</c>.</param>
    /// <param name="engineMessage">The engine's own message.</param>
    /// <param name="hint">Another way to deliver the content.</param>
    /// <param name="facts">The product's facts that identify the target, if any.</param>
    public static CliException RenderFailed(string target, string engineMessage, string hint, JsonObject? facts = null)
    {
        JsonObject details = facts ?? [];
        details["engineMessage"] = engineMessage;
        return CliException.Create(
            ErrorCodes.RenderFailed,
            $"{target} could not be rendered: the engine failed while rasterizing its content ({engineMessage}).",
            hint: hint,
            details: details);
    }

    public static CliException OperationTimeout(int seconds) => CliException.Create(
        ErrorCodes.OperationTimeout,
        $"The operation did not finish within the {seconds}s timeout.",
        hint: "Raise --timeout, narrow the work (a smaller range or fewer ops), or split it into steps.",
        details: new JsonObject { ["timeoutSeconds"] = seconds });

    public static CliException OperationTimeout(int seconds, string phase) => CliException.Create(
        ErrorCodes.OperationTimeout,
        $"The operation did not finish within the {seconds}s timeout.",
        hint: "Raise --timeout, narrow the work (a smaller range or fewer ops), or split it into steps.",
        details: new JsonObject
        {
            ["timeoutSeconds"] = seconds,
            ["phase"] = phase,
        });

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
            hint: "Simplify the expression or search a narrower part of the document.",
            details: new JsonObject { ["phase"] = "regex" },
            innerException: exception);
    }

    public static CliException WorkerTerminationFailed(int processId) => CliException.Create(
        ErrorCodes.WorkerTerminationFailed,
        "The operation deadline expired, but the worker process tree could not be confirmed stopped.",
        hint: "Stop the reported process before retrying or accessing the same output files.",
        details: new JsonObject
        {
            ["pid"] = processId,
            ["quiescenceConfirmed"] = false,
        });

    /// <summary>An output file exists and the caller did not ask to replace it.</summary>
    /// <param name="path">The file.</param>
    /// <param name="parameter">The option or argument that names the output, such as <c>--out-dir</c>; null when it is not known.</param>
    /// <param name="partsDependOnDocument">
    /// Whether the file is one the command may write as a numbered part, which a dry run cannot
    /// tell without reading how many parts the document has.
    /// </param>
    public static CliException OutputExists(string path, string? parameter = null, bool partsDependOnDocument = false) => CliException.Create(
        ErrorCodes.OutputExists,
        partsDependOnDocument
            ? $"Output file already exists: {path}; the command may write it, since how many parts it writes, and so their names, depend on the document."
            : $"Output file already exists: {path}",
        hint: parameter is null
            ? "Pass --overwrite to replace it, or choose a different output path."
            : $"Pass --overwrite to replace it, or choose a different path with {parameter}.",
        details: new JsonObject { ["path"] = path });

    /// <summary>
    /// An output published as one directory names a directory or file that already exists, and
    /// the caller did not ask to replace it.
    /// </summary>
    public static CliException OutputDirectoryExists(string path) => CliException.Create(
        ErrorCodes.OutputExists,
        $"Output directory already exists: {path}",
        hint: "Pass --overwrite to replace it, or choose a path that does not exist yet with --out.",
        details: new JsonObject { ["path"] = path });

    /// <summary>
    /// An output published as one directory names an existing directory that <c>--overwrite</c>
    /// may not replace: it is neither empty nor an earlier output of the command, which alone it
    /// replaces. It is the fact <see cref="OutputDirectoryExists"/> states, an output in the way,
    /// so it has its code; only the way out differs.
    /// </summary>
    /// <param name="path">The existing directory.</param>
    /// <param name="owned">What an earlier output of the command holds, such as <c>review evidence (review.json)</c>.</param>
    public static CliException OutputDirectoryNotOwned(string path, string owned) => CliException.Create(
        ErrorCodes.OutputExists,
        $"Output directory already exists and is not an earlier {owned}: {path}",
        hint: $"--overwrite replaces only an empty directory or an earlier {owned}; choose another path with --out.",
        details: new JsonObject { ["path"] = path });

    /// <summary>
    /// A product view that <c>review</c> cannot render as evidence, because its parts are no
    /// images, such as the Cells <c>workbook</c> view; <c>preview open</c> shows it.
    /// </summary>
    /// <param name="view">The requested view.</param>
    /// <param name="product">The product that reads the file.</param>
    /// <param name="reviewViews">The product's views that review can render.</param>
    public static CliException ViewNotReviewable(string view, string product, IReadOnlyList<string> reviewViews)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(view);
        ArgumentException.ThrowIfNullOrWhiteSpace(product);
        ArgumentNullException.ThrowIfNull(reviewViews);
        return CliException.Create(
            ErrorCodes.FeatureUnsupported,
            $"Review cannot use the '{view}' view of {product}: its parts are not images.",
            hint: $"Use {string.Join(" or ", reviewViews.Select(static item => "--view " + item))}, or show the '{view}' view with '{DistributionInfo.CommandName} preview open'.",
            details: new JsonObject
            {
                ["option"] = "--view",
                ["feature"] = $"review --view {view}",
                ["product"] = product,
                ["available"] = Strings(reviewViews),
            });
    }

    /// <summary>
    /// An output that the CLI manages, such as an installed Skill, holds content it did not
    /// write, so updating it could destroy that content.
    /// </summary>
    /// <param name="subject">What the output is, starting the message, such as <c>The Skill target</c>.</param>
    /// <param name="path">The output.</param>
    /// <param name="reason">What in it the CLI does not manage.</param>
    /// <param name="hint">How to keep the content and still update.</param>
    /// <param name="innerException">The failure that showed it, if any.</param>
    public static CliException OutputNotManaged(
        string subject,
        string path,
        string reason,
        string hint,
        Exception? innerException = null) => CliException.Create(
        ErrorCodes.OutputExists,
        $"{subject} cannot be updated safely: {path} ({reason}).",
        hint: hint,
        details: new JsonObject { ["path"] = path, ["reason"] = reason },
        innerException: innerException);

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
            hint: "Check file permissions, locks and free disk space, then retry.",
            innerException: exception);
    }

    /// <summary>
    /// An explicit output resolves to an input document. Replacing the input is the in-place
    /// mode's job, which alone carries its backup and fingerprint precondition.
    /// </summary>
    internal static CliException OutputIsInput(string parameter, string path, bool inPlaceAvailable) => CliException.Create(
        ErrorCodes.OptionInvalid,
        $"Invalid use of {parameter}: the output resolves to an input file: {path}",
        hint: inPlaceAvailable
            ? $"Pass --in-place instead of {parameter} to modify the input, with --backup or --if-match as needed; or choose another output path."
            : "Choose another output path; this command never replaces its input.",
        details: new JsonObject { [ParameterKey(parameter)] = parameter, ["path"] = path });

    /// <summary>
    /// An output named <c>-</c>, which names standard input only: every output is a file or a
    /// directory, never standard output.
    /// </summary>
    /// <param name="parameter">The option or argument that names the output, such as <c>--out</c>.</param>
    /// <param name="directory">Whether the output is a directory.</param>
    public static CliException OutputIsStandardStream(string parameter, bool directory = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameter);
        return CliException.Create(
            ErrorCodes.OptionInvalid,
            $"Invalid use of {parameter}: '-' names standard input, not an output",
            hint: directory
                ? "Write to a directory; `-` is not supported: name a file-system path instead."
                : "Write to a file; `-` is not supported: name a file instead.",
            details: new JsonObject { [ParameterKey(parameter)] = parameter });
    }

    /// <summary>Two outputs of one operation, including a backup, resolve to the same path.</summary>
    internal static CliException DuplicateOutput(string path) => CliException.Create(
        ErrorCodes.UsageError,
        $"Two outputs of this operation resolve to the same path: {path}",
        hint: "Give every output, including a backup, its own path.",
        details: new JsonObject { ["path"] = path });

    /// <summary>
    /// The engine wrote a directory beside an output published with its companion files. Only
    /// files are published with an output, so the set is refused and nothing is published.
    /// </summary>
    internal static CliException CompanionDirectoryUnpublished(string path, string directory) => CliException.Create(
        ErrorCodes.OutputPublicationFailed,
        $"The output set could not be published: the engine wrote the directory '{directory}' beside {path}, and only companion files are published with an output.",
        hint: "Nothing was written. Convert to another format that keeps its content in one file, and report the format that wrote a directory.",
        details: new JsonObject { ["path"] = path, ["directory"] = directory });

    public static CliException OutputUnwritable(
        string path,
        string reason,
        Exception? inner = null,
        string phase = "prepare") => CliException.Create(
        ErrorCodes.OutputUnwritable,
        $"Output file could not be written: {path} ({reason})",
        hint: phase switch
        {
            "backup" => "The safety backup could not be created, so the file was not replaced. "
                + "Check the directory permissions and available disk space, then retry.",
            "write" => "Check that the output directory is writable and has enough free disk space.",
            "replace" => "Ask the user to close the file in the other application, then retry; it was not changed.",
            _ => "Check that the directory exists and the process has write permission.",
        },
        details: new JsonObject
        {
            ["path"] = path,
            ["reason"] = reason,
            ["phase"] = phase,
        },
        innerException: inner);

    public static CliException LoopbackPortInUse(int port) => CliException.Create(
        ErrorCodes.LoopbackPortInUse,
        port == 0
            ? "No available ephemeral loopback HTTP port could be bound."
            : $"Loopback port {port} is already in use by another process.",
        hint: port == 0
            ? "Close unused local services, verify loopback networking, and retry."
            : "Pass --port 0 to bind a free ephemeral port, or choose a different port with --port.",
        details: new JsonObject { ["port"] = port });

    public static CliException LoopbackListenerUnavailable(
        bool permissionDenied,
        Exception innerException) => CliException.Create(
        ErrorCodes.LoopbackListenerUnavailable,
        permissionDenied
            ? "The current user is not permitted to create the loopback HTTP listener."
            : "The platform could not create the loopback HTTP listener.",
        hint: permissionDenied
            ? "Allow the current user to bind loopback HTTP listeners, then retry."
            : $"Run '{DistributionInfo.CommandName} doctor', verify loopback networking, and retry.",
        details: new JsonObject
        {
            ["stage"] = "listener",
            ["reason"] = permissionDenied
                ? "permission-denied"
                : "listener-unavailable",
        },
        innerException: innerException);

    /// <summary>A format id that names none of <paramref name="supported"/>; the details list them and the closest ones.</summary>
    public static CliException FormatUnsupported(string requested, IReadOnlyList<string> supported)
    {
        var mistake = Mistake.Of(requested, supported);
        var details = new JsonObject { ["requested"] = requested };
        mistake.WriteTo(details);
        return CliException.Create(
            ErrorCodes.FormatUnsupported,
            $"Unsupported format '{requested}'. {mistake.MustBeOneOf()}",
            hint: mistake.Hint($"Use one of the supported format ids, or run '{DistributionInfo.CommandName} capabilities' to list everything this build supports."),
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
    /// <param name="mistyped">
    /// Whether the extension names no format at all, so it is a mistake for the closest of
    /// <paramref name="extensions"/>, which <c>details.suggestions</c> lists; false for a missing
    /// extension or one of a format the command does not write.
    /// </param>
    /// <param name="hint">How to fix the command line.</param>
    /// <param name="facts">The format that decided <paramref name="extensions"/>, or the one the extension names.</param>
    /// <param name="meant">
    /// The extension a mistyped one stands for when it is none of <paramref name="extensions"/>
    /// but that of a format another command writes, such as <c>.pdf</c> for <c>.pfd</c>; it is
    /// the suggestion. Null otherwise.
    /// </param>
    internal static CliException OutputExtensionInvalid(
        string parameter,
        string output,
        string subject,
        string problem,
        IReadOnlyList<string> extensions,
        bool mistyped,
        string hint,
        JsonObject facts,
        string? meant = null)
    {
        string extension = Path.GetExtension(output);
        // Only a mistyped extension is compared; an empty request is close to nothing.
        var mistake = Mistake.Of(mistyped || meant is not null ? extension : string.Empty, extensions, meant,
            keyOf: static name => name.TrimStart('.').ToLowerInvariant());
        facts[ParameterKey(parameter)] = parameter;
        facts["extension"] = extension;
        mistake.WriteTo(facts);
        return CliException.Create(
            ErrorCodes.OptionInvalid,
            $"{subject} {problem}. {mistake.MustBeOneOf()}.",
            mistake.Hint(hint),
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
            $"'{Path.GetFileName(path)}' has the {Path.GetExtension(path)} extension of a {declaredProduct} document, "
                + $"but its content looks like {LooksLike(detectedProducts)}.",
            hint: "Check where the file came from. " + ProductChoiceHint(detectedProducts),
            details: new JsonObject
            {
                ["path"] = path,
                ["declared"] = declaredProduct,
                ["detected"] = detected,
            });
    }

    /// <summary>
    /// Restates a product's <c>FILE_CORRUPT</c> about a file whose content has the format of
    /// other products, such as a Word document renamed to <c>.pdf</c>, as the mismatch generic
    /// routing reports for the same file, with the same message. The product's details stay, so
    /// <c>details.reason</c> still says why the product could not read it.
    /// </summary>
    /// <param name="corrupt">The product's <c>FILE_CORRUPT</c>.</param>
    /// <param name="path">The file, when the details do not name it already.</param>
    /// <param name="declaredProduct">The product that could not read the file.</param>
    /// <param name="detectedProducts">The products whose format the content has.</param>
    /// <param name="hint">The command that reads the file.</param>
    public static CliException FormatMismatch(
        CliException corrupt,
        string path,
        string declaredProduct,
        IReadOnlyList<string> detectedProducts,
        string hint)
    {
        ArgumentNullException.ThrowIfNull(corrupt);
        if (corrupt.Code != ErrorCodes.FileCorrupt)
        {
            throw new ArgumentException($"{corrupt.Code.Name} is not a corrupt input.", nameof(corrupt));
        }

        JsonObject details = corrupt.Details?.DeepClone().AsObject() ?? [];
        details["path"] ??= path;
        details["declared"] = declaredProduct;
        details["detected"] = Strings(detectedProducts);
        string file = details["path"]!.GetValue<string>();
        return CliException.Create(
            ErrorCodes.FormatMismatch,
            $"'{Path.GetFileName(file)}' has the {Path.GetExtension(file)} extension of a {declaredProduct} document, "
                + $"but its content looks like {LooksLike(detectedProducts)}.",
            hint: hint,
            details: details,
            docs: corrupt.Docs,
            innerException: corrupt.InnerException);
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
            hint: "The file may be empty, truncated or damaged; renaming a file does not change its format. "
                + "Ask for an intact copy of the document.",
            details: new JsonObject { ["path"] = path, ["reason"] = reason });
    }

    /// <summary>
    /// Creates the error for a file in a format this CLI only writes, such as the <c>.xps</c> a
    /// convert wrote: <c>FORMAT_MISMATCH</c>, as for content no product recognizes, but the hint
    /// names the commands that write the format and, for <c>review</c>, the PDF of the same
    /// content to review instead, since naming a product cannot help.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <param name="writers">The commands that write the format, as <c>product verb</c>.</param>
    /// <param name="pdfWriter">A product whose convert writes both the format and PDF; null for none.</param>
    /// <param name="operation">The generic operation that tried to read it, such as <c>review</c>.</param>
    /// <param name="candidates">The products that take part in the operation.</param>
    public static CliException FormatOnlyWritten(
        string path,
        IReadOnlyList<string> writers,
        string? pdfWriter,
        string operation,
        IReadOnlyList<string> candidates)
    {
        ArgumentNullException.ThrowIfNull(writers);
        if (writers.Count == 0)
        {
            throw new ArgumentException("A format only written has a command that writes it.", nameof(writers));
        }

        string extension = Path.GetExtension(path);
        string listed = string.Join(", ", writers.Select(static writer => $"'{DistributionInfo.CommandName} {writer}'"));
        string next = operation == "review" && pdfWriter is not null
            ? $" Review the same content as a PDF instead: write it from the source document, as with '{DistributionInfo.CommandName} {pdfWriter} convert <document> --to pdf', "
                + $"then run '{DistributionInfo.CommandName} review' on that PDF."
            : " Use the source document, or the same content in a format a product reads, such as PDF.";
        return CliException.Create(
            ErrorCodes.FormatMismatch,
            $"No product reads '{Path.GetFileName(path)}': {DistributionInfo.CommandName} writes {extension} files but reads none.",
            hint: $"{extension} files are only written, by {listed}.{next}",
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
    /// it: it does not match the format its extension names, or no product recognizes it.
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
        string message = (declaredProduct, detectedProducts.Count) switch
        {
            (null, _) => $"The content of '{file}' does not show which product reads it.",
            (_, 0) => $"'{file}' has the {Path.GetExtension(path)} extension of a {declaredProduct} document, "
                + "but its content does not look like one.",
            _ => $"'{file}' has the {Path.GetExtension(path)} extension of a {declaredProduct} document, "
                + $"but its content looks like {LooksLike(detectedProducts)}.",
        };
        string hint = detectedProducts.Count == 0
            ? "Check what the file really is, or select its product explicitly: --product <id> where the command "
                + "offers it, or that product's commands. The product is never chosen from the extension alone."
            : declaredProduct is null
                ? $"The content looks like {LooksLike(detectedProducts)}. " + ProductChoiceHint(detectedProducts)
                : ProductChoiceHint(detectedProducts);
        return CliException.Create(
            ErrorCodes.FormatMismatch,
            message,
            hint: hint,
            details: new JsonObject
            {
                ["path"] = path,
                ["declared"] = declaredProduct,
                ["detected"] = detected,
                ["available"] = available,
            });
    }

    private static string LooksLike(IReadOnlyList<string> products) => products.Count == 1
        ? $"a {products[0]} document"
        : $"a document of one of: {string.Join(", ", products)}";

    private static string ProductChoiceHint(IReadOnlyList<string> products) => products.Count == 1
        ? $"Give the file its real extension, or select the product explicitly: --product {products[0]} where "
            + $"the command offers it, or the {products[0]} commands."
        : "Give the file its real extension, or select the product explicitly: --product <id> where the command "
            + "offers it, or that product's commands.";

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
            hint: "Select the intended product explicitly: --product <id> where the command offers it, or that "
                + "product's commands. The CLI does not guess between them.",
            details: new JsonObject
            {
                ["path"] = path,
                ["available"] = available,
            });
    }

    /// <summary>
    /// A value of an operation field that does not parse, such as an A1 reference, raised by a
    /// parser that operations and options share: <c>OPS_INVALID</c> with the reason in
    /// <c>details.reason</c>, which an operation's field check quotes. A command that parses an
    /// option with the same parser reports <see cref="OptionInvalid(string, string, string)"/> instead.
    /// </summary>
    /// <param name="message">The sentence that names the value and why it does not parse.</param>
    /// <param name="reason">Why it does not parse, without the value's field.</param>
    /// <param name="hint">The forms the value takes.</param>
    public static CliException OperationValueInvalid(string message, string reason, string hint) => CliException.Create(
        ErrorCodes.OpsInvalid,
        message,
        hint: hint,
        details: new JsonObject { ["reason"] = reason });

    /// <summary>
    /// An option or positional argument used wrongly. <paramref name="option"/> names an option
    /// such as <c>--pages</c>, stated in <c>details.option</c>, or an argument such as
    /// <c>file</c>, stated in <c>details.argument</c>.
    /// </summary>
    public static CliException OptionInvalid(string option, string reason, string hint) => CliException.Create(
        ErrorCodes.OptionInvalid,
        $"Invalid use of {option}: {reason}",
        hint: hint,
        details: new JsonObject { [ParameterKey(option)] = option });

    /// <summary>The details key that names a parameter: <c>option</c> for an option, <c>argument</c> for a positional argument.</summary>
    private static string ParameterKey(string parameter) => parameter.StartsWith('-') ? "option" : "argument";

    /// <summary>
    /// An <c>*-env</c> option names an environment variable that is not set or is empty. An
    /// operation's <c>*Env</c> field reports the same mistake with the same words.
    /// </summary>
    /// <param name="option">The option, such as <c>--password-env</c>.</param>
    /// <param name="variable">The variable it names.</param>
    /// <param name="standardInputOption">
    /// The option that reads the same secret from standard input, such as
    /// <c>--password-stdin</c>, which the hint offers as the other source; null when there is none.
    /// </param>
    public static CliException SecretMissing(string option, string variable, string? standardInputOption = null) => CliException.Create(
        ErrorCodes.OptionInvalid,
        $"Invalid use of {option}: {SecretMissingReason(variable)}",
        hint: standardInputOption is null
            ? SecretMissingHint(variable)
            : $"Set {variable} to the secret and run the command again, or pass the secret as the first line of standard input with {standardInputOption} instead of {option}.",
        details: new JsonObject { ["option"] = option, ["variable"] = variable });

    /// <summary>The reason of a <see cref="SecretMissing"/> mistake.</summary>
    internal static string SecretMissingReason(string variable) =>
        $"environment variable '{variable}' is not set or is empty";

    /// <summary>The hint of a <see cref="SecretMissing"/> mistake.</summary>
    internal static string SecretMissingHint(string variable) =>
        $"Set {variable} to the secret, then run the command again.";

    /// <summary>
    /// An option or argument value that names none of the values it accepts; the details list
    /// them and the closest ones, which the hint asks about before <paramref name="hint"/>, and
    /// the message ends with the same values.
    /// </summary>
    public static CliException OptionInvalid(string option, string reason, string hint, Mistake mistake)
    {
        ArgumentNullException.ThrowIfNull(mistake);
        var details = new JsonObject { [ParameterKey(option)] = option };
        mistake.WriteTo(details);
        string message = Sentence($"Invalid use of {option}: {reason}");
        return CliException.Create(
            ErrorCodes.OptionInvalid,
            mistake.Available.Count == 0 ? message : $"{message} {mistake.MustBeOneOf()}",
            mistake.Hint(hint),
            details);
    }

    /// <summary>Creates an error for an unavailable product capability.</summary>
    public static CliException FeatureUnsupported(
        string feature,
        string product,
        IReadOnlyList<string> available)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(feature);
        ArgumentException.ThrowIfNullOrWhiteSpace(product);
        ArgumentNullException.ThrowIfNull(available);
        JsonArray values = Strings(available);

        return CliException.Create(
            ErrorCodes.FeatureUnsupported,
            $"Product '{product}' does not support {feature}.",
            available.Count == 0
                ? "This distribution has no product that supports this feature."
                : $"Use a product that supports this feature: {string.Join(", ", available)}.",
            new JsonObject
            {
                ["feature"] = feature,
                ["product"] = product,
                ["available"] = values,
            });
    }

    /// <summary>
    /// Input content names a resource the CLI does not let the document engine read: a network
    /// address, script, a compressed resource it cannot check, or a file outside the input's
    /// directory. Nothing was written. <c>details.reason</c> is <c>resource-refused</c>.
    /// </summary>
    /// <param name="message">What the input names and why it is refused.</param>
    /// <param name="hint">How to make the resource local and safe.</param>
    public static CliException ResourceRefused(string message, string hint) => CliException.Create(
        ErrorCodes.FeatureUnsupported,
        message,
        hint: hint,
        details: new JsonObject { ["reason"] = "resource-refused" });

    /// <summary>
    /// Input content that the output cannot hold, such as a table larger than a slide table
    /// may be. The caller has to restructure the content. <c>details.reason</c> is
    /// <c>content-too-large</c>.
    /// </summary>
    /// <param name="message">What the content is and the limit it exceeds.</param>
    /// <param name="hint">How to restructure the content.</param>
    public static CliException ContentUnsupported(string message, string hint) => CliException.Create(
        ErrorCodes.FeatureUnsupported,
        message,
        hint: hint,
        details: new JsonObject { ["reason"] = "content-too-large" });

    /// <summary>
    /// Content the document engine would write wrong, a confirmed defect of the engine that
    /// <c>KNOWN-ISSUES.md</c> records, so the command refuses it before writing anything.
    /// <c>details.reason</c> is <c>engine-defect</c>.
    /// </summary>
    /// <param name="message">What the engine would write wrong, and for which content.</param>
    /// <param name="hint">How to get a correct output another way.</param>
    public static CliException EngineDefect(string message, string hint) => CliException.Create(
        ErrorCodes.FeatureUnsupported,
        message,
        hint: hint,
        details: new JsonObject { ["reason"] = "engine-defect" });

    /// <summary>
    /// The local environment a document engine reads, such as the font registry, holds
    /// something the engine cannot read, so every document would fail the same way.
    /// <c>details.reason</c> is <c>environment</c>.
    /// </summary>
    /// <param name="message">What in the environment the engine cannot read.</param>
    /// <param name="hint">How to repair the environment.</param>
    public static CliException EnvironmentUnsupported(string message, string hint) => CliException.Create(
        ErrorCodes.FeatureUnsupported,
        message,
        hint: hint,
        details: new JsonObject { ["reason"] = "environment" });

    public static CliException LicenseFileNotFound(string path, string source) => CliException.Create(
        ErrorCodes.LicenseFileNotFound,
        $"License file configured via {source} does not exist: {path}",
        hint: "Fix the license path, or remove the setting to run in evaluation mode.",
        details: LicenseFileDetails(path, source),
        docs: "licensing");

    public static CliException LicensePathIsDirectory(string path, string source) => CliException.Create(
        ErrorCodes.LicenseFileNotFound,
        $"License path configured via {source} is a directory, not a license file: {path}",
        hint: "Name the license file itself, such as Aspose.Total.lic, or remove the setting to run in evaluation mode.",
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
        hint: "Verify the file is a valid Aspose license, or remove the setting to run in evaluation mode.",
        details: new JsonObject { ["source"] = source, ["reason"] = reason },
        docs: "licensing",
        innerException: inner);

    /// <summary>
    /// A command refuses what evaluation mode would silently get wrong, such as an export the
    /// engine would write from another worksheet than the one asked for. The hint leads with the
    /// license that lifts the limit. A limit the engine itself raises is reported by the SDK
    /// alone, from its product's evaluation profile; no product translates an engine exception.
    /// </summary>
    /// <param name="message">What evaluation mode allows and what it stopped.</param>
    /// <param name="license">The license that lifts the limit, such as <c>Aspose.PDF</c>.</param>
    /// <param name="alternative">What the caller can do without one, completing "or ...", if anything.</param>
    /// <param name="details">The facts of the limit, if any.</param>
    public static CliException EvaluationLimit(
        string message,
        string license,
        string? alternative = null,
        JsonObject? details = null) => CliException.Create(
        ErrorCodes.EvaluationLimit,
        message,
        hint: alternative is null ? $"Apply an {license} license." : $"Apply an {license} license, or {alternative}.",
        details: details,
        docs: "licensing");

    /// <summary>
    /// The error for an evaluation limit the engine raised, which the SDK write pipeline alone
    /// builds from the facts its product's evaluation profile recognized in the engine's
    /// exception, so every product reports it with the same message, hint and details.
    /// </summary>
    /// <param name="engine">The engine, which also names the license, such as <c>Aspose.PDF</c>.</param>
    /// <param name="allows">What evaluation mode allows, completing "Aspose.X evaluation mode ...".</param>
    /// <param name="limit">The limit, in <paramref name="unit"/>.</param>
    /// <param name="unit">The unit of the limit, such as <c>pages</c>.</param>
    /// <param name="alternatives">What the caller can do without a license that works for this command, each completing "or ..."; empty when only a license does.</param>
    /// <param name="engineError">The engine's exception, kept as the inner exception.</param>
    internal static CliException EngineEvaluationLimit(
        string engine,
        string allows,
        int limit,
        string unit,
        IReadOnlyList<string> alternatives,
        Exception engineError) => CliException.Create(
        ErrorCodes.EvaluationLimit,
        $"{engine} evaluation mode {allows}, and this command needs more, so it stopped and wrote nothing.",
        hint: alternatives.Count == 0
            ? $"Apply an {engine} license."
            : $"Apply an {engine} license, or {string.Join(", or ", alternatives)}.",
        details: new JsonObject { ["limit"] = limit, ["unit"] = unit },
        docs: "licensing",
        innerException: engineError);

    /// <summary>
    /// The error for a named target the document does not contain. The details list the names
    /// that exist (at most <see cref="MaximumAvailableNames"/>) and the closest ones, so the
    /// caller can correct the request directly; the message ends with the same names.
    /// </summary>
    /// <param name="code">A code declared with <see cref="ErrorCode.NotFound"/>.</param>
    /// <param name="subject">What was looked up, e.g. <c>sheet</c> or <c>bookmark</c>.</param>
    /// <param name="requested">The name as the caller wrote it.</param>
    /// <param name="available">Every name of this kind in document order.</param>
    /// <param name="hint">A product-specific next step, after the question that names the closest names; by default that question or the available names.</param>
    public static CliException NotFound(
        ErrorCode code,
        string subject,
        string requested,
        IReadOnlyCollection<string> available,
        string? hint = null) => NotFoundIn(code, subject, requested, available, hint, listInMessage: true);

    /// <summary>
    /// The error for a text a search did not find, such as a heading or a find text. The texts
    /// searched are not names to choose from, so the message does not list them; the details
    /// state them and the closest ones as <see cref="NotFound"/> does.
    /// </summary>
    /// <param name="code">A code declared with <see cref="ErrorCode.NotFound"/>.</param>
    /// <param name="subject">What was searched for, e.g. <c>text</c> or <c>heading</c>.</param>
    /// <param name="requested">The text as the caller wrote it.</param>
    /// <param name="searched">The texts searched, in document order.</param>
    /// <param name="hint">A product-specific next step, after the question that names the closest texts.</param>
    public static CliException TextNotFound(
        ErrorCode code,
        string subject,
        string requested,
        IReadOnlyCollection<string> searched,
        string? hint = null) => NotFoundIn(code, subject, requested, searched, hint, listInMessage: false);

    private static CliException NotFoundIn(
        ErrorCode code,
        string subject,
        string requested,
        IReadOnlyCollection<string> available,
        string? hint,
        bool listInMessage)
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
        hint = hint is not null ? mistake.Hint(hint)
            : mistake.Question
                ?? (available.Count == 0 ? $"The document has no {subject} to select." : "Use one of the names in details.available.");
        string message = listInMessage && available.Count > 0
            ? $"No {subject} '{requested}' was found. {mistake.MustBeOneOf()}"
            : $"No {subject} '{requested}' was found.";
        return CliException.Create(code, message, hint, details);
    }

    /// <summary>
    /// The error for a number or range that reaches past the targets the document contains,
    /// such as page 12 of 10. Numbering starts at 1 unless the hint says otherwise.
    /// </summary>
    /// <param name="code">A code declared with <see cref="ErrorCode.NotFound"/>.</param>
    /// <param name="subject">What was looked up, e.g. <c>page</c> or <c>slide</c>.</param>
    /// <param name="requested">The number or range as the caller wrote it.</param>
    /// <param name="count">How many targets of this kind exist, numbered from 1.</param>
    /// <param name="hint">A product-specific next step; by default the valid numbers.</param>
    public static CliException NotFoundAt(
        ErrorCode code,
        string subject,
        string requested,
        int count,
        string? hint = null)
    {
        RequireNotFoundCode(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(requested);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        return CliException.Create(
            code,
            count == 1 ? $"No {subject} '{requested}' was found; 1 exists." : $"No {subject} '{requested}' was found; {count} exist.",
            hint ?? (count == 0 ? $"The document has no {subject} to select." : $"Use a {subject} from 1 through {count}."),
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
            hint: "This is a bug in the CLI. Please report it with the command you ran.",
            details: new JsonObject { ["diagnosticId"] = diagnosticId },
            innerException: exception);
    }
}
