using System.Globalization;
using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.IO;

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
    /// unknown command, option or value, whose closest names the hint asks about first.
    /// </summary>
    public static CliException Usage(IReadOnlyList<string> problems, Mistake? mistake = null)
    {
        var details = new JsonObject { ["errors"] = Strings(problems) };
        mistake?.WriteTo(details);
        string next = $"Run the command again with --help for usage; '{DistributionInfo.CommandName} --help' lists the commands and '{DistributionInfo.CommandName} docs' the documentation topics.";
        return new CliException(ErrorCodes.UsageError, string.Join("; ", problems), mistake?.Hint(next) ?? next, details);
    }

    public static CliException FileNotFound(string path) => new(
        ErrorCodes.FileNotFound,
        $"Input file not found: {path}",
        hint: "Check the path. Relative paths resolve against --workdir (or the current directory).",
        details: new JsonObject { ["path"] = path });

    public static CliException FileLocked(string path) => new(
        ErrorCodes.FileLocked,
        $"File is in use by another process: {path}",
        hint: "Close the application holding this file, then retry.",
        details: new JsonObject { ["path"] = path });

    /// <summary>
    /// An encrypted file opened without a password. <paramref name="operationField"/> names the
    /// operation field that supplies the password of a file an operation reads; without it the
    /// hint names the command's password option.
    /// </summary>
    public static CliException PasswordRequired(string path, string? operationField = null) => new(
        ErrorCodes.PasswordRequired,
        $"File is encrypted and requires a password: {path}",
        hint: PasswordHint(ErrorCodes.PasswordRequired, operationField),
        details: new JsonObject { ["path"] = path });

    /// <summary>A password that does not open the file; <paramref name="operationField"/> as for <see cref="PasswordRequired"/>.</summary>
    public static CliException PasswordInvalid(string path, string? operationField = null) => new(
        ErrorCodes.PasswordInvalid,
        $"The provided password does not open the file: {path}",
        hint: PasswordHint(ErrorCodes.PasswordInvalid, operationField),
        details: new JsonObject { ["path"] = path });

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
    /// Restates a password error about one of the documents a command reads, naming that
    /// input in the details and its own password option in the hint.
    /// </summary>
    internal static CliException ForInput(CliException error, string input, string passwordEnvironmentOption)
    {
        string path = error.Details!["path"]!.GetValue<string>();
        return new CliException(
            error.Code,
            $"{error.Message} (the {input} input)",
            hint: PasswordHint(error.Code, operationField: null, passwordEnvironmentOption),
            details: new JsonObject { ["path"] = path, ["input"] = input });
    }

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
                : $"{lead}retry with --password-env <NAME>; a command with several inputs names the option "
                    + "per input, such as --left-password-env.";
    }

    public static CliException FileAccessDenied(string path) => new(
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
        Exception? innerException = null) => new(
        ErrorCodes.FileCorrupt,
        $"Input is not a valid {document}: {path} ({reason}).",
        hint: hint,
        details: new JsonObject { ["path"] = Path.GetFullPath(path), ["reason"] = reason },
        innerException: innerException);

    public static CliException FileTooLarge(long sizeBytes, long limitBytes) => new(
        ErrorCodes.FileTooLarge,
        $"Input file is {sizeBytes} bytes, exceeding the {limitBytes}-byte budget.",
        hint: "Raise --max-input-bytes within its advertised safety maximum, or split the input into smaller files.",
        details: new JsonObject
        {
            ["resource"] = ResourceBudgetKinds.InputBytes,
            ["observed"] = sizeBytes,
            ["limit"] = limitBytes,
            ["unit"] = "bytes",
            ["phase"] = "pre-runtime-metadata",
            ["sizeBytes"] = sizeBytes,
            ["limitBytes"] = limitBytes,
        });

    public static CliException InputBudgetExceeded(
        string resource,
        long observed,
        long limit,
        string unit,
        string phase) => new(
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
            ["observed"] = observed,
            ["limit"] = limit,
            ["unit"] = unit,
            ["phase"] = phase,
        });

    public static CliException InputChanged(
        string path,
        long expectedBytes,
        long observedBytes) => new(
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
        string observedSha256) => new(
        ErrorCodes.InputChanged,
        $"Input no longer matches the requested fingerprint: {path}",
        hint: "Inspect the current file, review intervening changes, and retry with its new source.fingerprint.sha256 value.",
        details: new JsonObject
        {
            ["path"] = path,
            ["expectedSha256"] = expectedSha256,
            ["observedSha256"] = observedSha256,
        });

    public static CliException InputEncodingInvalid(
        string phase,
        Exception? inner = null) => new(
        ErrorCodes.InputEncodingInvalid,
        "Text input is not valid UTF-8.",
        hint: "Save or pipe the text as UTF-8 (a UTF-8 BOM is accepted), then retry.",
        details: new JsonObject
        {
            ["resource"] = ResourceBudgetKinds.DecodedTextCharacters,
            ["phase"] = phase,
        },
        innerException: inner);

    public static CliException PreviewBudgetExceeded(
        string resource,
        long actual,
        long limit) => new(
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
        long limit) => new(
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
        return new CliException(
            ErrorCodes.RenderTooLarge,
            $"Rendering{resolution} needs a {width}x{height} pixel image ({megabytes} MB), which exceeds the limit of {maxPixels} pixels per image.",
            hint: hint,
            details: new JsonObject
            {
                ["width"] = width,
                ["height"] = height,
                ["dpi"] = dpi,
                ["maxPixels"] = maxPixels,
            });
    }

    public static CliException OperationTimeout(int seconds) => new(
        ErrorCodes.OperationTimeout,
        $"The operation did not finish within the {seconds}s timeout.",
        hint: "Raise --timeout, narrow the work (a smaller range or fewer ops), or split it into steps.",
        details: new JsonObject { ["timeoutSeconds"] = seconds });

    public static CliException OperationTimeout(int seconds, string phase) => new(
        ErrorCodes.OperationTimeout,
        $"The operation did not finish within the {seconds}s timeout.",
        hint: "Raise --timeout, narrow the work (a smaller range or fewer ops), or split it into steps.",
        details: new JsonObject
        {
            ["timeoutSeconds"] = seconds,
            ["phase"] = phase,
        });

    public static CliException WorkerTerminationFailed(int processId) => new(
        ErrorCodes.WorkerTerminationFailed,
        "The operation deadline expired, but the worker process tree could not be confirmed stopped.",
        hint: "Stop the reported process before retrying or accessing the same output files.",
        details: new JsonObject
        {
            ["pid"] = processId,
            ["quiescenceConfirmed"] = false,
        });

    /// <summary>
    /// The one wording of a failure inside a product's document engine. The engine usually
    /// rejects a feature of this document, but when every document fails the same way the
    /// local environment it reads (fonts, imaging libraries) is the cause, so the hint names both.
    /// </summary>
    /// <param name="message">What failed, naming the product or the operation.</param>
    /// <param name="innerException">The engine failure.</param>
    /// <param name="details">Position details, such as the failing operation.</param>
    public static CliException EngineFailed(string message, Exception innerException, JsonObject? details = null) => new(
        ErrorCodes.FeatureUnsupported,
        message,
        // A file the engine could not open says nothing about the document's features; truncated
        // or malformed data (EndOfStreamException, other IOExceptions) does.
        hint: IsFileAccessFailure(innerException) || IsFileAccessFailure(innerException.InnerException)
            ? "The engine could not open a file the message names: close any program that holds it, check that it "
                + "can be read, and run the command again."
            : "The engine may not support a feature this document uses: simplify or remove that content or operation, "
                + "or retry with a standard copy of the document or another output format. If other documents fail the "
                + "same way, the local environment (for example its installed fonts) is the cause, not the document.",
        details: details,
        innerException: innerException);

    private static bool IsFileAccessFailure(Exception? exception) => exception is UnauthorizedAccessException
        or FileNotFoundException
        or DirectoryNotFoundException
        || exception is IOException io && FileAccessProbe.IsSharingViolation(io);

    public static CliException OutputExists(string path) => new(
        ErrorCodes.OutputExists,
        $"Output file already exists: {path}",
        hint: "Pass --overwrite to replace it, or choose a different path with --out.",
        details: new JsonObject { ["path"] = path });

    /// <summary>
    /// An explicit output resolves to an input document. Replacing the input is the in-place
    /// mode's job, which alone carries its backup and fingerprint precondition.
    /// </summary>
    internal static CliException OutputIsInput(string parameter, string path, bool inPlaceAvailable) => new(
        ErrorCodes.OptionInvalid,
        $"Invalid use of {parameter}: the output resolves to an input file: {path}",
        hint: inPlaceAvailable
            ? $"Pass --in-place instead of {parameter} to modify the input, with --backup or --if-match as needed; or choose another output path."
            : "Choose another output path; this command never replaces its input.",
        details: new JsonObject { ["option"] = parameter, ["path"] = path });

    /// <summary>Two outputs of one operation, including a backup, resolve to the same path.</summary>
    internal static CliException DuplicateOutput(string path) => new(
        ErrorCodes.UsageError,
        $"Two outputs of this operation resolve to the same path: {path}",
        hint: "Give every output, including a backup, its own path.",
        details: new JsonObject { ["path"] = path });

    /// <summary>
    /// The engine wrote a directory beside an output published with its companion files. Only
    /// files are published with an output, so the set is refused and nothing is published.
    /// </summary>
    internal static CliException CompanionDirectoryUnpublished(string path, string directory) => new(
        ErrorCodes.OutputPublicationFailed,
        $"The output set could not be published: the engine wrote the directory '{directory}' beside {path}, and only companion files are published with an output.",
        hint: "Nothing was written. Convert to another format that keeps its content in one file, and report the format that wrote a directory.",
        details: new JsonObject { ["path"] = path, ["directory"] = directory });

    public static CliException OutputUnwritable(
        string path,
        string reason,
        Exception? inner = null,
        string phase = "prepare") => new(
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

    internal static CliException OutputConflict(
        string path,
        FilePublicationSnapshot expected,
        FilePublicationSnapshot? actual,
        Exception? inner = null) => new(
        ErrorCodes.OutputConflict,
        $"Output changed while the operation was preparing to publish it: {path}",
        hint: "Inspect the newer file, then retry from that version or choose a different output path.",
        details: new JsonObject
        {
            ["path"] = path,
            ["expectedExists"] = expected.Exists,
            ["expectedSizeBytes"] = expected.Exists ? expected.Length : null,
            ["expectedSha256"] = expected.Sha256,
            ["actualExists"] = actual?.Exists,
            ["actualSizeBytes"] = actual is { Exists: true } ? actual.Length : null,
            ["actualSha256"] = actual?.Sha256,
        },
        innerException: inner);

    internal static CliException OutputPublicationFailure(
        Exception commitFailure,
        PublicationRecoveryReport recovery)
    {
        ArgumentNullException.ThrowIfNull(commitFailure);
        ArgumentNullException.ThrowIfNull(recovery);
        var items = new JsonArray();
        foreach (PublicationRecoveryItem item in recovery.Items)
        {
            items.Add(new JsonObject
            {
                ["target"] = item.Target,
                ["originalExisted"] = item.OriginalExisted,
                ["published"] = item.Published,
                ["status"] = item.Status,
                ["contentVerified"] = item.ContentVerified,
                ["metadataVerified"] = item.MetadataVerified,
                ["failure"] = item.Failure,
            });
        }

        string originalCode = commitFailure is CliException cli
            ? cli.Code.Name
            : ErrorCodes.OutputUnwritable.Name;
        var details = new JsonObject
        {
            ["originalError"] = new JsonObject
            {
                ["code"] = originalCode,
                ["details"] = commitFailure is CliException source && source.Details is not null
                    ? source.Details.DeepClone()
                    : null,
            },
            ["recoveryComplete"] = recovery.RecoveryComplete,
            ["targets"] = items,
        };
        if (recovery.RecoveryComplete
            && commitFailure is CliException original)
        {
            JsonObject originalDetails = original.Details?.DeepClone().AsObject() ?? [];
            originalDetails["recoveryComplete"] = true;
            originalDetails["targets"] = items.DeepClone();
            return new CliException(
                original.Code,
                original.Message,
                hint: original.Hint,
                details: originalDetails,
                docs: original.Docs,
                innerException: original);
        }

        return new CliException(
            recovery.RecoveryComplete
                ? ErrorCodes.OutputPublicationFailed
                : ErrorCodes.OutputPublicationPartial,
            recovery.RecoveryComplete
                ? "The output set could not be published; every target was restored and verified."
                : "The output set could not be published and recovery left one or more targets in an unknown or mixed state.",
            hint: recovery.RecoveryComplete
                ? "Correct the original output error and retry."
                : "Stop modifying the listed targets, inspect details.targets, and restore unknown targets from a trusted backup before retrying.",
            details: details,
            innerException: commitFailure);
    }

    public static CliException LoopbackPortInUse(int port) => new(
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
        Exception innerException) => new(
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

    public static CliException FormatUnsupported(string requested, IReadOnlyList<string> supported)
    {
        JsonArray ids = Strings(supported);

        return new CliException(
            ErrorCodes.FormatUnsupported,
            $"Unsupported format '{requested}'. Supported formats: {string.Join(", ", supported)}",
            hint: $"Use one of the supported format ids, or run '{DistributionInfo.CommandName} capabilities' to list everything this build supports.",
            details: new JsonObject { ["requested"] = requested, ["supported"] = ids });
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
    /// <param name="extensions">The extensions the output may have.</param>
    /// <param name="hint">How to fix the command line.</param>
    /// <param name="facts">The format or formats that decided <paramref name="extensions"/>.</param>
    internal static CliException OutputExtensionInvalid(
        string parameter,
        string output,
        string subject,
        string problem,
        IReadOnlyList<string> extensions,
        string hint,
        JsonObject facts)
    {
        facts["option"] = parameter;
        facts["extension"] = Path.GetExtension(output);
        facts["extensions"] = Strings(extensions);
        return new CliException(
            ErrorCodes.UsageError,
            $"{subject} {problem}; use {string.Join(", ", extensions)}.",
            hint,
            details: facts);
    }

    /// <summary>Creates an extension/content ownership conflict error.</summary>
    public static CliException FormatMismatch(
        string path,
        string declaredProduct,
        IReadOnlyList<string> detectedProducts)
    {
        JsonArray detected = Strings(detectedProducts);

        return new CliException(
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
        string message = declaredProduct is null
            ? $"The content of '{file}' does not show which product reads it."
            : $"'{file}' has the {Path.GetExtension(path)} extension of a {declaredProduct} document, "
                + "but its content does not look like one.";
        string hint = detectedProducts.Count == 0
            ? "Check what the file really is, or run the intended product's command on it explicitly. "
                + "The product is never chosen from the extension alone."
            : $"The content looks like {LooksLike(detectedProducts)}. " + ProductChoiceHint(detectedProducts);
        return new CliException(
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

        return new CliException(
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

    public static CliException OptionInvalid(string option, string reason, string hint) => new(
        ErrorCodes.OptionInvalid,
        $"Invalid use of {option}: {reason}",
        hint: hint,
        details: new JsonObject { ["option"] = option });

    /// <summary>
    /// An option or argument value that names none of the values it accepts; the details list
    /// them and the closest ones, which the hint asks about before <paramref name="hint"/>.
    /// </summary>
    public static CliException OptionInvalid(string option, string reason, string hint, Mistake mistake)
    {
        ArgumentNullException.ThrowIfNull(mistake);
        var details = new JsonObject { ["option"] = option };
        mistake.WriteTo(details);
        return new CliException(ErrorCodes.OptionInvalid, $"Invalid use of {option}: {reason}", mistake.Hint(hint), details);
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

        return new CliException(
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

    public static CliException LicenseFileNotFound(string path, string source) => new(
        ErrorCodes.LicenseFileNotFound,
        $"License file configured via {source} does not exist: {path}",
        hint: "Fix the license path, or remove the setting to run in evaluation mode.",
        details: new JsonObject { ["path"] = path, ["source"] = source },
        docs: "licensing");

    public static CliException LicensePathIsDirectory(string path, string source) => new(
        ErrorCodes.LicenseFileNotFound,
        $"License path configured via {source} is a directory, not a license file: {path}",
        hint: "Name the license file itself, such as Aspose.Total.lic, or remove the setting to run in evaluation mode.",
        details: new JsonObject { ["path"] = path, ["source"] = source },
        docs: "licensing");

    public static CliException LicenseInvalid(string source, string reason, Exception? inner = null) => new(
        ErrorCodes.LicenseInvalid,
        $"The license configured via {source} was rejected: {reason}",
        hint: "Verify the file is a valid Aspose license, or remove the setting to run in evaluation mode.",
        details: new JsonObject { ["source"] = source, ["reason"] = reason },
        docs: "licensing",
        innerException: inner);

    /// <summary>Creates an error for a license operation with no applicable product.</summary>
    public static CliException LicenseNotApplicable(
        string operation,
        string? product,
        IReadOnlyList<string> available)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentNullException.ThrowIfNull(available);
        JsonArray values = Strings(available);

        string subject = product is null
            ? "this distribution"
            : $"product '{product}'";
        string hint = available.Count == 0
            ? "Install Aspose CLI to configure an Aspose license."
            : $"Choose a product that uses Aspose licensing: {string.Join(", ", available)}.";
        return new CliException(
            ErrorCodes.LicenseNotApplicable,
            $"License {operation} is not applicable to {subject}.",
            hint,
            new JsonObject
            {
                ["operation"] = operation,
                ["product"] = product,
                ["available"] = values,
            },
            docs: "licensing");
    }

    /// <summary>
    /// The error for a named target the document does not contain. The details list the names
    /// that exist (at most <see cref="MaximumAvailableNames"/>) and the closest ones, so the
    /// caller can correct the request directly.
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
        string? hint = null)
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
        return new CliException(code, $"No {subject} '{requested}' was found.", hint, details);
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

        return new CliException(
            code,
            $"No {subject} '{requested}' was found; {count} exist.",
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
        if (code.DetailsSchemaId != CommonSchemaIds.NotFoundDetails)
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
        return new CliException(
            ErrorCodes.Internal,
            "Unexpected internal error.",
            hint: "This is a bug in the CLI. Please report it with the command you ran.",
            details: new JsonObject { ["diagnosticId"] = diagnosticId },
            innerException: exception);
    }
}
