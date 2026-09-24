using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.Sdk;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Host.Updating;

/// <summary>Verifies detached release metadata without loading a product engine.</summary>
internal static class ReleaseManifestVerifier
{
    public const string TrustedKeyRingEnvironmentVariable =
        DistributionInfo.EnvironmentVariablePrefix + "RELEASE_TRUSTED_KEYS";

    private const int MaximumManifestBytes = 64 * 1024;
    private const int MaximumSignatureBytes = 16 * 1024;
    private const long MaximumArchiveBytes = 1L * 1024 * 1024 * 1024;
    private const int MaximumKeys = 16;

    /// <summary>Reads the configured trusted public-key ring.</summary>
    public static TrustedReleaseKeyRing LoadConfiguredKeyRing()
    {
        string? path = Environment.GetEnvironmentVariable(
            TrustedKeyRingEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(path))
        {
            return TrustedReleaseKeyRing.Empty;
        }

        try
        {
            byte[] bytes = StripUtf8Bom(ReadBoundedFile(
                path,
                MaximumManifestBytes,
                "The configured release trust ring is missing or exceeds its 64 KiB limit."));
            using JsonDocument document = JsonDocument.Parse(
                bytes,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 16,
                });
            BoundedJsonValidation.ValidateNoDuplicateProperties(
                document.RootElement,
                static message => new InvalidDataException(message));
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("keys", out JsonElement keys)
                || keys.ValueKind != JsonValueKind.Array
                || keys.GetArrayLength() > MaximumKeys)
            {
                throw new InvalidDataException("The trust ring must contain a bounded keys array.");
            }

