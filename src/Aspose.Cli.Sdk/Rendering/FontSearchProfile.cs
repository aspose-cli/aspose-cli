using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace Aspose.Cli.Sdk.Rendering;

/// <summary>Validated font roots shared by bounded visual commands.</summary>
public sealed record FontSearchProfile
{
    private const int MaximumCandidates = 4096;
    private const long MaximumFontBytes = 64L * 1024 * 1024;
    private const long MaximumProfileBytes = 1024L * 1024 * 1024;

    public static FontSearchProfile Ambient { get; } = CreateAmbient();

    public required IReadOnlyList<string> Directories { get; init; }

    public required bool UseAmbientSystemFonts { get; init; }

    public required string Fingerprint { get; init; }

    [JsonIgnore]
    public IReadOnlyDictionary<string, string> FileFingerprints { get; init; } =
        new Dictionary<string, string>();

    public bool IsAmbient => Directories.Count == 0 && UseAmbientSystemFonts;

    public static FontSearchProfile Explicit(IReadOnlyList<string> directories)
    {
        ArgumentNullException.ThrowIfNull(directories);
        (string fingerprint, IReadOnlyDictionary<string, string> files) =
            CaptureExplicit(directories);
        return new FontSearchProfile
        {
            Directories = Array.AsReadOnly(directories.ToArray()),
            UseAmbientSystemFonts = false,
            Fingerprint = fingerprint,
            FileFingerprints = files,
        };
    }

    public FontSearchProfile? CaptureCurrent()
    {
        if (IsAmbient)
        {
            return this;
        }
        try
        {
            FontSearchProfile current = Explicit(Directories);
            return string.Equals(Fingerprint, current.Fingerprint, StringComparison.Ordinal)
                ? current
                : null;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public bool IsCurrent() => CaptureCurrent() is not null;

    private static (string Fingerprint, IReadOnlyDictionary<string, string> Files)
        CaptureExplicit(IReadOnlyList<string> directories)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var fingerprints = new Dictionary<string, string>(
            OperatingSystem.IsWindows()
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal);
        hash.AppendData("explicit\0"u8);
        long totalBytes = 0;
        int candidates = 0;
        for (int rootIndex = 0; rootIndex < directories.Count; rootIndex++)
        {
            string root = directories[rootIndex];
            hash.AppendData([(byte)rootIndex]);
            hash.AppendData(Encoding.UTF8.GetBytes(Normalize(root)));
            hash.AppendData([0]);

            string[] files = Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly)
                .Where(IsFontFile)
                .OrderBy(Normalize, StringComparer.Ordinal)
                .ToArray();
            if (candidates + files.Length > MaximumCandidates)
            {
                throw new InvalidDataException(
                    $"Explicit font roots exceed the {MaximumCandidates} file budget.");
            }

            foreach (string file in files)
            {
                candidates++;
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidDataException("Explicit font files must not be reparse points.");
                }

                var info = new FileInfo(file);
                if (info.Length > MaximumFontBytes
                    || totalBytes > MaximumProfileBytes - info.Length)
                {
                    throw new InvalidDataException("Explicit font roots exceed the byte budget.");
                }
                totalBytes += info.Length;
                hash.AppendData(Encoding.UTF8.GetBytes(Normalize(Path.GetFileName(file))));
                hash.AppendData([0]);
                using var stream = new FileStream(
                    file,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    64 * 1024,
                    FileOptions.SequentialScan);
                byte[] fileHash = SHA256.HashData(stream);
                string canonicalPath = Path.GetFullPath(file);
                fingerprints.Add(
                    canonicalPath,
                    Convert.ToHexString(fileHash).ToLowerInvariant());
                hash.AppendData(fileHash);
                hash.AppendData([0]);
            }
        }
        return (
            Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(),
            fingerprints);
    }

    private static FontSearchProfile CreateAmbient()
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData("ambient\0"u8);
        var roots = new HashSet<string>(
            OperatingSystem.IsWindows()
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal);
        AddRoot(roots, Environment.GetFolderPath(Environment.SpecialFolder.Fonts));
        string? windows = Environment.GetEnvironmentVariable("WINDIR");
        if (!string.IsNullOrWhiteSpace(windows))
        {
            AddRoot(roots, Path.Combine(windows, "Fonts"));
        }
        foreach (string root in roots.OrderBy(Normalize, StringComparer.Ordinal))
        {
            hash.AppendData(Encoding.UTF8.GetBytes(Normalize(root)));
            hash.AppendData([0]);
            try
            {
                string[] files = Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly)
                    .Where(IsFontFile)
                    .Take(MaximumCandidates + 1)
                    .ToArray();
                if (files.Length > MaximumCandidates)
                {
                    hash.AppendData("overflow\0"u8);
                    hash.AppendData(Encoding.UTF8.GetBytes(
                        Directory.GetLastWriteTimeUtc(root).Ticks.ToString(
                            CultureInfo.InvariantCulture)));
                    continue;
                }
                foreach (string file in files.OrderBy(Normalize, StringComparer.Ordinal))
                {
                    var info = new FileInfo(file);
                    hash.AppendData(Encoding.UTF8.GetBytes(Normalize(Path.GetFileName(file))));
                    hash.AppendData(Encoding.UTF8.GetBytes(
                        info.Length.ToString(CultureInfo.InvariantCulture)));
                    hash.AppendData([0]);
                    hash.AppendData(Encoding.UTF8.GetBytes(
                        info.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture)));
                    hash.AppendData([0]);
                }
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                hash.AppendData("unavailable\0"u8);
            }
        }
        return new FontSearchProfile
        {
            Directories = [],
            UseAmbientSystemFonts = true,
            Fingerprint = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(),
        };
    }

    private static void AddRoot(ISet<string> roots, string? path)
    {
        if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
        {
            roots.Add(Path.GetFullPath(path));
        }
    }

    private static bool IsFontFile(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Equals(".ttf", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".otf", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".ttc", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".otc", StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string value) =>
        OperatingSystem.IsWindows() ? value.ToUpperInvariant() : value;
}
