using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace Aspose.Cli.TestKit;

/// <summary>
/// What a build's <c>schema</c> command printed: the listed ids in order and each raw document.
/// </summary>
internal sealed record PublishedSchemaSnapshot(IReadOnlyList<string> Ids, IReadOnlyDictionary<string, string> Documents);

/// <summary>
/// Keeps the schemas one build of the CLI printed on disk, so the test processes of a run ask that
/// build once instead of once each. An entry belongs to one launcher output directory and holds the
/// fingerprint of the build it was read from: the name, size and write time of every file in that
/// directory, which a rebuild changes for each assembly it rewrites. A different fingerprint is a
/// miss, so a rebuilt CLI is read again and its entry replaced.
/// </summary>
internal static class PublishedSchemaCache
{
    private static readonly TimeSpan LockTimeout = TimeSpan.FromMinutes(10);

    /// <summary>The directory a run keeps its entries in.</summary>
    public static string DefaultDirectory { get; } =
        Path.Combine(Path.GetTempPath(), "aspose-cli-tests", "published-schemas");

    /// <summary>
    /// The cached schemas of <paramref name="executable"/>'s build, or what
    /// <paramref name="read"/> returns for it, kept for the next process. One process reads at a
    /// time; the others wait and then find its entry.
    /// </summary>
    public static PublishedSchemaSnapshot GetOrAdd(string executable, string directory, Func<PublishedSchemaSnapshot> read)
    {
        ArgumentException.ThrowIfNullOrEmpty(executable);
        ArgumentNullException.ThrowIfNull(read);
        string output = Path.GetDirectoryName(Path.GetFullPath(executable))!;
        Directory.CreateDirectory(directory);
        string entry = Path.Combine(directory, Hash(output.ToUpperInvariant())[..32] + ".json");
        using FileStream held = Acquire(entry + ".lock");
        string fingerprint = Fingerprint(executable);
        if (TryRead(entry, fingerprint) is { } cached)
        {
            return cached;
        }

        PublishedSchemaSnapshot snapshot = read();
        // A build that finished while the schemas were read may have answered for either build.
        if (Fingerprint(executable) == fingerprint)
        {
            Write(entry, fingerprint, snapshot);
        }

        return snapshot;
    }

    /// <summary>The identity of the build in <paramref name="executable"/>'s directory.</summary>
    public static string Fingerprint(string executable)
    {
        string full = Path.GetFullPath(executable);
        var text = new StringBuilder(full.ToUpperInvariant()).Append('\n');
        foreach (FileInfo file in new DirectoryInfo(Path.GetDirectoryName(full)!).EnumerateFiles()
            .OrderBy(static file => file.Name, StringComparer.OrdinalIgnoreCase))
        {
            text.Append(file.Name.ToUpperInvariant()).Append('|').Append(file.Length).Append('|')
                .Append(file.LastWriteTimeUtc.Ticks).Append('\n');
        }

        return Hash(text.ToString());
    }

    private static FileStream Acquire(string path)
    {
        DateTime deadline = DateTime.UtcNow + LockTimeout;
        while (true)
        {
            try
            {
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(50);
            }
        }
    }

    private static PublishedSchemaSnapshot? TryRead(string entry, string fingerprint)
    {
        if (!File.Exists(entry))
        {
            return null;
        }

        // Entries are only ever published whole, so anything else is a miss that reading the build replaces.
        try
        {
            if (JsonNode.Parse(File.ReadAllText(entry)) is not JsonObject root
                || root["fingerprint"]?.GetValue<string>() != fingerprint
                || root["ids"] is not JsonArray listed
                || root["documents"] is not JsonObject documents)
            {
                return null;
            }

            string[] ids = [.. listed.Select(static id => id?.GetValue<string>() ?? string.Empty)];
            var raw = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string id in ids)
            {
                if (documents[id]?.GetValue<string>() is not { } document)
                {
                    return null;
                }

                raw[id] = document;
            }

            return new PublishedSchemaSnapshot(ids, raw);
        }
        catch (Exception exception) when (exception is System.Text.Json.JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    private static void Write(string entry, string fingerprint, PublishedSchemaSnapshot snapshot)
    {
        var documents = new JsonObject();
        foreach (string id in snapshot.Ids)
        {
            documents[id] = snapshot.Documents[id];
        }

        var root = new JsonObject
        {
            ["fingerprint"] = fingerprint,
            ["ids"] = new JsonArray([.. snapshot.Ids.Select(static id => (JsonNode)id)]),
            ["documents"] = documents,
        };
        string staged = entry + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(staged, root.ToJsonString());
        File.Move(staged, entry, overwrite: true);
    }

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