            var entries = new List<TrustedReleaseKey>();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (JsonElement key in keys.EnumerateArray())
            {
                if (key.ValueKind != JsonValueKind.Object
                    || !key.TryGetProperty("keyId", out JsonElement idNode)
                    || !key.TryGetProperty("publicKeyPem", out JsonElement pemNode))
                {
                    throw new InvalidDataException("Each trust-ring key requires keyId and publicKeyPem.");
                }

                string id = idNode.GetString() ?? string.Empty;
                string pem = pemNode.GetString() ?? string.Empty;
                if (id.Length != 64
                    || id.Any(static c => !Uri.IsHexDigit(c))
                    || !ids.Add(id)
                    || pem.Length is 0 or > 16 * 1024)
                {
                    throw new InvalidDataException("The trust-ring key identity or PEM is invalid.");
                }

                using ECDsa publicKey = ECDsa.Create();
                publicKey.ImportFromPem(pem);
                if (publicKey.KeySize != 256)
                {
                    throw new InvalidDataException("Release trust keys must use ECDSA P-256.");
                }
                string calculatedKeyId = Convert.ToHexString(
                    SHA256.HashData(publicKey.ExportSubjectPublicKeyInfo()));
                if (!string.Equals(calculatedKeyId, id, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("The trust-ring key id does not match its public key.");
                }

                entries.Add(new TrustedReleaseKey(id.ToLowerInvariant(), pem));
            }

            return new TrustedReleaseKeyRing(entries);
        }
        catch (ReleaseVerificationException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidDataException or CryptographicException
                or IOException or UnauthorizedAccessException or ArgumentException
                or InvalidOperationException or FormatException or NotSupportedException)
        {
            throw new ReleaseVerificationException(
                "The configured release trust ring is invalid.",
                trustedKeysConfigured: true,
                exception);
        }
    }

    /// <summary>Verifies a manifest, detached signature and optional archive.</summary>
    public static ReleaseManifestInfo Verify(
        string manifestPath,
        TrustedReleaseKeyRing keyRing,
        string? signaturePath = null,
        string? archivePath = null,
        string? expectedEdition = null,
        string? expectedRuntimeIdentifier = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        ArgumentNullException.ThrowIfNull(keyRing);

        try
        {
            byte[] manifestBytes = StripUtf8Bom(ReadBoundedFile(
                manifestPath,
                MaximumManifestBytes,
                "The release manifest is missing or exceeds its 64 KiB limit."));
            using JsonDocument document = JsonDocument.Parse(
                manifestBytes,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 16,
                });
            BoundedJsonValidation.ValidateNoDuplicateProperties(
                document.RootElement,
                static message => new InvalidDataException(message));
            JsonElement root = document.RootElement;
            AssertProperties(root, "schemaVersion", "productId", "edition", "runtimeIdentifier", "artifactVersion", "sourceRevision", "buildDirty", "enginePackages", "archive", "signature");
            string edition = RequiredString(root, "edition");
            string rid = RequiredString(root, "runtimeIdentifier");
            string version = RequiredString(root, "artifactVersion");
            string revision = RequiredString(root, "sourceRevision");
            string productId = RequiredString(root, "productId");
            bool buildDirty = root.GetProperty("buildDirty").GetBoolean();
            if (root.GetProperty("schemaVersion").GetInt32() != 1
                || productId != DistributionInfo.Id
                || edition != DistributionInfo.Edition
                || rid != "win-x64"
                || buildDirty
                || revision.Length != 40
                || revision.Any(static c => !Uri.IsHexDigit(c)))
            {
                throw new InvalidDataException("Release manifest provenance is invalid.");
            }

            if (expectedEdition is not null && !string.Equals(
                    edition,
                    expectedEdition,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException("Release edition does not match the running distribution.");
            }

            if (expectedRuntimeIdentifier is not null && rid != expectedRuntimeIdentifier)
            {
                throw new InvalidDataException("Release runtime identifier does not match this host.");
            }


            JsonElement enginePackages = root.GetProperty("enginePackages");
            if (enginePackages.ValueKind != JsonValueKind.Array
                || enginePackages.GetArrayLength() is < 1 or > 32)
            {
                throw new InvalidDataException("Release SDK provenance is invalid.");
            }
            var links = new List<ReleaseEnginePackage>();
            var products = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonElement package in enginePackages.EnumerateArray())
            {
                AssertProperties(package, "product", "packageId", "version", "contentHash");
                string product = RequiredString(package, "product");
                string packageId = RequiredString(package, "packageId");
                string versionText = RequiredString(package, "version");
                string contentHash = RequiredString(package, "contentHash");
                if (!products.Add(product)
                    || !IsIdentifier(product)
                    || packageId.Length > 128 || packageId.Any(static c => !char.IsAsciiLetterOrDigit(c) && c is not '.' and not '-' and not '_')
                    || versionText.Length > 128 || versionText.Any(static c => !char.IsAsciiLetterOrDigit(c) && c is not '.' and not '-' and not '+')
                    || Convert.FromBase64String(contentHash).Length != 64
                    || Convert.ToBase64String(Convert.FromBase64String(contentHash)) != contentHash)
                {
                    throw new InvalidDataException("Release SDK provenance is invalid.");
                }
                links.Add(new ReleaseEnginePackage(product, packageId, versionText, contentHash));
            }

            JsonElement archive = root.GetProperty("archive");
            AssertProperties(archive, "path", "size", "sha256");
            string archiveName = RequiredString(archive, "path");
            long archiveSize = archive.GetProperty("size").GetInt64();
            string archiveHash = RequiredString(archive, "sha256");
            if (!IsSafeRelativePath(archiveName)
                || archiveSize is < 0 or > MaximumArchiveBytes
                || archiveHash.Length != 64
                || archiveHash.Any(static c => !Uri.IsHexDigit(c)))
            {
                throw new InvalidDataException("Release archive metadata is invalid.");
            }

            JsonElement signature = root.GetProperty("signature");
            AssertProperties(signature, "status", "algorithm", "format", "keyId", "path");
            string status = RequiredString(signature, "status");
            string algorithm = RequiredString(signature, "algorithm");
            string format = RequiredString(signature, "format");
            string keyId = RequiredString(signature, "keyId").ToLowerInvariant();
            string detachedName = RequiredString(signature, "path");
            if (status != "signed"
                || algorithm != "ECDSA-P256-SHA256"
                || format != "rfc3279-der"
                || keyId.Length != 64
                || keyId.Any(static c => !Uri.IsHexDigit(c))
                || !IsSafeRelativePath(detachedName))
            {
                throw new InvalidDataException("Release signature metadata is invalid.");
            }

            string expectedSignaturePath = Path.GetFullPath(
                Path.Combine(Path.GetDirectoryName(manifestPath)!, detachedName));
            if (signaturePath is not null
                && !string.Equals(
                    Path.GetFullPath(signaturePath),
                    expectedSignaturePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The detached signature path does not match the signed manifest.");
            }

            if (!keyRing.TryGet(keyId, out TrustedReleaseKey? key))
            {
                throw new ReleaseVerificationException(
                    $"No trusted release key is configured for key id '{keyId}'.",
                    trustedKeysConfigured: keyRing.Count > 0);
            }

            string actualSignaturePath = signaturePath
                ?? Path.Combine(Path.GetDirectoryName(manifestPath)!, detachedName);
            byte[] signatureBytes = Convert.FromBase64String(
                Encoding.ASCII.GetString(ReadBoundedFile(
                    actualSignaturePath,
                    MaximumSignatureBytes,
                    "The detached release signature is missing or exceeds its 16 KiB limit.")).Trim());
            if (signatureBytes.Length > MaximumSignatureBytes)
            {
                throw new InvalidDataException("The detached release signature exceeds its 16 KiB limit.");
            }

            using ECDsa verifier = ECDsa.Create();
            verifier.ImportFromPem(key!.PublicKeyPem);
            string calculatedKeyId = Convert.ToHexString(
                SHA256.HashData(verifier.ExportSubjectPublicKeyInfo()));
            if (!string.Equals(calculatedKeyId, keyId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The trust-ring key id does not match its public key.");
            }
            if (!verifier.VerifyData(
                    CreateSigningPayload(
                        productId,
                        edition,
                        rid,
                        version,
                        revision,
                        archiveName,
                        archiveSize,
                        archiveHash,
                        buildDirty,
                        links,
                        status,
                        algorithm,
                        format,
                        keyId,
                        detachedName),
                    signatureBytes,
                    HashAlgorithmName.SHA256,
                    DSASignatureFormat.Rfc3279DerSequence))
            {
                throw new InvalidDataException("The detached release signature is invalid.");
            }

            if (archivePath is not null)
            {
                if (!string.Equals(
                        ComputeFileSha256(archivePath, archiveSize),
                        archiveHash,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("The release archive hash or size does not match the signed manifest.");
                }
            }

            return new ReleaseManifestInfo(
                edition,
                rid,
                version,
                revision.ToLowerInvariant(),
                archiveName,
                archiveSize,
                archiveHash.ToLowerInvariant(),
                keyId,
                detachedName);
        }
        catch (ReleaseVerificationException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidDataException or CryptographicException
                or FormatException or KeyNotFoundException or IOException
                or UnauthorizedAccessException or ArgumentException
                or InvalidOperationException or OverflowException or NotSupportedException)
        {
            throw new ReleaseVerificationException(
                "The release manifest, signature, or archive failed verification.",
                trustedKeysConfigured: keyRing.Count > 0,
                exception);
        }
    }

    /// <summary>Returns the stable bytes signed by package tooling.</summary>
    public static byte[] CreateSigningPayload(
        string productId,
        string edition,
        string runtimeIdentifier,
        string artifactVersion,
        string sourceRevision,
        string archivePath,
        long archiveSize,
        string archiveSha256,
        bool buildDirty,
        IEnumerable<ReleaseEnginePackage> enginePackages,
        string signatureStatus,
        string signatureAlgorithm,
        string signatureFormat,
        string signatureKeyId,
        string signaturePath)
    {
        var lines = new List<string>
        {
            DistributionInfo.Id + "-release-v1",
            $"productId={productId}",
            $"edition={edition}",
            $"runtimeIdentifier={runtimeIdentifier}",
            $"artifactVersion={artifactVersion}",
            $"sourceRevision={sourceRevision}",
            $"buildDirty={buildDirty.ToString().ToLowerInvariant()}",
        };
        lines.AddRange(enginePackages
            .OrderBy(static link => link.Product, StringComparer.Ordinal)
            .SelectMany(static link => new[]
            {
                $"enginePackages.{link.Product}.packageId={link.PackageId}",
                $"enginePackages.{link.Product}.version={link.Version}",
                $"enginePackages.{link.Product}.contentHash={link.ContentHash}",
            }));
        lines.AddRange(
        [
            $"archive.path={archivePath}",
            $"archive.size={archiveSize}",
            $"archive.sha256={archiveSha256.ToLowerInvariant()}",
            $"signature.status={signatureStatus}",
            $"signature.algorithm={signatureAlgorithm}",
            $"signature.format={signatureFormat}",
            $"signature.keyId={signatureKeyId.ToLowerInvariant()}",
            $"signature.path={signaturePath}",
            string.Empty,
        ]);
        return Encoding.UTF8.GetBytes(string.Join("\n", lines));
    }

    private static bool IsIdentifier(string value) =>
        value.Length is > 0 and <= 64
        && value[0] is >= 'a' and <= 'z'
        && value.All(static c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');

    private static void AssertProperties(JsonElement value, params string[] allowed)
    {
        if (value.ValueKind != JsonValueKind.Object
            || value.EnumerateObject().Any(property => !allowed.Contains(property.Name, StringComparer.Ordinal)))
        {
            throw new InvalidDataException("Release manifest contains an unknown field.");
        }
    }

    private static string RequiredString(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new InvalidDataException($"Release manifest field '{name}' is missing or invalid.");

    private static bool IsSafeRelativePath(string path) =>
        path.Length is > 0 and <= 256
        && !Path.IsPathRooted(path)
        && !path.StartsWith("/", StringComparison.Ordinal)
        && !path.Contains(":", StringComparison.Ordinal)
        && !path.Contains("\\", StringComparison.Ordinal)
        && path.IndexOfAny(['%', '?', '#']) < 0
        && path.Replace('\\', '/').Split('/').All(static segment =>
            segment.Length > 0 && segment is not "." and not ".."
            && !segment.EndsWith('.') && !segment.EndsWith(' '));

    private static byte[] ReadBoundedFile(string path, int maximumBytes, string error)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            options: FileOptions.SequentialScan);
        byte[] bytes = new byte[maximumBytes + 1];
        int total = 0;
        while (total < bytes.Length)
        {
            int read = stream.Read(bytes, total, bytes.Length - total);
            if (read == 0)
            {
                Array.Resize(ref bytes, total);
                return bytes;
            }
            total += read;
        }
        throw new InvalidDataException(error);
    }

    private static string ComputeFileSha256(string path, long expectedLength)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            options: FileOptions.SequentialScan);
        if (stream.Length != expectedLength)
        {
            throw new InvalidDataException("The release archive size does not match the signed manifest.");
        }
        string hash = Convert.ToHexString(SHA256.HashData(stream));
        if (stream.Length != expectedLength)
        {
            throw new InvalidDataException("The release archive changed while it was being verified.");
        }
        return hash;
    }

    private static byte[] StripUtf8Bom(byte[] bytes) =>
        bytes is [0xEF, 0xBB, 0xBF, .. var content]
            ? content
            : bytes;
}

