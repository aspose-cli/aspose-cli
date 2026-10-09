using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.Diagnostics;

/// <summary>Diagnostics owned by genuinely cross-product SDK mechanisms.</summary>
public static class CommonDiagnostics
{
    /// <summary>Stable owner id for cross-product diagnostics.</summary>
    public const string Owner = "common";

    /// <summary>All common descriptors in deterministic code order.</summary>
    public static IReadOnlyList<DiagnosticDescriptor> All { get; } =
        ErrorCodes.All
            .Select(static code => DiagnosticDescriptor.Error(code, Owner))
            .Concat(WarningCodes.All.Select(static code => DiagnosticDescriptor.Warning(code, Owner)))
            .OrderBy(static descriptor => descriptor.Code, StringComparer.Ordinal)
            .ToArray();
}
