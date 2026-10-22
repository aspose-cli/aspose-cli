using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using SkiaSharp;

namespace Aspose.Cli.Invariants.Tests.Isolation;

/// <summary>
/// Finds a marker resource in what a command wrote: a solid colour in any raster image (an output
/// file, a part of a package, an image stream of a PDF or a <c>data:</c> URI of a text output) and a
/// unique text in any byte stream. A package is opened part by part, a PDF stream inflated and an
/// SVGZ unzipped, so a resource that reached an output under another encoding is still found.
/// </summary>
internal static partial class OutputScan
{
    /// <summary>The fewest matching pixels that count as the colour, above anti-aliasing noise.</summary>
    private const int PixelThreshold = 16;

    /// <summary>The largest difference per channel that still matches, for lossy encodings.</summary>
    private const int Tolerance = 40;

    private const int MaxDepth = 4;
    private const int MaxBytes = 256 * 1024 * 1024;

    /// <summary>Where the colour or the text occurs in a file, one line per occurrence.</summary>
    public static List<string> Find(string name, byte[] content, SKColor colour, string? text)
    {
        var findings = new List<string>();
        Scan(name, content, colour, text, 0, findings);
        return findings;
    }

    /// <summary>Whether the text occurs in UTF-8 or UTF-16 in the content.</summary>
    public static bool HasText(ReadOnlySpan<byte> content, string text) =>
        content.IndexOf(Encoding.UTF8.GetBytes(text)) >= 0 || content.IndexOf(Encoding.Unicode.GetBytes(text)) >= 0;

    /// <summary>The number of pixels of a decodable raster image that match the colour, or -1.</summary>
    public static int Pixels(byte[] image, SKColor colour)
    {
        using SKCodec? codec = SKCodec.Create(new SKMemoryStream(image));
        if (codec is null)
        {
            return -1;
        }
        using SKBitmap? bitmap = SKBitmap.Decode(codec);
        if (bitmap is null)
        {
            return -1;
        }
        int count = 0;
        foreach (SKColor pixel in bitmap.Pixels)
        {
            if (pixel.Alpha > 200 && Near(pixel.Red, colour.Red) && Near(pixel.Green, colour.Green) && Near(pixel.Blue, colour.Blue))
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>A solid square PNG of the colour.</summary>
    public static byte[] Png(SKColor colour, int size = 40)
    {
        using var bitmap = new SKBitmap(size, size);
        bitmap.Erase(colour);
        using SKData data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static bool Near(byte actual, byte expected) => Math.Abs(actual - expected) <= Tolerance;

    private static void Scan(string name, byte[] content, SKColor colour, string? text, int depth, List<string> findings)
    {
        if (depth > MaxDepth || content.Length == 0)
        {
            return;
        }
        if (text is not null && HasText(content, text))
        {
            findings.Add($"{name}: holds the marker text '{text}'");
        }
        int pixels = Pixels(content, colour);
        if (pixels >= PixelThreshold)
        {
            findings.Add($"{name}: {pixels} pixels of the marker colour");
            return;
        }
        if (content.AsSpan().StartsWith("PK\u0003\u0004"u8))
        {
            ScanZip(name, content, colour, text, depth, findings);
            return;
        }
        if (content.AsSpan().StartsWith("%PDF"u8))
        {
            ScanPdf(name, content, colour, text, depth, findings);
            return;
        }
        if (content.Length > 2 && content[0] == 0x1f && content[1] == 0x8b)
        {
            Scan(name + "!gunzip", Inflate(content, static stream => new GZipStream(stream, CompressionMode.Decompress)), colour, text, depth + 1, findings);
            return;
        }
        ScanDataUris(name, content, colour, text, depth, findings);
    }

    private static void ScanZip(string name, byte[] content, SKColor colour, string? text, int depth, List<string> findings)
    {
        ZipArchive archive;
        try
        {
            archive = new ZipArchive(new MemoryStream(content, writable: false), ZipArchiveMode.Read);
        }
        catch (InvalidDataException)
        {
            return;
        }
        using (archive)
        {
            foreach (ZipArchiveEntry entry in archive.Entries.Where(static entry => entry.Length is > 0 and < MaxBytes))
            {
                using var buffer = new MemoryStream();
                using (Stream stream = entry.Open())
                {
                    stream.CopyTo(buffer);
                }
                Scan($"{name}!{entry.FullName}", buffer.ToArray(), colour, text, depth + 1, findings);
            }
        }
    }

    /// <summary>
    /// Each stream of the PDF, inflated when it is Flate-encoded: a raw image's samples hold runs of
    /// the colour, a JPEG stream decodes as an image and a content or font stream may hold the text.
    /// </summary>
    private static void ScanPdf(string name, byte[] content, SKColor colour, string? text, int depth, List<string> findings)
    {
        byte[] run = [.. Enumerable.Repeat(new[] { colour.Red, colour.Green, colour.Blue }, 8).SelectMany(static pixel => pixel)];
        int index = 0;
        int number = 0;
        while (true)
        {
            int found = content.AsSpan(index).IndexOf("stream"u8);
            if (found < 0)
            {
                break;
            }
            index += found;
            int start = index + "stream".Length;
            bool end = index >= 3 && content.AsSpan(index - 3, 3).SequenceEqual("end"u8);
            if (end || start >= content.Length || content[start] is not ((byte)'\r' or (byte)'\n'))
            {
                index = start;
                continue;
            }
            while (start < content.Length && content[start] is (byte)'\r' or (byte)'\n')
            {
                start++;
            }
            int length = content.AsSpan(start).IndexOf("endstream"u8);
            if (length < 0)
            {
                break;
            }
            number++;
            byte[] raw = content.AsSpan(start, length).ToArray();
            string stream = $"{name}!stream{number}";
            byte[] inflated = Inflate(raw, static source => new ZLibStream(source, CompressionMode.Decompress));
            byte[] data = inflated.Length > 0 ? inflated : raw;
            if (data.AsSpan().IndexOf(run) >= 0)
            {
                findings.Add($"{stream}: raw image samples of the marker colour");
            }
            else
            {
                Scan(stream, data, colour, text, depth + 1, findings);
            }
            index = start + length + "endstream".Length;
        }
    }

    private static void ScanDataUris(string name, byte[] content, SKColor colour, string? text, int depth, List<string> findings)
    {
        if (content.AsSpan().IndexOf("base64,"u8) < 0)
        {
            return;
        }
        string decoded = Encoding.UTF8.GetString(content);
        int number = 0;
        foreach (Match match in DataUri().Matches(decoded))
        {
            byte[] payload;
            try
            {
                payload = Convert.FromBase64String(match.Groups["data"].Value);
            }
            catch (FormatException)
            {
                continue;
            }
            Scan($"{name}!data{++number}", payload, colour, text, depth + 1, findings);
        }
    }

    private static byte[] Inflate(byte[] content, Func<Stream, Stream> open)
    {
        try
        {
            using var output = new MemoryStream();
            using (Stream stream = open(new MemoryStream(content, writable: false)))
            {
                stream.CopyTo(output);
            }
            return output.ToArray();
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            return [];
        }
    }

    [GeneratedRegex(@"data:[a-z]+/[a-z0-9.+-]+;base64,(?<data>[A-Za-z0-9+/=\s]+)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 10_000)]
    private static partial Regex DataUri();
}