internal sealed record ReleaseManifestInfo(
    string Edition,
    string RuntimeIdentifier,
    string ArtifactVersion,
    string SourceRevision,
    string ArchivePath,
    long ArchiveSize,
    string ArchiveSha256,
    string KeyId,
    string SignaturePath);

internal sealed record ReleaseEnginePackage(string Product, string PackageId, string Version, string ContentHash);

internal sealed record TrustedReleaseKey(string KeyId, string PublicKeyPem);

internal sealed class TrustedReleaseKeyRing
{
    private readonly IReadOnlyDictionary<string, TrustedReleaseKey> _keys;

    public static TrustedReleaseKeyRing Empty { get; } = new([]);

    public TrustedReleaseKeyRing(IEnumerable<TrustedReleaseKey> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var unique = new Dictionary<string, TrustedReleaseKey>(StringComparer.OrdinalIgnoreCase);
        foreach (TrustedReleaseKey key in keys)
        {
            if (!unique.TryAdd(key.KeyId, key))
            {
                throw new ReleaseVerificationException(
                    "The configured release trust ring contains duplicate key identities.",
                    trustedKeysConfigured: true);
            }
        }
        _keys = unique;
    }

    public int Count => _keys.Count;

    public bool TryGet(string keyId, out TrustedReleaseKey? key) =>
        _keys.TryGetValue(keyId, out key);
}

