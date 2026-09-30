using Aspose.Cli.Sdk.Diagnostics;

namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// A problem found while verifying an edited output that had already been produced.
/// Any issue makes the verification <c>ok:false</c> and the command exit 8.
/// </summary>
public sealed record VerificationIssue
{
    /// <summary>Stable SCREAMING_SNAKE_CASE identifier an agent can branch on.</summary>
    public required string Code { get; init; }

    /// <summary>Human-readable explanation.</summary>
    public required string Message { get; init; }

    /// <summary>Stable product-owned address of the affected item, when there is exactly one.</summary>
    public string? Location { get; init; }

    /// <summary>Recommended next action for the caller.</summary>
    public string? Hint { get; init; }

    /// <summary>
    /// Creates an issue whose code comes from a declared descriptor. Only descriptors in the
    /// <see cref="DiagnosticDescriptor.VerificationCategory"/> category are accepted, so every
    /// issue code is one listed in <c>capabilities</c>.
    /// </summary>
    /// <exception cref="ArgumentException">The descriptor is not a verification descriptor.</exception>
    public static VerificationIssue Of(
        DiagnosticDescriptor descriptor,
        string message,
        string? location = null,
        string? hint = null)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (descriptor.Category != DiagnosticDescriptor.VerificationCategory)
        {
            throw new ArgumentException(
                $"Diagnostic {descriptor.Code} has category '{descriptor.Category}', not '{DiagnosticDescriptor.VerificationCategory}'.",
                nameof(descriptor));
        }

        return new VerificationIssue
        {
            Code = descriptor.Code,
            Message = message,
            Location = location,
            Hint = hint,
        };
    }

    /// <summary>Carries a warning into verification, keeping its code, message, location and hint.</summary>
    public static VerificationIssue From(Warning warning)
    {
        ArgumentNullException.ThrowIfNull(warning);
        return new VerificationIssue
        {
            Code = warning.Code,
            Message = warning.Message,
            Location = warning.Location,
            Hint = warning.Hint,
        };
    }
}
