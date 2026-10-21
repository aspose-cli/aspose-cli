using System.Security.Cryptography;

namespace Aspose.Cli.Sdk.IO.Publication;

/// <summary>
/// Proves that a directory is an earlier output of its owner before a publication replaces it:
/// it holds exactly the files the owner recorded when it wrote them, each with the SHA-256 it
/// recorded, the directories those files are in and nothing else. Its name is no proof, and
/// neither is one recorded file such as a manifest: a directory with anything more, anything
/// less or anything changed is someone else's.
/// </summary>
public static class OwnedDirectoryInventory
{
    /// <summary>
    /// Why <paramref name="root"/> is not exactly the recorded <paramref name="files"/>, or null
    /// when it is. A link anywhere below the root is never a recorded file.
    /// </summary>
    /// <param name="root">The existing directory.</param>
    /// <param name="files">
    /// Each recorded file by its path relative to the root, with <c>/</c> or <c>\</c> between
    /// segments, and its lower- or upper-case SHA-256; null for a file that cannot record its
    /// own digest, such as the manifest that lists the others, which must only be present.
    /// </param>
    public static string? Mismatch(string root, IReadOnlyDictionary<string, string?> files)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentNullException.ThrowIfNull(files);
        var expected = new Dictionary<string, string?>(FilePaths.Comparer);
        var directories = new HashSet<string>(FilePaths.Comparer);
        foreach ((string path, string? sha256) in files)
        {
            string relative = path.Replace('\\', '/');
            expected[relative] = sha256?.ToLowerInvariant();
            for (int slash = relative.LastIndexOf('/'); slash > 0; slash = relative.LastIndexOf('/', slash - 1))
            {
                directories.Add(relative[..slash]);
            }
        }

        string full = Path.GetFullPath(root);
        int found = 0;
        var pending = new Stack<DirectoryInfo>();
        pending.Push(new DirectoryInfo(full));
        while (pending.TryPop(out DirectoryInfo? directory))
        {
            foreach (FileSystemInfo item in directory.EnumerateFileSystemInfos())
            {
                string relative = Path.GetRelativePath(full, item.FullName).Replace('\\', '/');
                if ((item.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    return $"it holds the link '{relative}'";
                }
                if (item is DirectoryInfo child)
                {
                    if (!directories.Contains(relative))
                    {
                        return $"it holds the unrecorded directory '{relative}'";
                    }
                    pending.Push(child);
                    continue;
                }
                if (!expected.TryGetValue(relative, out string? sha256))
                {
                    return $"it holds the unrecorded file '{relative}'";
                }
                if (sha256 is not null && !string.Equals(Sha256(item.FullName), sha256, StringComparison.Ordinal))
                {
                    return $"its file '{relative}' changed";
                }
                found++;
            }
        }

        return found == expected.Count ? null : "a file it recorded is missing";
    }

    private static string Sha256(string path)
    {
        using FileStream input = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();
    }
}
