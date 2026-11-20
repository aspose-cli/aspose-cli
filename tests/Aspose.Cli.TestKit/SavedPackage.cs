using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace Aspose.Cli.TestKit;

/// <summary>
/// The parts of a saved Open Packaging file, by name, as bytes, for comparing two saves of the
/// same change. The one value that depends on the moment of the save is taken out: the
/// <c>dcterms:modified</c> element of <c>docProps/core.xml</c>, which an engine writes as the save's
/// time, so two saves a second apart differ there alone. Every other part is compared as saved;
/// the zip entries' own timestamps are not read.
/// </summary>
public static partial class SavedPackage
{
    /// <summary>The core properties part, which holds the save's time.</summary>
    public const string CoreProperties = "docProps/core.xml";

    /// <summary>Every part of the package, with the save's time taken out of the core properties.</summary>
    public static Dictionary<string, byte[]> Parts(string path)
    {
        using ZipArchive archive = ZipFile.OpenRead(path);
        var parts = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            using Stream stream = entry.Open();
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            parts[entry.FullName] = entry.FullName == CoreProperties ? WithoutSaveTime(buffer.ToArray()) : buffer.ToArray();
        }

        return parts;
    }

    /// <summary>The names of the parts two packages hold with different bytes, or that only one holds, in order.</summary>
    public static string[] Differences(string left, string right)
    {
        Dictionary<string, byte[]> a = Parts(left);
        Dictionary<string, byte[]> b = Parts(right);
        return [.. a.Keys.Union(b.Keys, StringComparer.Ordinal)
            .Where(name => !a.TryGetValue(name, out byte[]? x) || !b.TryGetValue(name, out byte[]? y) || !x.AsSpan().SequenceEqual(y))
            .Order(StringComparer.Ordinal)];
    }

    private static byte[] WithoutSaveTime(byte[] part) =>
        Encoding.UTF8.GetBytes(Modified().Replace(Encoding.UTF8.GetString(part), string.Empty));

    [GeneratedRegex(@"<dcterms:modified\b[^>]*>[^<]*</dcterms:modified>", RegexOptions.CultureInvariant)]
    private static partial Regex Modified();
}
