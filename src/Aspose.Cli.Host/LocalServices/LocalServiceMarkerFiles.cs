using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Host.LocalServices;

/// <summary>
/// Stores public discovery metadata and its secret companion as separate,
/// current-user-only atomic files.
/// </summary>
internal sealed class LocalServiceMarkerFiles<TMarker, TSecrets>
    where TMarker : class
    where TSecrets : class
{
    private readonly string _markerPath;
    private readonly string _secretPath;
    private readonly JsonTypeInfo<TMarker> _markerType;
    private readonly JsonTypeInfo<TSecrets> _secretsType;

    public LocalServiceMarkerFiles(
        string markerPath,
        string secretPath,
        JsonTypeInfo<TMarker> markerType,
        JsonTypeInfo<TSecrets> secretsType)
    {
        _markerPath = Path.GetFullPath(markerPath);
        _secretPath = Path.GetFullPath(secretPath);
        _markerType = markerType
            ?? throw new ArgumentNullException(nameof(markerType));
        _secretsType = secretsType
            ?? throw new ArgumentNullException(nameof(secretsType));
        PrivateUserStorage.EnsureDirectory(
            Path.GetDirectoryName(_markerPath)!);
        if (!string.Equals(
                Path.GetDirectoryName(_markerPath),
                Path.GetDirectoryName(_secretPath),
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A marker and its secrets must share one directory.",
                nameof(secretPath));
        }
    }

    public (TMarker Marker, TSecrets Secrets)? Read()
    {
        try
        {
            if (!File.Exists(_markerPath)
                || !File.Exists(_secretPath))
            {
                return null;
            }

            PrivateUserStorage.ValidateFile(_markerPath);
            PrivateUserStorage.ValidateFile(_secretPath);
            TMarker? marker = JsonSerializer.Deserialize(
                PrivateUserStorage.ReadAllText(_markerPath),
                _markerType);
            TSecrets? secrets = JsonSerializer.Deserialize(
                PrivateUserStorage.ReadAllText(_secretPath),
                _secretsType);
            return marker is null || secrets is null
                ? null
                : (marker, secrets);
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or JsonException
                or ArgumentException)
        {
            return null;
        }
    }

    public void Write(TMarker marker, TSecrets secrets)
    {
        ArgumentNullException.ThrowIfNull(marker);
        ArgumentNullException.ThrowIfNull(secrets);
        PrivateUserStorage.ValidateDirectory(
            Path.GetDirectoryName(_markerPath)!);
        PrivateUserStorage.WriteAllText(
            _secretPath,
            JsonSerializer.Serialize(
                secrets,
                _secretsType));
        PrivateUserStorage.WriteAllText(
            _markerPath,
            JsonSerializer.Serialize(
                marker,
                _markerType));
    }

    public void Delete()
    {
        LocalFileCleanup.DeleteFile(_markerPath);
        LocalFileCleanup.DeleteFile(_secretPath);
    }
}
