using System.Security.Cryptography;

namespace Aspose.Cli.TestKit;

/// <summary>Content identity of files a test compares or expects the CLI to report.</summary>
public static class FileHashes
{
    /// <summary>The lowercase hexadecimal SHA-256 of a file, the spelling the CLI reports.</summary>
    public static string Sha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }
}
