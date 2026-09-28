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
    public static readonly ErrorCode FileNotFound = new("FILE_NOT_FOUND", ExitCode.InputError);

    /// <summary>The input file is opened exclusively by another process.</summary>
    public static readonly ErrorCode FileLocked = new("FILE_LOCKED", ExitCode.InputError);

    /// <summary>The input file could not be parsed as its declared document type.</summary>
    public static readonly ErrorCode FileCorrupt = new("FILE_CORRUPT", ExitCode.InputError);

    /// <summary>The input file is encrypted and no password was provided.</summary>
    public static readonly ErrorCode PasswordRequired = new("PASSWORD_REQUIRED", ExitCode.InputError);

    /// <summary>The provided password does not open the input file.</summary>
    public static readonly ErrorCode PasswordInvalid = new("PASSWORD_INVALID", ExitCode.InputError);

    /// <summary>The input file exceeds the configured size budget.</summary>
    public static readonly ErrorCode FileTooLarge = new("FILE_TOO_LARGE", ExitCode.InputError);

    /// <summary>A bounded input resource exceeded its versioned safety limit.</summary>
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
    /// which is reserved for CLI defects.
    /// </summary>
    public static readonly ErrorCode RenderFailed = new("RENDER_FAILED", ExitCode.ValidationError);

    /// <summary>An ops batch could not be parsed, validated or applied.</summary>
    public static readonly ErrorCode OpsInvalid = new("OPS_INVALID", ExitCode.ValidationError);

    public static readonly ErrorCode PageRangeInvalid = new("PAGE_RANGE_INVALID", ExitCode.ValidationError);
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
    /// <summary>The selected distribution or product does not use Aspose licensing.</summary>
    public static readonly ErrorCode LicenseNotApplicable =
        new("LICENSE_NOT_APPLICABLE", ExitCode.ValidationError);
    // -- Output problems (exit 5) --------------------------------------------

    /// <summary>The output file already exists and --overwrite was not given.</summary>
    public static readonly ErrorCode OutputExists = new("OUTPUT_EXISTS", ExitCode.OutputError);

    /// <summary>The output file could not be written.</summary>
    public static readonly ErrorCode OutputUnwritable = new("OUTPUT_UNWRITABLE", ExitCode.OutputError);

    /// <summary>The output changed after publication planning and was not overwritten.</summary>
    public static readonly ErrorCode OutputConflict = new("OUTPUT_CONFLICT", ExitCode.OutputError);

    /// <summary>A multi-file publication failed but verified recovery completed.</summary>
    public static readonly ErrorCode OutputPublicationFailed = new("OUTPUT_PUBLICATION_FAILED", ExitCode.OutputError);

    public static readonly ErrorCode ReleaseVerificationFailed =
        new("RELEASE_VERIFICATION_FAILED", ExitCode.OutputError);

    /// <summary>The release feed could not be read, so nothing was verified or installed; retrying later can succeed.</summary>
    public static readonly ErrorCode ReleaseFeedUnavailable =
        new("RELEASE_FEED_UNAVAILABLE", ExitCode.OutputError);

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

    /// <summary>The requested format id is unknown or not supported here.</summary>
    public static readonly ErrorCode FormatUnsupported = new("FORMAT_UNSUPPORTED", ExitCode.FormatError);
    /// <summary>A known extension disagrees with a different strong content match.</summary>
    public static readonly ErrorCode FormatMismatch = new("FORMAT_MISMATCH", ExitCode.FormatError);
    /// <summary>Multiple products strongly recognize the same unowned input.</summary>
    public static readonly ErrorCode FormatAmbiguous = new("FORMAT_AMBIGUOUS", ExitCode.FormatError);
    public static readonly ErrorCode FeatureUnsupported = new("FEATURE_UNSUPPORTED", ExitCode.FormatError);

    // -- License problems (exit 7) -------------------------------------------

    /// <summary>A license was configured but points to a missing file.</summary>
    public static readonly ErrorCode LicenseFileNotFound = new("LICENSE_FILE_NOT_FOUND", ExitCode.LicenseError);

    /// <summary>The configured license file was rejected by the engine.</summary>
    public static readonly ErrorCode LicenseInvalid = new("LICENSE_INVALID", ExitCode.LicenseError);

    /// <summary>An SDK evaluation restriction prevented the requested operation.</summary>
    public static readonly ErrorCode EvaluationLimit = new("EVALUATION_LIMIT", ExitCode.LicenseError);

    // -- Timeout (exit 9) ----------------------------------------------------

    /// <summary>The operation exceeded the configured <c>--timeout</c>.</summary>
    public static readonly ErrorCode OperationTimeout = new("OPERATION_TIMEOUT", ExitCode.OperationTimeout);

    // -- Partial publication (exit 8) ----------------------------------------

    /// <summary>A failed multi-file publication could not be fully recovered.</summary>
    public static readonly ErrorCode OutputPublicationPartial = new(
        "OUTPUT_PUBLICATION_PARTIAL",
        ExitCode.PartialFailure);

    /// <summary>A timed-out worker process tree could not be confirmed stopped.</summary>
    public static readonly ErrorCode WorkerTerminationFailed = new(
        "WORKER_TERMINATION_FAILED",
        ExitCode.PartialFailure);

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
        PageRangeInvalid,
        PageNotFound,
        BookmarkNotFound,
        StyleNotFound,
        ExtractBudgetExceeded,
        PreviewBudgetExceeded,
        UploadBudgetExceeded,
        LicenseNotApplicable,
        OutputExists,
        OutputUnwritable,
        OutputConflict,
        OutputPublicationFailed,
        ReleaseVerificationFailed,
        ReleaseFeedUnavailable,
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
        OutputPublicationPartial,
        WorkerTerminationFailed,
        Internal,
    ];
}
