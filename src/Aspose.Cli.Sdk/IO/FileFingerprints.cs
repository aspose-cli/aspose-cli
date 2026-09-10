using System.Security.Cryptography;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Execution;

namespace Aspose.Cli.Sdk.IO;

/// <summary>Captures and checks canonical SHA-256 file identities.</summary>
public static class FileFingerprints
{
    /// <summary>Hashes the complete file through a bounded admitted path.</summary>
    public static FileFingerprint Capture(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);
        string readPath = WorkerOutputSession.ResolveReadPath(fullPath);
        try
        {
            using var stream = new FileStream(
                readPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read | FileShare.Delete,
                bufferSize: 128 * 1024,
                FileOptions.SequentialScan);
            return new FileFingerprint
            {
                Sha256 = Convert.ToHexString(SHA256.HashData(stream))
                    .ToLowerInvariant(),
            };
        }
        catch (Exception exception) when (
            exception is FileNotFoundException or DirectoryNotFoundException)
        {
            throw CliErrors.FileNotFound(fullPath);
        }
        catch (UnauthorizedAccessException)
        {
            throw CliErrors.FileAccessDenied(fullPath);
        }
        catch (IOException)
        {
            throw File.Exists(fullPath)
                ? CliErrors.FileLocked(fullPath)
                : CliErrors.FileNotFound(fullPath);
        }
    }

    /// <summary>Rejects a stale or malformed edit precondition.</summary>
    public static void EnsureMatch(
        string path,
        string? expectedSha256,
        FileFingerprint actual)
    {
        ArgumentNullException.ThrowIfNull(actual);
        if (expectedSha256 is null)
        {
            return;
        }

        string expected = expectedSha256.Trim();
        if (expected.Length != 64
            || expected.Any(static character => !char.IsAsciiHexDigit(character)))
        {
            throw CliErrors.OptionInvalid(
                "--if-match",
                "the fingerprint must be exactly 64 hexadecimal SHA-256 characters",
                "Copy source.fingerprint.sha256 from a current inspect or query result.");
        }

        if (!string.Equals(
                expected,
                actual.Sha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw CliErrors.InputChanged(
                Path.GetFullPath(path),
                expected.ToLowerInvariant(),
                actual.Sha256);
        }
    }

    /// <summary>Rejects a source that changed while its product engine loaded it.</summary>
    public static void EnsureUnchanged(
        string path,
        FileFingerprint before,
        FileFingerprint after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        if (!string.Equals(
                before.Sha256,
                after.Sha256,
                StringComparison.Ordinal))
        {
            throw CliErrors.InputChanged(
                Path.GetFullPath(path),
                before.Sha256,
                after.Sha256);
        }
    }
}
