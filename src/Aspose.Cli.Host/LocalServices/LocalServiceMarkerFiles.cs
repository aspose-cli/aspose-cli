using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Aspose.Cli.Host.LocalServices;

/// <summary>
/// Stores public discovery metadata and its secret companion as separate atomic files in a
/// directory of the current user's configuration root, whose per-user permissions protect them.
/// The caller owns that directory. One resource lock keeps paired reads and writes coherent.
/// </summary>
internal sealed class LocalServiceMarkerFiles<TMarker, TSecrets>
    where TMarker : class
    where TSecrets : class
{
    private readonly string _markerPath;
    private readonly string _lockKey;
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
        string[] paths = [_markerPath, _secretPath];
        if (OperatingSystem.IsWindows()) { paths = paths.Select(static path => path.ToUpperInvariant()).ToArray(); }
        _lockKey = string.Join("\n", paths.Order(StringComparer.Ordinal));
        _markerType = markerType
            ?? throw new ArgumentNullException(nameof(markerType));
        _secretsType = secretsType
            ?? throw new ArgumentNullException(nameof(secretsType));
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
            using LocalServiceOperationLock lease = Acquire();
            return ReadHeld();
        }
        catch (TimeoutException)
        {
            return null;
        }
    }

    /// <summary>
    /// Deletes the pair only if the currently stored pair, re-read under the same lock,
    /// still satisfies <paramref name="predicate"/>. A caller that decided on an older read
    /// can therefore never delete a pair another process published in between.
    /// </summary>
    public void DeleteIf(Func<(TMarker Marker, TSecrets Secrets)?, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        using LocalServiceOperationLock lease = Acquire();
        if (predicate(ReadHeld()))
        {
            LocalFileCleanup.DeleteFile(_markerPath);
            LocalFileCleanup.DeleteFile(_secretPath);
        }
    }

    private (TMarker Marker, TSecrets Secrets)? ReadHeld()
    {
        try
        {
            if (!File.Exists(_markerPath)
                || !File.Exists(_secretPath))
            {
                return null;
            }

            TMarker? marker = JsonSerializer.Deserialize(
                File.ReadAllText(_markerPath, Encoding.UTF8),
                _markerType);
            TSecrets? secrets = JsonSerializer.Deserialize(
                File.ReadAllText(_secretPath, Encoding.UTF8),
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
        using LocalServiceOperationLock lease = Acquire();
        UserTextFile.Replace(
            _secretPath,
            JsonSerializer.Serialize(
                secrets,
                _secretsType));
        UserTextFile.Replace(
            _markerPath,
            JsonSerializer.Serialize(
                marker,
                _markerType));
    }

    private LocalServiceOperationLock Acquire() =>
        LocalServiceOperationLock.Acquire("marker-files", _lockKey, LocalServiceControlCodec.DefaultStageTimeout);
}
