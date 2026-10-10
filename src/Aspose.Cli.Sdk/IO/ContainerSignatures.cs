namespace Aspose.Cli.Sdk.IO;

/// <summary>
/// The container signatures that formats of several products share, spelled once: the OLE
/// compound file of legacy Office binaries and encrypted Office packages, the PDF header and
/// the ZIP container of Office Open XML, OpenDocument and EPUB packages.
/// </summary>
public static class ContainerSignatures
{
    /// <summary>The number of leading bytes in which a PDF reader looks for the PDF header.</summary>
    public const int PdfHeaderWindow = 1024;

    /// <summary>The OLE compound file signature, at the start of the file.</summary>
    public static ReadOnlySpan<byte> OleCompoundFile => [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

    /// <summary>The PDF header, which may follow other bytes within <see cref="PdfHeaderWindow"/>.</summary>
    public static ReadOnlySpan<byte> PdfHeader => "%PDF-"u8;

    /// <summary>The ZIP local file header that starts an archive with entries.</summary>
    public static ReadOnlySpan<byte> ZipLocalFileHeader => [0x50, 0x4B, 0x03, 0x04];

    // An empty archive starts with its end of central directory; a spanned one with its marker.
    private static ReadOnlySpan<byte> ZipEmptyArchive => [0x50, 0x4B, 0x05, 0x06];

    private static ReadOnlySpan<byte> ZipSpannedArchive => [0x50, 0x4B, 0x07, 0x08];

    /// <summary>Whether <paramref name="prefix"/> starts with the PDF header.</summary>
    public static bool StartsWithPdfHeader(ReadOnlySpan<byte> prefix) => prefix.StartsWith(PdfHeader);

    /// <summary>
    /// Whether the PDF header occurs within the first <see cref="PdfHeaderWindow"/> bytes of
    /// <paramref name="prefix"/>, where a PDF reader accepts it.
    /// </summary>
    public static bool HasPdfHeader(ReadOnlySpan<byte> prefix) =>
        prefix[..Math.Min(prefix.Length, PdfHeaderWindow)].IndexOf(PdfHeader) >= 0;

    /// <summary>Whether <paramref name="prefix"/> starts as a ZIP archive.</summary>
    public static bool IsZip(ReadOnlySpan<byte> prefix) =>
        prefix.StartsWith(ZipLocalFileHeader)
        || prefix.StartsWith(ZipEmptyArchive)
        || prefix.StartsWith(ZipSpannedArchive);

    /// <summary>
    /// The first bytes of a file, at most <paramref name="length"/>; empty when the file cannot
    /// be read, since a signature check only describes a file another read reports on.
    /// </summary>
    public static byte[] ReadPrefix(string path, int length = PdfHeaderWindow)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        try
        {
            using FileStream stream = InputFiles.OpenRead(path);
            return ReadPrefix(stream, length);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return [];
        }
    }

    /// <summary>
    /// The next bytes of <paramref name="stream"/>, at most <paramref name="length"/>; a seekable
    /// stream is returned to where it was.
    /// </summary>
    public static byte[] ReadPrefix(Stream stream, int length = PdfHeaderWindow)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        long? start = stream.CanSeek ? stream.Position : null;
        byte[] prefix = new byte[length];
        int read = stream.ReadAtLeast(prefix, length, throwOnEndOfStream: false);
        if (start is { } position)
        {
            stream.Position = position;
        }
        return read == length ? prefix : prefix[..read];
    }
}