internal sealed class ReleaseVerificationException(
    string message,
    bool trustedKeysConfigured = false,
    Exception? innerException = null) : Exception(message, innerException)
{
    public bool TrustedKeysConfigured { get; } = trustedKeysConfigured;
}

/// <summary>Errors raised while establishing release trust for an update.</summary>
internal static class ReleaseErrors
{
    public static CliException TrustUnavailable(string reason) => new(
        ErrorCodes.ReleaseTrustUnavailable,
        $"Release updates are unavailable: {reason}",
        hint: $"Configure {ReleaseManifestVerifier.TrustedKeyRingEnvironmentVariable} with the approved public-key ring, then retry. No production key is bundled in this build.",
        details: new JsonObject
        {
            ["reason"] = reason,
            ["trustEnvironmentVariable"] = ReleaseManifestVerifier.TrustedKeyRingEnvironmentVariable,
        });

    public static CliException VerificationFailed(string reason) => new(
        ErrorCodes.ReleaseVerificationFailed,
        $"The release could not be verified: {reason}",
        hint: "Use the official feed and retry. Do not bypass signature or archive verification.",
        details: new JsonObject { ["reason"] = reason });

    /// <summary>The feed could not be read; nothing was verified or installed.</summary>
    public static CliException FeedUnavailable(Uri feed, string reason) => new(
        ErrorCodes.ReleaseFeedUnavailable,
        $"The release feed could not be reached: {reason}",
        hint: "Check the network connection, proxy settings (HTTPS_PROXY) and the feed URL, then retry. A downloaded feed directory also works as a local feed path.",
        details: new JsonObject
        {
            ["reason"] = reason,
            // Feed URLs are validated to carry no credentials, query or fragment.
            ["feed"] = feed.AbsoluteUri,
        });
}
