using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Sdk;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Host.Updating;

/// <summary>
/// Reads the RELEASE-MANIFEST.json a release publishes beside its archive: the version and source
/// revision it installs, and the archive's file name, size and SHA-256, which a download must match.
/// Every installed version reads the manifest of the latest release, so this reader is part of the
/// frozen update handoff: it requires schema version 1 and its fields and ignores any other field.
/// A later schema version is refused with a pointer to a fresh install, which needs no handoff.
/// </summary>
internal static class ReleaseManifest
{
    internal const string FileName = "RELEASE-MANIFEST.json";
    private const int SchemaVersion = 1;
    private static readonly string InstallScript =
        $"https://github.com/{DistributionInfo.ReleaseRepository}/releases/latest/download/install.ps1";
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
            JsonElement root = RequiredObject(document.RootElement, "the manifest");
            int schemaVersion = Required(root, "schemaVersion").GetInt32();
            if (schemaVersion > SchemaVersion)
            {
                throw LaterSchema(schemaVersion);
            }
            string revision = RequiredString(root, "sourceRevision");
            if (schemaVersion != SchemaVersion
                || RequiredString(root, "productId") != DistributionInfo.Id
                || RequiredString(root, "runtimeIdentifier") != "win-x64"
                || revision.Length != 40
                || revision.Any(static c => !Uri.IsHexDigit(c)))
            {
                throw new InvalidDataException("the manifest describes another product, runtime or source revision");
            }

            JsonElement archive = RequiredObject(Required(root, "archive"), "the field 'archive'");
            string name = RequiredString(archive, "path");
            long size = Required(archive, "size").GetInt64();
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
            throw HostDiagnostics.ReleaseVerificationFailed($"the release manifest is invalid: {exception.Message}");
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

    private static JsonElement RequiredObject(JsonElement value, string subject) =>
        value.ValueKind == JsonValueKind.Object
            ? value
            : throw new InvalidDataException($"{subject} is not a JSON object");

    private static JsonElement Required(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out JsonElement value)
            ? value
            : throw new InvalidDataException($"the field '{name}' is missing");

    private static string RequiredString(JsonElement parent, string name) =>
        Required(parent, name) is { ValueKind: JsonValueKind.String } value && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new InvalidDataException($"the field '{name}' is missing or invalid");

    private static CliException LaterSchema(int schemaVersion) => new(
        HostDiagnostics.ReleaseVerificationFailedCode,
        $"The release could not be verified: its manifest uses schema version {schemaVersion}, which this version cannot read.",
        hint: "Install the latest release with its installer, which replaces this version: "
            + $"irm {InstallScript} | iex",
        details: new JsonObject { ["reason"] = "manifest-schema-unsupported" });
}

/// <summary>The fields of a release manifest an update needs.</summary>
internal sealed record ReleaseManifestInfo(
    string ArtifactVersion,
    string SourceRevision,
    string ArchivePath,
    long ArchiveSize,
    string ArchiveSha256);
