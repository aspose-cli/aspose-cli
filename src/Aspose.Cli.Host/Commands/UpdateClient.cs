using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Release;
using Aspose.Cli.Sdk;

namespace Aspose.Cli.Host.Commands;

/// <summary>Resolves and verifies one explicit release feed.</summary>
internal static class UpdateClient
{
    private const int MaximumManifestBytes = 64 * 1024;
    private const long MaximumArchiveBytes = 1L * 1024 * 1024 * 1024;
    private const int MaximumZipEntries = 4096;

    public static UpdateResult Check(CommandContext context, string feed)
    {
        using var files = FeedFiles.Open(feed, context.Paths.BaseDirectory);
        var manifest = Verify(files);
        int comparison = CompareVersions(manifest);

        return new UpdateResult
        {
            Status = comparison == 0 ? "up-to-date" : "available",
            Edition = DistributionInfo.Edition,
            CurrentVersion = VersionInfo.ArtifactVersion,
            AvailableVersion = comparison == 0 ? null : manifest.ArtifactVersion,
            SourceRevision = manifest.SourceRevision,
            Feed = feed,
            ArchiveSha256 = manifest.ArchiveSha256,
        };
    }

    public static UpdateResult Install(CommandContext context, string feed)
    {
        if (!OperatingSystem.IsWindows() || !string.Equals(RuntimeInformation.RuntimeIdentifier, "win-x64", StringComparison.OrdinalIgnoreCase))
        {
            throw CliErrors.OptionInvalid("update install", "the customer updater currently supports Windows x64 only", "Run the win-x64 distribution on Windows, or use update check for feed verification.");
        }

        using var files = FeedFiles.Open(feed, context.Paths.BaseDirectory);
        var manifest = Verify(files);
        if (CompareVersions(manifest) == 0)
        {
            return new UpdateResult
            {
                Status = "up-to-date",
                Edition = DistributionInfo.Edition,
                CurrentVersion = VersionInfo.ArtifactVersion,
                SourceRevision = manifest.SourceRevision,
                Feed = feed,
                ArchiveSha256 = manifest.ArchiveSha256,
            };
        }

        string powerShellPath = ResolveWindowsPowerShell();
        string package = files.DownloadArchive(manifest);
        string? extracted = null;
        bool handedOff = false;
        try
        {
            extracted = ExtractPackage(package);
            string installRoot = Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            int processId = HandoffToInstaller(
                powerShellPath,
                extracted,
                installRoot);
            handedOff = true;
            return new UpdateResult
            {
                Status = "pending",
                Edition = DistributionInfo.Edition,
                CurrentVersion = VersionInfo.ArtifactVersion,
                AvailableVersion = manifest.ArtifactVersion,
                SourceRevision = manifest.SourceRevision,
                Feed = feed,
                ArchiveSha256 = manifest.ArchiveSha256,
                ProcessId = processId,
            };
        }
        finally
        {
            if (!handedOff && extracted is not null)
            {
                DeleteTree(extracted);
            }
            TryDeleteFile(package);
        }
    }

    private static ReleaseManifestInfo Verify(FeedFiles files)
    {
        try
        {
            var keys = ReleaseManifestVerifier.LoadConfiguredKeyRing();
            if (keys.Count == 0)
            {
                throw CliErrors.ReleaseTrustUnavailable("the trusted public-key ring is empty");
            }

            return ReleaseManifestVerifier.Verify(files.ManifestPath, keys, files.SignaturePath, expectedEdition: DistributionInfo.Edition, expectedRuntimeIdentifier: "win-x64");
        }
        catch (CliException) { throw; }
        catch (ReleaseVerificationException exception) when (!exception.TrustedKeysConfigured)
        {
            throw CliErrors.ReleaseTrustUnavailable(exception.Message);
        }
        catch (ReleaseVerificationException exception)
        {
            throw CliErrors.ReleaseVerificationFailed(exception.Message);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            throw CliErrors.ReleaseVerificationFailed(exception.Message);
        }
    }

