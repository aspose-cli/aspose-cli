using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Diagnostics;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Host.Invocation;

/// <summary>The diagnostics only the host's own commands emit, listed in <c>capabilities</c>.</summary>
internal static class HostDiagnostics
{
    /// <summary>The owner of the host's diagnostics.</summary>
    internal const string Owner = "host";

    /// <summary>A release could not be verified, so nothing was installed.</summary>
    internal static readonly ErrorCode ReleaseVerificationFailedCode =
        new("RELEASE_VERIFICATION_FAILED", ExitCode.OutputError);

    /// <summary>The release feed could not be read, so nothing was verified or installed; retrying later can succeed.</summary>
    internal static readonly ErrorCode ReleaseFeedUnavailableCode =
        new("RELEASE_FEED_UNAVAILABLE", ExitCode.OutputError);

    /// <summary>The last update's installer failed or stopped before it reported a result.</summary>
    internal static readonly WarningCode UpdateFailed = new("UPDATE_FAILED");

    /// <summary>The last update's installer is still running.</summary>
    internal static readonly WarningCode UpdateInProgress = new("UPDATE_IN_PROGRESS");

    /// <summary>Every host diagnostic.</summary>
    internal static IReadOnlyList<DiagnosticDescriptor> All { get; } =
    [
        DiagnosticDescriptor.Error(ReleaseVerificationFailedCode, Owner),
        DiagnosticDescriptor.Error(ReleaseFeedUnavailableCode, Owner),
        DiagnosticDescriptor.Warning(UpdateFailed, Owner),
        DiagnosticDescriptor.Warning(UpdateInProgress, Owner),
    ];

    /// <summary>A release could not be verified, so nothing was installed.</summary>
    /// <param name="reason">Why the release failed verification.</param>
    internal static CliException ReleaseVerificationFailed(string reason) => new(
        ReleaseVerificationFailedCode,
        $"The release could not be verified: {reason}",
        hint: "Retry from the official release. Do not bypass the archive check.",
        details: new JsonObject { ["reason"] = reason });

    /// <summary>The release feed could not be read; nothing was verified or installed.</summary>
    /// <param name="feed">The feed, which carries no credentials, query or fragment.</param>
    /// <param name="reason">Why it could not be read.</param>
    internal static CliException ReleaseFeedUnavailable(Uri feed, string reason)
    {
        ArgumentNullException.ThrowIfNull(feed);
        return new CliException(
            ReleaseFeedUnavailableCode,
            $"The release feed could not be reached: {reason}",
            hint: "Check the network connection, proxy settings (HTTPS_PROXY) and the feed URL, then retry. A downloaded release directory also works as a local feed path.",
            details: new JsonObject
            {
                ["reason"] = reason,
                ["feed"] = feed.AbsoluteUri,
            });
    }
}
