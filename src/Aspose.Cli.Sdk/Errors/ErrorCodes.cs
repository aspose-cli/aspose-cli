using System.Collections.Frozen;

namespace Aspose.Cli.Sdk.Errors;

/// <summary>
/// The complete common error code taxonomy of the CLI, grouped by exit-code
/// category.
/// </summary>
public static partial class ErrorCodes
{
    // -- Usage (exit 2) ------------------------------------------------------

    /// <summary>The command line could not be parsed.</summary>
    public static readonly ErrorCode UsageError = new("USAGE_ERROR", ExitCode.Usage);

    /// <summary>An option value is invalid or not applicable in this context.</summary>
    public static readonly ErrorCode OptionInvalid = new("OPTION_INVALID", ExitCode.Usage);

    // -- Input file problems (exit 3) ----------------------------------------

    /// <summary>The input file does not exist.</summary>
    public static readonly ErrorCode FileNotFound = ErrorCode.FileNotFound("FILE_NOT_FOUND", ExitCode.InputError);

    /// <summary>The input file is opened exclusively by another process.</summary>
    public static readonly ErrorCode FileLocked = new("FILE_LOCKED", ExitCode.InputError);

    /// <summary>The input file could not be parsed as its declared document type.</summary>
    public static readonly ErrorCode FileCorrupt = new("FILE_CORRUPT", ExitCode.InputError);

    /// <summary>The input file is encrypted and no password was provided.</summary>
    public static readonly ErrorCode PasswordRequired = new("PASSWORD_REQUIRED", ExitCode.InputError);

    /// <summary>The provided password does not open the input file.</summary>
    public static readonly ErrorCode PasswordInvalid = new("PASSWORD_INVALID", ExitCode.InputError);

    /// <summary>
    /// The input exceeds the <c>--max-input-bytes</c> budget, whether its size showed it before
    /// reading or its bytes did while they were read.
    /// </summary>
    public static readonly ErrorCode FileTooLarge = new("FILE_TOO_LARGE", ExitCode.InputError);

    /// <summary>
    /// A bounded resource of reading or processing the input, other than the input's bytes
    /// (<c>FILE_TOO_LARGE</c>), exceeded its safety limit.
    /// </summary>
    public static readonly ErrorCode InputBudgetExceeded =
        new("INPUT_BUDGET_EXCEEDED", ExitCode.InputError);

    /// <summary>An admitted input changed before or while it was consumed.</summary>
    public static readonly ErrorCode InputChanged =
        new("INPUT_CHANGED", ExitCode.InputError);

    /// <summary>Text input is not valid in the selected deterministic encoding.</summary>
    public static readonly ErrorCode InputEncodingInvalid =
        new("INPUT_ENCODING_INVALID", ExitCode.InputError);

    /// <summary>The input file exists but the process is not permitted to read it.</summary>
    public static readonly ErrorCode FileAccessDenied = new("FILE_ACCESS_DENIED", ExitCode.InputError);

    // -- Domain validation (exit 4) ------------------------------------------

    /// <summary>
    /// The image a render would produce is too large to rasterize. This is about
    /// output pixels, so a small selection at a high dpi can trip it while a big
    /// one at 96 does not.
    /// </summary>
    public static readonly ErrorCode RenderTooLarge = new("RENDER_TOO_LARGE", ExitCode.ValidationError);

    /// <summary>
    /// The engine failed while rasterizing selected content. The input can
    /// still be structurally valid; this remains distinct from INTERNAL,
    /// which is reserved for CLI defects. Content the engine cannot render is
    /// unsupported content, so it exits as a format error (6).
    /// </summary>
    public static readonly ErrorCode RenderFailed = new("RENDER_FAILED", ExitCode.FormatError);

    /// <summary>An ops batch could not be parsed, validated or applied.</summary>
    public static readonly ErrorCode OpsInvalid = new("OPS_INVALID", ExitCode.ValidationError);

    public static readonly ErrorCode PageNotFound = ErrorCode.NotFound("PAGE_NOT_FOUND");

    /// <summary>A bookmark named by the caller does not exist (documents with bookmarks share it).</summary>
    public static readonly ErrorCode BookmarkNotFound = ErrorCode.NotFound("BOOKMARK_NOT_FOUND");

    /// <summary>A named style does not exist in the document.</summary>
    public static readonly ErrorCode StyleNotFound = ErrorCode.NotFound("STYLE_NOT_FOUND");
    public static readonly ErrorCode ExtractBudgetExceeded = new("EXTRACT_BUDGET_EXCEEDED", ExitCode.ValidationError);
    public static readonly ErrorCode PreviewBudgetExceeded =
        new("PREVIEW_BUDGET_EXCEEDED", ExitCode.ValidationError);
    public static readonly ErrorCode UploadBudgetExceeded =
        new("UPLOAD_BUDGET_EXCEEDED", ExitCode.ValidationError);
    // -- Output problems (exit 5) --------------------------------------------

    /// <summary>The output file already exists and --overwrite was not given.</summary>
    public static readonly ErrorCode OutputExists = new("OUTPUT_EXISTS", ExitCode.OutputError);

    /// <summary>The output file could not be written.</summary>
    public static readonly ErrorCode OutputUnwritable = new("OUTPUT_UNWRITABLE", ExitCode.OutputError);

    /// <summary>The output changed after publication planning and was not overwritten.</summary>
    public static readonly ErrorCode OutputConflict = new("OUTPUT_CONFLICT", ExitCode.OutputError);

