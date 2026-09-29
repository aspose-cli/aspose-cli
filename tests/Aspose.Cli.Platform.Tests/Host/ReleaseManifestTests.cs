using System.Text.Json.Nodes;
using Aspose.Cli.Host.Updating;
using Aspose.Cli.Sdk;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Host.Tests;

public sealed class ReleaseManifestTests : IDisposable
{
    private readonly TempDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    [Fact]
    public void ReadsTheArchiveIdentity()
    {
        ReleaseManifestInfo manifest = ReleaseManifest.Read(Write(Manifest()));

        Assert.Equal("1.2.3", manifest.ArtifactVersion);
        Assert.Equal(new string('a', 40), manifest.SourceRevision);
        Assert.Equal("aspose-cli-1.2.3-win-x64.zip", manifest.ArchivePath);
        Assert.Equal(7, manifest.ArchiveSize);
        Assert.Equal(new string('b', 64), manifest.ArchiveSha256);
    }

    public static TheoryData<string, JsonNode?> InvalidFields => new()
    {
        { "productId", "another-cli" },
        { "runtimeIdentifier", "linux-x64" },
        { "schemaVersion", 2 },
        { "sourceRevision", "main" },
        { "archive.path", "../aspose-cli.zip" },
        { "archive.path", "https://example.test/aspose-cli.zip" },
        { "archive.path", "aspose-cli.exe" },
        { "archive.size", 0 },
        { "archive.sha256", "not-a-hash" },
        { "unexpected", "field" },
        { "archive", null },
    };

    [Theory]
    [MemberData(nameof(InvalidFields))]
    public void RejectsAnotherReleaseOrAnUnsafeArchive(string field, JsonNode? value)
    {
        JsonObject manifest = Manifest();
        JsonObject parent = field.StartsWith("archive.", StringComparison.Ordinal)
            ? manifest["archive"]!.AsObject()
            : manifest;
        parent[field.Split('.')[^1]] = value?.DeepClone();

        CliException error = Assert.Throws<CliException>(() => ReleaseManifest.Read(Write(manifest)));

        Assert.Equal(ErrorCodes.ReleaseVerificationFailed, error.Code);
    }

    [Fact]
    public void RejectsMalformedAndOversizedManifestsBeforeReadingThem()
    {
        string path = _directory.File(ReleaseManifest.FileName);
        File.WriteAllText(path, Manifest().ToJsonString().Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"schemaVersion\":1", StringComparison.Ordinal));
        Assert.Equal(ErrorCodes.ReleaseVerificationFailed, Assert.Throws<CliException>(() => ReleaseManifest.Read(path)).Code);

        File.WriteAllBytes(path, new byte[64 * 1024 + 1]);
        Assert.Equal(ErrorCodes.ReleaseVerificationFailed, Assert.Throws<CliException>(() => ReleaseManifest.Read(path)).Code);
    }

    private string Write(JsonObject manifest)
    {
        string path = _directory.File(ReleaseManifest.FileName);
        File.WriteAllText(path, manifest.ToJsonString());
        return path;
    }

    private static JsonObject Manifest() => new()
    {
        ["schemaVersion"] = 1,
        ["productId"] = DistributionInfo.Id,
        ["runtimeIdentifier"] = "win-x64",
        ["artifactVersion"] = "1.2.3",
        ["sourceRevision"] = new string('a', 40),
        ["archive"] = new JsonObject
        {
            ["path"] = "aspose-cli-1.2.3-win-x64.zip",
            ["size"] = 7,
            ["sha256"] = new string('b', 64),
        },
    };
}
