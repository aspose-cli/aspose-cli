using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Host.Viewer;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Host.Tests.PreviewInfrastructure;

/// <summary>
/// The sink stays the hard bound of a view's files, and an artifact it refuses names the limit
/// that refused it: the snapshot's when what is left of it is less than one file may hold, the
/// single file's otherwise, each by the environment variable that sets it (PDF-009).
/// </summary>
public sealed class BoundedViewArtifactSinkTests
{
    private const string FileBytes = "ASPOSE_CLI_PREVIEW_MAX_FILE_BYTES";
    private const string SnapshotBytes = "ASPOSE_CLI_PREVIEW_MAX_SNAPSHOT_BYTES";

    [Fact]
    public void Write_BeyondWhatIsLeftOfTheSnapshot_NamesTheSnapshotVariable()
    {
        using var temp = new TempDirectory();
        var sink = new BoundedViewArtifactSink(temp.Path, Limits(fileBytes: 1000, snapshotBytes: 1500));
        sink.Write("page-0001.png", static stream => stream.Write(new byte[1000]));

        CliException error = Assert.Throws<CliException>(
            () => sink.Write("page-0002.png", static stream => stream.Write(new byte[600])));

        Assert.Equal("PREVIEW_BUDGET_EXCEEDED", error.Code.Name);
        Assert.Equal("view snapshot bytes", error.Details!["resource"]!.GetValue<string>());
        Assert.Equal(1500, error.Details["limit"]!.GetValue<long>());
        Assert.Contains(SnapshotBytes, error.Hint, StringComparison.Ordinal);
        Assert.DoesNotContain(FileBytes, error.Hint, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(temp.Path, "page-0002.png")));
    }

    [Fact]
    public void Write_BeyondOneFile_NamesTheFileVariable()
    {
        using var temp = new TempDirectory();
        var sink = new BoundedViewArtifactSink(temp.Path, Limits(fileBytes: 1000, snapshotBytes: 4000));

        CliException error = Assert.Throws<CliException>(
            () => sink.Write("page-0001.png", static stream => stream.Write(new byte[1001])));

        Assert.Equal("view artifact bytes", error.Details!["resource"]!.GetValue<string>());
        Assert.Equal(1000, error.Details["limit"]!.GetValue<long>());
        Assert.Contains(FileBytes, error.Hint, StringComparison.Ordinal);
        Assert.DoesNotContain(SnapshotBytes, error.Hint, StringComparison.Ordinal);
    }

    private static LocalServiceResourceLimits Limits(long fileBytes, long snapshotBytes) =>
        LocalServiceResourceLimits.Resolve(static _ => null) with
        {
            MaximumSnapshotFileBytes = fileBytes,
            MaximumSnapshotBytes = snapshotBytes,
        };
}