    /// <summary>A multi-file publication failed but verified recovery completed.</summary>
    public static readonly ErrorCode OutputPublicationFailed = new("OUTPUT_PUBLICATION_FAILED", ExitCode.OutputError);

    /// <summary>A failed multi-file publication could not be fully recovered.</summary>
    public static readonly ErrorCode OutputPublicationPartial = new("OUTPUT_PUBLICATION_PARTIAL", ExitCode.OutputError);

    /// <summary>
    /// What the command writes exceeded a safety limit of its output, such as its bytes or the
    /// number of files of one output set.
    /// </summary>
    public static readonly ErrorCode OutputBudgetExceeded = new("OUTPUT_BUDGET_EXCEEDED", ExitCode.OutputError);

    /// <summary>The requested live-preview port is already bound by another process.</summary>
    public static readonly ErrorCode LoopbackPortInUse = new("LOOPBACK_PORT_IN_USE", ExitCode.OutputError);

    /// <summary>The platform refused or could not create a loopback HTTP listener.</summary>
    public static readonly ErrorCode LoopbackListenerUnavailable = new("LOOPBACK_LISTENER_UNAVAILABLE", ExitCode.OutputError);

    /// <summary>
    /// The App, or the per-user service that hosts it and live previews, is
    /// transitioning or held by another instance and cannot take the request.
    /// </summary>
    public static readonly ErrorCode AppBusy = new("APP_BUSY", ExitCode.OutputError);

    // -- Format problems (exit 6) --------------------------------------------

    /// <summary>
    /// The requested format id is unknown or not supported here, or an input is in no format a
    /// product reads: its content matches none, or its format is one the CLI only writes.
    /// </summary>
    public static readonly ErrorCode FormatUnsupported = new("FORMAT_UNSUPPORTED", ExitCode.FormatError);
    /// <summary>
    /// The extension names one product's format and the content looks like another's. Content
    /// no product recognizes, or a format the CLI only writes, is <c>FORMAT_UNSUPPORTED</c>.
    /// </summary>
    public static readonly ErrorCode FormatMismatch = new("FORMAT_MISMATCH", ExitCode.FormatError);
    /// <summary>
    /// The input can be read more than one way and the caller must choose: several products
    /// strongly recognize it, or a text file writes numbers in a form that depends on its culture.
    /// </summary>
    public static readonly ErrorCode FormatAmbiguous = new("FORMAT_AMBIGUOUS", ExitCode.FormatError);
    public static readonly ErrorCode FeatureUnsupported = new("FEATURE_UNSUPPORTED", ExitCode.FormatError);

    // -- License problems (exit 7) -------------------------------------------

    /// <summary>A license was configured but points to a missing file.</summary>
    public static readonly ErrorCode LicenseFileNotFound = ErrorCode.FileNotFound("LICENSE_FILE_NOT_FOUND", ExitCode.LicenseError);

    /// <summary>The configured license file was rejected by the engine.</summary>
    public static readonly ErrorCode LicenseInvalid = new("LICENSE_INVALID", ExitCode.LicenseError);

    /// <summary>An SDK evaluation restriction prevented the requested operation.</summary>
    public static readonly ErrorCode EvaluationLimit = new("EVALUATION_LIMIT", ExitCode.LicenseError);

    // -- Timeout (exit 9) ----------------------------------------------------

    /// <summary>The operation exceeded the configured <c>--timeout</c>.</summary>
    public static readonly ErrorCode OperationTimeout = new("OPERATION_TIMEOUT", ExitCode.OperationTimeout);

    /// <summary>A timed-out worker process tree could not be confirmed stopped.</summary>
    public static readonly ErrorCode WorkerTerminationFailed = new("WORKER_TERMINATION_FAILED", ExitCode.OperationTimeout);

    // No error exits 8: it means only that a printed result did not pass.

    // -- Internal (exit 1) ----------------------------------------------------

    /// <summary>An unexpected internal error; a bug that should be reported.</summary>
    public static readonly ErrorCode Internal = new("INTERNAL_ERROR", ExitCode.Internal);

    /// <summary>
    /// Every error code in this build. Kept in sync with the fields above by a
    /// unit test, so a new code can never be forgotten here.
    /// </summary>
    public static IReadOnlyList<ErrorCode> All { get; } =
    [
        UsageError,
        OptionInvalid,
        FileNotFound,
        FileLocked,
        FileCorrupt,
        PasswordRequired,
        PasswordInvalid,
        FileTooLarge,
        InputBudgetExceeded,
        InputChanged,
        InputEncodingInvalid,
        FileAccessDenied,
        RenderTooLarge,
        RenderFailed,
        OpsInvalid,
        PageNotFound,
        BookmarkNotFound,
        StyleNotFound,
        ExtractBudgetExceeded,
        PreviewBudgetExceeded,
        UploadBudgetExceeded,
        OutputExists,
        OutputUnwritable,
        OutputConflict,
        OutputPublicationFailed,
        OutputPublicationPartial,
        OutputBudgetExceeded,
        LoopbackPortInUse,
        LoopbackListenerUnavailable,
        AppBusy,
        FormatUnsupported,
        FormatMismatch,
        FormatAmbiguous,
        FeatureUnsupported,
        LicenseFileNotFound,
        LicenseInvalid,
        EvaluationLimit,
        OperationTimeout,
        WorkerTerminationFailed,
        Internal,
    ];

    private static readonly FrozenSet<string> SharedNames =
        All.Select(static code => code.Name).ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// Whether <paramref name="code"/> is one of these shared codes, which only the SDK's
    /// factories build.
    /// </summary>
    internal static bool IsShared(ErrorCode code) => SharedNames.Contains(code.Name);
}