    private static int CompareVersions(ReleaseManifestInfo candidate) =>
        CompareUpdateCandidate(
            candidate.ArtifactVersion,
            candidate.SourceRevision,
            VersionInfo.ArtifactVersion,
            VersionInfo.SourceRevision);

    internal static int CompareUpdateCandidate(
        string candidateVersion,
        string candidateRevision,
        string currentVersion,
        string currentRevision)
    {
        int comparison = CompareArtifactIdentity(
            candidateVersion,
            candidateRevision,
            currentVersion,
            currentRevision);
        if (comparison < 0)
        {
            throw CliErrors.ReleaseVerificationFailed(
                $"the feed version '{candidateVersion}' is older than the installed version '{currentVersion}'");
        }
        return comparison;
    }

    internal static int CompareArtifactIdentity(
        string candidateVersion,
        string candidateRevision,
        string currentVersion,
        string currentRevision)
    {
        SemanticVersion candidate = SemanticVersion.Parse(candidateVersion);
        SemanticVersion current = SemanticVersion.Parse(currentVersion);
        int precedence = candidate.CompareTo(current);
        if (precedence != 0)
        {
            return precedence;
        }

        if (string.Equals(candidateVersion, currentVersion, StringComparison.Ordinal)
            && string.Equals(candidateRevision, currentRevision, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        throw CliErrors.ReleaseVerificationFailed(
            "the feed and installed versions have equal semantic precedence but different immutable identities; publish a higher semantic version");
    }

    internal static string ResolveWindowsPowerShell()
    {
        string systemDirectory = Path.GetFullPath(Environment.SystemDirectory);
        string executable = Path.GetFullPath(Path.Combine(
            systemDirectory,
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe"));
        if (!Path.IsPathFullyQualified(executable)
            || !executable.StartsWith(systemDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || !File.Exists(executable)
            || (File.GetAttributes(executable) & FileAttributes.ReparsePoint) != 0)
        {
            throw CliErrors.ReleaseVerificationFailed("the trusted Windows PowerShell executable is unavailable");
        }

        return executable;
    }

    internal static int HandoffToInstaller(
        string powerShellPath,
        string extracted,
        string installRoot)
    {
        bool started = false;
        Process? process = null;
        try
        {
            var start = new ProcessStartInfo(powerShellPath)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            foreach (string argument in new[]
            {
                "-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                "-File", Path.Combine(extracted, "install.ps1"),
                "-PackageRoot", extracted,
                "-InstallDirectory", installRoot,
                "-SkipLicensePrompt",
                "-WaitForProcessId", Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "-CleanupRoot", extracted,
            })
            {
                start.ArgumentList.Add(argument);
            }

            process = Process.Start(start)
                ?? throw CliErrors.ReleaseVerificationFailed("the installer process could not be started");
            started = true;
            return process.Id;
        }
        catch
        {
            if (!started)
            {
                DeleteTree(extracted);
            }
            throw;
        }
        finally
        {
            process?.Dispose();
        }
    }

    private static string ExtractPackage(string archive)
    {
        string root = Path.Combine(Path.GetTempPath(), "aspose-cli-update-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var zip = ZipFile.OpenRead(archive);
            if (zip.Entries.Count is 0 or > MaximumZipEntries)
            {
                throw CliErrors.ReleaseVerificationFailed("the update archive has an invalid entry count");
            }
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long total = 0;
            foreach (var entry in zip.Entries)
            {
                bool directory = entry.FullName.EndsWith('/');
                string relative = ValidateZipPath(
                    directory ? entry.FullName.TrimEnd('/') : entry.FullName);
                if (!paths.Add(relative))
                {
                    throw CliErrors.ReleaseVerificationFailed($"the update archive contains duplicate entry '{relative}'");
                }
                if (directory)
                {
                    Directory.CreateDirectory(Path.Combine(root, relative));
                    continue;
                }
                total = checked(total + entry.Length);
                if (total > MaximumArchiveBytes)
                {
                    throw CliErrors.ReleaseVerificationFailed("the update archive exceeds its decompressed size budget");
                }
                string destination = Path.Combine(root, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                using var input = entry.Open();
                using FileStream output = new(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                long copied = CopyBounded(
                    input,
                    output,
                    MaximumArchiveBytes - (total - entry.Length));
                if (copied != entry.Length)
                {
                    throw CliErrors.ReleaseVerificationFailed($"the update archive entry '{relative}' has an invalid decompressed size");
                }
            }

            foreach (string required in new[] { "aspose-cli.exe", "install.ps1", "SHA256SUMS" })
            {
                if (!File.Exists(Path.Combine(root, required)))
                {
                    throw CliErrors.ReleaseVerificationFailed($"the update archive is missing '{required}'");
                }
            }
            return root;
        }
        catch
        {
            DeleteTree(root);
            throw;
        }
    }

    private static string ValidateZipPath(string value)
    {
        string normalized = value.Replace('\\', '/');
        string[] segments = normalized.Split('/');
        string[] reserved = ["CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"];
        if (normalized.Length is 0 or > 512
            || normalized.StartsWith('/')
            || normalized.Contains(":", StringComparison.Ordinal)
            || segments.Any(segment => segment.Length is 0 or > 255
                || segment is "." or ".."
                || segment.EndsWith('.')
                || segment.EndsWith(' ')
                || reserved.Contains(segment.Split('.')[0].ToUpperInvariant(), StringComparer.Ordinal)
                || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
        {
            throw CliErrors.ReleaseVerificationFailed($"the update archive contains an unsafe path '{value}'");
        }
        return normalized;
    }

    private static long CopyBounded(Stream input, Stream output, long maximum)
    {
        byte[] buffer = new byte[64 * 1024];
        long total = 0;
        while (true)
        {
            int read = input.Read(buffer, 0, buffer.Length);
            if (read == 0)
            {
                return total;
            }
            total = checked(total + read);
            if (total > maximum)
            {
                throw CliErrors.ReleaseVerificationFailed("the update archive decompressed beyond its safety budget");
            }
            output.Write(buffer, 0, read);
        }
    }

    private static void DeleteTree(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch
        {
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private sealed class FeedFiles : IDisposable
    {
        private readonly string? _temporaryRoot;
        private readonly Uri? _remoteBase;

        private FeedFiles(string manifestPath, string signaturePath, string? temporaryRoot, Uri? remoteBase)
        {
            ManifestPath = manifestPath;
            SignaturePath = signaturePath;
            _temporaryRoot = temporaryRoot;
            _remoteBase = remoteBase;
        }

        public string ManifestPath { get; }
        public string SignaturePath { get; }

        public static FeedFiles Open(string feed, string baseDirectory)
        {
            if (!Path.IsPathRooted(feed)
                && Uri.TryCreate(feed, UriKind.Absolute, out var uri))
            {
                ValidateHttpsFeedUri(uri);

                string root = Path.Combine(Path.GetTempPath(), "aspose-cli-feed-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(root);
                try
                {
                    string manifest = Path.Combine(root, "RELEASE-MANIFEST.json");
                    Download(uri, manifest, MaximumManifestBytes);
                    string signature = Path.Combine(root, "RELEASE-MANIFEST.sig");
                    Download(new Uri(uri, "RELEASE-MANIFEST.sig"), signature, 16 * 1024);
                    return new FeedFiles(manifest, signature, root, uri);
                }
                catch
                {
                    DeleteTree(root);
                    throw;
                }
            }

            string path = Path.GetFullPath(feed, baseDirectory);
            if (Directory.Exists(path))
            {
                path = Path.Combine(path, "RELEASE-MANIFEST.json");
            }
            EnsureLocal(path);
            string signaturePath = Path.Combine(Path.GetDirectoryName(path)!, "RELEASE-MANIFEST.sig");
            EnsureLocal(signaturePath);
            return new FeedFiles(path, signaturePath, null, null);
        }

        public string DownloadArchive(ReleaseManifestInfo manifest)
        {
            string target = Path.Combine(Path.GetTempPath(), "aspose-cli-update-" + Guid.NewGuid().ToString("N") + ".zip");
            try
            {
                if (_remoteBase is not null)
                {
                    if (manifest.ArchivePath.Contains("..", StringComparison.Ordinal))
                    {
                        throw CliErrors.ReleaseVerificationFailed("the feed archive path is unsafe");
                    }
                    Download(new Uri(new Uri(_remoteBase, "."), manifest.ArchivePath), target, MaximumArchiveBytes);
                }
                else
                {
                    string source = Path.Combine(Path.GetDirectoryName(ManifestPath)!, manifest.ArchivePath.Replace('/', Path.DirectorySeparatorChar));
                    EnsureLocal(source);
                    CopyBounded(source, target, manifest.ArchiveSize);
                }
                if (new FileInfo(target).Length != manifest.ArchiveSize
                    || !string.Equals(ComputeSha256(target), manifest.ArchiveSha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw CliErrors.ReleaseVerificationFailed("the downloaded archive hash does not match the signed manifest");
                }
                return target;
            }
            catch
            {
                TryDeleteFile(target);
                throw;
            }
        }

        public void Dispose()
        {
            if (_temporaryRoot is not null)
            {
                DeleteTree(_temporaryRoot);
            }
        }

        private static void Download(Uri uri, string destination, long maximum)
        {
            ValidateHttpsFeedUri(uri);
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
            using var response = client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
            if (response.StatusCode != HttpStatusCode.OK
                || response.Content.Headers.ContentLength is > 0 and var length && length > maximum)
            {
                throw CliErrors.ReleaseVerificationFailed($"HTTPS feed returned {(int)response.StatusCode} or exceeded its size budget");
            }
            try
            {
                using var input = response.Content.ReadAsStream();
                using FileStream output = new(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                CopyBounded(input, output, maximum);
            }
            catch
            {
                TryDeleteFile(destination);
                throw;
            }
        }

        private static void CopyBounded(string source, string destination, long expected)
        {
            using var input = File.OpenRead(source);
            using FileStream output = new(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            CopyBounded(input, output, expected);
        }

        private static void CopyBounded(Stream input, Stream output, long maximum)
        {
            byte[] buffer = new byte[64 * 1024];
            long total = 0;
            while (true)
            {
                int read = input.Read(buffer);
                if (read == 0)
                {
                    break;
                }

                total = checked(total + read);
                if (total > maximum)
                {
                    throw CliErrors.ReleaseVerificationFailed("feed download exceeds its size budget");
                }

                output.Write(buffer, 0, read);
            }
        }

        private static string ComputeSha256(string path)
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream));
        }

        private static void EnsureLocal(string path)
        {
            string full = Path.GetFullPath(path);
            if (full.StartsWith("\\\\", StringComparison.Ordinal)
                || full.StartsWith("\\\\?\\", StringComparison.Ordinal)
                || full.StartsWith("\\\\.\\", StringComparison.Ordinal))
            {
                throw CliErrors.OptionInvalid("feed", "UNC and device paths are not accepted", "Use a local fixed-disk feed path.");
            }
            string root = Path.GetPathRoot(full)
                ?? throw CliErrors.OptionInvalid("feed", "the feed path has no local drive root", "Use a local fixed-disk feed path.");
            if (new DriveInfo(root).DriveType != DriveType.Fixed)
            {
                throw CliErrors.OptionInvalid("feed", "the feed must be on a fixed local disk", "Use a local fixed-disk feed path.");
            }
            string? cursor = full;
            while (cursor is not null)
            {
                if ((File.Exists(cursor) || Directory.Exists(cursor))
                    && (File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0)
                {
                    throw CliErrors.OptionInvalid("feed", "the feed path traverses a reparse point", "Use a stable local directory.");
                }
                string? parent = Path.GetDirectoryName(cursor);
                if (parent == cursor)
                {
                    break;
                }

                cursor = parent;
            }
            if (!File.Exists(full))
            {
                throw CliErrors.ReleaseVerificationFailed($"feed file not found: {full}");
            }
        }
    }

    internal static void ValidateHttpsFeedUri(Uri uri)
    {
        if (!uri.IsAbsoluteUri
            || uri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw CliErrors.OptionInvalid(
                "feed",
                "only credential-free HTTPS URLs without query strings or fragments are accepted",
                "Use a plain HTTPS manifest URL or a local manifest path.");
        }
    }

    private readonly record struct SemanticVersion(
        string[] Core,
        string[] PreRelease) : IComparable<SemanticVersion>
    {
        public static SemanticVersion Parse(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 256)
            {
                throw Invalid(value);
            }

            int plus = value.IndexOf('+');
            if (plus >= 0)
            {
                if (plus == value.Length - 1
                    || value.IndexOf('+', plus + 1) >= 0
                    || !ValidIdentifiers(value[(plus + 1)..], allowLeadingZero: true))
                {
                    throw Invalid(value);
                }
                value = value[..plus];
            }

            int dash = value.IndexOf('-');
            string[] preRelease = [];
            if (dash >= 0)
            {
                if (dash == value.Length - 1
                    || !ValidIdentifiers(value[(dash + 1)..], allowLeadingZero: false))
                {
                    throw Invalid(value);
                }
                preRelease = value[(dash + 1)..].Split('.');
                value = value[..dash];
            }

            string[] core = value.Split('.');
            if (core.Length != 3 || core.Any(static part =>
                    part.Length == 0
                    || part.Length > 1 && part[0] == '0'
                    || part.Any(static c => c is < '0' or > '9')))
            {
                throw Invalid(value);
            }

            return new SemanticVersion(core, preRelease);
        }

        public int CompareTo(SemanticVersion other)
        {
            for (int index = 0; index < Core.Length; index++)
            {
                int comparison = CompareNumeric(Core[index], other.Core[index]);
                if (comparison != 0)
                {
                    return comparison;
                }
            }

            if (PreRelease.Length == 0 || other.PreRelease.Length == 0)
            {
                return PreRelease.Length == other.PreRelease.Length
                    ? 0
                    : PreRelease.Length == 0 ? 1 : -1;
            }

            int shared = Math.Min(PreRelease.Length, other.PreRelease.Length);
            for (int index = 0; index < shared; index++)
            {
                string left = PreRelease[index];
                string right = other.PreRelease[index];
                bool leftNumeric = left.All(static c => c is >= '0' and <= '9');
                bool rightNumeric = right.All(static c => c is >= '0' and <= '9');
                int comparison = leftNumeric && rightNumeric
                    ? CompareNumeric(left, right)
                    : leftNumeric != rightNumeric
                        ? leftNumeric ? -1 : 1
                        : string.CompareOrdinal(left, right);
                if (comparison != 0)
                {
                    return comparison;
                }
            }
            return PreRelease.Length.CompareTo(other.PreRelease.Length);
        }

        private static int CompareNumeric(string left, string right) =>
            left.Length != right.Length
                ? left.Length.CompareTo(right.Length)
                : string.CompareOrdinal(left, right);

        private static bool ValidIdentifiers(string value, bool allowLeadingZero) =>
            value.Split('.').All(identifier =>
                identifier.Length > 0
                && identifier.All(static c =>
                    c is >= '0' and <= '9'
                        or >= 'A' and <= 'Z'
                        or >= 'a' and <= 'z'
                        or '-')
                && (allowLeadingZero
                    || identifier.Length == 1
                    || identifier[0] != '0'
                    || identifier.Any(static c => c is < '0' or > '9')));

        private static CliException Invalid(string value) =>
            CliErrors.ReleaseVerificationFailed($"version '{value}' is not a supported semantic version");
    }
}
