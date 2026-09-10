namespace Aspose.Cli.Sdk.Extensibility;

/// <summary>
/// Immutable file metadata plus a cached, bounded prefix exposed to recognizers.
/// The host owns the underlying stream and never exposes a writable handle.
/// </summary>
public sealed class FileProbeSession
{
    private readonly ReadOnlyMemory<byte> _prefix;

    /// <summary>Creates a bounded recognition session.</summary>
    public FileProbeSession(
        string fullPath,
        long length,
        ReadOnlyMemory<byte> prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        FullPath = fullPath;
        FileName = Path.GetFileName(fullPath);
        Extension = Path.GetExtension(fullPath);
        Length = length;
        _prefix = prefix;
    }

    /// <summary>Absolute path supplied by the host.</summary>
    public string FullPath { get; }

    /// <summary>Leaf file name.</summary>
    public string FileName { get; }

    /// <summary>Original extension including the leading dot.</summary>
    public string Extension { get; }

    /// <summary>File length captured before probing.</summary>
    public long Length { get; }

    /// <summary>Cached prefix whose size is limited by the host probe budget.</summary>
    public ReadOnlyMemory<byte> Prefix => _prefix;
}
