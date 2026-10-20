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
    /// <param name="reason">Why the release failed verification, completing the message.</param>
    internal static CliException ReleaseVerificationFailed(string reason) => new(
        ReleaseVerificationFailedCode,
        $"The release could not be verified: {reason}",
        hint: "Retry from the official release. Do not bypass the archive check.");

    /// <summary>The release feed answered with a status other than success.</summary>
    /// <param name="feed">The feed, which carries no credentials, query or fragment.</param>
    /// <param name="status">The HTTP status it answered.</param>
    internal static CliException ReleaseFeedAnswered(Uri feed, System.Net.HttpStatusCode status) =>
        status == System.Net.HttpStatusCode.NotFound
            ? FeedUnavailable(
                feed,
                $"http-{(int)status}",
                $"the feed answered HTTP {(int)status}",
                "The server holds no release at this address: none has been published there yet, or the URL is wrong. "
                + "Pass the URL of a release's RELEASE-MANIFEST.json, or a downloaded release directory, as the feed.")
            : FeedUnavailable(feed, $"http-{(int)status}", $"the feed answered HTTP {(int)status}", hint: null);

    /// <summary>
    /// The release feed could not be reached over the network; nothing was verified or installed.
    /// <c>details.reason</c> is <c>unreachable</c>.
    /// </summary>
    /// <param name="feed">The feed, which carries no credentials, query or fragment.</param>
    /// <param name="reason">Why it could not be reached, completing the message.</param>
    internal static CliException ReleaseFeedUnavailable(Uri feed, string reason) =>
        FeedUnavailable(feed, "unreachable", reason, hint: null);

    /// <summary>The release feed could not be read, for the reason <paramref name="token"/> names.</summary>
    /// <param name="feed">The feed.</param>
    /// <param name="token">The kebab-case <c>details.reason</c>, such as <c>http-503</c>.</param>
    /// <param name="reason">Why it could not be read, completing the message.</param>
    /// <param name="hint">What to do, when the network is not the cause.</param>
    private static CliException FeedUnavailable(Uri feed, string token, string reason, string? hint)
    {
        ArgumentNullException.ThrowIfNull(feed);
        return new CliException(
            ReleaseFeedUnavailableCode,
            $"The release feed could not be reached: {reason}",
            hint: hint ?? "Check the network connection, proxy settings (HTTPS_PROXY) and the feed URL, then retry. A downloaded release directory also works as a local feed path.",
            details: new JsonObject
            {
                ["reason"] = token,
                ["feed"] = feed.AbsoluteUri,
            });
    }

    /// <summary>A local feed path names no file; nothing was verified or installed.</summary>
    /// <param name="path">The absolute path of the manifest the feed names.</param>
    internal static CliException ReleaseFeedNotFound(string path) => new(
        ReleaseFeedUnavailableCode,
        $"The release feed could not be read: no file exists at '{path}'.",
        hint: "Pass a downloaded release directory, or the RELEASE-MANIFEST.json in it, as the feed.",
        details: new JsonObject
        {
            ["reason"] = "file-not-found",
            ["feed"] = path,
        });
}
