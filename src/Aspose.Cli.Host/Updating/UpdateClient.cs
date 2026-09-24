using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk;
using Aspose.Cli.Sdk.Execution;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Host.Updating;

/// <summary>Resolves and verifies one explicit release feed.</summary>
internal static class UpdateClient
{
    private const int MaximumManifestBytes = 64 * 1024;
    internal const long MaximumArchiveBytes = 1L * 1024 * 1024 * 1024;
    private const int MaximumZipEntries = 4096;

    public static UpdateResult Check(CommandContext context, string feed)
    {
        using var files = FeedFiles.Open(feed, context.Paths.BaseDirectory, context.ResourceBudgets);
        var manifest = Verify(files);
        return WithLastUpdateWarning(Describe(manifest, feed, CompareVersions(manifest) == 0));
    }

    /// <summary>Adds the outcome of a failed or unfinished earlier installer run, if any.</summary>
    internal static UpdateResult WithLastUpdateWarning(UpdateResult result)
    {
        Warning? warning = UpdateStatus.ReadWarning(UpdateStatus.PathFor(AppContext.BaseDirectory));
        return warning is null ? result : result with { Warnings = [.. result.Warnings ?? [], warning] };
    }

    /// <summary>Produces a verified package; only the owning parent can launch its installer.</summary>
    internal static UpdateResult Prepare(CommandContext context, string feed, string outputDirectory)
    {
        using var files = FeedFiles.Open(feed, context.Paths.BaseDirectory, context.ResourceBudgets);
        ReleaseManifestInfo manifest = Verify(files);
        bool current = CompareVersions(manifest) == 0;
        if (!current)
        {
            string package = files.DownloadArchive(manifest);
            using var publication = new AtomicNewDirectoryWriter(context.ResourceBudgets, outputDirectory, "update-package");
            ExtractPackage(package, publication.StagingDirectory, context.Deadline);
            publication.Commit();
        }
        return Describe(manifest, feed, current);
    }

    private static UpdateResult Describe(ReleaseManifestInfo manifest, string feed, bool current) =>
        new()
        {
            Status = current ? "up-to-date" : "available",
            Edition = DistributionInfo.Edition,
            CurrentVersion = VersionInfo.ArtifactVersion,
            AvailableVersion = current ? null : manifest.ArtifactVersion,
            SourceRevision = manifest.SourceRevision,
            Feed = feed,
            ArchiveSha256 = manifest.ArchiveSha256,
        };

