using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Errors;
using SkiaSharp;

namespace Aspose.Cli.Sdk.IO;

/// <summary>An image format that only the document engines read; the CLI recognizes it from its signature.</summary>
public enum EngineImageFormat
{
    /// <summary>Not one of these formats.</summary>
    None,

    Svg,
    Emf,
    Wmf,
    Tiff,
    Jpeg2000,
    Pict,
    Dicom,
}

/// <summary>
/// The one check that an image file is an image before a document engine embeds it. Engines
/// accept an image lazily and keep a damaged one, or fail only when the document is saved. A
/// format the CLI can decode (PNG, JPEG, GIF, BMP, WebP, ICO) is decoded in full and must decode
/// without error, so a truncated image is refused; a format only the engines read (SVG, EMF, WMF,
/// TIFF, JPEG 2000, PICT, DICOM) is recognized from its signature; anything else is refused.
/// The decode's output is small whatever the image's size, so checking an image never refuses
/// one the engines would take. An image a created or imported document
/// embeds is <c>FILE_CORRUPT</c> naming the image; an image an edit operation inserts goes
/// through <see cref="Operations.OperationImages"/> instead.
/// </summary>
public static class ImageInput
{
    // The most a decode's output buffer holds, whatever the image's size.
    private const long OutputBytes = 1L << 20;

    // Every image format the check accepts, once: the bitmaps the CLI decodes, by the decoder
    // that recognizes them; TIFF, a bitmap the engines decode, and the formats the engines alone
    // read, by their signature (EngineFormatOf).
    private static readonly ImageFormat[] Table =
    [
        new(Raster("png", ".png"), SKEncodedImageFormat.Png),
        new(Raster("jpeg", ".jpg", ".jpeg"), SKEncodedImageFormat.Jpeg),
        new(Raster("gif", ".gif"), SKEncodedImageFormat.Gif),
        new(Raster("bmp", ".bmp"), SKEncodedImageFormat.Bmp),
        new(Raster("webp", ".webp"), SKEncodedImageFormat.Webp),
        new(Raster("ico", ".ico"), SKEncodedImageFormat.Ico),
        new(Raster("tiff", ".tif", ".tiff"), Signature: EngineImageFormat.Tiff),
        new(Other("svg", ".svg"), Signature: EngineImageFormat.Svg),
        new(Other("emf", ".emf"), Signature: EngineImageFormat.Emf),
        new(Other("wmf", ".wmf"), Signature: EngineImageFormat.Wmf),
        new(Other("jpeg2000", ".jp2", ".j2k"), Signature: EngineImageFormat.Jpeg2000),
        new(Other("pict", ".pct", ".pict"), Signature: EngineImageFormat.Pict),
        new(Other("dicom", ".dcm"), Signature: EngineImageFormat.Dicom),
    ];

    /// <summary>
    /// Every image format the check accepts, by its long id: the bitmaps (<see cref="FormatDescriptor.Raster"/>)
    /// the CLI decodes, and TIFF, which the engines decode; then the vector and other formats the
    /// engines alone read. <see cref="FormatOf"/> names an image's format from its content.
    /// </summary>
    public static IReadOnlyList<FormatDescriptor> Formats { get; } = [.. Table.Select(static row => row.Format)];

    // The root element of an SVG document, with or without a namespace prefix.
    private static readonly Regex SvgRoot = new(
        @"<(?:[a-z_][a-z0-9_.\-]*:)?svg[\s/>]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking,
        TimeSpan.FromSeconds(10));

    internal const string Hint =
        "Supply an intact image file, such as PNG or JPEG; export a damaged or truncated image again from its source.";

    /// <summary>
    /// Reads an image file in full, charged as <see cref="InputSource.ReadAllBytes"/> charges it,
    /// and refuses it unless it is an intact image.
    /// </summary>
    /// <param name="inputs">The invocation's input reader.</param>
    /// <param name="path">The image file.</param>
    /// <returns>The image's bytes, for the engine.</returns>
    public static byte[] Read(InputSource inputs, string path) => Read(inputs, path, Corrupt);

    /// <summary>
    /// Reads an image file in full through a scope's bounded stream, charged to the input budget
    /// as an engine's own read of the file would be, and refuses it unless it is an intact image.
    /// </summary>
    /// <param name="inputs">The scope that reads the file.</param>
    /// <param name="path">The image file.</param>
    /// <param name="refuseNetworkReferences">Whether an SVG that names a network address is refused
    /// first (<see cref="NetworkReferenceGuard.EnsureNoneInImage"/>), for an engine that would
    /// fetch it with no resource hook.</param>
    /// <returns>The image's bytes, for the engine.</returns>
    public static byte[] Read(InputResourceScope inputs, string path, bool refuseNetworkReferences) =>
        Read(inputs, path, refuseNetworkReferences, Corrupt);

