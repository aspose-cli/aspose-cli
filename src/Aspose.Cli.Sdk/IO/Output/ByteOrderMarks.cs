using System.Buffers;

namespace Aspose.Cli.Sdk.IO.Output;

/// <summary>
/// Whether a file the write pipeline publishes, an extraction's files included, starts with the
/// UTF-8 byte order mark: only when its write declares the mark, as a CSV or TSV output does that
/// the caller asked to carry one or that is edited back from an input that carried one, or when it
/// is a file a document embeds, whose bytes are copied as the document stores them. A mark an
/// engine writes otherwise, such as at the start of text, Markdown, HTML or XFDF, is removed from
/// the staged file before the file is inspected, fingerprinted and published, so text a command
/// writes is UTF-8 without a mark. No format table decides it: no binary format starts with the
/// mark's three bytes.
/// </summary>
internal static class ByteOrderMarks
{
    private static ReadOnlySpan<byte> Utf8 => [0xEF, 0xBB, 0xBF];

    /// <summary><paramref name="write"/>, then the removal of a mark it wrote unless <paramref name="declared"/>.</summary>
    internal static Action<string> Unless(bool declared, Action<string> write)
    {
        ArgumentNullException.ThrowIfNull(write);
        return declared ? write : path =>
        {
            write(path);
            using var file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            Unless(declared: false, file);
        };
    }

    /// <summary>
    /// Removes the mark a file written through <paramref name="written"/>, open to read and write,
    /// starts with, unless <paramref name="declared"/>.
    /// </summary>
    internal static void Unless(bool declared, FileStream written)
    {
        ArgumentNullException.ThrowIfNull(written);
        if (!declared && StartsWithMark(written))
        {
            Shift(written);
        }
    }

    private static bool StartsWithMark(FileStream file)
    {
        file.Position = 0;
        Span<byte> head = stackalloc byte[3];
        return file.ReadAtLeast(head, head.Length, throwOnEndOfStream: false) == head.Length && head.SequenceEqual(Utf8);
    }

    /// <summary>Moves what follows the mark over it in place, without reading the file whole.</summary>
    private static void Shift(FileStream file)
    {
        file.Position = Utf8.Length;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            long kept = 0;
            for (int read; (read = file.Read(buffer)) > 0; kept += read)
            {
                file.Position = kept;
                file.Write(buffer, 0, read);
                file.Position = kept + read + Utf8.Length;
            }

            file.SetLength(kept);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