    private static ReleaseManifestInfo Verify(FeedFiles files)
    {
        try
        {
            var keys = ReleaseManifestVerifier.LoadConfiguredKeyRing();
            if (keys.Count == 0)
            {
                throw ReleaseErrors.TrustUnavailable("the trusted public-key ring is empty");
            }

            return ReleaseManifestVerifier.Verify(files.ManifestPath, keys, files.SignaturePath, expectedEdition: DistributionInfo.Edition, expectedRuntimeIdentifier: "win-x64");
        }
        catch (CliException) { throw; }
        catch (ReleaseVerificationException exception) when (!exception.TrustedKeysConfigured)
        {
            throw ReleaseErrors.TrustUnavailable(exception.Message);
        }
        catch (ReleaseVerificationException exception)
        {
            throw ReleaseErrors.VerificationFailed(exception.Message);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            throw ReleaseErrors.VerificationFailed(exception.Message);
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
            throw ReleaseErrors.VerificationFailed(
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

        throw ReleaseErrors.VerificationFailed(
            "the feed and installed versions have equal semantic precedence but different immutable identities; publish a higher semantic version");
    }

    internal static int HandoffToInstaller(
        string powerShellPath,
        string extracted,
        string installRoot,
        string statusPath,
        OperationDeadline? deadline = null)
    {
        bool started = false;
        Process? process = null;
        try
        {
            var start = new ProcessStartInfo(powerShellPath)
            {
                // Detach standard handles as well as lifetime: the installer must not keep CLI result pipes open.
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            foreach (string argument in new[]
            {
                "-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                "-File", Path.Combine(extracted, "install.ps1"),
                "-PackageRoot", extracted,
                // The installer owns the one spelling of the directory; a trailing separator
                // would otherwise reach its PATH entry.
                "-InstallDirectory", Path.TrimEndingDirectorySeparator(installRoot),
                // Update replays the choices the installation was made with (PATH, Skills, MCP)
                // and never prompts.
                "-Update",
                "-WaitForProcessId", Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "-CleanupRoot", extracted,
                "-StatusPath", statusPath,
            })
            {
                start.ArgumentList.Add(argument);
            }

            deadline?.ThrowIfExpired("update-installer-start");
            process = Process.Start(start)
                ?? throw ReleaseErrors.VerificationFailed("the installer process could not be started");
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

    private static void ExtractPackage(string archive, string root, OperationDeadline deadline)
    {
        using var zip = ZipFile.OpenRead(archive);
        if (zip.Entries.Count is 0 or > MaximumZipEntries)
        {
            throw ReleaseErrors.VerificationFailed("the update archive has an invalid entry count");
        }
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        foreach (var entry in zip.Entries)
        {
            deadline.ThrowIfExpired("update-extract");
            bool directory = entry.FullName.EndsWith('/');
            string relative = ValidateZipPath(
                directory ? entry.FullName.TrimEnd('/') : entry.FullName);
            if (!paths.Add(relative))
            {
                throw ReleaseErrors.VerificationFailed($"the update archive contains duplicate entry '{relative}'");
            }
            if (directory)
            {
                Directory.CreateDirectory(Path.Combine(root, relative));
                continue;
            }
            total = checked(total + entry.Length);
            if (total > MaximumArchiveBytes)
            {
                throw ReleaseErrors.VerificationFailed("the update archive exceeds its decompressed size budget");
            }
            string destination = Path.Combine(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using var input = entry.Open();
            using FileStream output = new(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            long copied = CopyBounded(
                input,
                output,
                MaximumArchiveBytes - (total - entry.Length), deadline);
            if (copied != entry.Length)
            {
                throw ReleaseErrors.VerificationFailed($"the update archive entry '{relative}' has an invalid decompressed size");
            }
        }

        foreach (string required in new[] { DistributionInfo.ExecutableName, "install.ps1", "SHA256SUMS" })
        {
            if (!File.Exists(Path.Combine(root, required)))
            {
                throw ReleaseErrors.VerificationFailed($"the update archive is missing '{required}'");
            }
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
            throw ReleaseErrors.VerificationFailed($"the update archive contains an unsafe path '{value}'");
        }
        return normalized;
    }

    private static long CopyBounded(Stream input, Stream output, long maximum, OperationDeadline deadline)
    {
        byte[] buffer = new byte[64 * 1024];
        long total = 0;
        while (true)
        {
            deadline.ThrowIfExpired("update-copy");
            int read = input.ReadAsync(buffer.AsMemory(), deadline.Token).AsTask().GetAwaiter().GetResult();
            if (read == 0)
            {
                return total;
            }
            total = checked(total + read);
            if (total > maximum)
            {
                throw ReleaseErrors.VerificationFailed("the update archive decompressed beyond its safety budget");
            }
            output.WriteAsync(buffer.AsMemory(0, read), deadline.Token).AsTask().GetAwaiter().GetResult();
        }
    }

    private static void DeleteTree(string path) => PrivateUserStorage.TryDeleteTree(path);

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
        private readonly string _temporaryRoot;
        private readonly Uri? _remoteBase;
        private readonly OperationDeadline _deadline;

        private FeedFiles(string manifestPath, string signaturePath, string temporaryRoot, Uri? remoteBase, OperationDeadline deadline)
        {
            ManifestPath = manifestPath;
            SignaturePath = signaturePath;
            _temporaryRoot = temporaryRoot;
            _remoteBase = remoteBase;
            _deadline = deadline;
        }

        public string ManifestPath { get; }
        public string SignaturePath { get; }

        public static FeedFiles Open(string feed, string baseDirectory, ResourceBudgetLedger budgets)
        {
            string root = budgets.OutputSession?.CreatePrivateDirectory("update-feed")
                ?? PrivateUserStorage.CreateTemporaryDirectory("update-feed");
            try
            {
                if (!Path.IsPathRooted(feed) && Uri.TryCreate(feed, UriKind.Absolute, out var uri))
                {
                    ValidateHttpsFeedUri(uri);
                    string manifest = Path.Combine(root, "RELEASE-MANIFEST.json");
                    Download(uri, manifest, MaximumManifestBytes, budgets.Deadline);
                    string signature = Path.Combine(root, "RELEASE-MANIFEST.sig");
                    Download(new Uri(uri, "RELEASE-MANIFEST.sig"), signature, 16 * 1024, budgets.Deadline);
                    return new FeedFiles(manifest, signature, root, uri, budgets.Deadline);
                }
                string path = Path.GetFullPath(feed, baseDirectory);
                if (Directory.Exists(path)) { path = Path.Combine(path, "RELEASE-MANIFEST.json"); }
                EnsureLocal(path);
                string signaturePath = Path.Combine(Path.GetDirectoryName(path)!, "RELEASE-MANIFEST.sig");
                EnsureLocal(signaturePath);
                return new FeedFiles(path, signaturePath, root, null, budgets.Deadline);
            }
            catch { DeleteTree(root); throw; }
        }

        public string DownloadArchive(ReleaseManifestInfo manifest)
        {
            string target = Path.Combine(_temporaryRoot, "archive.zip");
            try
            {
                if (_remoteBase is not null)
                {
                    if (manifest.ArchivePath.Contains("..", StringComparison.Ordinal))
                    { throw ReleaseErrors.VerificationFailed("the feed archive path is unsafe"); }
                    Download(new Uri(new Uri(_remoteBase, "."), manifest.ArchivePath), target, MaximumArchiveBytes, _deadline);
                }
                else
                {
                    string source = Path.Combine(Path.GetDirectoryName(ManifestPath)!, manifest.ArchivePath.Replace('/', Path.DirectorySeparatorChar));
                    EnsureLocal(source);
                    using var input = File.OpenRead(source);
                    using FileStream output = PrivateUserStorage.CreateFile(target);
                    CopyBounded(input, output, Math.Min(manifest.ArchiveSize, MaximumArchiveBytes), _deadline);
                }
                using var stream = File.OpenRead(target);
                if (stream.Length != manifest.ArchiveSize || !string.Equals(
                    Convert.ToHexString(SHA256.HashDataAsync(stream, _deadline.Token).GetAwaiter().GetResult()),
                    manifest.ArchiveSha256, StringComparison.OrdinalIgnoreCase))
                { throw ReleaseErrors.VerificationFailed("the downloaded archive hash does not match the signed manifest"); }
                _deadline.ThrowIfExpired("update-archive-verified");
                return target;
            }
            catch { TryDeleteFile(target); throw; }
        }

        public void Dispose() => DeleteTree(_temporaryRoot);

        private static void Download(Uri uri, string destination, long maximum, OperationDeadline deadline)
        {
            ValidateHttpsFeedUri(uri);
            // The operation deadline alone bounds the transfer. HttpClient's own 100-second
            // timeout would end a slow feed as a cancellation without an error.
            using var client = new HttpClient(new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                ConnectTimeout = TimeSpan.FromSeconds(30),
            })
            { Timeout = Timeout.InfiniteTimeSpan };
            try
            {
                using var response = client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, deadline.Token).GetAwaiter().GetResult();
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw ReleaseErrors.FeedUnavailable(uri, $"the feed answered HTTP {(int)response.StatusCode}"
                        + ((int)response.StatusCode is >= 300 and < 400 ? "; redirects are not followed, so use the final HTTPS URL" : string.Empty));
                }
                if (response.Content.Headers.ContentLength is > 0 and var length && length > maximum)
                {
                    throw ReleaseErrors.VerificationFailed($"the feed file exceeds its {maximum}-byte budget");
                }
                try
                {
                    using var input = response.Content.ReadAsStreamAsync(deadline.Token).GetAwaiter().GetResult();
                    using FileStream output = PrivateUserStorage.CreateFile(destination);
                    CopyBounded(input, output, maximum, deadline);
                }
                catch { TryDeleteFile(destination); throw; }
            }
            catch (HttpRequestException exception)
            {
                throw ReleaseErrors.FeedUnavailable(uri, exception.Message);
            }
            catch (IOException exception) when (!deadline.Token.IsCancellationRequested
                && (exception is HttpIOException || exception.InnerException is System.Net.Sockets.SocketException))
            {
                // The connection broke while the response body was being read.
                throw ReleaseErrors.FeedUnavailable(uri, exception.Message);
            }
            catch (OperationCanceledException) when (!deadline.Token.IsCancellationRequested)
            {
                // Only the connection timeout cancels without the deadline or the caller.
                throw ReleaseErrors.FeedUnavailable(uri, "the connection was not established within 30 seconds");
            }
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
                throw ReleaseErrors.VerificationFailed($"feed file not found: {full}");
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
            ReleaseErrors.VerificationFailed($"version '{value}' is not a supported semantic version");
    }
}
