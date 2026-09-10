using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.Diagnostics;

/// <summary>The public severity of an immutable diagnostic declaration.</summary>
public enum DiagnosticSeverity
{
    /// <summary>The command fails and returns a non-zero exit code.</summary>
    Error,

    /// <summary>The command succeeds but discloses a non-fatal condition.</summary>
    Warning,
}

/// <summary>
/// Immutable metadata for one CLI diagnostic. Dynamic values remain in
/// product factories and never enter the catalog declaration.
/// </summary>
public sealed record DiagnosticDescriptor
{
    /// <summary>SCREAMING_SNAKE_CASE public code.</summary>
    public required string Code { get; init; }

    /// <summary>Product id, or <c>common</c> for shared infrastructure.</summary>
    public required string Owner { get; init; }

    /// <summary>Whether this is a fatal error or a non-fatal warning.</summary>
    public required DiagnosticSeverity Severity { get; init; }

    /// <summary>Error exit category; null for warnings.</summary>
    public ExitCode? ExitCode { get; init; }

    /// <summary>Category exposed by the current capability catalog.</summary>
    public required string Category { get; init; }

    /// <summary>Identity of the product-owned message template.</summary>
    public required string MessageTemplateId { get; init; }

    /// <summary>Identity of the product-owned recovery template.</summary>
    public required string HintTemplateId { get; init; }

    /// <summary>Schema governing the dynamic details object.</summary>
    public string DetailsSchemaId { get; init; } = CommonSchemaIds.DiagnosticDetails;

    /// <summary>Declares an immutable error descriptor.</summary>
    public static DiagnosticDescriptor Error(
        ErrorCode code,
        string owner,
        string category,
        string detailsSchemaId = CommonSchemaIds.DiagnosticDetails)
    {
        ArgumentNullException.ThrowIfNull(code);
        return Create(
            code.Name,
            owner,
            DiagnosticSeverity.Error,
            code.ExitCode,
            category,
            detailsSchemaId);
    }

    /// <summary>Declares an immutable warning descriptor.</summary>
    public static DiagnosticDescriptor Warning(
        string code,
        string owner,
        string category,
        string detailsSchemaId = CommonSchemaIds.DiagnosticDetails) =>
        Create(
            code,
            owner,
            DiagnosticSeverity.Warning,
            null,
            category,
            detailsSchemaId);

    private static DiagnosticDescriptor Create(
        string code,
        string owner,
        DiagnosticSeverity severity,
        ExitCode? exitCode,
        string category,
        string detailsSchemaId)
    {
        string normalizedOwner = owner?.Trim() ?? string.Empty;
        string templateStem =
            $"{normalizedOwner}.{code.ToLowerInvariant().Replace('_', '-')}";
        return new DiagnosticDescriptor
        {
            Code = code,
            Owner = normalizedOwner,
            Severity = severity,
            ExitCode = exitCode,
            Category = category,
            MessageTemplateId = templateStem + ".message.v1",
            HintTemplateId = templateStem + ".hint.v1",
            DetailsSchemaId = detailsSchemaId,
        };
    }
}
