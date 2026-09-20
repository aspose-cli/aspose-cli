using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Host.Preview;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Host.Tests.PreviewInfrastructure;

public sealed class PreviewArtifactManifestTests
{
    [Fact]
    public void TryOpenRead_WhenValidatedFileDisappears_FailsClosed()
    {
        using var temp = new TempDirectory();
        string root = temp.File("snapshot");
        Directory.CreateDirectory(root);
        string entry = Path.Combine(root, "index.html");
        File.WriteAllText(entry, "view");
        PreviewArtifactManifest manifest = PreviewArtifactManifest.Validate(
            root,
            "index.html",
            Limits());

        File.Delete(entry);

        Assert.Null(manifest.TryOpenRead("index.html"));
    }

    [Fact]
    public void TryOpenRead_WhenValidatedFileChangesLength_FailsClosed()
    {
        using var temp = new TempDirectory();
        string root = temp.File("snapshot");
        Directory.CreateDirectory(root);
        string entry = Path.Combine(root, "index.html");
        File.WriteAllText(entry, "view");
        PreviewArtifactManifest manifest = PreviewArtifactManifest.Validate(
            root,
            "index.html",
            Limits());

        File.WriteAllText(entry, "changed");

        Assert.Null(manifest.TryOpenRead("index.html"));
    }

    [Fact]
    public void TryOpenRead_WhenValidatedFileChangesWithoutChangingLength_FailsClosed()
    {
        using var temp = new TempDirectory();
        string root = temp.File("snapshot");
        Directory.CreateDirectory(root);
        string entry = Path.Combine(root, "index.html");
        File.WriteAllText(entry, "view");
        PreviewArtifactManifest manifest = PreviewArtifactManifest.Validate(
            root,
            "index.html",
            Limits());

        File.WriteAllText(entry, "edit");

        Assert.Null(manifest.TryOpenRead("index.html"));
    }

    [Fact]
    public void Validate_WhenArtifactPathExceedsDepthBound_RejectsBeforePublication()
    {
        using var temp = new TempDirectory();
        string root = temp.File("snapshot");
        string relative = string.Join(
            '/',
            Enumerable.Repeat(
                "level",
                PreviewArtifactManifest.MaximumPathSegments + 1));
        string directory = Path.Combine(
            root,
            relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "index.html"), "view");

        Assert.Throws<InvalidDataException>(() =>
            PreviewArtifactManifest.Validate(
                root,
                relative + "/index.html",
                Limits()));
    }

    [Fact]
    public void Validate_WhenDirectoryCountExceedsFileBound_RejectsEmptyTopology()
    {
        using var temp = new TempDirectory();
        string root = temp.File("snapshot");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "index.html"), "view");
        for (int index = 0; index < 5; index++)
        {
            Directory.CreateDirectory(Path.Combine(root, $"empty-{index}"));
        }

        Assert.ThrowsAny<Exception>(() =>
            PreviewArtifactManifest.Validate(
                root,
                "index.html",
                Limits()));
    }

    [Fact]
    public void TryOpenRead_WhenValidatedFileBecomesADirectory_FailsClosed()
    {
        using var temp = new TempDirectory();
        string root = temp.File("snapshot");
        Directory.CreateDirectory(root);
        string entry = Path.Combine(root, "index.html");
        File.WriteAllText(entry, "view");
        PreviewArtifactManifest manifest = PreviewArtifactManifest.Validate(
            root,
            "index.html",
            Limits());
        File.Delete(entry);
        Directory.CreateDirectory(entry);

        Assert.Null(manifest.TryOpenRead("index.html"));
    }

    private static LocalServiceResourceLimits Limits() => new(
        MaximumConcurrentRequests: 4,
        MaximumSseClients: 4,
        SseQueueCapacity: 4,
        SseWriteTimeout: TimeSpan.FromSeconds(1),
        MaximumSnapshotFiles: 4,
        MaximumSnapshotFileBytes: 1024,
        MaximumSnapshotBytes: 4096,
        MaximumInlineHtmlBytes: 1024,
        MaximumUploadFiles: 4,
        MaximumUploadSessionBytes: 4096,
        RenderTimeout: TimeSpan.FromMinutes(1));
}
