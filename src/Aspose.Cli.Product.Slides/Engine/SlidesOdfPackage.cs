using System.IO.Compression;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>
/// Puts an OpenDocument package in the order the format requires. Aspose.Slides writes the
/// <c>mimetype</c> entry compressed and after others, where tools that recognize a package by
/// its first bytes, this CLI's content check among them, cannot read it; OpenDocument requires
/// it first and stored. Every other entry keeps its name, content and time.
/// </summary>
internal static class SlidesOdfPackage
{
    private const string MimeType = "mimetype";

    /// <summary>Rewrites the package at <paramref name="path"/> with its <c>mimetype</c> entry first and stored.</summary>
    internal static void PutMimeTypeFirst(string path)
    {
        List<(string Name, DateTimeOffset Time, byte[] Content)> entries = [];
        using (ZipArchive source = ZipFile.OpenRead(path))
        {
            if (source.Entries.Count == 0
                || source.Entries[0].FullName == MimeType && source.Entries[0].CompressedLength == source.Entries[0].Length)
            {
                return;
            }

            foreach (ZipArchiveEntry entry in source.Entries)
            {
                using Stream stream = entry.Open();
                using var content = new MemoryStream();
                stream.CopyTo(content);
                entries.Add((entry.FullName, entry.LastWriteTime, content.ToArray()));
            }
        }

        using var target = new ZipArchive(File.Create(path), ZipArchiveMode.Create);
        foreach ((string name, DateTimeOffset time, byte[] content) in entries.OrderBy(static entry => entry.Name == MimeType ? 0 : 1))
        {
            ZipArchiveEntry entry = target.CreateEntry(name, name == MimeType ? CompressionLevel.NoCompression : CompressionLevel.Optimal);
            entry.LastWriteTime = time;
            using Stream stream = entry.Open();
            stream.Write(content);
        }
    }
}
