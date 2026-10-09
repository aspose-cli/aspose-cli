using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Diagnostics;

namespace Aspose.Cli.Host.Invocation;

/// <summary>The warning codes the host's own commands emit, listed in <c>capabilities</c>.</summary>
internal static class HostDiagnostics
{
    /// <summary>The owner of the host's diagnostics.</summary>
    internal const string Owner = "host";

    /// <summary>The last update's installer failed or stopped before it reported a result.</summary>
    internal static readonly WarningCode UpdateFailed = new("UPDATE_FAILED");

    /// <summary>The last update's installer is still running.</summary>
    internal static readonly WarningCode UpdateInProgress = new("UPDATE_IN_PROGRESS");

    /// <summary>Every host diagnostic.</summary>
    internal static IReadOnlyList<DiagnosticDescriptor> All { get; } =
    [
        DiagnosticDescriptor.Warning(UpdateFailed, Owner),
        DiagnosticDescriptor.Warning(UpdateInProgress, Owner),
    ];
}
