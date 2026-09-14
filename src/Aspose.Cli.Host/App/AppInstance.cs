using System.Text.Json.Serialization;
using Aspose.Cli.Host.LocalServices;

namespace Aspose.Cli.Host.App;

internal sealed record AppInstance(
    int Pid,
    long StartTicksUtc,
    int Port,
    [property: JsonIgnore]
    string Token,
    string Route,
    string? File,
    string Nonce,
    string? FontProfileFingerprint = null,
    int Version = 1,
    string? LicenseIdentity = null);

internal sealed record AppInstanceSecrets(
    string Token);

/// <summary>Atomic, process-identity-checked discovery for the local App instance.</summary>
internal sealed class AppInstanceStore
{
    private readonly object _gate = new();
    private readonly LocalServiceMarkerFiles<
        AppInstance,
        AppInstanceSecrets> _files;

    public AppInstanceStore(string path)
    {
        string markerPath = Path.GetFullPath(path);
        _files = new LocalServiceMarkerFiles<
            AppInstance,
            AppInstanceSecrets>(
            markerPath,
            markerPath + ".secret",
            AppLocalServiceJsonContext.Default.AppInstance,
            AppLocalServiceJsonContext.Default.AppInstanceSecrets);
    }

    public AppInstance? ReadLive()
    {
        lock (_gate)
        {
            AppInstance? marker = Read();
            if (marker is null || !MatchesLiveProcess(marker))
            {
                DeleteIfOwned(marker?.Token);
                return null;
            }

            return marker;
        }
    }

    /// <summary>
    /// Reads a just-published ready candidate without stale-state cleanup.
    /// Startup supervision owns the child handle and validates the candidate
    /// against that stronger identity before accepting it.
    /// </summary>
    public AppInstance? ReadReadyCandidate()
    {
        lock (_gate)
        {
            return Read();
        }
    }

    public void Write(AppInstance marker)
    {
        ArgumentNullException.ThrowIfNull(marker);
        lock (_gate)
        {
            _files.Write(
                marker,
                new AppInstanceSecrets(
                    marker.Token));
        }
    }

    public void DeleteIfOwned(string? token)
    {
        lock (_gate)
        {
            if (token is not null)
            {
                AppInstance? current = Read();
                if (current is not null && !string.Equals(current.Token, token, StringComparison.Ordinal))
                {
                    return;
                }
            }

            _files.Delete();
        }
    }

    private AppInstance? Read()
    {
        (AppInstance Marker, AppInstanceSecrets Secrets)? stored =
            _files.Read();
        if (stored is null
            || string.IsNullOrWhiteSpace(
                stored.Value.Secrets.Token)
            || string.IsNullOrWhiteSpace(
                stored.Value.Marker.Nonce))
        {
            return null;
        }

        return stored.Value.Marker with
        {
            Token = stored.Value.Secrets.Token,
        };
    }

    private static bool MatchesLiveProcess(AppInstance marker)
        => LocalServiceProcessIdentity.IsLive(
            marker.Version,
            marker.Pid,
            marker.StartTicksUtc,
            marker.Nonce);
}
