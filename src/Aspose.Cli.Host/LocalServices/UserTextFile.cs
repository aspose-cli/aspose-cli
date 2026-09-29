using System.Text;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Host.LocalServices;

/// <summary>
/// Writes the small UTF-8 state, marker and log files the Host keeps in the current user's
/// configuration and temporary directories. Those directories' own per-user permissions
/// protect the files; this helper sets no permissions of its own.
/// </summary>
internal static class UserTextFile
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Replaces a file atomically: the text is written and flushed to a temporary file in the
    /// same directory, which is then renamed over the target, waiting briefly out a scanner's lock.
    /// </summary>
    public static void Replace(string path, string contents)
    {
        ArgumentNullException.ThrowIfNull(contents);
        string full = Path.GetFullPath(path);
        string directory = Directory.CreateDirectory(Path.GetDirectoryName(full)!).FullName;
        string temporary = Path.Combine(directory, $".{Path.GetFileName(full)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(
                       temporary,
                       FileMode.CreateNew,
                       FileAccess.ReadWrite,
                       FileShare.None,
                       bufferSize: 4096,
                       FileOptions.WriteThrough))
            {
                stream.Write(Utf8NoBom.GetBytes(contents));
                stream.Flush(flushToDisk: true);
            }

            AtomicFileRename.Move(temporary, full, overwrite: true);
        }
        finally
        {
            LocalFileCleanup.DeleteFile(temporary);
        }
    }

    /// <summary>
    /// Appends one line to a bounded log. A file already over <paramref name="maximumBytes"/>
    /// is atomically emptied before the append.
    /// </summary>
    public static void AppendLine(string path, string line, long maximumBytes)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        string full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var existing = new FileInfo(full);
        if (existing.Exists && existing.Length > maximumBytes)
        {
            Replace(full, string.Empty);
        }

        using var stream = new FileStream(full, FileMode.Append, FileAccess.Write, FileShare.Read);
        stream.Write(Utf8NoBom.GetBytes(line + Environment.NewLine));
        stream.Flush(flushToDisk: true);
    }
}