    /// <summary>Refuses image bytes another reader read unless they are an intact image.</summary>
    /// <param name="inputs">The invocation's input reader, whose budgets the decode is checked against.</param>
    /// <param name="content">The image's bytes.</param>
    /// <param name="path">The image file, for the error.</param>
    public static void Check(InputSource inputs, byte[] content, string path)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        Check(inputs.Budgets, content, path, Corrupt);
    }

    internal static byte[] Read(InputSource inputs, string path, Func<string, string, Exception> invalid)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        byte[] content = inputs.ReadAllBytes(path);
        Check(inputs.Budgets, content, path, invalid);
        return content;
    }

    internal static byte[] Read(
        InputResourceScope inputs, string path, bool refuseNetworkReferences, Func<string, string, Exception> invalid)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        byte[] content;
        using (var buffer = new MemoryStream())
        {
            inputs.OpenFile(path).CopyTo(buffer);
            content = buffer.ToArray();
        }

        if (refuseNetworkReferences)
        {
            NetworkReferenceGuard.EnsureNoneInImage(new MemoryStream(content, writable: false), path);
        }

        Check(inputs.Source.Budgets, content, path, invalid);
        return content;
    }

    private static void Check(ResourceBudgetLedger budgets, byte[] content, string path, Func<string, string, Exception> invalid)
    {
        if (Refusal(content, budgets, path, invalid) is { } refused)
        {
            throw refused;
        }
    }

    /// <summary>
    /// The error for bytes that are not an intact image, or null when they are. A compressed
    /// image is refused as <see cref="NetworkReferenceGuard"/> refuses one, since it would be a
    /// compressed SVG that no one can check; any other problem becomes the caller's error.
    /// </summary>
    internal static Exception? Refusal(byte[] content, ResourceBudgetLedger budgets, string path, Func<string, string, Exception> invalid)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (NetworkReferenceGuard.CompressedImageRefusal(content, path) is { } compressed)
        {
            return compressed;
        }

        return Problem(content, budgets) is { } reason ? invalid(path, reason) : null;
    }

    /// <summary>The <c>FILE_CORRUPT</c> error for an image input that is not an intact image.</summary>
    internal static Exception Corrupt(string path, string reason) => CliErrors.InputUnreadable(path, "image", reason, Hint);

    /// <summary>Why the bytes are not an intact image, or null when they are.</summary>
    internal static string? Problem(byte[] content, ResourceBudgetLedger budgets)
    {
        if (content.Length == 0)
        {
            return "the file is empty";
        }

        using (SKData data = SKData.CreateCopy(content))
        using (SKCodec? codec = SKCodec.Create(data))
        {
            if (Decoded(codec) is not null)
            {
                return Decode(codec, budgets, HasInterlacedPng(content) || IsProgressiveJpeg(content));
            }
        }

        return EngineFormatOf(content) != EngineImageFormat.None
            ? null
            : "its content is not an image in a format the document engines read";
    }

    /// <summary>
    /// Decodes all the data of the first frame. A format that decodes in parts does so into an
    /// output that stays small whatever the image's size, charged to the memory budget: JPEG,
    /// BMP and an ICO holding a bitmap a strip of scanlines at a time, PNG (and an ICO holding
    /// one) incrementally into a one-pixel-wide column, WebP at a reduced scale; each still reads
    /// and decodes every byte of the image data. A decode that holds the whole frame, GIF's and
    /// an interlaced PNG's and a progressive JPEG's, is held to the raster budget of one image
    /// instead.
    /// </summary>
    private static string? Decode(SKCodec codec, ResourceBudgetLedger budgets, bool holdsWholeFrame)
    {
        SKImageInfo info = codec.Info.WithColorType(SKColorType.Bgra8888);
        if (holdsWholeFrame)
        {
            // Skia de-interlaces a PNG, and libjpeg keeps a progressive JPEG's coefficients, in a
            // buffer of the whole image the decoder allocates itself.
            EnsureWholeFrame(budgets, info);
        }

        long rowBytes = (long)info.Width * info.BytesPerPixel;
        string format = codec.EncodedFormat.ToString().ToUpperInvariant();
        if (codec.StartScanlineDecode(info) == SKCodecResult.Success)
        {
            int strip = (int)Math.Clamp(OutputBytes / rowBytes, 1, info.Height);
            return WithBuffer<string?>(budgets, strip * rowBytes, pixels =>
            {
                for (int decoded = 0; decoded < info.Height;)
                {
                    budgets.Deadline.ThrowIfExpired("image-decode");
                    int wanted = Math.Min(strip, info.Height - decoded);
                    if (codec.GetScanlines(pixels, wanted, (int)rowBytes) < wanted)
                    {
                        return Outcome(SKCodecResult.IncompleteInput, format);
                    }

                    decoded += wanted;
                }

                return null;
            });
        }

        SKCodecResult? column = WithBuffer<SKCodecResult?>(budgets, (long)info.Height * info.BytesPerPixel, pixels =>
            codec.StartIncrementalDecode(info, pixels, info.BytesPerPixel, new SKCodecOptions(new SKRectI(0, 0, 1, info.Height)))
                == SKCodecResult.Success
                ? codec.IncrementalDecode()
                : null);
        if (column is { } result)
        {
            return Outcome(result, format);
        }

        float scale = (float)Math.Min(1, Math.Sqrt((double)OutputBytes / (rowBytes * info.Height)));
        SKSizeI scaled = codec.GetScaledDimensions(scale);
        if (scaled.Width < info.Width && scaled.Height < info.Height)
        {
            SKImageInfo small = info.WithSize(scaled.Width, scaled.Height);
            return Outcome(WithBuffer(budgets, (long)small.BytesSize, pixels => codec.GetPixels(small, pixels)), format);
        }

        EnsureWholeFrame(budgets, info);
        byte[] whole = new byte[rowBytes * info.Height];
        return Outcome(codec.GetPixels(info, whole), format);
    }

    // A decode that holds the whole frame is held to the pixels of one raster image, the limit
    // rendering has.
    private static void EnsureWholeFrame(ResourceBudgetLedger budgets, SKImageInfo info) =>
        budgets.EnsureWithin(ResourceBudgetKinds.RasterPixels, (long)info.Width * info.Height, "pixels", "image-decode");

    /// <summary>
    /// Whether the bytes are an Adam7-interlaced PNG, or an ICO that holds one: the interlace
    /// method is the last byte of the IHDR chunk, which a PNG starts with.
    /// </summary>
    private static bool HasInterlacedPng(ReadOnlySpan<byte> content)
    {
        if (IsInterlacedPng(content))
        {
            return true;
        }

        if (content.Length < 6 || !content.StartsWith((ReadOnlySpan<byte>)[0x00, 0x00]) || content[2] is not (1 or 2) || content[3] != 0)
        {
            return false;
        }

        int count = content[4] | (content[5] << 8);
        for (int entry = 0; entry < count && 6 + (entry * 16) + 16 <= content.Length; entry++)
        {
            int offset = BitConverter.ToInt32(content.Slice(6 + (entry * 16) + 12, 4));
            if (offset > 0 && offset < content.Length && IsInterlacedPng(content[offset..]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether the bytes are a progressive JPEG: the first start-of-frame marker before the
    /// first scan is a progressive one (SOF2, SOF6, SOF10 or SOF14).
    /// </summary>
    private static bool IsProgressiveJpeg(ReadOnlySpan<byte> content)
    {
        if (!content.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8]))
        {
            return false;
        }

        int position = 2;
        while (position + 4 <= content.Length)
        {
            if (content[position] != 0xFF)
            {
                return false;
            }

            byte marker = content[position + 1];
            if (marker == 0xFF)
            {
                position++;
                continue;
            }

            if (marker is 0x01 or (>= 0xD0 and <= 0xD7))
            {
                position += 2;
                continue;
            }

            if (marker is 0xC2 or 0xC6 or 0xCA or 0xCE)
            {
                return true;
            }

            if (marker is (>= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC) or 0xDA)
            {
                return false;
            }

            position += 2 + ((content[position + 2] << 8) | content[position + 3]);
        }

        return false;
    }

    private static bool IsInterlacedPng(ReadOnlySpan<byte> content) =>
        content.Length > 28
        && content.StartsWith((ReadOnlySpan<byte>)[0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A])
        && content[12..16].SequenceEqual("IHDR"u8)
        && content[28] == 1;

    private static string? Outcome(SKCodecResult result, string format) => result switch
    {
        SKCodecResult.Success => null,
        SKCodecResult.IncompleteInput or SKCodecResult.ErrorInInput =>
            $"the {format} data ends or breaks off before the image is complete",
        _ => $"the {format} data cannot be decoded ({result})",
    };

    // A small output buffer must fit within the memory budget that remains; it is freed before
    // the check returns, so it is not charged for good.
    private static T WithBuffer<T>(ResourceBudgetLedger budgets, long bytes, Func<IntPtr, T> decode)
    {
        string kind = ResourceBudgetKinds.MemoryBufferBytes;
        long inUse = budgets.Limit(kind) - budgets.Remaining(kind);
        budgets.EnsureWithin(kind, bytes > long.MaxValue - inUse ? long.MaxValue : inUse + bytes, "bytes", "image-decode");
        byte[] buffer = new byte[bytes];
        GCHandle pinned = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try { return decode(pinned.AddrOfPinnedObject()); }
        finally { pinned.Free(); }
    }

    /// <summary>
    /// The format of an image the check accepts, from its content whatever its file is named:
    /// as the check does, a format the CLI decodes by its decoder first, any other by its
    /// signature; null when the bytes are no image of <see cref="Formats"/>.
    /// </summary>
    /// <param name="content">The image's bytes.</param>
    public static FormatDescriptor? FormatOf(ReadOnlySpan<byte> content)
    {
        if (content.IsEmpty)
        {
            return null;
        }

        using (SKData data = SKData.CreateCopy(content))
        using (SKCodec? codec = SKCodec.Create(data))
        {
            if (Decoded(codec) is { } decoded)
            {
                return decoded.Format;
            }
        }

        EngineImageFormat signature = EngineFormatOf(content);
        return signature == EngineImageFormat.None ? null : Table.First(row => row.Signature == signature).Format;
    }

    // The format the CLI decodes that the decoder recognized, or null.
    private static ImageFormat? Decoded(SKCodec? codec) =>
        codec is null ? null : Array.Find(Table, row => row.Decoder == codec.EncodedFormat);

    private static FormatDescriptor Raster(string id, params string[] extensions) =>
        new(id, FormatUse.Input, extensions) { Raster = true };

    private static FormatDescriptor Other(string id, params string[] extensions) =>
        new(id, FormatUse.Input, extensions) { Image = true };

    /// <summary>One accepted image format, with how the check recognizes it.</summary>
    private sealed record ImageFormat(
        FormatDescriptor Format,
        SKEncodedImageFormat? Decoder = null,
        EngineImageFormat Signature = EngineImageFormat.None);

    /// <summary>
    /// The format that only the document engines read, recognized from the bytes' signature:
    /// SVG markup, EMF, WMF, TIFF, JPEG 2000, PICT or DICOM; <see cref="EngineImageFormat.None"/>
    /// for any other content, including the formats the CLI decodes.
    /// </summary>
    /// <param name="content">The image's bytes.</param>
    public static EngineImageFormat EngineFormatOf(ReadOnlySpan<byte> content) => content switch
    {
        _ when IsSvg(content) => EngineImageFormat.Svg,
        _ when content.Length >= 44 && content.StartsWith((ReadOnlySpan<byte>)[0x01, 0x00, 0x00, 0x00])
            && content[40..44].SequenceEqual(" EMF"u8) => EngineImageFormat.Emf,
        _ when content.StartsWith((ReadOnlySpan<byte>)[0xD7, 0xCD, 0xC6, 0x9A])
            || (content.Length >= 6
                && (content.StartsWith((ReadOnlySpan<byte>)[0x01, 0x00, 0x09, 0x00]) || content.StartsWith((ReadOnlySpan<byte>)[0x02, 0x00, 0x09, 0x00]))
                && content[5] is 0x01 or 0x03 && content[4] == 0x00) => EngineImageFormat.Wmf,
        _ when content.StartsWith((ReadOnlySpan<byte>)[(byte)'I', (byte)'I', 0x2A, 0x00])
            || content.StartsWith((ReadOnlySpan<byte>)[(byte)'M', (byte)'M', 0x00, 0x2A])
            || content.StartsWith((ReadOnlySpan<byte>)[(byte)'I', (byte)'I', 0x2B, 0x00])
            || content.StartsWith((ReadOnlySpan<byte>)[(byte)'M', (byte)'M', 0x00, 0x2B]) => EngineImageFormat.Tiff,
        _ when content.StartsWith((ReadOnlySpan<byte>)[0x00, 0x00, 0x00, 0x0C, 0x6A, 0x50, 0x20, 0x20, 0x0D, 0x0A, 0x87, 0x0A])
            || content.StartsWith((ReadOnlySpan<byte>)[0xFF, 0x4F, 0xFF, 0x51]) => EngineImageFormat.Jpeg2000,
        _ when content.Length >= 526
            && (content[522..526].SequenceEqual((ReadOnlySpan<byte>)[0x00, 0x11, 0x02, 0xFF])
                || content[522..524].SequenceEqual((ReadOnlySpan<byte>)[0x11, 0x01])) => EngineImageFormat.Pict,
        _ when content.Length >= 132 && content[128..132].SequenceEqual("DICM"u8) => EngineImageFormat.Dicom,
        _ => EngineImageFormat.None,
    };

    // Markup with an svg element. Latin-1 keeps every ASCII token intact, and removing NUL
    // exposes those of unmarked UTF-16.
    private static bool IsSvg(ReadOnlySpan<byte> content) =>
        NetworkReferenceGuard.IsMarkup(content)
        && SvgRoot.IsMatch(Encoding.Latin1.GetString(content).Replace("\0", "", StringComparison.Ordinal));
}
