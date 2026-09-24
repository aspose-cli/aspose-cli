namespace Aspose.Cli.Sdk.Errors;

/// <summary>
/// Coarse-grained process exit categories. Per-error distinctions are carried
/// by the error code in the envelope, not by additional process codes.
/// </summary>
public enum ExitCode
{
    /// <summary>The command completed successfully.</summary>
    Success = 0,

    /// <summary>An unexpected internal error (a bug in the CLI).</summary>
    Internal = 1,

    /// <summary>The command line could not be parsed or an option was misused.</summary>
    Usage = 2,

    /// <summary>The input file is missing, locked, corrupt or password-protected.</summary>
    InputError = 3,

    /// <summary>Domain validation failed (unknown part, invalid range, invalid ops).</summary>
    ValidationError = 4,

    /// <summary>The output file could not be produced (exists, unwritable).</summary>
    OutputError = 5,

    /// <summary>The requested format is unknown or unsupported for the operation.</summary>
    FormatError = 6,

    /// <summary>A license problem (invalid file, evaluation limit reached).</summary>
    LicenseError = 7,

    /// <summary>A batch completed partially under --best-effort.</summary>
    PartialFailure = 8,

    /// <summary>The operation exceeded the --timeout budget.</summary>
    OperationTimeout = 9,
}
