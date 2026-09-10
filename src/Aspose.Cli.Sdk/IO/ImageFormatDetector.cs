using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.IO;

/// <summary>Detects common raster and PDF signatures without loading an engine.</summary>
public static class ImageFormatDetector
{
    /// <summary>Returns the detected format id or a product-scoped format error.</summary>
    public static string Detect(
        string path,
        IReadOnlyList<string> supportedFormats)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(supportedFormats);
        if (!File.Exists(path))
        {
            throw CliErrors.FileNotFound(path);
        }

        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            Span<byte> header = stackalloc byte[12];
            int read = stream.Read(header);

            string? format = Detect(header[..read]);
            return format is not null
                ? format
                : throw CliErrors.FormatUnsupported(
                    "unknown-image",
                    supportedFormats);
        }
        catch (UnauthorizedAccessException)
        {
            throw CliErrors.FileAccessDenied(path);
        }
        catch (IOException)
        {
            throw CliErrors.FileLocked(path);
        }
    }

    private static string? Detect(ReadOnlySpan<byte> header)
    {
        if (IsPdf(header))
        {
            return "pdf";
        }
        if (IsPng(header))
        {
            return "png";
        }
        if (IsJpeg(header))
        {
            return "jpeg";
        }
        if (IsBitmap(header))
        {
            return "bmp";
        }
        if (IsGif(header))
        {
            return "gif";
        }
        if (IsTiff(header))
        {
            return "tiff";
        }

        return null;
    }

    private static bool IsPdf(ReadOnlySpan<byte> header) =>
        header.Length >= 5
        && header[..5].SequenceEqual("%PDF-"u8);

    private static bool IsPng(ReadOnlySpan<byte> header)
    {
        ReadOnlySpan<byte> signature =
            [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];
        return header.Length >= signature.Length
            && header[..signature.Length].SequenceEqual(signature);
    }

    private static bool IsJpeg(ReadOnlySpan<byte> header) =>
        header.Length >= 3
        && header[0] == 0xff
        && header[1] == 0xd8
        && header[2] == 0xff;

    private static bool IsBitmap(ReadOnlySpan<byte> header) =>
        header.Length >= 2
        && header[0] == (byte)'B'
        && header[1] == (byte)'M';

    private static bool IsGif(ReadOnlySpan<byte> header) =>
        header.Length >= 6
        && (header[..6].SequenceEqual("GIF87a"u8)
            || header[..6].SequenceEqual("GIF89a"u8));

    private static bool IsTiff(ReadOnlySpan<byte> header) =>
        header.Length >= 4
        && (IsLittleEndianTiff(header) || IsBigEndianTiff(header));

    private static bool IsLittleEndianTiff(ReadOnlySpan<byte> header) =>
        header[0] == (byte)'I'
        && header[1] == (byte)'I'
        && header[2] == 42
        && header[3] == 0;

    private static bool IsBigEndianTiff(ReadOnlySpan<byte> header) =>
        header[0] == (byte)'M'
        && header[1] == (byte)'M'
        && header[2] == 0
        && header[3] == 42;
}
