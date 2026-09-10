using System.Text.Json.Serialization;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Preview;

namespace Aspose.Cli.Host.Preview;

internal sealed record PreviewSessionMarker(
    string Id,
    [property: JsonIgnore]
    string Token,
    int Pid,
    long StartTicksUtc,
    int Port,
    string Url,
    string File,
    string View,
    string Product,
    ResultEnvelopeMetadata Metadata,
    string Nonce,
    int Version = 1,
    ProductPreviewPayload? Selector = null,
    string? FontProfileFingerprint = null);

internal sealed record PreviewSessionSecrets(
    string Token);

/// <summary>Atomic current-user discovery store for independent background previews.</summary>
internal sealed class PreviewSessionStore
{
    private readonly string _directory;

    public static string DirectoryPath => Path.Combine(Aspose.Cli.Sdk.Configuration.ConfigurationPaths.EnsureUserDirectory(), "previews");

    public PreviewSessionStore(string? directory = null)
    {
        _directory = PrivateUserStorage.EnsureDirectory(
            Path.GetFullPath(directory ?? DirectoryPath));
    }

    public IReadOnlyList<PreviewSessionMarker> ReadLive()
    {
        PrivateUserStorage.ValidateDirectory(_directory);

        var live = new List<PreviewSessionMarker>();
        foreach (string path in Directory.EnumerateFiles(_directory, "*.json").Order(StringComparer.Ordinal))
        {
            PreviewSessionMarker? marker = Read(path);
            if (marker is not null && IsValidId(marker.Id) && MatchesLiveProcess(marker))
            {
                live.Add(marker);
            }
            else
            {
                string id = Path.GetFileNameWithoutExtension(path);
                if (IsValidId(id))
                {
                    Files(id).Delete();
                }
                else
                {
                    LocalFileCleanup.DeleteFile(path);
                }
            }
        }

        return live;
    }

    /// <summary>
    /// Reads one newly-published ready candidate without applying stale
    /// cleanup. The startup supervisor validates it against its child handle.
    /// </summary>
    public PreviewSessionMarker? ReadReadyCandidate(string id) =>
        Read(MarkerPath(id));

    public void Write(PreviewSessionMarker marker)
    {
        ArgumentNullException.ThrowIfNull(marker);
        if (!IsValidId(marker.Id))
        {
            throw new ArgumentException("Preview session ids must contain 32 lowercase hexadecimal characters.", nameof(marker));
        }

        Files(marker.Id).Write(
            marker,
            new PreviewSessionSecrets(
                marker.Token));
    }

    public void DeleteIfOwned(string id, string token)
    {
        string path = MarkerPath(id);
        PreviewSessionMarker? current = Read(path);
        if (current is null || string.Equals(current.Token, token, StringComparison.Ordinal))
        {
            Files(id).Delete();
        }
    }

    private PreviewSessionMarker? Read(string path)
    {
        try
        {
            string id = Path.GetFileNameWithoutExtension(path);
            if (!IsValidId(id))
            {
                return null;
            }

            (PreviewSessionMarker Marker, PreviewSessionSecrets Secrets)?
                stored = Files(id).Read();
            if (stored is null
                || string.IsNullOrWhiteSpace(
                    stored.Value.Marker.Product)
                || stored.Value.Marker.Metadata is null
                || string.IsNullOrWhiteSpace(
                    stored.Value.Marker.Nonce)
                || string.IsNullOrWhiteSpace(
                    stored.Value.Secrets.Token))
            {
                return null;
            }

            return stored.Value.Marker with
            {
                Token = stored.Value.Secrets.Token,
            };
        }
        catch (Exception ex) when (
            ex is IOException
                or UnauthorizedAccessException
                or ArgumentException)
        {
            return null;
        }
    }

    private static bool MatchesLiveProcess(PreviewSessionMarker marker)
        => LocalServiceProcessIdentity.IsLive(
            marker.Version,
            marker.Pid,
            marker.StartTicksUtc,
            marker.Nonce);

    private string MarkerPath(string id)
    {
        if (!IsValidId(id))
        {
            throw new ArgumentException("Invalid preview session id.", nameof(id));
        }

        return Path.Combine(_directory, id + ".json");
    }

    public static bool IsValidId(string id) =>
        id.Length == 32 && id.All(static c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    private LocalServiceMarkerFiles<
        PreviewSessionMarker,
        PreviewSessionSecrets> Files(string id) =>
        new(
            MarkerPath(id),
            Path.Combine(_directory, id + ".secret"),
            PreviewLocalServiceJsonContext.Default.PreviewSessionMarker,
            PreviewLocalServiceJsonContext.Default.PreviewSessionSecrets);
}
