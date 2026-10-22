using System.IO.Compression;
using System.Text;
using SkiaSharp;
using Xunit;

namespace Aspose.Cli.Invariants.Tests.Isolation;

/// <summary>
/// <see cref="OutputScan"/> finds a planted sentinel in every encoding the isolation matrix relies
/// on, and reports nothing for the same container holding clean content, so a passing matrix case
/// means the sentinel is absent rather than unseen.
/// </summary>
public sealed class OutputScanTests
{
    private static readonly SKColor Sentinel = IsolationLayout.SentinelColour;
    private static readonly SKColor Clean = new(128, 128, 128);
    private const string Marker = IsolationLayout.Marker;

    /// <summary>Each container with the sentinel colour (planted) or the clean colour.</summary>
    public static TheoryData<string> Containers =>
        ["png", "jpeg", "zip", "nested zip", "pdf raw image", "pdf flate image", "data uri", "gzip"];

    [Theory]
    [MemberData(nameof(Containers))]
    public void FindsThePlantedColour_AndNothingInTheCleanOne(string container)
    {
        Assert.NotEmpty(OutputScan.Find(container, Sample(container, Sentinel), Sentinel, Marker));
        Assert.Empty(OutputScan.Find(container, Sample(container, Clean), Sentinel, Marker));
    }

    [Theory]
    [InlineData("utf-8")]
    [InlineData("utf-16")]
    [InlineData("zip")]
    [InlineData("gzip")]
    [InlineData("pdf flate")]
    public void FindsThePlantedText_AndNothingInTheCleanOne(string container)
    {
        Assert.NotEmpty(OutputScan.Find(container, Text(container, $"before {Marker} after"), Sentinel, Marker));
        Assert.Empty(OutputScan.Find(container, Text(container, "before ISOLATION-OTHER after"), Sentinel, Marker));
    }

    private static byte[] Sample(string container, SKColor colour) => container switch
    {
        "png" => OutputScan.Png(colour),
        "jpeg" => Jpeg(colour),
        "zip" => Zip("word/media/image1.png", OutputScan.Png(colour)),
        "nested zip" => Zip("attachments/inner.zip", Zip("media/image.png", OutputScan.Png(colour))),
        "pdf raw image" => Pdf(Samples(colour), flate: false),
        "pdf flate image" => Pdf(Samples(colour), flate: true),
        "data uri" => Encoding.UTF8.GetBytes($"<img src=\"data:image/png;base64,{Convert.ToBase64String(OutputScan.Png(colour))}\">"),
        "gzip" => Gzip(OutputScan.Png(colour)),
        _ => throw new ArgumentOutOfRangeException(nameof(container)),
    };

    private static byte[] Text(string container, string text) => container switch
    {
        "utf-8" => Encoding.UTF8.GetBytes(text),
        "utf-16" => Encoding.Unicode.GetBytes(text),
        "zip" => Zip("word/document.xml", Encoding.UTF8.GetBytes($"<w:t>{text}</w:t>")),
        "gzip" => Gzip(Encoding.UTF8.GetBytes(text)),
        "pdf flate" => Pdf(Encoding.ASCII.GetBytes($"BT ({text}) Tj ET"), flate: true),
        _ => throw new ArgumentOutOfRangeException(nameof(container)),
    };

    private static byte[] Jpeg(SKColor colour)
    {
        using var bitmap = new SKBitmap(40, 40);
        bitmap.Erase(colour);
        using SKData data = bitmap.Encode(SKEncodedImageFormat.Jpeg, 90);
        return data.ToArray();
    }

    /// <summary>The raw RGB samples of a 40-pixel square, as a PDF image XObject holds them.</summary>
    private static byte[] Samples(SKColor colour) =>
        [.. Enumerable.Repeat(new[] { colour.Red, colour.Green, colour.Blue }, 40 * 40).SelectMany(static pixel => pixel)];

    /// <summary>A one-page PDF whose only stream holds the content, raw or Flate-encoded.</summary>
    private static byte[] Pdf(byte[] content, bool flate)
    {
        byte[] data = flate ? Deflate(content) : content;
        using var pdf = new MemoryStream();
        void Write(string text) => pdf.Write(Encoding.ASCII.GetBytes(text));
        Write("%PDF-1.4\n1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj\n");
        Write("2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >> endobj\n");
        Write("3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 40 40] /Resources << /XObject << /Im1 4 0 R >> >> >> endobj\n");
        Write($"4 0 obj << /Type /XObject /Subtype /Image /Width 40 /Height 40 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Length {data.Length}{(flate ? " /Filter /FlateDecode" : string.Empty)} >>\nstream\n");
        pdf.Write(data);
        Write("\nendstream\nendobj\ntrailer << /Root 1 0 R >>\n%%EOF\n");
        return pdf.ToArray();
    }

    private static byte[] Deflate(byte[] content)
    {
        using var buffer = new MemoryStream();
        using (var zlib = new ZLibStream(buffer, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(content);
        }
        return buffer.ToArray();
    }

    private static byte[] Gzip(byte[] content)
    {
        using var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionLevel.Optimal, leaveOpen: true))
        {
            gzip.Write(content);
        }
        return buffer.ToArray();
    }

    private static byte[] Zip(string name, byte[] content)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            using Stream stream = archive.CreateEntry(name).Open();
            stream.Write(content);
        }
        return buffer.ToArray();
    }
}
