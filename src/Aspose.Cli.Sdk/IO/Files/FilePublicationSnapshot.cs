using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;

namespace Aspose.Cli.Sdk.IO.Files;

/// <summary>
/// The file attributes a publication restores with a file's content. Access control is the
/// file system's: a replaced file keeps its own DACL and a new file inherits from its folder.
/// </summary>
internal sealed record FilePublicationMetadata(FileAttributes Attributes)
{
    public static FilePublicationMetadata Capture(SafeFileHandle handle) =>
        new(File.GetAttributes(handle));

    public void Apply(string path) =>
        File.SetAttributes(path, Attributes & ~FileAttributes.Directory & ~FileAttributes.ReparsePoint);

    public bool Matches(string path)
    {
        try
        {
            return Attributes == File.GetAttributes(path);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

internal sealed record FilePublicationSnapshot(
    bool Exists,
    long Length,
    long LastWriteUtcTicks,
    string? Sha256,
    FilePublicationMetadata? Metadata,
    FilePhysicalIdentity? PhysicalIdentity)
{
    public static FilePublicationSnapshot Missing { get; } =
        new(false, 0, 0, null, null, null);

    public static FilePublicationSnapshot Capture(string path)
    {
        if (!File.Exists(path))
        {
            return Missing;
        }

        try
        {
            // Everything is read from this one handle, so another process may still replace
            // or delete the path meanwhile, as a concurrent in-place edit does.
            using SafeFileHandle handle = File.OpenHandle(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read | FileShare.Delete,
                FileOptions.SequentialScan);
            FilePhysicalIdentity? identity =
                FilePublicationOwnedDelete.TryGetIdentity(handle);
            if (identity is null)
            {
                throw new IOException(
                    $"Could not establish the physical identity of '{path}'.");
            }
            long length = RandomAccess.GetLength(handle);
            long lastWriteUtcTicks = File.GetLastWriteTimeUtc(handle).Ticks;
            string hash;
            using (var stream = new FileStream(handle, FileAccess.Read))
            {
                hash = Convert.ToHexString(SHA256.HashData(stream));
                long verifiedLength = stream.Length;
                long verifiedLastWriteUtcTicks =
                    File.GetLastWriteTimeUtc(handle).Ticks;
                if (length != verifiedLength
                    || lastWriteUtcTicks != verifiedLastWriteUtcTicks)
                {
                    throw new IOException(
                        $"File '{path}' changed while its publication snapshot was captured.");
                }
                return new FilePublicationSnapshot(
                    true,
                    length,
                    verifiedLastWriteUtcTicks,
                    hash,
                    FilePublicationMetadata.Capture(handle),
                    identity);
            }
        }
        catch (FileNotFoundException)
        {
            return Missing;
        }
        catch (DirectoryNotFoundException)
        {
            return Missing;
        }
    }

    public bool ContentMatches(string path)
    {
        FilePublicationSnapshot current = Capture(path);
        return ContentEquals(current);
    }

    public bool ContentEquals(FilePublicationSnapshot other) =>
        Exists == other.Exists
        && (!Exists
            || (Length == other.Length
                && string.Equals(Sha256, other.Sha256, StringComparison.Ordinal)));

    public bool VersionEquals(FilePublicationSnapshot other) =>
        ContentEquals(other)
        && (!Exists
            || (PhysicalIdentity is not null
                && other.PhysicalIdentity is not null
                && PhysicalIdentity.Value == other.PhysicalIdentity.Value));

    public bool IsStructurallyValid() =>
        Exists
            ? Length >= 0
                && LastWriteUtcTicks > 0
                && Sha256 is { Length: 64 }
                && Sha256.All(Uri.IsHexDigit)
                && Metadata is not null
                && PhysicalIdentity is not null
            : Length == 0
                && LastWriteUtcTicks == 0
                && Sha256 is null
                && Metadata is null
                && PhysicalIdentity is null;
}
