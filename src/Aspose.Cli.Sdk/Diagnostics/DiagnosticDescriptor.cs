using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.Diagnostics;

/// <summary>The public severity of an immutable diagnostic declaration.</summary>
public enum DiagnosticSeverity
{
    /// <summary>The command fails and returns a non-zero exit code.</summary>
    Error,

    /// <summary>
    /// The command succeeds but discloses a non-fatal condition. A code in the
    /// <see cref="DiagnosticDescriptor.VerificationCategory"/> category instead reports a
    /// verification issue, which makes the verification <c>ok:false</c> and the command exit 8.
    /// </summary>
    Warning,
}

/// <summary>
/// Immutable metadata for one CLI diagnostic. Dynamic values remain in
/// product factories and never enter the catalog declaration.
/// </summary>
public sealed record DiagnosticDescriptor
{
    /// <summary>Category of every warning that is not a verification issue.</summary>
    public const string WarningCategory = "warning";

    /// <summary>Category of codes reported as <see cref="VerificationIssue"/> entries.</summary>
    public const string VerificationCategory = "verification";

    private DiagnosticDescriptor()
    {
    }

    /// <summary>SCREAMING_SNAKE_CASE public code.</summary>
    public required string Code { get; init; }

    /// <summary>
    /// Product id, <c>common</c> for the SDK's shared mechanisms, or <c>host</c>. A product declares
    /// its own without one; its definition's <c>Diagnostics</c> fills in the product id.
    /// </summary>
    public required string Owner { get; init; }

    /// <summary>Whether this is a fatal error or a non-fatal warning.</summary>
    public required DiagnosticSeverity Severity { get; init; }

    /// <summary>Error exit category; null for warnings.</summary>
    public ExitCode? ExitCode { get; init; }

    /// <summary>
    /// Category exposed by the capability catalog: an error's follows from its exit code, a
    /// warning's is <see cref="WarningCategory"/> or <see cref="VerificationCategory"/>.
    /// </summary>
    public required string Category { get; init; }

    /// <summary>Schema governing the dynamic details object.</summary>
    public string DetailsSchemaId { get; init; } = DiagnosticDetails.CatalogId;

    /// <summary>
    /// Whether only licensing produces this diagnostic, as its code declares; a build whose
    /// products need no license leaves it out of <c>capabilities</c>.
    /// </summary>
    public bool LicenseSurface { get; init; }

    /// <summary>Declares an error; its category follows from its exit code and its details schema is the code's own.</summary>
    /// <param name="code">The error code.</param>
    /// <param name="owner">The owner, or empty for a product's own.</param>
    public static DiagnosticDescriptor Error(ErrorCode code, string owner = "")
    {
        ArgumentNullException.ThrowIfNull(code);
        return new DiagnosticDescriptor
        {
            Code = code.Name,
            Owner = Normalized(owner),
            Severity = DiagnosticSeverity.Error,
            ExitCode = code.ExitCode,
            Category = CategoryOf(code.ExitCode),
            DetailsSchemaId = code.DetailsSchemaId,
            LicenseSurface = code.LicenseSurface,
        };
    }

    /// <summary>Declares a warning in the <see cref="WarningCategory"/> category.</summary>
    /// <param name="code">The warning code.</param>
    /// <param name="owner">The owner, or empty for a product's own.</param>
    public static DiagnosticDescriptor Warning(WarningCode code, string owner = "")
    {
        ArgumentNullException.ThrowIfNull(code);
        return new DiagnosticDescriptor
        {
            Code = code.Name,
            Owner = Normalized(owner),
            Severity = DiagnosticSeverity.Warning,
            Category = WarningCategory,
            LicenseSurface = code.LicenseSurface,
        };
    }

    /// <summary>Declares a verification issue in the <see cref="VerificationCategory"/> category.</summary>
    /// <param name="code">The issue code.</param>
    /// <param name="owner">The owner, or empty for a product's own.</param>
    public static DiagnosticDescriptor Verification(string code, string owner = "") =>
        new()
        {
            Code = code,
            Owner = Normalized(owner),
            Severity = DiagnosticSeverity.Warning,
            Category = VerificationCategory,
        };

    private static string Normalized(string owner) => owner?.Trim() ?? string.Empty;

    /// <summary>The category an error with <paramref name="exitCode"/> belongs to.</summary>
    private static string CategoryOf(ExitCode exitCode) =>
        exitCode switch
        {
            global::Aspose.Cli.Sdk.Errors.ExitCode.Internal => "internal",
            global::Aspose.Cli.Sdk.Errors.ExitCode.Usage => "usage",
            global::Aspose.Cli.Sdk.Errors.ExitCode.InputError => "input",
            global::Aspose.Cli.Sdk.Errors.ExitCode.ValidationError => "validation",
            global::Aspose.Cli.Sdk.Errors.ExitCode.OutputError => "output",
            global::Aspose.Cli.Sdk.Errors.ExitCode.FormatError => "format",
            global::Aspose.Cli.Sdk.Errors.ExitCode.LicenseError => "license",
            global::Aspose.Cli.Sdk.Errors.ExitCode.PartialFailure => "partial",
            global::Aspose.Cli.Sdk.Errors.ExitCode.OperationTimeout => "timeout",
            _ => "error",
        };
}
