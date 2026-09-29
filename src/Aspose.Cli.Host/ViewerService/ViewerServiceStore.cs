using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.Configuration;

namespace Aspose.Cli.Host.ViewerService;

/// <summary>
/// Where the current user's viewer service publishes itself: one public
/// marker with its address, and the secret companion that proves a control
/// request comes from the same user's tools. A marker whose process is gone
/// is removed on the next read, so a crash never blocks the next start.
/// </summary>
internal sealed class ViewerServiceStore
{
    private const string MarkerName = "service";
    private readonly string _directory;

    public static string DirectoryPath =>
        Path.Combine(ConfigurationPaths.EnsureUserDirectory(), "viewer");

    public ViewerServiceStore(string? directory = null) =>
        _directory = Directory.CreateDirectory(
            Path.GetFullPath(directory ?? DirectoryPath)).FullName;

    /// <summary>The running service, or null when none is.</summary>
    public ViewerServiceMarker? ReadLive()
    {
        ViewerServiceMarker? marker = Read();
        if (marker is null)
        {
            return null;
        }
        if (LocalServiceProcessIdentity.IsLive(marker.Version, marker.Pid, marker.StartTicksUtc, marker.Nonce))
        {
            return marker;
        }
        // Liveness was judged outside the lock; delete only the stale pair that was judged.
        Files().DeleteIf(current => current is null || IsSameService(current.Value, marker));
        return null;
    }

    /// <summary>
    /// The just-published marker of a service being started, without the
    /// liveness cleanup; the start supervisor validates it against its child.
    /// </summary>
    public ViewerServiceMarker? ReadCandidate() => Read();

    public void Write(ViewerServiceMarker marker)
    {
        ArgumentNullException.ThrowIfNull(marker);
        Files().Write(marker, new ViewerServiceSecrets(marker.Token));
    }

    /// <summary>Removes the marker, unless another service has published its own.</summary>
    public void DeleteIfOwned(string token) =>
        Files().DeleteIf(current => current is null
            || string.Equals(current.Value.Secrets.Token, token, StringComparison.Ordinal));

    private static bool IsSameService((ViewerServiceMarker Marker, ViewerServiceSecrets Secrets) stored, ViewerServiceMarker judged) =>
        string.Equals(stored.Secrets.Token, judged.Token, StringComparison.Ordinal)
        && string.Equals(stored.Marker.Nonce, judged.Nonce, StringComparison.Ordinal);

    private ViewerServiceMarker? Read()
    {
        try
        {
            (ViewerServiceMarker Marker, ViewerServiceSecrets Secrets)? stored = Files().Read();
            if (stored is null
                || string.IsNullOrWhiteSpace(stored.Value.Marker.Nonce)
                || string.IsNullOrWhiteSpace(stored.Value.Marker.Url)
                || string.IsNullOrWhiteSpace(stored.Value.Secrets.Token))
            {
                return null;
            }
            return stored.Value.Marker with { Token = stored.Value.Secrets.Token };
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    private LocalServiceMarkerFiles<ViewerServiceMarker, ViewerServiceSecrets> Files() =>
        new(
            Path.Combine(_directory, MarkerName + ".json"),
            Path.Combine(_directory, MarkerName + ".secret"),
            ViewerServiceJsonContext.Default.ViewerServiceMarker,
            ViewerServiceJsonContext.Default.ViewerServiceSecrets);
}
