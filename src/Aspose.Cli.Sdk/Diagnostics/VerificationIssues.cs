using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Sdk.Diagnostics;

/// <summary>Creates verification issues whose codes come from declared diagnostics.</summary>
public static class VerificationIssues
{
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
}
