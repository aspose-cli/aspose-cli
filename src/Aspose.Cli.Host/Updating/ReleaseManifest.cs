using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.Sdk;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Host.Updating;

/// <summary>
/// Reads the RELEASE-MANIFEST.json a release publishes beside its archive: the version and source
/// revision it installs, and the archive's file name, size and SHA-256, which a download must match.
/// </summary>
internal static class ReleaseManifest
{
    internal const string FileName = "RELEASE-MANIFEST.json";
    private const int MaximumBytes = 64 * 1024;
    private const long MaximumArchiveBytes = 1L * 1024 * 1024 * 1024;

    public static ReleaseManifestInfo Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            using JsonDocument document = JsonDocument.Parse(
                ReadBounded(path),
                new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Disallow, MaxDepth = 8 });
            BoundedJsonValidation.ValidateNoDuplicateProperties(
                document.RootElement,
                static message => new InvalidDataException(message));
            JsonElement root = document.RootElement;
            AssertProperties(root, "schemaVersion", "productId", "runtimeIdentifier", "artifactVersion", "sourceRevision", "archive");
            string revision = RequiredString(root, "sourceRevision");
            if (root.GetProperty("schemaVersion").GetInt32() != 1
                || RequiredString(root, "productId") != DistributionInfo.Id
                || RequiredString(root, "runtimeIdentifier") != "win-x64"
                || revision.Length != 40
                || revision.Any(static c => !Uri.IsHexDigit(c)))
            {
                throw new InvalidDataException("the manifest describes another product, runtime or source revision");
            }

            JsonElement archive = root.GetProperty("archive");
            AssertProperties(archive, "path", "size", "sha256");
            string name = RequiredString(archive, "path");
            long size = archive.GetProperty("size").GetInt64();
            string sha256 = RequiredString(archive, "sha256");
            // The archive is published beside the manifest, so it is named by a plain file name.
            if (name.Length > 128
                || !char.IsAsciiLetterOrDigit(name[0])
                || !name.EndsWith(".zip", StringComparison.Ordinal)
                || name.Any(static c => !char.IsAsciiLetterOrDigit(c) && c is not '.' and not '-' and not '_')
                || size is <= 0 or > MaximumArchiveBytes
                || sha256.Length != 64
                || sha256.Any(static c => !Uri.IsHexDigit(c)))
            {
                throw new InvalidDataException("the archive name, size or SHA-256 is invalid");
            }

            return new ReleaseManifestInfo(
                RequiredString(root, "artifactVersion"),
                revision.ToLowerInvariant(),
                name,
                size,
                sha256.ToLowerInvariant());
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidDataException or FormatException or KeyNotFoundException
                or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            throw ReleaseErrors.VerificationFailed($"the release manifest is invalid: {exception.Message}");
        }
    }

    private static byte[] ReadBounded(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > MaximumBytes)
        {
            throw new InvalidDataException($"the manifest exceeds its {MaximumBytes}-byte limit");
        }

        byte[] bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);
        return bytes is [0xEF, 0xBB, 0xBF, .. var content] ? content : bytes;
    }

    private static void AssertProperties(JsonElement value, params string[] names)
    {
        if (value.ValueKind != JsonValueKind.Object
            || value.EnumerateObject().Count() != names.Length
            || value.EnumerateObject().Any(property => !names.Contains(property.Name, StringComparer.Ordinal)))
        {
            throw new InvalidDataException($"the manifest must have exactly the fields {string.Join(", ", names)}");
        }
    }

    private static string RequiredString(JsonElement parent, string name) =>
        parent.GetProperty(name) is { ValueKind: JsonValueKind.String } value && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new InvalidDataException($"the field '{name}' is missing or invalid");
}

/// <summary>The fields of a release manifest an update needs.</summary>
internal sealed record ReleaseManifestInfo(
    string ArtifactVersion,
    string SourceRevision,
    string ArchivePath,
    long ArchiveSize,
    string ArchiveSha256);

/// <summary>Errors raised while reading or checking a release for an update.</summary>
internal static class ReleaseErrors
{
    public static CliException VerificationFailed(string reason) => new(
        ErrorCodes.ReleaseVerificationFailed,
        $"The release could not be verified: {reason}",
        hint: "Retry from the official release. Do not bypass the archive check.",
        details: new JsonObject { ["reason"] = reason });

    /// <summary>The feed could not be read; nothing was verified or installed.</summary>
    public static CliException FeedUnavailable(Uri feed, string reason) => new(
        ErrorCodes.ReleaseFeedUnavailable,
        $"The release feed could not be reached: {reason}",
        hint: "Check the network connection, proxy settings (HTTPS_PROXY) and the feed URL, then retry. A downloaded release directory also works as a local feed path.",
        details: new JsonObject
        {
            ["reason"] = reason,
            // Feed URLs are validated to carry no credentials, query or fragment.
            ["feed"] = feed.AbsoluteUri,
        });
}
